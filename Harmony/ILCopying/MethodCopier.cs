using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace Harmony.ILCopying
{
	public class MethodCopier
	{
		readonly MethodBodyReader reader;
		readonly List<MethodInfo> transpilers = new List<MethodInfo>();

		public MethodCopier(MethodBase fromMethod, ILGenerator toILGenerator, LocalBuilder[] existingVariables = null)
		{
			if (fromMethod == null) throw new ArgumentNullException("Method cannot be null");
			reader = new MethodBodyReader(fromMethod, toILGenerator);
			reader.DeclareVariables(existingVariables);
			reader.ReadInstructions();
		}

		public void AddTranspiler(MethodInfo transpiler)
		{
			transpilers.Add(transpiler);
		}

		public void Finalize(List<Label> endLabels, List<ExceptionBlock> endBlocks)
		{
			reader.FinalizeILCodes(transpilers, endLabels, endBlocks);
		}
	}

	public class MethodBodyReader
	{
		readonly ILGenerator generator;

		readonly MethodBase method;
		readonly Module module;
		readonly Type[] typeArguments;
		readonly Type[] methodArguments;
		readonly ByteBuffer ilBytes;
		readonly ParameterInfo this_parameter;
		readonly ParameterInfo[] parameters;
		readonly IList<LocalVariableInfo> locals;
		readonly IList<ExceptionHandlingClause> exceptions;
		List<ILInstruction> ilInstructions;

		LocalBuilder[] variables;

		// NOTE: you cannot simply "copy" ILInstructions from a method. They contain references to
		// local variables which must be CREATED on an ILGenerator or else they are invalid when you
		// want to use the ILInstruction. If you are really clever, you can supply a dummy generator
		// and edit out all labels during the processing but that might be more trickier than you think
		//
		// In order to copy together a bunch of method parts within a transpiler, you have to pass in
		// your current generator that builds your new method
		//
		// You will end up with the sum of all declared local variables of all methods you run
		// GetInstructions on or use a dummy generator but edit out the invalid labels from the codes
		// you copy
		//
		public static List<ILInstruction> GetInstructions(ILGenerator generator, MethodBase method)
		{
			if (method == null) throw new ArgumentNullException("Method cannot be null");
			var reader = new MethodBodyReader(method, generator);
			reader.DeclareVariables(null);
			reader.ReadInstructions();
			return reader.ilInstructions;
		}

		// constructor
		//
		public MethodBodyReader(MethodBase method, ILGenerator generator)
		{
			this.generator = generator;
			this.method = method;
			module = method.Module;

			var body = method.GetMethodBody();
			if (body == null)
				throw new ArgumentException("Method " + method.FullDescription() + " has no body");

			var bytes = body.GetILAsByteArray();
			if (bytes == null)
				throw new ArgumentException("Can not get IL bytes of method " + method.FullDescription());
			ilBytes = new ByteBuffer(bytes);
			ilInstructions = new List<ILInstruction>((bytes.Length + 1) / 2);

			var type = method.DeclaringType;

			if (type.IsGenericType)
			{
				try { typeArguments = type.GetGenericArguments(); }
				catch { typeArguments = null; }
			}

			if (method.IsGenericMethod)
			{
				try { methodArguments = method.GetGenericArguments(); }
				catch { methodArguments = null; }
			}

			if (!method.IsStatic)
				this_parameter = new ThisParameter(method);
			parameters = method.GetParameters();

			locals = body.LocalVariables;
			exceptions = body.ExceptionHandlingClauses;
		}

		// read and parse IL codes
		//
		public void ReadInstructions()
		{
			while (ilBytes.position < ilBytes.buffer.Length)
			{
				var loc = ilBytes.position; // get location first (ReadOpCode will advance it)
				var instruction = new ILInstruction(ReadOpCode()) { offset = loc };
				ReadOperand(instruction);
				ilInstructions.Add(instruction);
			}

			ResolveBranches();
			ParseExceptions();
		}

		// declare local variables
		//
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

		// process all jumps
		//
		void ResolveBranches()
		{
			foreach (var ilInstruction in ilInstructions)
			{
				switch (ilInstruction.opcode.OperandType)
				{
					case OperandType.ShortInlineBrTarget:
					case OperandType.InlineBrTarget:
						ilInstruction.operand = GetInstruction((int)ilInstruction.operand, false);
						break;

					case OperandType.InlineSwitch:
						var offsets = (int[])ilInstruction.operand;
						var branches = new ILInstruction[offsets.Length];
						for (var j = 0; j < offsets.Length; j++)
							branches[j] = GetInstruction(offsets[j], false);

						ilInstruction.operand = branches;
						break;
				}
			}
		}

		// process all exception blocks
		//
		void ParseExceptions()
		{
			foreach (var exception in exceptions)
			{
				var try_start = exception.TryOffset;
				var try_end = exception.TryOffset + exception.TryLength - 1;

				var handler_start = exception.HandlerOffset;
				var handler_end = exception.HandlerOffset + exception.HandlerLength - 1;

				var instr1 = GetInstruction(try_start, false);
				instr1.blocks.Add(new ExceptionBlock(ExceptionBlockType.BeginExceptionBlock, null));

				var instr2 = GetInstruction(handler_end, true);
				instr2.blocks.Add(new ExceptionBlock(ExceptionBlockType.EndExceptionBlock, null));

				// The FilterOffset property is meaningful only for Filter clauses. 
				// The CatchType property is not meaningful for Filter or Finally clauses. 
				//
				switch (exception.Flags)
				{
					case ExceptionHandlingClauseOptions.Filter:
						var instr3 = GetInstruction(exception.FilterOffset, false);
						instr3.blocks.Add(new ExceptionBlock(ExceptionBlockType.BeginExceptFilterBlock, null));
						break;

					case ExceptionHandlingClauseOptions.Finally:
						var instr4 = GetInstruction(handler_start, false);
						instr4.blocks.Add(new ExceptionBlock(ExceptionBlockType.BeginFinallyBlock, null));
						break;

					case ExceptionHandlingClauseOptions.Clause:
						var instr5 = GetInstruction(handler_start, false);
						instr5.blocks.Add(new ExceptionBlock(ExceptionBlockType.BeginCatchBlock, exception.CatchType));
						break;

					case ExceptionHandlingClauseOptions.Fault:
						var instr6 = GetInstruction(handler_start, false);
						instr6.blocks.Add(new ExceptionBlock(ExceptionBlockType.BeginFaultBlock, null));
						break;
				}
			}
		}

		// used in FinalizeILCodes to convert short jumps to long ones
		static Dictionary<OpCode, OpCode> shortJumps = new Dictionary<OpCode, OpCode>()
		{
			{ OpCodes.Leave_S, OpCodes.Leave },
			{ OpCodes.Brfalse_S, OpCodes.Brfalse },
			{ OpCodes.Brtrue_S, OpCodes.Brtrue },
			{ OpCodes.Beq_S, OpCodes.Beq },
			{ OpCodes.Bge_S, OpCodes.Bge },
			{ OpCodes.Bgt_S, OpCodes.Bgt },
			{ OpCodes.Ble_S, OpCodes.Ble },
			{ OpCodes.Blt_S, OpCodes.Blt },
			{ OpCodes.Bne_Un_S, OpCodes.Bne_Un },
			{ OpCodes.Bge_Un_S, OpCodes.Bge_Un },
			{ OpCodes.Bgt_Un_S, OpCodes.Bgt_Un },
			{ OpCodes.Ble_Un_S, OpCodes.Ble_Un },
			{ OpCodes.Br_S, OpCodes.Br },
			{ OpCodes.Blt_Un_S, OpCodes.Blt_Un }
		};

		// use parsed IL codes and emit them to a generator
		//
		public void FinalizeILCodes(List<MethodInfo> transpilers, List<Label> endLabels, List<ExceptionBlock> endBlocks)
		{
			if (generator == null) return;

			// pass1 - define labels and add them to instructions that are target of a jump
			//
			foreach (var ilInstruction in ilInstructions)
			{
				switch (ilInstruction.opcode.OperandType)
				{
					case OperandType.InlineSwitch:
						{
							var targets = ilInstruction.operand as ILInstruction[];
							if (targets != null)
							{
								var labels = new List<Label>();
								foreach (var target in targets)
								{
									var label = generator.DefineLabel();
									target.labels.Add(label);
									labels.Add(label);
								}
								ilInstruction.argument = labels.ToArray();
							}
							break;
						}

					case OperandType.ShortInlineBrTarget:
					case OperandType.InlineBrTarget:
						{
							var target = ilInstruction.operand as ILInstruction;
							if (target != null)
							{
								var label = generator.DefineLabel();
								target.labels.Add(label);
								ilInstruction.argument = label;
							}
							break;
						}
				}
			}

			// pass2 - filter through all processors
			//
			var codeTranspiler = new CodeTranspiler(ilInstructions);
			transpilers.Do(transpiler => codeTranspiler.Add(transpiler));
			var codeInstructions = codeTranspiler.GetResult(generator, method);

			// pass3 - remove RET if it appears at the end
			while (true)
			{
				var lastInstruction = codeInstructions.LastOrDefault();
				if (lastInstruction == null || lastInstruction.opcode != OpCodes.Ret) break;

				// remember any existing labels
				endLabels.AddRange(lastInstruction.labels);

				codeInstructions.RemoveAt(codeInstructions.Count - 1);
			}

			// pass4 - mark labels and exceptions and emit codes
			//
			var idx = 0;
			codeInstructions.Do(codeInstruction =>
			{
				// mark all labels
				codeInstruction.labels.Do(label => Emitter.MarkLabel(generator, label));

				// start all exception blocks
				// TODO: we ignore the resulting label because we have no way to use it
				//
				codeInstruction.blocks.Do(block => { Label? label; Emitter.MarkBlockBefore(generator, block, out label); });

				var code = codeInstruction.opcode;
				var operand = codeInstruction.operand;

				// replace RET with a jump to the end (outside this code)
				if (code == OpCodes.Ret)
				{
					var endLabel = generator.DefineLabel();
					code = OpCodes.Br;
					operand = endLabel;
					endLabels.Add(endLabel);
				}

				// replace short jumps with long ones (can be optimized but requires byte counting, not instruction counting)
				if (shortJumps.TryGetValue(code, out var longJump))
					code = longJump;

				var emitCode = true;

				//if (code == OpCodes.Leave || code == OpCodes.Leave_S)
				//{
				//	// skip LEAVE on EndExceptionBlock
				//	if (codeInstruction.blocks.Any(block => block.blockType == ExceptionBlockType.EndExceptionBlock))
				//		emitCode = false;

				//	// skip LEAVE on next instruction starts a new exception handler and we are already in 
				//	if (idx < instructions.Length - 1)
				//		if (instructions[idx + 1].blocks.Any(block => block.blockType != ExceptionBlockType.EndExceptionBlock))
				//			emitCode = false;
				//}

				if (emitCode)
				{
					switch (code.OperandType)
					{
						case OperandType.InlineNone:
							Emitter.Emit(generator, code);
							break;

						case OperandType.InlineSig:

							// TODO the following will fail because we do not convert the token (operand)
							// All the decompilers can show the arguments correctly, we just need to find out how
							//
							if (operand == null) throw new Exception("Wrong null argument: " + codeInstruction);
							if ((operand is int) == false) throw new Exception("Wrong Emit argument type " + operand.GetType() + " in " + codeInstruction);
							Emitter.Emit(generator, code, (int)operand);

							/*
							// the following will only work if we can convert the original signature token to the required arguments
							//
							var callingConvention = System.Runtime.InteropServices.CallingConvention.ThisCall;
							var returnType = typeof(object);
							var parameterTypes = new[] { typeof(object) };
							Emitter.EmitCalli(generator, code, callingConvention, returnType, parameterTypes);

							var callingConventions = System.Reflection.CallingConventions.Standard;
							var optionalParameterTypes = new[] { typeof(object) };
							Emitter.EmitCalli(generator, code, callingConventions, returnType, parameterTypes, optionalParameterTypes);
							*/
							break;

						default:
							if (operand == null) throw new Exception("Wrong null argument: " + codeInstruction);
							var emitMethod = EmitMethodForType(operand.GetType());
							if (emitMethod == null) throw new Exception("Unknown Emit argument type " + operand.GetType() + " in " + codeInstruction);
							if (HarmonyInstance.DEBUG) FileLog.LogBuffered(Emitter.CodePos(generator) + code + " " + Emitter.FormatArgument(operand));
							emitMethod.Invoke(generator, new object[] { code, operand });
							break;
					}
				}

				codeInstruction.blocks.Do(block => Emitter.MarkBlockAfter(generator, block));

				idx++;
			});
		}

		// interpret member info value
		//
		static void GetMemberInfoValue(MemberInfo info, out object result)
		{
			result = null;
			switch (info.MemberType)
			{
				case MemberTypes.Constructor:
					result = (ConstructorInfo)info;
					break;

				case MemberTypes.Event:
					result = (EventInfo)info;
					break;

				case MemberTypes.Field:
					result = (FieldInfo)info;
					break;

				case MemberTypes.Method:
					result = (MethodInfo)info;
					break;

				case MemberTypes.TypeInfo:
				case MemberTypes.NestedType:
					result = (Type)info;
					break;

				case MemberTypes.Property:
					result = (PropertyInfo)info;
					break;
			}
		}

		// interpret instruction operand
		//
		void ReadOperand(ILInstruction instruction)
		{
			switch (instruction.opcode.OperandType)
			{
				case OperandType.InlineNone:
					{
						instruction.argument = null;
						break;
					}

				case OperandType.InlineSwitch:
					{
						var length = ilBytes.ReadInt32();
						var base_offset = ilBytes.position + (4 * length);
						var branches = new int[length];
						for (var i = 0; i < length; i++)
							branches[i] = ilBytes.ReadInt32() + base_offset;
						instruction.operand = branches;
						break;
					}

				case OperandType.ShortInlineBrTarget:
					{
						var val = (sbyte)ilBytes.ReadByte();
						instruction.operand = val + ilBytes.position;
						break;
					}

				case OperandType.InlineBrTarget:
					{
						var val = ilBytes.ReadInt32();
						instruction.operand = val + ilBytes.position;
						break;
					}

				case OperandType.ShortInlineI:
					{
						if (instruction.opcode == OpCodes.Ldc_I4_S)
						{
							var sb = (sbyte)ilBytes.ReadByte();
							instruction.operand = sb;
							instruction.argument = (sbyte)instruction.operand;
						}
						else
						{
							var b = ilBytes.ReadByte();
							instruction.operand = b;
							instruction.argument = (byte)instruction.operand;
						}
						break;
					}

				case OperandType.InlineI:
					{
						var val = ilBytes.ReadInt32();
						instruction.operand = val;
						instruction.argument = (int)instruction.operand;
						break;
					}

				case OperandType.ShortInlineR:
					{
						var val = ilBytes.ReadSingle();
						instruction.operand = val;
						instruction.argument = (float)instruction.operand;
						break;
					}

				case OperandType.InlineR:
					{
						var val = ilBytes.ReadDouble();
						instruction.operand = val;
						instruction.argument = (double)instruction.operand;
						break;
					}

				case OperandType.InlineI8:
					{
						var val = ilBytes.ReadInt64();
						instruction.operand = val;
						instruction.argument = (long)instruction.operand;
						break;
					}

				case OperandType.InlineSig:
					{
						var val = ilBytes.ReadInt32();
						var bytes = module.ResolveSignature(val);
						instruction.operand = bytes;
						instruction.argument = bytes;
						Debugger.Log(0, "TEST", "METHOD " + method.FullDescription() + "\n");
						Debugger.Log(0, "TEST", "Signature = " + bytes.Select(b => string.Format("0x{0:x02}", b)).Aggregate((a, b) => a + " " + b) + "\n");
						Debugger.Break();
						break;
					}

				case OperandType.InlineString:
					{
						var val = ilBytes.ReadInt32();
						instruction.operand = module.ResolveString(val);
						instruction.argument = (string)instruction.operand;
						break;
					}

				case OperandType.InlineTok:
					{
						var val = ilBytes.ReadInt32();
						instruction.operand = module.ResolveMember(val, typeArguments, methodArguments);
						GetMemberInfoValue((MemberInfo)instruction.operand, out instruction.argument);
						break;
					}

				case OperandType.InlineType:
					{
						var val = ilBytes.ReadInt32();
						instruction.operand = module.ResolveType(val, typeArguments, methodArguments);
						instruction.argument = (Type)instruction.operand;
						break;
					}

				case OperandType.InlineMethod:
					{
						var val = ilBytes.ReadInt32();
						instruction.operand = module.ResolveMethod(val, typeArguments, methodArguments);
						if (instruction.operand is ConstructorInfo)
							instruction.argument = (ConstructorInfo)instruction.operand;
						else
							instruction.argument = (MethodInfo)instruction.operand;
						break;
					}

				case OperandType.InlineField:
					{
						var val = ilBytes.ReadInt32();
						instruction.operand = module.ResolveField(val, typeArguments, methodArguments);
						instruction.argument = (FieldInfo)instruction.operand;
						break;
					}

				case OperandType.ShortInlineVar:
					{
						var idx = ilBytes.ReadByte();
						if (TargetsLocalVariable(instruction.opcode))
						{
							var lvi = GetLocalVariable(idx);
							if (lvi == null)
								instruction.argument = idx;
							else
							{
								instruction.operand = lvi;
								instruction.argument = variables[lvi.LocalIndex];
							}
						}
						else
						{
							instruction.operand = GetParameter(idx);
							instruction.argument = idx;
						}
						break;
					}

				case OperandType.InlineVar:
					{
						var idx = ilBytes.ReadInt16();
						if (TargetsLocalVariable(instruction.opcode))
						{
							var lvi = GetLocalVariable(idx);
							if (lvi == null)
								instruction.argument = idx;
							else
							{
								instruction.operand = lvi;
								instruction.argument = variables[lvi.LocalIndex];
							}
						}
						else
						{
							instruction.operand = GetParameter(idx);
							instruction.argument = idx;
						}
						break;
					}

				default:
					throw new NotSupportedException();
			}
		}

		ILInstruction GetInstruction(int offset, bool isEndOfInstruction)
		{
			var lastInstructionIndex = ilInstructions.Count - 1;
			if (offset < 0 || offset > ilInstructions[lastInstructionIndex].offset)
				throw new Exception("Instruction offset " + offset + " is outside valid range 0 - " + ilInstructions[lastInstructionIndex].offset);

			var min = 0;
			var max = lastInstructionIndex;
			while (min <= max)
			{
				var mid = min + ((max - min) / 2);
				var instruction = ilInstructions[mid];

				if (isEndOfInstruction)
				{
					if (offset == instruction.offset + instruction.GetSize() - 1)
						return instruction;
				}
				else
				{
					if (offset == instruction.offset)
						return instruction;
				}

				if (offset < instruction.offset)
					max = mid - 1;
				else
					min = mid + 1;
			}

			throw new Exception("Cannot find instruction for " + offset.ToString("X4"));
		}

		static bool TargetsLocalVariable(OpCode opcode)
		{
			return opcode.Name.Contains("loc");
		}

		LocalVariableInfo GetLocalVariable(int index)
		{
			return locals?[index];
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

		MethodInfo EmitMethodForType(Type type)
		{
			foreach (var entry in emitMethods)
				if (entry.Key == type) return entry.Value;
			foreach (var entry in emitMethods)
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
				.Do(method =>
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