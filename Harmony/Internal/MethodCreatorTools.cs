using MonoMod.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using static HarmonyLib.Code;

namespace HarmonyLib
{
	internal static class MethodCreatorTools
	{
		internal const string PARAM_INDEX_PREFIX = "__";
		const string INSTANCE_FIELD_PREFIX = "___";

		static readonly Dictionary<OpCode, OpCode> shortJumps = new()
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

		internal static List<CodeInstruction> GenerateVariableInit(this MethodCreator _, LocalBuilder variable, bool isReturnValue = false)
		{
			var codes = new List<CodeInstruction>();
			var type = variable.LocalType;
			if (type.IsByRef)
			{
				if (isReturnValue)
				{
					codes.Add(Ldc_I4_1);
					codes.Add(Newarr[type.GetElementType()]);
					codes.Add(Ldc_I4_0);
					codes.Add(Ldelema[type.GetElementType()]);
					codes.Add(Stloc[variable]);
					return codes;
				}
				else
					type = type.GetElementType();
			}
			if (type.IsEnum)
				type = Enum.GetUnderlyingType(type);

			if (AccessTools.IsClass(type))
			{
				codes.Add(Ldnull);
				codes.Add(Stloc[variable]);
				return codes;
			}
			if (AccessTools.IsStruct(type))
			{
				codes.Add(Ldloca[variable]);
				codes.Add(Initobj[type]);
				return codes;
			}
			if (AccessTools.IsValue(type))
			{
				if (type == typeof(float))
					codes.Add(Ldc_R4[(float)0]);
				else if (type == typeof(double))
					codes.Add(Ldc_R8[(double)0]);
				else if (type == typeof(long) || type == typeof(ulong))
					codes.Add(Ldc_I8[(long)0]);
				else
					codes.Add(Ldc_I4[0]);
				codes.Add(Stloc[variable]);
				return codes;
			}
			return codes;
		}

		internal static List<CodeInstruction> PrepareArgumentArray(this MethodCreator creator)
		{
			var codes = new List<CodeInstruction>();
			var original = creator.config.original;
			var originalIsStatic = original.IsStatic;
			var parameters = original.GetParameters();
			var i = 0;
			foreach (var pInfo in parameters)
			{
				var argIndex = i++ + (originalIsStatic ? 0 : 1);
				if (pInfo.IsOut || pInfo.IsRetval)
					codes.AddRange(InitializeOutParameter(argIndex, pInfo.ParameterType));
			}
			codes.Add(Ldc_I4[parameters.Length]);
			codes.Add(Newarr[typeof(object)]);
			i = 0;
			var arrayIdx = 0;
			foreach (var pInfo in parameters)
			{
				var argIndex = i++ + (originalIsStatic ? 0 : 1);
				var pType = pInfo.ParameterType;
				var paramByRef = pType.IsByRef;
				if (paramByRef)
					pType = pType.GetElementType();
				codes.Add(Dup);
				codes.Add(Ldc_I4[arrayIdx++]);
				codes.Add(Ldarg[argIndex]);
				if (paramByRef)
				{
					if (AccessTools.IsStruct(pType))
						codes.Add(Ldobj[pType]);
					else
						codes.Add(LoadIndOpCodeFor(pType));
				}
				if (pType.IsValueType)
					codes.Add(Box[pType]);
				codes.Add(Stelem_Ref);
			}
			return codes;
		}

		internal static bool AffectsOriginal(this MethodCreator creator, MethodInfo fix)
		{
			if (fix.ReturnType == typeof(bool))
				return true;

			if (creator.config.injections.TryGetValue(fix, out var injectedParameters) == false)
				return false;

			return injectedParameters.Any(parameter =>
			{
				if (parameter.injectionType == InjectionType.Instance)
					return false;
				if (parameter.injectionType == InjectionType.OriginalMethod)
					return false;
				if (parameter.injectionType == InjectionType.State)
					return false;

				var p = parameter.parameterInfo;
				if (p.IsOut || p.IsRetval)
					return true;
				var type = p.ParameterType;
				if (type.IsByRef)
					return true;
				if (AccessTools.IsValue(type) is false && AccessTools.IsStruct(type) is false)
					return true;

				return false;
			});
		}

		internal static CodeInstruction MarkBlock(this MethodCreator _, ExceptionBlockType blockType)
			=> Nop.WithBlocks(new ExceptionBlock(blockType));

		internal static List<CodeInstruction> EmitCallParameter(
			this MethodCreator creator,
			MethodInfo patch,
			bool allowFirsParamPassthrough,
			out LocalBuilder tmpInstanceBoxingVar,
			out LocalBuilder tmpObjectVar,
			out bool refResultUsed,
			List<KeyValuePair<LocalBuilder, Type>> tmpBoxVars
		)
		{
			tmpInstanceBoxingVar = null;
			tmpObjectVar = null;
			refResultUsed = false;
			var codes = new List<CodeInstruction>();

			var config = creator.config;
			var original = config.original;
			var originalIsStatic = original.IsStatic;
			var returnType = config.returnType;
			var injections = config.injections[patch].ToList();

			var isInstance = originalIsStatic is false;
			var originalParameters = original.GetParameters();
			var originalParameterNames = originalParameters.Select(p => p.Name).ToArray();
			var originalType = original.DeclaringType;

			var parameters = patch.GetParameters().ToList();
			if (allowFirsParamPassthrough && patch.ReturnType != typeof(void) && parameters.Count > 0 && parameters[0].ParameterType == patch.ReturnType)
			{
				injections.RemoveAt(0);
				parameters.RemoveAt(0);
			}

			foreach (var injection in injections)
			{
				var injectionType = injection.injectionType;
				var paramRealName = injection.realName;
				var paramType = injection.parameterInfo.ParameterType;

				if (injectionType == InjectionType.OriginalMethod)
				{
					if (EmitOriginalBaseMethod(original, codes))
						continue;

					codes.Add(Ldnull);
					continue;
				}

				if (injectionType == InjectionType.Exception)
				{
					if (config.exceptionVariable != null)
						codes.Add(Ldloc[config.exceptionVariable]);
					else
						codes.Add(Ldnull);
					continue;
				}

				if (injectionType == InjectionType.RunOriginal)
				{
					if (config.runOriginalVariable != null)
						codes.Add(Ldloc[config.runOriginalVariable]);
					else
						codes.Add(Ldc_I4_0);
					continue;
				}

				if (injectionType == InjectionType.Instance)
				{
					if (originalIsStatic)
						codes.Add(Ldnull);
					else
					{
						var parameterIsRef = paramType.IsByRef;
						var parameterIsObject = paramType == typeof(object) || paramType == typeof(object).MakeByRefType();

						if (AccessTools.IsStruct(originalType))
						{
							if (parameterIsObject)
							{
								if (parameterIsRef)
								{
									codes.Add(Ldarg_0);
									codes.Add(Ldobj[originalType]);
									codes.Add(Box[originalType]);
									tmpInstanceBoxingVar = config.DeclareLocal(typeof(object));
									codes.Add(Stloc[tmpInstanceBoxingVar]);
									codes.Add(Ldloca[tmpInstanceBoxingVar]);
								}
								else
								{
									codes.Add(Ldarg_0);
									codes.Add(Ldobj[originalType]);
									codes.Add(Box[originalType]);
								}
							}
							else
							{
								if (parameterIsRef)
									codes.Add(Ldarg_0);
								else
								{
									codes.Add(Ldarg_0);
									codes.Add(Ldobj[originalType]);
								}
							}
						}
						else
						{
							if (parameterIsRef)
								codes.Add(Ldarga[0]);
							else
								codes.Add(Ldarg_0);
						}
					}
					continue;
				}

				if (injectionType == InjectionType.ArgsArray)
				{
					if (config.localVariables.TryGetValue(InjectionType.ArgsArray, out var argsArrayVar))
						codes.Add(Ldloc[argsArrayVar]);
					else
						codes.Add(Ldnull);
					continue;
				}

				if (paramRealName.StartsWith(INSTANCE_FIELD_PREFIX, StringComparison.Ordinal))
				{
					var fieldName = paramRealName.Substring(INSTANCE_FIELD_PREFIX.Length);
					FieldInfo fieldInfo;
					if (fieldName.All(char.IsDigit))
					{
						fieldInfo = AccessTools.DeclaredField(originalType, int.Parse(fieldName));
						if (fieldInfo is null)
							throw new ArgumentException($"No field found at given index in class {originalType?.AssemblyQualifiedName ?? "null"}", fieldName);
					}
					else
					{
						fieldInfo = AccessTools.Field(originalType, fieldName);
						if (fieldInfo is null)
							throw new ArgumentException($"No such field defined in class {originalType?.AssemblyQualifiedName ?? "null"}", fieldName);
					}

					if (fieldInfo.IsStatic)
						codes.Add(paramType.IsByRef ? Ldsflda[fieldInfo] : Ldsfld[fieldInfo]);
					else
					{
						codes.Add(Ldarg_0);
						codes.Add(paramType.IsByRef ? Ldflda[fieldInfo] : Ldfld[fieldInfo]);
					}
					continue;
				}

				if (injectionType == InjectionType.State)
				{
					var ldlocCode = paramType.IsByRef ? OpCodes.Ldloca : OpCodes.Ldloc;
					if (config.localVariables.TryGetValue(patch.DeclaringType?.AssemblyQualifiedName ?? "null", out var stateVar))
						codes.Add(new CodeInstruction(ldlocCode, stateVar));
					else
						codes.Add(Ldnull);
					continue;
				}

				if (injectionType == InjectionType.Result)
				{
					if (returnType == typeof(void))
						throw new Exception($"Cannot get result from void method {original.FullDescription()}");
					var resultType = paramType;
					if (resultType.IsByRef && returnType.IsByRef is false)
						resultType = resultType.GetElementType();
					if (resultType.IsAssignableFrom(returnType) is false)
						throw new Exception($"Cannot assign method return type {returnType.FullName} to {InjectedParameter.RESULT_VAR} type {resultType.FullName} for method {original.FullDescription()}");
					var ldlocCode = paramType.IsByRef && returnType.IsByRef is false ? OpCodes.Ldloca : OpCodes.Ldloc;
					if (returnType.IsValueType && paramType == typeof(object).MakeByRefType())
						ldlocCode = OpCodes.Ldloc;
					codes.Add(new CodeInstruction(ldlocCode, config.GetLocal(InjectionType.Result)));
					if (returnType.IsValueType)
					{
						if (paramType == typeof(object))
							codes.Add(Box[returnType]);
						else if (paramType == typeof(object).MakeByRefType())
						{
							codes.Add(Box[returnType]);
							tmpObjectVar = config.DeclareLocal(typeof(object));
							codes.Add(Stloc[tmpObjectVar]);
							codes.Add(Ldloca[tmpObjectVar]);
						}
					}
					continue;
				}

				if (injectionType == InjectionType.ResultRef)
				{
					if (!returnType.IsByRef)
						throw new Exception(
							 $"Cannot use {InjectionType.ResultRef} with non-ref return type {returnType.FullName} of method {original.FullDescription()}");

					var resultType = paramType;
					var expectedTypeRef = typeof(RefResult<>).MakeGenericType(returnType.GetElementType()).MakeByRefType();
					if (resultType != expectedTypeRef)
						throw new Exception(
							 $"Wrong type of {InjectedParameter.RESULT_REF_VAR} for method {original.FullDescription()}. Expected {expectedTypeRef.FullName}, got {resultType.FullName}");

					codes.Add(Ldloca[config.GetLocal(InjectionType.ResultRef)]);

					refResultUsed = true;
					continue;
				}

				if (config.localVariables.TryGetValue(paramRealName, out var localBuilder))
				{
					var ldlocCode = paramType.IsByRef ? OpCodes.Ldloca : OpCodes.Ldloc;
					codes.Add(new CodeInstruction(ldlocCode, localBuilder));
					continue;
				}

				int argumentIdx;
				if (paramRealName.StartsWith(PARAM_INDEX_PREFIX, StringComparison.Ordinal))
				{
					var val = paramRealName.Substring(PARAM_INDEX_PREFIX.Length);
					if (!int.TryParse(val, out argumentIdx))
						throw new Exception($"Parameter {paramRealName} does not contain a valid index");
					if (argumentIdx < 0 || argumentIdx >= originalParameters.Length)
						throw new Exception($"No parameter found at index {argumentIdx}");
				}
				else
				{
					argumentIdx = patch.GetArgumentIndex(originalParameterNames, injection.parameterInfo);
					if (argumentIdx == -1)
					{
						var harmonyMethod = HarmonyMethodExtensions.GetMergedFromType(paramType);
						harmonyMethod.methodType ??= MethodType.Normal;
						var delegateOriginal = harmonyMethod.GetOriginalMethod();
						if (delegateOriginal is MethodInfo methodInfo)
						{
							var delegateConstructor = paramType.GetConstructor([typeof(object), typeof(IntPtr)]);
							if (delegateConstructor is not null)
							{
								if (methodInfo.IsStatic)
									codes.Add(Ldnull);
								else
								{
									codes.Add(Ldarg_0);
									if (originalType != null && originalType.IsValueType)
									{
										codes.Add(Ldobj[originalType]);
										codes.Add(Box[originalType]);
									}
								}

								if (methodInfo.IsStatic is false && harmonyMethod.nonVirtualDelegate is false)
								{
									codes.Add(Dup);
									codes.Add(Ldvirtftn[methodInfo]);
								}
								else
									codes.Add(Ldftn[methodInfo]);
								codes.Add(Newobj[delegateConstructor]);
								continue;
							}
						}

						throw new Exception($"Parameter \"{paramRealName}\" not found in method {original.FullDescription()}");
					}
				}

				var originalParamType = originalParameters[argumentIdx].ParameterType;
				var originalParamElementType = originalParamType.IsByRef ? originalParamType.GetElementType() : originalParamType;
				var patchParamType = paramType;
				var patchParamElementType = patchParamType.IsByRef ? patchParamType.GetElementType() : patchParamType;
				var originalIsNormal = originalParameters[argumentIdx].IsOut is false && originalParamType.IsByRef is false;
				var patchIsNormal = injection.parameterInfo.IsOut is false && patchParamType.IsByRef is false;
				var needsBoxing = originalParamElementType.IsValueType && patchParamElementType.IsValueType is false;
				var patchArgIndex = argumentIdx + (isInstance ? 1 : 0);

				if (originalIsNormal == patchIsNormal)
				{
					codes.Add(Ldarg[patchArgIndex]);
					if (needsBoxing)
					{
						if (patchIsNormal)
							codes.Add(Box[originalParamElementType]);
						else
						{
							codes.Add(Ldobj[originalParamElementType]);
							codes.Add(Box[originalParamElementType]);
							var tmpBoxVar = config.DeclareLocal(patchParamElementType);
							codes.Add(Stloc[tmpBoxVar]);
							codes.Add(Ldloca_S[tmpBoxVar]);
							tmpBoxVars.Add(new KeyValuePair<LocalBuilder, Type>(tmpBoxVar, originalParamElementType));
						}
					}
					continue;
				}

				if (originalIsNormal && patchIsNormal is false)
				{
					if (needsBoxing)
					{
						codes.Add(Ldarg[patchArgIndex]);
						codes.Add(Box[originalParamElementType]);
						var tmpBoxVar = config.DeclareLocal(patchParamElementType);
						codes.Add(Stloc[tmpBoxVar]);
						codes.Add(Ldloca_S[tmpBoxVar]);
					}
					else
						codes.Add(Ldarga[patchArgIndex]);
					continue;
				}

				codes.Add(Ldarg[patchArgIndex]);
				if (needsBoxing)
				{
					codes.Add(Ldobj[originalParamElementType]);
					codes.Add(Box[originalParamElementType]);
				}
				else
				{
					if (originalParamElementType.IsValueType)
						codes.Add(Ldobj[originalParamElementType]);
					else
						codes.Add(new CodeInstruction(LoadIndOpCodeFor(originalParameters[argumentIdx].ParameterType)));
				}
			}
			return codes;
		}

		internal static LocalBuilder[] DeclareOriginalLocalVariables(this MethodCreator creator, MethodBase member)
		{
			var vars = member.GetMethodBody()?.LocalVariables;
			if (vars is null)
				return [];
			return [.. vars.Select(lvi => creator.config.il.DeclareLocal(lvi.LocalType, lvi.IsPinned))];
		}

		internal static List<CodeInstruction> RestoreArgumentArray(this MethodCreator creator)
		{
			var codes = new List<CodeInstruction>();
			var original = creator.config.original;
			var originalIsStatic = original.IsStatic;
			var parameters = original.GetParameters();
			var i = 0;
			var arrayIdx = 0;
			foreach (var pInfo in parameters)
			{
				var argIndex = i++ + (originalIsStatic ? 0 : 1);
				var pType = pInfo.ParameterType;
				if (pType.IsByRef)
				{
					pType = pType.GetElementType();

					codes.Add(Ldarg[argIndex]);
					codes.Add(Ldloc[creator.config.GetLocal(InjectionType.ArgsArray)]);
					codes.Add(Ldc_I4[arrayIdx]);
					codes.Add(Ldelem_Ref);

					if (pType.IsValueType)
					{
						codes.Add(Unbox_Any[pType]);
						if (AccessTools.IsStruct(pType))
							codes.Add(Stobj[pType]);
						else
							codes.Add(StoreIndOpCodeFor(pType));
					}
					else
					{
						codes.Add(Castclass[pType]);
						codes.Add(Stind_Ref);
					}
				}
				else
				{
					codes.Add(Ldloc[creator.config.GetLocal(InjectionType.ArgsArray)]);
					codes.Add(Ldc_I4[arrayIdx]);
					codes.Add(Ldelem_Ref);
					if (pType.IsValueType)
						codes.Add(Unbox_Any[pType]);
					else
						codes.Add(Castclass[pType]);
					codes.Add(Starg[argIndex]);
				}
				arrayIdx++;
			}
			return codes;
		}

		internal static IEnumerable<CodeInstruction> CleanupCodes(this MethodCreator creator, IEnumerable<CodeInstruction> instructions, List<Label> endLabels)
		{
			foreach (var instruction in instructions)
			{
				var code = instruction.opcode;
				if (code == OpCodes.Ret)
				{
					var endLabel = creator.config.DefineLabel();
					yield return Br[endLabel].WithLabels(instruction.labels).WithBlocks(instruction.blocks);
					endLabels.Add(endLabel);
				}
				else if (shortJumps.TryGetValue(code, out var longJump))
					yield return new CodeInstruction(longJump, instruction.operand).WithLabels(instruction.labels).WithBlocks(instruction.blocks);
				else
					yield return instruction;
			}
		}

		internal static void LogCodes(this MethodCreator _, Emitter emitter, List<CodeInstruction> codeInstructions)
		{
			var codePos = emitter.CurrentPos();
			emitter.Variables().Do(FileLog.LogIL);

			codeInstructions.Do(codeInstruction =>
			{
				codeInstruction.labels.Do(label => FileLog.LogIL(codePos, label));
				codeInstruction.blocks.Do(block => FileLog.LogILBlockBegin(codePos, block));

				var code = codeInstruction.opcode;
				var operand = codeInstruction.operand;

				var realCode = true;
				switch (code.OperandType)
				{
					case OperandType.InlineNone:
						var comment = codeInstruction.IsAnnotation();
						if (comment != null)
						{
							FileLog.LogILComment(codePos, comment);
							realCode = false;
						}
						else
							FileLog.LogIL(codePos, code);
						break;

					case OperandType.InlineSig:
						FileLog.LogIL(codePos, code, (ICallSiteGenerator)operand);
						break;

					default:
						FileLog.LogIL(codePos, code, operand);
						break;
				}

				codeInstruction.blocks.Do(block => FileLog.LogILBlockEnd(codePos, block));
				if (realCode) codePos += codeInstruction.GetSize();
			});

			FileLog.FlushBuffer();
		}

		internal static void EmitCodes(this MethodCreator _, Emitter emitter, List<CodeInstruction> codeInstructions)
		{
			// pass5 - mark labels and exceptions and emit codes
			//
			codeInstructions.Do(codeInstruction =>
			{
				// mark all labels
				codeInstruction.labels.Do(label => emitter.MarkLabel(label));

				// start all exception blocks
				codeInstruction.blocks.Do(block => emitter.MarkBlockBefore(block, out var _));

				var code = codeInstruction.opcode;
				var operand = codeInstruction.operand;

				switch (code.OperandType)
				{
					case OperandType.InlineNone:
						if (codeInstruction.IsAnnotation() == null)
							emitter.Emit(code);
						break;

					case OperandType.InlineSig:
						if (operand is null)
							throw new Exception($"Wrong null argument: {codeInstruction}");
						if ((operand is ICallSiteGenerator) is false)
							throw new Exception($"Wrong Emit argument type {operand.GetType()} in {codeInstruction}");
						emitter.Emit(code, (ICallSiteGenerator)operand);
						break;

					default:
						if (operand is null)
							throw new Exception($"Wrong null argument: {codeInstruction}");
						emitter.DynEmit(code, operand);
						break;
				}

				codeInstruction.blocks.Do(block => emitter.MarkBlockAfter(block));
			});
		}

		static List<CodeInstruction> InitializeOutParameter(int argIndex, Type type)
		{
			var codes = new List<CodeInstruction>();
			if (type.IsByRef)
				type = type.GetElementType();
			codes.Add(Ldarg[argIndex]);
			if (AccessTools.IsStruct(type))
			{
				codes.Add(Initobj[type]);
				return codes;
			}
			if (AccessTools.IsValue(type))
			{
				if (type == typeof(float))
				{
					codes.Add(Ldc_R4[(float)0]);
					codes.Add(Stind_R4);
					return codes;
				}
				else if (type == typeof(double))
				{
					codes.Add(Ldc_R8[(double)0]);
					codes.Add(Stind_R8);
					return codes;
				}
				else if (type == typeof(long))
				{
					codes.Add(Ldc_I8[(long)0]);
					codes.Add(Stind_I8);
					return codes;
				}
				else
				{
					codes.Add(Ldc_I4[0]);
					codes.Add(Stind_I4);
					return codes;
				}
			}

			// class or default
			codes.Add(Ldnull);
			codes.Add(Stind_Ref);

			return codes;
		}

		static CodeInstruction LoadIndOpCodeFor(Type type)
		{
			if (PrimitivesWithObjectTypeCode.Contains(type))
				return Ldind_I;

			return Type.GetTypeCode(type) switch
			{
				TypeCode.SByte or TypeCode.Byte or TypeCode.Boolean => Ldind_I1,
				TypeCode.Char or TypeCode.Int16 or TypeCode.UInt16 => Ldind_I2,
				TypeCode.Int32 or TypeCode.UInt32 => Ldind_I4,
				TypeCode.Int64 or TypeCode.UInt64 => Ldind_I8,
				TypeCode.Single => Ldind_R4,
				TypeCode.Double => Ldind_R8,
				TypeCode.DateTime or TypeCode.Decimal => throw new NotSupportedException(),
				TypeCode.Empty or TypeCode.Object or TypeCode.DBNull or TypeCode.String => Ldind_Ref,
				_ => Ldind_Ref,
			};
		}

		static readonly MethodInfo m_GetMethodFromHandle1 = typeof(MethodBase).GetMethod("GetMethodFromHandle", [typeof(RuntimeMethodHandle)]);
		static readonly MethodInfo m_GetMethodFromHandle2 = typeof(MethodBase).GetMethod("GetMethodFromHandle", [typeof(RuntimeMethodHandle), typeof(RuntimeTypeHandle)]);
		static bool EmitOriginalBaseMethod(MethodBase original, List<CodeInstruction> codes)
		{
			if (original is MethodInfo method)
				codes.Add(Ldtoken[method]);
			else if (original is ConstructorInfo constructor)
				codes.Add(Ldtoken[constructor]);
			else
				return false;

			var type = original.ReflectedType;
			if (type.IsGenericType)
				codes.Add(Ldtoken[type]);
			codes.Add(Call[type.IsGenericType ? m_GetMethodFromHandle2 : m_GetMethodFromHandle1]);
			return true;
		}

		static readonly HashSet<Type> PrimitivesWithObjectTypeCode = [typeof(nint), typeof(nuint), typeof(IntPtr), typeof(UIntPtr)];
		static CodeInstruction StoreIndOpCodeFor(Type type)
		{
			if (PrimitivesWithObjectTypeCode.Contains(type))
				return Stind_I;

			return Type.GetTypeCode(type) switch
			{
				TypeCode.SByte or TypeCode.Byte or TypeCode.Boolean => Stind_I1,
				TypeCode.Char or TypeCode.Int16 or TypeCode.UInt16 => Stind_I2,
				TypeCode.Int32 or TypeCode.UInt32 => Stind_I4,
				TypeCode.Int64 or TypeCode.UInt64 => Stind_I8,
				TypeCode.Single => Stind_R4,
				TypeCode.Double => Stind_R8,
				TypeCode.DateTime or TypeCode.Decimal => throw new NotSupportedException(),
				TypeCode.Empty or TypeCode.Object or TypeCode.DBNull or TypeCode.String => Stind_Ref,
				_ => Stind_Ref,
			};
		}
	}
}
