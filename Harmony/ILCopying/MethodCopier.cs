using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

// Based on the idea of https://github.com/jbevain/mono.reflection

namespace Harmony.ILCopying
{
	public class MethodCopier
	{
		MethodBodyReader reader;
		Dictionary<ILCode, ILCode> replacements;

		public MethodCopier(MethodBase fromMethod, DynamicMethod toDynamicMethod, LocalBuilder[] existingVariables = null)
		{
			if (fromMethod == null) throw new Exception("method cannot be null");
			var generator = toDynamicMethod.GetILGenerator();
			reader = new MethodBodyReader(fromMethod, generator);
			reader.DeclareVariables(existingVariables);
			reader.ReadInstructions();
			replacements = new Dictionary<ILCode, ILCode>();
		}

		public void AddReplacement(ILCode original, ILCode replacement)
		{
			replacements[original] = replacement;
		}

		public void Emit()
		{
			reader.ModifyInstructions(replacements);
			reader.FinalizeILCodes();
		}
	}

	public class MethodBodyReader
	{
		readonly ILGenerator generator;

		readonly Module module;
		readonly Type[] type_arguments;
		readonly Type[] method_arguments;
		readonly ByteBuffer ilBytes;
		readonly ParameterInfo this_parameter;
		readonly ParameterInfo[] parameters;
		readonly IList<LocalVariableInfo> locals;
		readonly IList<ExceptionHandlingClause> exceptions;
		readonly List<ILInstruction> instructions;

		LocalBuilder[] variables;

		public static List<ILInstruction> GetInstructions(MethodBase method)
		{
			if (method == null) throw new Exception("method cannot be null");
			var reader = new MethodBodyReader(method, null);
			reader.ReadInstructions();
			return reader.instructions;
		}

		// constructor

		public MethodBodyReader(MethodBase method, ILGenerator generator)
		{
			this.generator = generator;
			module = method.Module;

			var body = method.GetMethodBody();
			if (body == null)
				throw new ArgumentException("Method has no body");

			var bytes = body.GetILAsByteArray();
			if (bytes == null)
				throw new ArgumentException("Can not get the bytes of the method");
			ilBytes = new ByteBuffer(bytes);
			instructions = new List<ILInstruction>((bytes.Length + 1) / 2);

			if (!(method is ConstructorInfo))
				method_arguments = method.GetGenericArguments();

			if (method.DeclaringType != null)
				type_arguments = method.DeclaringType.GetGenericArguments();

			if (!method.IsStatic)
				this_parameter = new ThisParameter(method);
			parameters = method.GetParameters();

			locals = body.LocalVariables;
			exceptions = body.ExceptionHandlingClauses;
		}

		// read and parse IL codes

		public void ReadInstructions()
		{
			ILInstruction previous = null;

			while (ilBytes.position < ilBytes.buffer.Length)
			{
				var loc = ilBytes.position; // get location first (ReadOpCode will advance it)
				var instruction = new ILInstruction(ReadOpCode());
				instruction.Offset = loc;
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

		void ResolveBranches()
		{
			foreach (var instruction in instructions)
			{
				switch (instruction.OpCode.OperandType)
				{
					case OperandType.ShortInlineBrTarget:
					case OperandType.InlineBrTarget:
						instruction.Operand = GetInstruction((int)instruction.Operand);
						break;

					case OperandType.InlineSwitch:
						var offsets = (int[])instruction.Operand;
						var branches = new ILInstruction[offsets.Length];
						for (int j = 0; j < offsets.Length; j++)
							branches[j] = GetInstruction(offsets[j]);

						instruction.Operand = branches;
						break;
				}
			}
		}

		// 
		public void ModifyInstructions(Dictionary<ILCode, ILCode> replacements)
		{
			if (replacements != null)
			{
				instructions.ForEach((instr) =>
				{
					var original = new ILCode(instr);
					foreach (KeyValuePair<ILCode, ILCode> entry in replacements)
						if (original.Equals(entry.Key)) entry.Value.Apply(instr);
				});
			}
		}

		// declare local variables
		public void DeclareVariables(LocalBuilder[] existingVariables)
		{
			if (generator == null) return;
			if (existingVariables != null)
				variables = existingVariables;
			else
				variables = locals.Select(
					lvi => generator.DeclareLocal(lvi.LocalType, lvi.IsPinned)
				).ToArray();
		}

		// use parsed IL codes and emit them to a generator

		public void FinalizeILCodes()
		{
			if (generator == null) return;

			// pass1 - define labels and add them to instructions that are target of a jump
			instructions.ForEach(instruction =>
			{
				var code = instruction.OpCode;
				switch (code.OperandType)
				{
					case OperandType.InlineSwitch:
						{
							var targets = instruction.Operand as ILInstruction[];
							if (targets != null)
							{
								var labels = new List<Label>();
								foreach (var target in targets)
								{
									var label = generator.DefineLabel();
									target.AddLabel(label);
									labels.Add(label);
								}
								instruction.Argument = labels.ToArray();
							}
							break;
						}

					case OperandType.ShortInlineBrTarget:
					case OperandType.InlineBrTarget:
						{
							var target = instruction.Operand as ILInstruction;
							if (target != null)
							{
								var label = generator.DefineLabel();
								target.AddLabel(label);
								instruction.Argument = label;
							}
							break;
						}
				}
			});

			// pass2 - mark labels and emit codes
			instructions.ForEach(instruction =>
			{
				foreach (var label in instruction.Labels)
					generator.MarkLabel(label);

				var code = instruction.OpCode;
				var arg = instruction.Argument;
				if (code.OperandType == OperandType.InlineNone)
					generator.Emit(code);
				else
				{
					if (arg == null) throw new Exception("Wrong null argument: " + instruction);
					var emitMethod = EmitMethodForType(arg.GetType());
					if (emitMethod == null) throw new Exception("Unknown Emit argument type " + arg.GetType() + " in " + instruction);
					emitMethod.Invoke(generator, new object[] { code, arg });
				}
			});
		}

		// interpret instruction operand

		void ReadOperand(ILInstruction instruction)
		{
			switch (instruction.OpCode.OperandType)
			{
				case OperandType.InlineNone:
					{
						instruction.Argument = null;
						break;
					}

				case OperandType.InlineSwitch:
					{
						var length = ilBytes.ReadInt32();
						var base_offset = ilBytes.position + (4 * length);
						var branches = new int[length];
						for (int i = 0; i < length; i++)
							branches[i] = ilBytes.ReadInt32() + base_offset;
						instruction.Operand = branches;
						break;
					}

				case OperandType.ShortInlineBrTarget:
					{
						var val = (sbyte)ilBytes.ReadByte();
						instruction.Operand = val + ilBytes.position;
						break;
					}

				case OperandType.InlineBrTarget:
					{
						var val = ilBytes.ReadInt32();
						instruction.Operand = val + ilBytes.position;
						break;
					}

				case OperandType.ShortInlineI:
					{
						if (instruction.OpCode == OpCodes.Ldc_I4_S)
						{
							var sb = (sbyte)ilBytes.ReadByte();
							instruction.Operand = sb;
							instruction.Argument = (sbyte)instruction.Operand;
						}
						else
						{
							var b = ilBytes.ReadByte();
							instruction.Operand = b;
							instruction.Argument = (byte)instruction.Operand;
						}
						break;
					}

				case OperandType.InlineI:
					{
						var val = ilBytes.ReadInt32();
						instruction.Operand = val;
						instruction.Argument = (int)instruction.Operand;
						break;
					}

				case OperandType.ShortInlineR:
					{
						var val = ilBytes.ReadSingle();
						instruction.Operand = val;
						instruction.Argument = (float)instruction.Operand;
						break;
					}

				case OperandType.InlineR:
					{
						var val = ilBytes.ReadDouble();
						instruction.Operand = val;
						instruction.Argument = (double)instruction.Operand;
						break;
					}

				case OperandType.InlineI8:
					{
						var val = ilBytes.ReadInt64();
						instruction.Operand = val;
						instruction.Argument = (long)instruction.Operand;
						break;
					}

				case OperandType.InlineSig:
					{
						var val = ilBytes.ReadInt32();
						instruction.Operand = module.ResolveSignature(val);
						instruction.Argument = (SignatureHelper)instruction.Operand;
						break;
					}

				case OperandType.InlineString:
					{
						var val = ilBytes.ReadInt32();
						instruction.Operand = module.ResolveString(val);
						instruction.Argument = (string)instruction.Operand;
						break;
					}

				case OperandType.InlineTok:
					{
						var val = ilBytes.ReadInt32();
						instruction.Operand = module.ResolveMember(val, type_arguments, method_arguments);
						instruction.Argument = (Type)instruction.Operand;
						break;
					}

				case OperandType.InlineType:
					{
						var val = ilBytes.ReadInt32();
						instruction.Operand = module.ResolveMember(val, type_arguments, method_arguments);
						instruction.Argument = (Type)instruction.Operand;
						break;
					}

				case OperandType.InlineMethod:
					{
						var val = ilBytes.ReadInt32();
						instruction.Operand = module.ResolveMember(val, type_arguments, method_arguments);
						if (instruction.Operand is ConstructorInfo)
							instruction.Argument = (ConstructorInfo)instruction.Operand;
						else
							instruction.Argument = (MethodInfo)instruction.Operand;
						break;
					}

				case OperandType.InlineField:
					{
						var val = ilBytes.ReadInt32();
						instruction.Operand = module.ResolveMember(val, type_arguments, method_arguments);
						instruction.Argument = (FieldInfo)instruction.Operand;
						break;
					}

				case OperandType.ShortInlineVar:
					{
						var idx = ilBytes.ReadByte();
						if (TargetsLocalVariable(instruction.OpCode))
						{
							var lvi = GetLocalVariable(idx);
							if (lvi == null)
								instruction.Argument = idx;
							else
							{
								instruction.Operand = lvi;
								instruction.Argument = variables[lvi.LocalIndex];
							}
						}
						else
						{
							instruction.Operand = GetParameter(idx);
							instruction.Argument = idx;
						}
						break;
					}

				case OperandType.InlineVar:
					{
						var idx = ilBytes.ReadInt16();
						if (TargetsLocalVariable(instruction.OpCode))
						{
							var lvi = GetLocalVariable(idx);
							if (lvi == null)
								instruction.Argument = idx;
							else
							{
								instruction.Operand = lvi;
								instruction.Argument = variables[lvi.LocalIndex];
							}
						}
						else
						{
							instruction.Operand = GetParameter(idx);
							instruction.Argument = idx;
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
			foreach (var ehc in exceptions)
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

		ILInstruction GetInstruction(int offset)
		{
			var lastInstructionIndex = instructions.Count - 1;
			if (offset < 0 || offset > instructions[lastInstructionIndex].Offset)
				throw new Exception("Instruction offset " + offset + " is outside valid range 0 - " + instructions[lastInstructionIndex].Offset);

			int min = 0;
			int max = lastInstructionIndex;
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

			throw new Exception("Cannot find instruction for " + offset);
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
			var op = ilBytes.ReadByte();
			return op != 0xfe
				? one_byte_opcodes[op]
				: two_bytes_opcodes[ilBytes.ReadByte()];
		}

		private MethodInfo EmitMethodForType(Type type)
		{
			foreach (KeyValuePair<Type, MethodInfo> entry in emitMethods)
				if (entry.Key == type) return entry.Value;
			foreach (KeyValuePair<Type, MethodInfo> entry in emitMethods)
				if (entry.Key.IsAssignableFrom(type)) return entry.Value;
			return null;
		}

		// static initializer to prep opcodes

		static readonly OpCode[] one_byte_opcodes;
		static readonly OpCode[] two_bytes_opcodes;

		static readonly Dictionary<Type, MethodInfo> emitMethods;

		[MethodImpl(MethodImplOptions.Synchronized)]
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

			emitMethods = new Dictionary<Type, MethodInfo>();
			typeof(ILGenerator).GetMethods().ToList()
				.ForEach(method =>
				{
					if (method.Name != "Emit") return;
					var pinfos = method.GetParameters();
					if (pinfos.Length != 2) return;
					var types = pinfos.Select(p => p.ParameterType).ToArray();
					if (types[0] != typeof(OpCode)) return;
					emitMethods[types[1]] = method;
				});
		}

		// a custom this parameter

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