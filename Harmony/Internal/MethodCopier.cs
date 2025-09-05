using MonoMod.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace HarmonyLib
{
	internal class MethodCopier
	{
		readonly MethodBodyReader reader;
		readonly List<MethodInfo> transpilers = [];

		internal MethodCopier(MethodBase fromMethod, ILGenerator toILGenerator, LocalBuilder[] existingVariables = null)
		{
			if (fromMethod is null)
				throw new ArgumentNullException(nameof(fromMethod));
			reader = new MethodBodyReader(fromMethod, toILGenerator);
			reader.DeclareVariables(existingVariables);
			reader.GenerateInstructions();
		}

		internal MethodCopier(MethodCreatorConfig config)
		{
			if (config.MethodBase is null)
				throw new ArgumentNullException("config.methodbase");
			reader = new MethodBodyReader(config.MethodBase, config.il);
			reader.DeclareVariables(config.originalVariables);
			reader.GenerateInstructions();
			reader.SetDebugging(config.debug);
		}

		internal void AddTranspiler(MethodInfo transpiler) => transpilers.Add(transpiler);

		internal List<CodeInstruction> Finalize(bool stripLastReturn, out bool hasReturnCode, out bool methodEndsInDeadCode, List<Label> endLabels)
			=> reader.FinalizeILCodes(transpilers, stripLastReturn, out hasReturnCode, out methodEndsInDeadCode, endLabels);

		internal static List<CodeInstruction> GetInstructions(ILGenerator generator, MethodBase method, int maxTranspilers)
		{
			if (generator is null)
				throw new ArgumentNullException(nameof(generator));
			if (method is null)
				throw new ArgumentNullException(nameof(method));

			var originalVariables = MethodPatcherTools.DeclareOriginalLocalVariables(generator, method);
			var copier = new MethodCopier(method, generator, originalVariables);

			var info = Harmony.GetPatchInfo(method);
			if (info is not null)
			{
				var sortedTranspilers = PatchFunctions.GetSortedPatchMethods(method, [.. info.Transpilers], false);
				for (var i = 0; i < maxTranspilers && i < sortedTranspilers.Count; i++)
					copier.AddTranspiler(sortedTranspilers[i]);
			}

			return copier.Finalize(false, out _, out _, null);
		}
	}

	internal class MethodBodyReader
	{
		readonly ILGenerator generator;
		readonly MethodBase method;
		bool debug = false;

		readonly Module module;
		readonly Type[] typeArguments;
		readonly Type[] methodArguments;
		readonly ByteBuffer ilBytes;
		readonly ParameterInfo this_parameter;
		readonly ParameterInfo[] parameters;
		readonly IList<ExceptionHandlingClause> exceptions;
		readonly List<ILInstruction> ilInstructions;
		readonly List<LocalVariableInfo> localVariables;

		LocalBuilder[] variables;

		internal static List<ILInstruction> GetInstructions(ILGenerator generator, MethodBase method)
		{
			if (method is null)
				throw new ArgumentNullException(nameof(method));
			var reader = new MethodBodyReader(method, generator);
			reader.DeclareVariables(null);
			reader.GenerateInstructions();
			return reader.ilInstructions;
		}

		internal MethodBodyReader(MethodBase method, ILGenerator generator)
		{
			this.generator = generator;
			this.method = method;
			module = method.Module;

			var body = method.GetMethodBody();
			if ((body?.GetILAsByteArray()?.Length ?? 0) == 0)
			{
				ilBytes = new ByteBuffer([]);
				ilInstructions = [];
			}
			else
			{
				var bytes = body.GetILAsByteArray();
				if (bytes is null)
					throw new ArgumentException("Can not get IL bytes of method " + method.FullDescription());
				ilBytes = new ByteBuffer(bytes);
				ilInstructions = new List<ILInstruction>((bytes.Length + 1) / 2);
			}

			var type = method.DeclaringType;

			if (type is not null && type.IsGenericType)
			{
				try
				{ typeArguments = type.GetGenericArguments(); }
				catch { typeArguments = null; }
			}

			if (method.IsGenericMethod)
			{
				try
				{ methodArguments = method.GetGenericArguments(); }
				catch { methodArguments = null; }
			}

			if (!method.IsStatic)
				this_parameter = new ThisParameter(method);
			parameters = method.GetParameters();

			localVariables = body?.LocalVariables?.ToList() ?? [];
			exceptions = body?.ExceptionHandlingClauses ?? [];
		}

		internal void SetDebugging(bool debug) => this.debug = debug;

		internal void GenerateInstructions()
		{
			while (ilBytes.position < ilBytes.buffer.Length)
			{
				var loc = ilBytes.position; // get location first (ReadOpCode will advance it)
				var instruction = new ILInstruction(ReadOpCode()) { offset = loc };
				ReadOperand(instruction);
				ilInstructions.Add(instruction);
			}

			HandleNativeMethod();

			ResolveBranches();
			ParseExceptions();
		}

		// if we have no instructions we probably deal with a native dllimport method
		//
		internal void HandleNativeMethod()
		{
			if (method is not MethodInfo methodInfo)
				return;

			// when we load reflection only, we don't need to do anything
			if (methodInfo.ReflectedType is not null)
				return;

			var dllAttribute = methodInfo.GetCustomAttributes(false).OfType<DllImportAttribute>().FirstOrDefault();
			if (dllAttribute == null)
				return;

			// Unique name, even for overloads: hash parameter types and append to type name
			var paramTypes = methodInfo.GetParameters().Select(p => p.ParameterType.FullName ?? p.ParameterType.Name).ToArray();
			var paramSignature = string.Join("_", paramTypes);
			var paramHash = paramSignature.Length > 0 ? paramSignature.GetHashCode().ToString("X") : "0";
			var name = $"{(methodInfo.DeclaringType?.FullName ?? "").Replace(".", "_")}_{methodInfo.Name}_{paramHash}";
			var assemblyName = new AssemblyName(name);
#if NET35
			var dynamicAssembly = AppDomain.CurrentDomain.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
#else
			var dynamicAssembly = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
#endif

#if !NETSTANDARD2_0
			var dynamicModule = dynamicAssembly.DefineDynamicModule(assemblyName.Name);
			var typeBuilder = dynamicModule.DefineType("NativeMethodHolder", TypeAttributes.Public | TypeAttributes.UnicodeClass);
			var methodBuilder = typeBuilder.DefinePInvokeMethod(methodInfo.Name, dllAttribute.Value,
				MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.PinvokeImpl,
				CallingConventions.Standard, methodInfo.ReturnType, [.. methodInfo.GetParameters().Select(x => x.ParameterType)],
				dllAttribute.CallingConvention, dllAttribute.CharSet
			);
			methodBuilder.SetImplementationFlags(methodBuilder.GetMethodImplementationFlags() | MethodImplAttributes.PreserveSig);
			var type = typeBuilder.CreateType();
			var proxyMethod = type.GetMethod(methodInfo.Name);
#else
			using var module = Mono.Cecil.ModuleDefinition.CreateModule(name, new Mono.Cecil.ModuleParameters() { Kind = Mono.Cecil.ModuleKind.Dll, ReflectionImporterProvider = MMReflectionImporter.Provider });

			var typeAttribute = Mono.Cecil.TypeAttributes.Public | Mono.Cecil.TypeAttributes.UnicodeClass;
			var typeDefinition = new Mono.Cecil.TypeDefinition("", name, typeAttribute) { BaseType = module.TypeSystem.Object };
			module.Types.Add(typeDefinition);

			var methodAttributes = Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.Static | Mono.Cecil.MethodAttributes.PInvokeImpl;
			var returnType = module.ImportReference(methodInfo.ReturnType); // Resolve(module, module.ImportReference(methodInfo.ReturnType));
			typeDefinition.Methods.Add(new Mono.Cecil.MethodDefinition(method.Name, methodAttributes, returnType)
			{
				IsPInvokeImpl = true,
				IsPreserveSig = true,
			});

			var loadedType = ReflectionHelper.Load(module).GetType(name);
			var proxyMethod = loadedType.GetMethod(method.Name);
#endif

			var argCount = method.GetParameters().Length;
			for (var i = 0; i < argCount; i++)
				ilInstructions.Add(new ILInstruction(OpCodes.Ldarg, i) { offset = 0 });
			ilInstructions.Add(new ILInstruction(OpCodes.Call, proxyMethod) { offset = argCount });
			ilInstructions.Add(new ILInstruction(OpCodes.Ret, null) { offset = argCount + 5 });
		}

		internal void DeclareVariables(LocalBuilder[] existingVariables)
		{
			if (generator is null)
				return;
			if (existingVariables is not null)
				variables = existingVariables;
			else
				variables = [.. localVariables.Select(lvi => generator.DeclareLocal(lvi.LocalType, lvi.IsPinned))];
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
				// var try_end = exception.TryOffset + exception.TryLength - 1;

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

		bool EndsInDeadCode(List<CodeInstruction> list)
		{
			var n = list.Count;
			if (n < 2 || list.Last().opcode != OpCodes.Throw)
				return false;
			return list.GetRange(0, n - 1).All(code => code.opcode != OpCodes.Ret);
		}

		internal List<CodeInstruction> FinalizeILCodes(List<MethodInfo> transpilers, bool stripLastReturn, out bool hasReturnCode, out bool methodEndsInDeadCode, List<Label> endLabels)
		{
			hasReturnCode = false;
			methodEndsInDeadCode = false;
			if (generator is null)
				return null;

			// pass1 - define labels and add them to instructions that are target of a jump
			//
			foreach (var ilInstruction in ilInstructions)
			{
				switch (ilInstruction.opcode.OperandType)
				{
					case OperandType.InlineSwitch:
					{
						var targets = ilInstruction.operand as ILInstruction[];
						if (targets is not null)
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
						if (target is not null)
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
			transpilers.Do(codeTranspiler.Add);
			var codeInstructions = codeTranspiler.GetResult(generator, method);

			// pass3 - check for any RET
			//
			hasReturnCode = codeInstructions.Any(code => code.opcode == OpCodes.Ret);
			methodEndsInDeadCode = EndsInDeadCode(codeInstructions);

			// pass4 - remove RET if it appears at the end
			//
			while (stripLastReturn)
			{
				var lastInstruction = codeInstructions.LastOrDefault();
				if (lastInstruction is null || lastInstruction.opcode != OpCodes.Ret)
					break;

				// remember any existing labels
				endLabels?.AddRange(lastInstruction.labels);

				codeInstructions.RemoveAt(codeInstructions.Count - 1);
			}

			return codeInstructions;
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
					var signature = InlineSignatureParser.ImportCallSite(module, bytes);
					instruction.operand = signature;
					instruction.argument = signature;
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
					((MemberInfo)instruction.operand).DeclaringType?.FixReflectionCacheAuto();
					GetMemberInfoValue((MemberInfo)instruction.operand, out instruction.argument);
					break;
				}

				case OperandType.InlineType:
				{
					var val = ilBytes.ReadInt32();
					instruction.operand = module.ResolveType(val, typeArguments, methodArguments);
					((Type)instruction.operand).FixReflectionCacheAuto();
					instruction.argument = (Type)instruction.operand;
					break;
				}

				case OperandType.InlineMethod:
				{
					var val = ilBytes.ReadInt32();
					instruction.operand = module.ResolveMethod(val, typeArguments, methodArguments);
					((MemberInfo)instruction.operand).DeclaringType?.FixReflectionCacheAuto();
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
					((MemberInfo)instruction.operand).DeclaringType?.FixReflectionCacheAuto();
					instruction.argument = (FieldInfo)instruction.operand;
					break;
				}

				case OperandType.ShortInlineVar:
				{
					var idx = ilBytes.ReadByte();
					if (TargetsLocalVariable(instruction.opcode))
					{
						var lvi = GetLocalVariable(idx);
						if (lvi is null)
							instruction.argument = idx;
						else
						{
							instruction.operand = lvi;
							instruction.argument = variables?[lvi.LocalIndex] ?? lvi;
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
						if (lvi is null)
							instruction.argument = idx;
						else
						{
							instruction.operand = lvi;
							instruction.argument = variables?[lvi.LocalIndex] ?? lvi;
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
			if (offset < 0)
				throw new ArgumentOutOfRangeException(nameof(offset), offset, $"Instruction offset {offset} is less than 0");

			var lastInstructionIndex = ilInstructions.Count - 1;
			var instruction = ilInstructions[lastInstructionIndex];
			if (offset > instruction.offset + instruction.GetSize() - 1)
				throw new ArgumentOutOfRangeException(nameof(offset), offset, $"Instruction offset {offset} is outside valid range 0 - {instruction.offset + instruction.GetSize() - 1}");

			var min = 0;
			var max = lastInstructionIndex;
			while (min <= max)
			{
				var mid = min + ((max - min) / 2);
				instruction = ilInstructions[mid];

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

			throw new Exception($"Cannot find instruction for {offset:X4}");
		}

		static bool TargetsLocalVariable(OpCode opcode) => opcode.Name.Contains("loc");

		LocalVariableInfo GetLocalVariable(int index) => localVariables?[index];

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

		// static initializer to prep opcodes

		static readonly OpCode[] one_byte_opcodes;
		static readonly OpCode[] two_bytes_opcodes;

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
		}

		// a custom this parameter

		class ThisParameter : ParameterInfo
		{
			internal ThisParameter(MethodBase method)
			{
				MemberImpl = method;
				ClassImpl = method.DeclaringType;
				NameImpl = "this";
				PositionImpl = -1;
			}
		}
	}
}
