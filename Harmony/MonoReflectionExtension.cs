using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace Harmony
{
	// Based on https://github.com/jbevain/mono.reflection
	// Extended so it can Emit opcodes into a DynamicMethod too

	public static class MonoReflectionExtension
	{
		public static IList<Instruction> GetInstructions(this MethodBase self)
		{
			if (self == null)
				throw new Exception("self cannot be null");

			return MethodBodyReader.GetInstructions(self).AsReadOnly();
		}

		public static void CopyOpCodes(this MethodBase self, ILGenerator generator)
		{
			if (self == null)
				throw new Exception("self cannot be null");

			MethodBodyReader.GetInstructions(self, generator);
		}
	}

	public sealed class Instruction
	{
		readonly int offset;
		OpCode opcode;
		object operand;

		Instruction previous;
		Instruction next;

		public int Offset
		{
			get { return offset; }
		}

		public OpCode OpCode
		{
			get { return opcode; }
		}

		public object Operand
		{
			get { return operand; }
			internal set { operand = value; }
		}

		public Instruction Previous
		{
			get { return previous; }
			internal set { previous = value; }
		}

		public Instruction Next
		{
			get { return next; }
			internal set { next = value; }
		}

		internal Instruction(int offset, OpCode opcode)
		{
			this.offset = offset;
			this.opcode = opcode;
		}

		public int GetSize()
		{
			int size = opcode.Size;

			switch (opcode.OperandType)
			{
				case OperandType.InlineSwitch:
					size += (1 + ((int[])operand).Length) * 4;
					break;

				case OperandType.InlineI8:
				case OperandType.InlineR:
					size += 8;
					break;

				case OperandType.InlineBrTarget:
				case OperandType.InlineField:
				case OperandType.InlineI:
				case OperandType.InlineMethod:
				case OperandType.InlineString:
				case OperandType.InlineTok:
				case OperandType.InlineType:
				case OperandType.ShortInlineR:
					size += 4;
					break;

				case OperandType.InlineVar:
					size += 2;
					break;

				case OperandType.ShortInlineBrTarget:
				case OperandType.ShortInlineI:
				case OperandType.ShortInlineVar:
					size += 1;
					break;
			}

			return size;
		}

		public override string ToString()
		{
			var instruction = "";

			AppendLabel(ref instruction, this);
			instruction = instruction + ": " + opcode.Name;

			if (operand == null)
				return instruction;

			instruction = instruction + " ";

			switch (opcode.OperandType)
			{
				case OperandType.ShortInlineBrTarget:
				case OperandType.InlineBrTarget:
					AppendLabel(ref instruction, (Instruction)operand);
					break;

				case OperandType.InlineSwitch:
					var labels = (Instruction[])operand;
					for (int i = 0; i < labels.Length; i++)
					{
						if (i > 0)
							instruction = instruction + ",";

						AppendLabel(ref instruction, labels[i]);
					}
					break;

				case OperandType.InlineString:
					instruction = instruction + "\"" + operand + "\"";
					break;

				default:
					instruction = instruction + operand;
					break;
			}

			return instruction;
		}

		static void AppendLabel(ref string str, Instruction instruction)
		{
			str = str + "IL_" + instruction.offset.ToString("x4");
		}
	}

	class ByteBuffer
	{
		internal byte[] buffer;
		internal int position;

		public ByteBuffer(byte[] buffer)
		{
			this.buffer = buffer;
		}

		public byte ReadByte()
		{
			CheckCanRead(1);
			return buffer[position++];
		}

		public byte[] ReadBytes(int length)
		{
			CheckCanRead(length);
			var value = new byte[length];
			Buffer.BlockCopy(buffer, position, value, 0, length);
			position += length;
			return value;
		}

		public short ReadInt16()
		{
			CheckCanRead(2);
			var value = (short)(buffer[position]
				| (buffer[position + 1] << 8));
			position += 2;
			return value;
		}

		public int ReadInt32()
		{
			CheckCanRead(4);
			int value = buffer[position]
				| (buffer[position + 1] << 8)
				| (buffer[position + 2] << 16)
				| (buffer[position + 3] << 24);
			position += 4;
			return value;
		}

		public long ReadInt64()
		{
			CheckCanRead(8);
			var low = (uint)(buffer[position]
				| (buffer[position + 1] << 8)
				| (buffer[position + 2] << 16)
				| (buffer[position + 3] << 24));

			var high = (uint)(buffer[position + 4]
				| (buffer[position + 5] << 8)
				| (buffer[position + 6] << 16)
				| (buffer[position + 7] << 24));

			long value = (((long)high) << 32) | low;
			position += 8;
			return value;
		}

		public float ReadSingle()
		{
			if (!BitConverter.IsLittleEndian)
			{
				var bytes = ReadBytes(4);
				Array.Reverse(bytes);
				return BitConverter.ToSingle(bytes, 0);
			}

			CheckCanRead(4);
			var value = BitConverter.ToSingle(buffer, position);
			position += 4;
			return value;
		}

		public double ReadDouble()
		{
			if (!BitConverter.IsLittleEndian)
			{
				var bytes = ReadBytes(8);
				Array.Reverse(bytes);
				return BitConverter.ToDouble(bytes, 0);
			}

			CheckCanRead(8);
			var value = BitConverter.ToDouble(buffer, position);
			position += 8;
			return value;
		}

		void CheckCanRead(int count)
		{
			if (position + count > buffer.Length)
				throw new ArgumentOutOfRangeException();
		}
	}

	class MethodBodyReader
	{
		static readonly OpCode[] one_byte_opcodes;
		static readonly OpCode[] two_bytes_opcodes;

		static MethodBodyReader()
		{
			one_byte_opcodes = new OpCode[0xe1];
			two_bytes_opcodes = new OpCode[0x1f];

			var fields = typeof(OpCodes).GetFields(
				BindingFlags.Public | BindingFlags.Static);

			foreach (var field in fields)
			{
				var opcode = (OpCode)field.GetValue(null);
				if (opcode.OpCodeType == OpCodeType.Nternal)
					continue;

				if (opcode.Size == 1)
					one_byte_opcodes[opcode.Value] = opcode;
				else
					two_bytes_opcodes[opcode.Value & 0xff] = opcode;
			}
		}

		readonly ILGenerator generator;
		readonly LocalBuilder[] variables;

		readonly MethodBody body;
		readonly Module module;
		readonly Type[] type_arguments;
		readonly Type[] method_arguments;
		readonly ByteBuffer il;
		readonly ParameterInfo this_parameter;
		readonly ParameterInfo[] parameters;
		readonly IList<LocalVariableInfo> locals;
		readonly List<Instruction> instructions;

		Dictionary<int, Label> switchLabels = new Dictionary<int, Label>();

		MethodBodyReader(MethodBase method, ILGenerator generator)
		{
			this.generator = generator;
			byte[] bytes;

			if (method is DynamicMethod)
			{
				bytes = PatchTools.GetILCodesFromDynamicMethod(method as DynamicMethod);
				if (bytes == null)
					throw new ArgumentException("Can not get the bytes of the method");
			}
			else
			{
				body = method.GetMethodBody();
				if (body == null)
					throw new ArgumentException("Method has no body");

				if (generator != null)
					variables = body.LocalVariables
						.Select(lvi => generator.DeclareLocal(lvi.LocalType, lvi.IsPinned)).ToArray();

				bytes = body.GetILAsByteArray();
				if (bytes == null)
					throw new ArgumentException("Can not get the bytes of the method");
			}

			if (!(method is ConstructorInfo))
				method_arguments = method.GetGenericArguments();

			if (method.DeclaringType != null)
				type_arguments = method.DeclaringType.GetGenericArguments();

			if (!method.IsStatic)
				this_parameter = new ThisParameter(method);
			parameters = method.GetParameters();
			locals = body.LocalVariables;
			module = method.Module;
			il = new ByteBuffer(bytes);
			instructions = new List<Instruction>((bytes.Length + 1) / 2);
		}

		void ReadInstructions()
		{
			Instruction previous = null;

			while (il.position < il.buffer.Length)
			{
				var instruction = new Instruction(il.position, ReadOpCode());

				ReadOperand(instruction);

				if (previous != null)
				{
					instruction.Previous = previous;
					previous.Next = instruction;
				}

				instructions.Add(instruction);
				previous = instruction;
			}

			ResolveBranches();
		}

		void ReadOperand(Instruction instruction)
		{
			/*var bstr = " 0x";
			for (int i = 0; i < 5 && il.position + i < il.buffer.Length; i++)
			{
				var b = il.buffer[il.position + i];
				bstr += b.ToString("x");
			}
			Logger.Log("L_"
				+ instruction.Offset.ToString("x4")
				+ " " + instruction.OpCode.Name
				+ " " + instruction.OpCode.Value.ToString("x2")
				+ " " + instruction.OpCode.OpCodeType
				+ "," + instruction.OpCode.FlowControl
				+ "," + instruction.OpCode.StackBehaviourPop
				+ "," + instruction.OpCode.StackBehaviourPush
				+ " " + instruction.OpCode.OperandType + bstr);*/

			if (generator != null)
			{
				Label label;
				if (switchLabels.TryGetValue(instruction.Offset, out label))
					generator.MarkLabel(label);
			}

			switch (instruction.OpCode.OperandType)
			{
				case OperandType.InlineNone:
					{
						if (generator != null) generator.Emit(instruction.OpCode);
						break;
					}

				case OperandType.InlineSwitch:
					{
						var length = il.ReadInt32();
						var base_offset = il.position + (4 * length);
						var branches = new int[length];
						var labels = new Label[length];
						switchLabels = new Dictionary<int, Label>();
						for (int i = 0; i < length; i++)
						{
							branches[i] = il.ReadInt32() + base_offset;
							if (generator != null) labels[i] = generator.DefineLabel();
							switchLabels.Add(branches[i], labels[i]);
						}
						instruction.Operand = branches;
						if (generator != null) generator.Emit(instruction.OpCode, labels);
						break;
					}

				case OperandType.ShortInlineBrTarget:
					{
						var val = (sbyte)il.ReadByte();
						instruction.Operand = val + il.position;
						if (generator != null) generator.Emit(instruction.OpCode, val);
						break;
					}

				case OperandType.InlineBrTarget:
					{
						var val = il.ReadInt32();
						instruction.Operand = val + il.position;
						if (generator != null) generator.Emit(instruction.OpCode, val);
						break;
					}

				case OperandType.ShortInlineI:
					{
						if (instruction.OpCode == OpCodes.Ldc_I4_S)
						{
							var sb = (sbyte)il.ReadByte();
							instruction.Operand = sb;
							if (generator != null) generator.Emit(instruction.OpCode, (sbyte)instruction.Operand);
						}
						else
						{
							var b = il.ReadByte();
							instruction.Operand = b;
							if (generator != null) generator.Emit(instruction.OpCode, (byte)instruction.Operand);
						}
						break;
					}

				case OperandType.InlineI:
					{
						var val = il.ReadInt32();
						instruction.Operand = val;
						if (generator != null) generator.Emit(instruction.OpCode, (int)instruction.Operand);
						break;
					}

				case OperandType.ShortInlineR:
					{
						var val = il.ReadSingle();
						instruction.Operand = val;
						if (generator != null) generator.Emit(instruction.OpCode, (float)instruction.Operand);
						break;
					}

				case OperandType.InlineR:
					{
						var val = il.ReadDouble();
						instruction.Operand = val;
						if (generator != null) generator.Emit(instruction.OpCode, (double)instruction.Operand);
						break;
					}

				case OperandType.InlineI8:
					{
						var val = il.ReadInt64();
						instruction.Operand = val;
						if (generator != null) generator.Emit(instruction.OpCode, (long)instruction.Operand);
						break;
					}

				case OperandType.InlineSig:
					{
						var val = il.ReadInt32();
						instruction.Operand = module.ResolveSignature(val);
						if (generator != null) generator.Emit(instruction.OpCode, (SignatureHelper)instruction.Operand);
						break;
					}

				case OperandType.InlineString:
					{
						var val = il.ReadInt32();
						instruction.Operand = module.ResolveString(val);
						if (generator != null) generator.Emit(instruction.OpCode, (string)instruction.Operand);
						break;
					}

				case OperandType.InlineTok:
					{
						var val = il.ReadInt32();
						instruction.Operand = module.ResolveMember(val, type_arguments, method_arguments);
						if (generator != null) generator.Emit(instruction.OpCode, (Type)instruction.Operand);
						break;
					}

				case OperandType.InlineType:
					{
						var val = il.ReadInt32();
						instruction.Operand = module.ResolveMember(val, type_arguments, method_arguments);
						if (generator != null) generator.Emit(instruction.OpCode, (Type)instruction.Operand);
						break;
					}

				case OperandType.InlineMethod:
					{
						var val = il.ReadInt32();
						instruction.Operand = module.ResolveMember(val, type_arguments, method_arguments);
						if (generator != null)
						{
							if (instruction.Operand is ConstructorInfo)
								generator.Emit(instruction.OpCode, (ConstructorInfo)instruction.Operand);
							else
								generator.Emit(instruction.OpCode, (MethodInfo)instruction.Operand);
						}
						break;
					}

				case OperandType.InlineField:
					{
						var val = il.ReadInt32();
						instruction.Operand = module.ResolveMember(val, type_arguments, method_arguments);
						if (generator != null) generator.Emit(instruction.OpCode, (FieldInfo)instruction.Operand);
						break;
					}

				case OperandType.ShortInlineVar:
					{
						var idx = il.ReadByte();
						if (TargetsLocalVariable(instruction.OpCode))
						{
							var lvi = GetLocalVariable(idx);
							if (lvi == null)
							{
								if (generator != null) generator.Emit(instruction.OpCode, idx);
							}
							else
							{
								instruction.Operand = lvi;
								if (generator != null) generator.Emit(instruction.OpCode, variables[lvi.LocalIndex]);
							}
						}
						else
						{
							instruction.Operand = GetParameter(idx);
							if (generator != null) generator.Emit(instruction.OpCode, idx);
						}
						break;
					}

				case OperandType.InlineVar:
					{
						var idx = il.ReadInt16();
						if (TargetsLocalVariable(instruction.OpCode))
						{
							var lvi = GetLocalVariable(idx);
							if (lvi == null)
							{
								if (generator != null) generator.Emit(instruction.OpCode, idx);
							}
							else
							{
								instruction.Operand = lvi;
								if (generator != null) generator.Emit(instruction.OpCode, variables[lvi.LocalIndex]);
							}
						}
						else
						{
							instruction.Operand = GetParameter(idx);
							if (generator != null) generator.Emit(instruction.OpCode, idx);
						}
						break;
					}

				default:
					throw new NotSupportedException();
			}
		}

		// TODO - implement
		void ParseExceptions()
		{
			foreach (var ehc in body.ExceptionHandlingClauses)
			{
				//Log.Error("ExceptionHandlingClause, flags " + ehc.Flags.ToString());

				// The FilterOffset property is meaningful only for Filter
				// clauses. The CatchType property is not meaningful for 
				// Filter or Finally clauses. 
				switch (ehc.Flags)
				{
					case ExceptionHandlingClauseOptions.Filter:
						//Log.Error("    Filter Offset: " + ehc.FilterOffset);
						break;
					case ExceptionHandlingClauseOptions.Finally:
						break;
					//default:
						//Log.Error("Type of exception: " + ehc.CatchType);
						//break;
				}

				//Log.Error("   Handler Length: " + ehc.HandlerLength);
				//Log.Error("   Handler Offset: " + ehc.HandlerOffset);
				//Log.Error(" Try Block Length: " + ehc.TryLength);
				//Log.Error(" Try Block Offset: " + ehc.TryOffset);
			}
		}

		void ResolveBranches()
		{
			foreach (var instruction in instructions)
			{
				switch (instruction.OpCode.OperandType)
				{
					case OperandType.ShortInlineBrTarget:
					case OperandType.InlineBrTarget:
						instruction.Operand = GetInstruction(instructions, (int)instruction.Operand);
						break;

					case OperandType.InlineSwitch:
						var offsets = (int[])instruction.Operand;
						var branches = new Instruction[offsets.Length];
						for (int j = 0; j < offsets.Length; j++)
							branches[j] = GetInstruction(instructions, offsets[j]);

						instruction.Operand = branches;
						break;
				}
			}
		}

		static Instruction GetInstruction(List<Instruction> instructions, int offset)
		{
			var size = instructions.Count;
			if (offset < 0 || offset > instructions[size - 1].Offset)
				return null;

			int min = 0;
			int max = size - 1;
			while (min <= max)
			{
				int mid = min + ((max - min) / 2);
				var instruction = instructions[mid];
				var instruction_offset = instruction.Offset;

				if (offset == instruction_offset)
					return instruction;

				if (offset < instruction_offset)
					max = mid - 1;
				else
					min = mid + 1;
			}

			return null;
		}

		static bool TargetsLocalVariable(OpCode opcode)
		{
			return opcode.Name.Contains("loc");
		}

		LocalVariableInfo GetLocalVariable(int index)
		{
			return locals == null ? null : locals[index];
		}

		ParameterInfo GetParameter(int index)
		{
			if (index == 0)
				return this_parameter;

			return parameters[index - 1];
		}

		OpCode ReadOpCode()
		{
			var op = il.ReadByte();
			return op != 0xfe
				? one_byte_opcodes[op]
				: two_bytes_opcodes[il.ReadByte()];
		}

		public static List<Instruction> GetInstructions(MethodBase method, ILGenerator generator = null)
		{
			var reader = new MethodBodyReader(method, generator);
			reader.ReadInstructions();
			return reader.instructions;
		}

		class ThisParameter : ParameterInfo
		{
			public ThisParameter(MethodBase method)
			{
				MemberImpl = method;
				ClassImpl = method.DeclaringType;
				NameImpl = "this";
				PositionImpl = -1;
			}
		}
	}
}