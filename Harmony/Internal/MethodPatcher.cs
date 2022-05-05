using MonoMod.RuntimeDetour;
using MonoMod.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace HarmonyLib
{
	internal class MethodPatcher
	{
		/// special parameter names that can be used in prefix and postfix methods

		const string INSTANCE_PARAM = "__instance";
		const string ORIGINAL_METHOD_PARAM = "__originalMethod";
		const string ARGS_ARRAY_VAR = "__args";
		const string RESULT_VAR = "__result";
		const string STATE_VAR = "__state";
		const string EXCEPTION_VAR = "__exception";
		const string RUN_ORIGINAL_VAR = "__runOriginal";
		const string PARAM_INDEX_PREFIX = "__";
		const string INSTANCE_FIELD_PREFIX = "___";

		readonly bool debug;
		readonly MethodBase original;
		readonly MethodBase source;
		readonly List<MethodInfo> prefixes;
		readonly List<MethodInfo> postfixes;
		readonly List<MethodInfo> transpilers;
		readonly List<MethodInfo> finalizers;
		readonly int idx;
		readonly bool useStructReturnBuffer;
		readonly Type returnType;
		readonly DynamicMethodDefinition patch;
		readonly ILGenerator il;
		readonly Emitter emitter;

		internal MethodPatcher(MethodBase original, MethodBase source, List<MethodInfo> prefixes, List<MethodInfo> postfixes, List<MethodInfo> transpilers, List<MethodInfo> finalizers, bool debug)
		{
			if (original is null)
				throw new ArgumentNullException(nameof(original));

			this.debug = debug;
			this.original = original;
			this.source = source;
			this.prefixes = prefixes;
			this.postfixes = postfixes;
			this.transpilers = transpilers;
			this.finalizers = finalizers;

			Memory.MarkForNoInlining(original);

			if (debug)
			{
				FileLog.LogBuffered($"### Patch: {original.FullDescription()}");
				FileLog.FlushBuffer();
			}

			idx = prefixes.Count + postfixes.Count + finalizers.Count;
			useStructReturnBuffer = StructReturnBuffer.NeedsFix(original);
			if (debug && useStructReturnBuffer) FileLog.Log($"### Note: A buffer for the returned struct is used. That requires an extra IntPtr argument before the first real argument");
			returnType = AccessTools.GetReturnedType(original);
			patch = CreateDynamicMethod(original, $"_Patch{idx}", debug);
			if (patch is null)
				throw new Exception("Could not create replacement method");

			il = patch.GetILGenerator();
			emitter = new Emitter(il, debug);
		}

		internal MethodInfo CreateReplacement(out Dictionary<int, CodeInstruction> finalInstructions)
		{
			var originalVariables = DeclareLocalVariables(il, source ?? original);
			var privateVars = new Dictionary<string, LocalBuilder>();
			var fixes = prefixes.Union(postfixes).Union(finalizers).ToList();

			LocalBuilder resultVariable = null;
			if (idx > 0)
			{
				resultVariable = DeclareLocalVariable(returnType, true);
				privateVars[RESULT_VAR] = resultVariable;
			}

			LocalBuilder argsArrayVariable = null;
			if (fixes.Any(fix => fix.GetParameters().Any(p => p.Name == ARGS_ARRAY_VAR)))
			{
				PrepareArgumentArray();
				argsArrayVariable = il.DeclareLocal(typeof(object[]));
				emitter.Emit(OpCodes.Stloc, argsArrayVariable);
				privateVars[ARGS_ARRAY_VAR] = argsArrayVariable;
			}

			Label? skipOriginalLabel = null;
			LocalBuilder runOriginalVariable = null;
			var prefixAffectsOriginal = prefixes.Any(fix => PrefixAffectsOriginal(fix));
			var anyFixHasRunOriginalVar = fixes.Any(fix => fix.GetParameters().Any(p => p.Name == RUN_ORIGINAL_VAR));
			if (prefixAffectsOriginal || anyFixHasRunOriginalVar)
			{
				runOriginalVariable = DeclareLocalVariable(typeof(bool));
				emitter.Emit(OpCodes.Ldc_I4_1);
				emitter.Emit(OpCodes.Stloc, runOriginalVariable);

				if (prefixAffectsOriginal)
					skipOriginalLabel = il.DefineLabel();
			}

			fixes.ForEach(fix =>
			{
				if (fix.DeclaringType is object && privateVars.ContainsKey(fix.DeclaringType.AssemblyQualifiedName) is false)
				{
					fix.GetParameters()
					.Where(patchParam => patchParam.Name == STATE_VAR)
					.Do(patchParam =>
					{
						var privateStateVariable = DeclareLocalVariable(patchParam.ParameterType);
						privateVars[fix.DeclaringType.AssemblyQualifiedName] = privateStateVariable;
					});
				}
			});

			LocalBuilder finalizedVariable = null;
			if (finalizers.Any())
			{
				finalizedVariable = DeclareLocalVariable(typeof(bool));

				privateVars[EXCEPTION_VAR] = DeclareLocalVariable(typeof(Exception));

				// begin try
				emitter.MarkBlockBefore(new ExceptionBlock(ExceptionBlockType.BeginExceptionBlock), out _);
			}

			AddPrefixes(privateVars, runOriginalVariable);
			if (skipOriginalLabel.HasValue)
			{
				emitter.Emit(OpCodes.Ldloc, runOriginalVariable);
				emitter.Emit(OpCodes.Brfalse, skipOriginalLabel.Value);
			}

			var copier = new MethodCopier(source ?? original, il, originalVariables);
			copier.SetArgumentShift(useStructReturnBuffer);
			copier.SetDebugging(debug);

			foreach (var transpiler in transpilers)
				copier.AddTranspiler(transpiler);

			var endLabels = new List<Label>();
			_ = copier.Finalize(emitter, endLabels, out var hasReturnCode);

			foreach (var label in endLabels)
				emitter.MarkLabel(label);
			if (resultVariable is object && hasReturnCode)
				emitter.Emit(OpCodes.Stloc, resultVariable);
			if (skipOriginalLabel.HasValue)
				emitter.MarkLabel(skipOriginalLabel.Value);

			_ = AddPostfixes(privateVars, runOriginalVariable, false);

			if (resultVariable is object && hasReturnCode)
				emitter.Emit(OpCodes.Ldloc, resultVariable);

			var needsToStorePassthroughResult = AddPostfixes(privateVars, runOriginalVariable, true);

			var hasFinalizers = finalizers.Any();
			if (hasFinalizers)
			{
				if (needsToStorePassthroughResult)
				{
					emitter.Emit(OpCodes.Stloc, resultVariable);
					emitter.Emit(OpCodes.Ldloc, resultVariable);
				}

				_ = AddFinalizers(privateVars, runOriginalVariable, false);
				emitter.Emit(OpCodes.Ldc_I4_1);
				emitter.Emit(OpCodes.Stloc, finalizedVariable);
				var noExceptionLabel1 = il.DefineLabel();
				emitter.Emit(OpCodes.Ldloc, privateVars[EXCEPTION_VAR]);
				emitter.Emit(OpCodes.Brfalse, noExceptionLabel1);
				emitter.Emit(OpCodes.Ldloc, privateVars[EXCEPTION_VAR]);
				emitter.Emit(OpCodes.Throw);
				emitter.MarkLabel(noExceptionLabel1);

				// end try, begin catch
				emitter.MarkBlockBefore(new ExceptionBlock(ExceptionBlockType.BeginCatchBlock), out var label);
				emitter.Emit(OpCodes.Stloc, privateVars[EXCEPTION_VAR]);

				emitter.Emit(OpCodes.Ldloc, finalizedVariable);
				var endFinalizerLabel = il.DefineLabel();
				emitter.Emit(OpCodes.Brtrue, endFinalizerLabel);

				var rethrowPossible = AddFinalizers(privateVars, runOriginalVariable, true);

				emitter.MarkLabel(endFinalizerLabel);

				var noExceptionLabel2 = il.DefineLabel();
				emitter.Emit(OpCodes.Ldloc, privateVars[EXCEPTION_VAR]);
				emitter.Emit(OpCodes.Brfalse, noExceptionLabel2);
				if (rethrowPossible)
					emitter.Emit(OpCodes.Rethrow);
				else
				{
					emitter.Emit(OpCodes.Ldloc, privateVars[EXCEPTION_VAR]);
					emitter.Emit(OpCodes.Throw);
				}
				emitter.MarkLabel(noExceptionLabel2);

				// end catch
				emitter.MarkBlockAfter(new ExceptionBlock(ExceptionBlockType.EndExceptionBlock));

				if (resultVariable is object)
					emitter.Emit(OpCodes.Ldloc, resultVariable);
			}

			if (useStructReturnBuffer)
			{
				var tmpVar = DeclareLocalVariable(returnType);
				emitter.Emit(OpCodes.Stloc, tmpVar);
				emitter.Emit(original.IsStatic ? OpCodes.Ldarg_0 : OpCodes.Ldarg_1);
				emitter.Emit(OpCodes.Ldloc, tmpVar);
				emitter.Emit(OpCodes.Stobj, returnType); // store result into ref
			}

			if (hasFinalizers || hasReturnCode)
				emitter.Emit(OpCodes.Ret);

			finalInstructions = emitter.GetInstructions();

			if (debug)
			{
				FileLog.LogBuffered("DONE");
				FileLog.LogBuffered("");
				FileLog.FlushBuffer();
			}

			return patch.Generate().Pin();
		}

		internal static DynamicMethodDefinition CreateDynamicMethod(MethodBase original, string suffix, bool debug)
		{
			if (original is null) throw new ArgumentNullException(nameof(original));
			var useStructReturnBuffer = StructReturnBuffer.NeedsFix(original);

			var patchName = $"{original.DeclaringType?.FullName ?? "GLOBALTYPE"}.{original.Name}{suffix}";
			patchName = patchName.Replace("<>", "");

			var parameters = original.GetParameters();
			var parameterTypes = new List<Type>();

			parameterTypes.AddRange(parameters.Types());
			if (useStructReturnBuffer)
				parameterTypes.Insert(0, typeof(IntPtr));
			if (original.IsStatic is false)
			{
				if (AccessTools.IsStruct(original.DeclaringType))
					parameterTypes.Insert(0, original.DeclaringType.MakeByRefType());
				else
					parameterTypes.Insert(0, original.DeclaringType);
			}

			var returnType = useStructReturnBuffer ? typeof(void) : AccessTools.GetReturnedType(original);

			var method = new DynamicMethodDefinition(
				patchName,
				returnType,
				parameterTypes.ToArray()
			)
			{
				OwnerType = original.DeclaringType
			};

			var offset = (original.IsStatic ? 0 : 1) + (useStructReturnBuffer ? 1 : 0);
			if (useStructReturnBuffer)
				method.Definition.Parameters[original.IsStatic ? 0 : 1].Name = "retbuf";
			if (!original.IsStatic)
				method.Definition.Parameters[0].Name = "this";
			for (var i = 0; i < parameters.Length; i++)
			{
				var param = method.Definition.Parameters[i + offset];
				param.Attributes = (Mono.Cecil.ParameterAttributes)parameters[i].Attributes;
				param.Name = parameters[i].Name;
			}

			if (debug)
			{
				var parameterStrings = parameterTypes.Select(p => p.FullDescription()).ToList();
				if (parameterTypes.Count == method.Definition.Parameters.Count)
					for (var i = 0; i < parameterTypes.Count; i++)
						parameterStrings[i] += $" {method.Definition.Parameters[i].Name}";
				FileLog.Log($"### Replacement: static {returnType.FullDescription()} {original.DeclaringType?.FullName ?? "GLOBALTYPE"}::{patchName}({parameterStrings.Join()})");
			}

			return method;
		}

		internal static LocalBuilder[] DeclareLocalVariables(ILGenerator il, MethodBase member)
		{
			var vars = member.GetMethodBody()?.LocalVariables;
			if (vars is null)
				return new LocalBuilder[0];
			return vars.Select(lvi => il.DeclareLocal(lvi.LocalType, lvi.IsPinned)).ToArray();
		}

		LocalBuilder DeclareLocalVariable(Type type, bool isReturnValue = false)
		{
			if (type.IsByRef && isReturnValue is false) type = type.GetElementType();
			if (type.IsEnum) type = Enum.GetUnderlyingType(type);

			if (AccessTools.IsClass(type))
			{
				var v = il.DeclareLocal(type);
				emitter.Emit(OpCodes.Ldnull);
				emitter.Emit(OpCodes.Stloc, v);
				return v;
			}
			if (AccessTools.IsStruct(type))
			{
				var v = il.DeclareLocal(type);
				emitter.Emit(OpCodes.Ldloca, v);
				emitter.Emit(OpCodes.Initobj, type);
				return v;
			}
			if (AccessTools.IsValue(type))
			{
				var v = il.DeclareLocal(type);
				if (type == typeof(float))
					emitter.Emit(OpCodes.Ldc_R4, (float)0);
				else if (type == typeof(double))
					emitter.Emit(OpCodes.Ldc_R8, (double)0);
				else if (type == typeof(long) || type == typeof(ulong))
					emitter.Emit(OpCodes.Ldc_I8, (long)0);
				else
					emitter.Emit(OpCodes.Ldc_I4, 0);
				emitter.Emit(OpCodes.Stloc, v);
				return v;
			}
			return null;
		}

		static OpCode LoadIndOpCodeFor(Type type)
		{
			if (type.IsEnum)
				return OpCodes.Ldind_I4;

			if (type == typeof(float)) return OpCodes.Ldind_R4;
			if (type == typeof(double)) return OpCodes.Ldind_R8;

			if (type == typeof(byte)) return OpCodes.Ldind_U1;
			if (type == typeof(ushort)) return OpCodes.Ldind_U2;
			if (type == typeof(uint)) return OpCodes.Ldind_U4;
			if (type == typeof(ulong)) return OpCodes.Ldind_I8;

			if (type == typeof(sbyte)) return OpCodes.Ldind_I1;
			if (type == typeof(short)) return OpCodes.Ldind_I2;
			if (type == typeof(int)) return OpCodes.Ldind_I4;
			if (type == typeof(long)) return OpCodes.Ldind_I8;

			return OpCodes.Ldind_Ref;
		}

		static OpCode StoreIndOpCodeFor(Type type)
		{
			if (type.IsEnum)
				return OpCodes.Stind_I4;

			if (type == typeof(float)) return OpCodes.Stind_R4;
			if (type == typeof(double)) return OpCodes.Stind_R8;

			if (type == typeof(byte)) return OpCodes.Stind_I1;
			if (type == typeof(ushort)) return OpCodes.Stind_I2;
			if (type == typeof(uint)) return OpCodes.Stind_I4;
			if (type == typeof(ulong)) return OpCodes.Stind_I8;

			if (type == typeof(sbyte)) return OpCodes.Stind_I1;
			if (type == typeof(short)) return OpCodes.Stind_I2;
			if (type == typeof(int)) return OpCodes.Stind_I4;
			if (type == typeof(long)) return OpCodes.Stind_I8;

			return OpCodes.Stind_Ref;
		}

		void InitializeOutParameter(int argIndex, Type type)
		{
			if (type.IsByRef) type = type.GetElementType();
			emitter.Emit(OpCodes.Ldarg, argIndex);

			if (AccessTools.IsStruct(type))
			{
				emitter.Emit(OpCodes.Initobj, type);
				return;
			}

			if (AccessTools.IsValue(type))
			{
				if (type == typeof(float))
				{
					emitter.Emit(OpCodes.Ldc_R4, (float)0);
					emitter.Emit(OpCodes.Stind_R4);
					return;
				}
				else if (type == typeof(double))
				{
					emitter.Emit(OpCodes.Ldc_R8, (double)0);
					emitter.Emit(OpCodes.Stind_R8);
					return;
				}
				else if (type == typeof(long))
				{
					emitter.Emit(OpCodes.Ldc_I8, (long)0);
					emitter.Emit(OpCodes.Stind_I8);
					return;
				}
				else
				{
					emitter.Emit(OpCodes.Ldc_I4, 0);
					emitter.Emit(OpCodes.Stind_I4);
					return;
				}
			}

			// class or default
			emitter.Emit(OpCodes.Ldnull);
			emitter.Emit(OpCodes.Stind_Ref);
		}

		static readonly MethodInfo m_GetMethodFromHandle1 = typeof(MethodBase).GetMethod("GetMethodFromHandle", new[] { typeof(RuntimeMethodHandle) });
		static readonly MethodInfo m_GetMethodFromHandle2 = typeof(MethodBase).GetMethod("GetMethodFromHandle", new[] { typeof(RuntimeMethodHandle), typeof(RuntimeTypeHandle) });
		bool EmitOriginalBaseMethod()
		{
			if (original is MethodInfo method)
				emitter.Emit(OpCodes.Ldtoken, method);
			else if (original is ConstructorInfo constructor)
				emitter.Emit(OpCodes.Ldtoken, constructor);
			else return false;

			var type = original.ReflectedType;
			if (type.IsGenericType) emitter.Emit(OpCodes.Ldtoken, type);
			emitter.Emit(OpCodes.Call, type.IsGenericType ? m_GetMethodFromHandle2 : m_GetMethodFromHandle1);
			return true;
		}

		void EmitCallParameter(MethodInfo patch, Dictionary<string, LocalBuilder> variables, LocalBuilder runOriginalVariable, bool allowFirsParamPassthrough, out LocalBuilder tmpObjectVar, List<KeyValuePair<LocalBuilder, Type>> tmpBoxVars)
		{
			tmpObjectVar = null;
			var isInstance = original.IsStatic is false;
			var originalParameters = original.GetParameters();
			var originalParameterNames = originalParameters.Select(p => p.Name).ToArray();

			// check for passthrough using first parameter (which must have same type as return type)
			var parameters = patch.GetParameters().ToList();
			if (allowFirsParamPassthrough && patch.ReturnType != typeof(void) && parameters.Count > 0 && parameters[0].ParameterType == patch.ReturnType)
				parameters.RemoveRange(0, 1);

			foreach (var patchParam in parameters)
			{
				if (patchParam.Name == ORIGINAL_METHOD_PARAM)
				{
					if (EmitOriginalBaseMethod())
						continue;

					emitter.Emit(OpCodes.Ldnull);
					continue;
				}

				if (patchParam.Name == RUN_ORIGINAL_VAR)
				{
					if (runOriginalVariable != null)
						emitter.Emit(OpCodes.Ldloc, runOriginalVariable);
					else
						emitter.Emit(OpCodes.Ldc_I4_0);
					continue;
				}

				if (patchParam.Name == INSTANCE_PARAM)
				{
					if (original.IsStatic)
						emitter.Emit(OpCodes.Ldnull);
					else
					{
						var instanceIsRef = original.DeclaringType is object && AccessTools.IsStruct(original.DeclaringType);
						var parameterIsRef = patchParam.ParameterType.IsByRef;
						if (instanceIsRef == parameterIsRef)
						{
							emitter.Emit(OpCodes.Ldarg_0);
						}
						if (instanceIsRef && parameterIsRef is false)
						{
							emitter.Emit(OpCodes.Ldarg_0);
							emitter.Emit(OpCodes.Ldobj, original.DeclaringType);
						}
						if (instanceIsRef is false && parameterIsRef)
						{
							emitter.Emit(OpCodes.Ldarga, 0);
						}
					}
					continue;
				}

				if (patchParam.Name == ARGS_ARRAY_VAR)
				{
					if (variables.TryGetValue(ARGS_ARRAY_VAR, out var argsArrayVar))
						emitter.Emit(OpCodes.Ldloc, argsArrayVar);
					else
						emitter.Emit(OpCodes.Ldnull);
					continue;
				}

				if (patchParam.Name.StartsWith(INSTANCE_FIELD_PREFIX, StringComparison.Ordinal))
				{
					var fieldName = patchParam.Name.Substring(INSTANCE_FIELD_PREFIX.Length);
					FieldInfo fieldInfo;
					if (fieldName.All(char.IsDigit))
					{
						// field access by index only works for declared fields
						fieldInfo = AccessTools.DeclaredField(original.DeclaringType, int.Parse(fieldName));
						if (fieldInfo is null)
							throw new ArgumentException($"No field found at given index in class {original.DeclaringType?.AssemblyQualifiedName ?? "null"}", fieldName);
					}
					else
					{
						fieldInfo = AccessTools.Field(original.DeclaringType, fieldName);
						if (fieldInfo is null)
							throw new ArgumentException($"No such field defined in class {original.DeclaringType?.AssemblyQualifiedName ?? "null"}", fieldName);
					}

					if (fieldInfo.IsStatic)
						emitter.Emit(patchParam.ParameterType.IsByRef ? OpCodes.Ldsflda : OpCodes.Ldsfld, fieldInfo);
					else
					{
						emitter.Emit(OpCodes.Ldarg_0);
						emitter.Emit(patchParam.ParameterType.IsByRef ? OpCodes.Ldflda : OpCodes.Ldfld, fieldInfo);
					}
					continue;
				}

				// state is special too since each patch has its own local var
				if (patchParam.Name == STATE_VAR)
				{
					var ldlocCode = patchParam.ParameterType.IsByRef ? OpCodes.Ldloca : OpCodes.Ldloc;
					if (variables.TryGetValue(patch.DeclaringType?.AssemblyQualifiedName ?? "null", out var stateVar))
						emitter.Emit(ldlocCode, stateVar);
					else
						emitter.Emit(OpCodes.Ldnull);
					continue;
				}

				// treat __result var special
				if (patchParam.Name == RESULT_VAR)
				{
					var returnType = AccessTools.GetReturnedType(original);
					if (returnType == typeof(void))
						throw new Exception($"Cannot get result from void method {original.FullDescription()}");
					var resultType = patchParam.ParameterType;
					if (resultType.IsByRef && returnType.IsByRef is false)
						resultType = resultType.GetElementType();
					if (resultType.IsAssignableFrom(returnType) is false)
						throw new Exception($"Cannot assign method return type {returnType.FullName} to {RESULT_VAR} type {resultType.FullName} for method {original.FullDescription()}");
					var ldlocCode = patchParam.ParameterType.IsByRef && returnType.IsByRef is false ? OpCodes.Ldloca : OpCodes.Ldloc;
					if (returnType.IsValueType && patchParam.ParameterType == typeof(object).MakeByRefType()) ldlocCode = OpCodes.Ldloc;
					emitter.Emit(ldlocCode, variables[RESULT_VAR]);
					if (returnType.IsValueType)
					{
						if (patchParam.ParameterType == typeof(object))
							emitter.Emit(OpCodes.Box, returnType);
						else if (patchParam.ParameterType == typeof(object).MakeByRefType())
						{
							emitter.Emit(OpCodes.Box, returnType);
							tmpObjectVar = il.DeclareLocal(typeof(object));
							emitter.Emit(OpCodes.Stloc, tmpObjectVar);
							emitter.Emit(OpCodes.Ldloca, tmpObjectVar);
						}
					}
					continue;
				}

				// any other declared variables
				if (variables.TryGetValue(patchParam.Name, out var localBuilder))
				{
					var ldlocCode = patchParam.ParameterType.IsByRef ? OpCodes.Ldloca : OpCodes.Ldloc;
					emitter.Emit(ldlocCode, localBuilder);
					continue;
				}

				int idx;
				if (patchParam.Name.StartsWith(PARAM_INDEX_PREFIX, StringComparison.Ordinal))
				{
					var val = patchParam.Name.Substring(PARAM_INDEX_PREFIX.Length);
					if (!int.TryParse(val, out idx))
						throw new Exception($"Parameter {patchParam.Name} does not contain a valid index");
					if (idx < 0 || idx >= originalParameters.Length)
						throw new Exception($"No parameter found at index {idx}");
				}
				else
				{
					idx = patch.GetArgumentIndex(originalParameterNames, patchParam);
					if (idx == -1)
					{
						var harmonyMethod = HarmonyMethodExtensions.GetMergedFromType(patchParam.ParameterType);
						if (harmonyMethod.methodType is null) // MethodType default is Normal
							harmonyMethod.methodType = MethodType.Normal;
						var delegateOriginal = harmonyMethod.GetOriginalMethod();
						if (delegateOriginal is MethodInfo methodInfo)
						{
							var delegateConstructor = patchParam.ParameterType.GetConstructor(new[] { typeof(object), typeof(IntPtr) });
							if (delegateConstructor is object)
							{
								var originalType = original.DeclaringType;
								if (methodInfo.IsStatic)
									emitter.Emit(OpCodes.Ldnull);
								else
								{
									emitter.Emit(OpCodes.Ldarg_0);
									if (originalType != null && originalType.IsValueType)
									{
										emitter.Emit(OpCodes.Ldobj, originalType);
										emitter.Emit(OpCodes.Box, originalType);
									}
								}

								if (methodInfo.IsStatic is false && harmonyMethod.nonVirtualDelegate is false)
								{
									emitter.Emit(OpCodes.Dup);
									emitter.Emit(OpCodes.Ldvirtftn, methodInfo);
								}
								else
									emitter.Emit(OpCodes.Ldftn, methodInfo);
								emitter.Emit(OpCodes.Newobj, delegateConstructor);
								continue;
							}
						}

						throw new Exception($"Parameter \"{patchParam.Name}\" not found in method {original.FullDescription()}");
					}
				}

				//   original -> patch     opcode
				// --------------------------------------
				// 1 normal   -> normal  : LDARG
				// 2 normal   -> ref/out : LDARGA
				// 3 ref/out  -> normal  : LDARG, LDIND_x
				// 4 ref/out  -> ref/out : LDARG
				//
				var originalParamType = originalParameters[idx].ParameterType;
				var originalParamElementType = originalParamType.IsByRef ? originalParamType.GetElementType() : originalParamType;
				var patchParamType = patchParam.ParameterType;
				var patchParamElementType = patchParamType.IsByRef ? patchParamType.GetElementType() : patchParamType;
				var originalIsNormal = originalParameters[idx].IsOut is false && originalParamType.IsByRef is false;
				var patchIsNormal = patchParam.IsOut is false && patchParamType.IsByRef is false;
				var needsBoxing = originalParamElementType.IsValueType && patchParamElementType.IsValueType is false;
				var patchArgIndex = idx + (isInstance ? 1 : 0) + (useStructReturnBuffer ? 1 : 0);

				// Case 1 + 4
				if (originalIsNormal == patchIsNormal)
				{
					emitter.Emit(OpCodes.Ldarg, patchArgIndex);
					if (needsBoxing)
					{
						if (patchIsNormal)
							emitter.Emit(OpCodes.Box, originalParamElementType);
						else
						{
							emitter.Emit(OpCodes.Ldobj, originalParamElementType);
							emitter.Emit(OpCodes.Box, originalParamElementType);
							var tmpBoxVar = il.DeclareLocal(patchParamElementType);
							emitter.Emit(OpCodes.Stloc, tmpBoxVar);
							emitter.Emit(OpCodes.Ldloca_S, tmpBoxVar);
							tmpBoxVars.Add(new KeyValuePair<LocalBuilder, Type>(tmpBoxVar, originalParamElementType));
						}
					}
					continue;
				}

				// Case 2
				if (originalIsNormal && patchIsNormal is false)
				{
					if (needsBoxing)
					{
						emitter.Emit(OpCodes.Ldarg, patchArgIndex);
						emitter.Emit(OpCodes.Box, originalParamElementType);
						var tmpBoxVar = il.DeclareLocal(patchParamElementType);
						emitter.Emit(OpCodes.Stloc, tmpBoxVar);
						emitter.Emit(OpCodes.Ldloca_S, tmpBoxVar);
					}
					else
						emitter.Emit(OpCodes.Ldarga, patchArgIndex);
					continue;
				}

				// Case 3
				emitter.Emit(OpCodes.Ldarg, patchArgIndex);
				if (needsBoxing)
				{
					emitter.Emit(OpCodes.Ldobj, originalParamElementType);
					emitter.Emit(OpCodes.Box, originalParamElementType);
				}
				else
				{
					if (originalParamElementType.IsValueType)
						emitter.Emit(OpCodes.Ldobj, originalParamElementType);
					else
						emitter.Emit(LoadIndOpCodeFor(originalParameters[idx].ParameterType));
				}
			}
		}

		static bool PrefixAffectsOriginal(MethodInfo fix)
		{
			if (fix.ReturnType == typeof(bool))
				return true;

			return fix.GetParameters().Any(p =>
			{
				var name = p.Name;
				var type = p.ParameterType;

				if (name == INSTANCE_PARAM) return false;
				if (name == ORIGINAL_METHOD_PARAM) return false;
				if (name == STATE_VAR) return false;

				if (p.IsOut || p.IsRetval) return true;
				if (type.IsByRef) return true;
				if (AccessTools.IsValue(type) is false && AccessTools.IsStruct(type) is false) return true;

				return false;
			});
		}

		void AddPrefixes(Dictionary<string, LocalBuilder> variables, LocalBuilder runOriginalVariable)
		{
			prefixes
				.Do(fix =>
				{
					//if (original.HasMethodBody() is false)
					//	throw new Exception("Methods without body cannot have prefixes. Use a transpiler instead.");

					var skipLabel = PrefixAffectsOriginal(fix) ? il.DefineLabel() : (Label?)null;
					if (skipLabel.HasValue)
					{
						emitter.Emit(OpCodes.Ldloc, runOriginalVariable);
						emitter.Emit(OpCodes.Brfalse, skipLabel.Value);
					}

					var tmpBoxVars = new List<KeyValuePair<LocalBuilder, Type>>();
					EmitCallParameter(fix, variables, runOriginalVariable, false, out var tmpObjectVar, tmpBoxVars);
					emitter.Emit(OpCodes.Call, fix);
					if (fix.GetParameters().Any(p => p.Name == ARGS_ARRAY_VAR))
						RestoreArgumentArray(variables);
					if (tmpObjectVar != null)
					{
						emitter.Emit(OpCodes.Ldloc, tmpObjectVar);
						emitter.Emit(OpCodes.Unbox_Any, AccessTools.GetReturnedType(original));
						emitter.Emit(OpCodes.Stloc, variables[RESULT_VAR]);
					}
					tmpBoxVars.Do(tmpBoxVar =>
					{
						emitter.Emit(original.IsStatic ? OpCodes.Ldarg_0 : OpCodes.Ldarg_1);
						emitter.Emit(OpCodes.Ldloc, tmpBoxVar.Key);
						emitter.Emit(OpCodes.Unbox_Any, tmpBoxVar.Value);
						emitter.Emit(OpCodes.Stobj, tmpBoxVar.Value);
					});

					var returnType = fix.ReturnType;
					if (returnType != typeof(void))
					{
						if (returnType != typeof(bool))
							throw new Exception($"Prefix patch {fix} has not \"bool\" or \"void\" return type: {fix.ReturnType}");
						emitter.Emit(OpCodes.Stloc, runOriginalVariable);
					}

					if (skipLabel.HasValue)
					{
						emitter.MarkLabel(skipLabel.Value);
						emitter.Emit(OpCodes.Nop);
					}
				});
		}

		bool AddPostfixes(Dictionary<string, LocalBuilder> variables, LocalBuilder runOriginalVariable, bool passthroughPatches)
		{
			var result = false;
			postfixes
				.Where(fix => passthroughPatches == (fix.ReturnType != typeof(void)))
				.Do(fix =>
				{
					//if (original.HasMethodBody() is false)
					//	throw new Exception("Methods without body cannot have postfixes. Use a transpiler instead.");

					var tmpBoxVars = new List<KeyValuePair<LocalBuilder, Type>>();
					EmitCallParameter(fix, variables, runOriginalVariable, true, out var tmpObjectVar, tmpBoxVars);
					emitter.Emit(OpCodes.Call, fix);
					if (fix.GetParameters().Any(p => p.Name == ARGS_ARRAY_VAR))
						RestoreArgumentArray(variables);
					if (tmpObjectVar != null)
					{
						emitter.Emit(OpCodes.Ldloc, tmpObjectVar);
						emitter.Emit(OpCodes.Unbox_Any, AccessTools.GetReturnedType(original));
						emitter.Emit(OpCodes.Stloc, variables[RESULT_VAR]);
					}
					tmpBoxVars.Do(tmpBoxVar =>
					{
						emitter.Emit(original.IsStatic ? OpCodes.Ldarg_0 : OpCodes.Ldarg_1);
						emitter.Emit(OpCodes.Ldloc, tmpBoxVar.Key);
						emitter.Emit(OpCodes.Unbox_Any, tmpBoxVar.Value);
						emitter.Emit(OpCodes.Stobj, tmpBoxVar.Value);
					});

					if (fix.ReturnType != typeof(void))
					{
						var firstFixParam = fix.GetParameters().FirstOrDefault();
						var hasPassThroughResultParam = firstFixParam is object && fix.ReturnType == firstFixParam.ParameterType;
						if (hasPassThroughResultParam)
							result = true;
						else
						{
							if (firstFixParam is object)
								throw new Exception($"Return type of pass through postfix {fix} does not match type of its first parameter");

							throw new Exception($"Postfix patch {fix} must have a \"void\" return type");
						}
					}
				});
			return result;
		}

		bool AddFinalizers(Dictionary<string, LocalBuilder> variables, LocalBuilder runOriginalVariable, bool catchExceptions)
		{
			var rethrowPossible = true;
			finalizers
				.Do(fix =>
				{
					//if (original.HasMethodBody() is false)
					//	throw new Exception("Methods without body cannot have finalizers. Use a transpiler instead.");

					if (catchExceptions)
						emitter.MarkBlockBefore(new ExceptionBlock(ExceptionBlockType.BeginExceptionBlock), out var label);

					var tmpBoxVars = new List<KeyValuePair<LocalBuilder, Type>>();
					EmitCallParameter(fix, variables, runOriginalVariable, false, out var tmpObjectVar, tmpBoxVars);
					emitter.Emit(OpCodes.Call, fix);
					if (fix.GetParameters().Any(p => p.Name == ARGS_ARRAY_VAR))
						RestoreArgumentArray(variables);
					if (tmpObjectVar != null)
					{
						emitter.Emit(OpCodes.Ldloc, tmpObjectVar);
						emitter.Emit(OpCodes.Unbox_Any, AccessTools.GetReturnedType(original));
						emitter.Emit(OpCodes.Stloc, variables[RESULT_VAR]);
					}
					tmpBoxVars.Do(tmpBoxVar =>
					{
						emitter.Emit(original.IsStatic ? OpCodes.Ldarg_0 : OpCodes.Ldarg_1);
						emitter.Emit(OpCodes.Ldloc, tmpBoxVar.Key);
						emitter.Emit(OpCodes.Unbox_Any, tmpBoxVar.Value);
						emitter.Emit(OpCodes.Stobj, tmpBoxVar.Value);
					});

					if (fix.ReturnType != typeof(void))
					{
						emitter.Emit(OpCodes.Stloc, variables[EXCEPTION_VAR]);
						rethrowPossible = false;
					}

					if (catchExceptions)
					{
						emitter.MarkBlockBefore(new ExceptionBlock(ExceptionBlockType.BeginCatchBlock), out _);
						emitter.Emit(OpCodes.Pop);
						emitter.MarkBlockAfter(new ExceptionBlock(ExceptionBlockType.EndExceptionBlock));
					}
				});

			return rethrowPossible;
		}

		void PrepareArgumentArray()
		{
			var parameters = original.GetParameters();
			var i = 0;
			foreach (var pInfo in parameters)
			{
				var argIndex = i++ + (original.IsStatic ? 0 : 1);
				if (pInfo.IsOut || pInfo.IsRetval)
					InitializeOutParameter(argIndex, pInfo.ParameterType);
			}
			emitter.Emit(OpCodes.Ldc_I4, parameters.Length);
			emitter.Emit(OpCodes.Newarr, typeof(object));
			i = 0;
			var arrayIdx = 0;
			foreach (var pInfo in parameters)
			{
				var argIndex = i++ + (original.IsStatic ? 0 : 1);
				var pType = pInfo.ParameterType;
				var paramByRef = pType.IsByRef;
				if (paramByRef) pType = pType.GetElementType();
				emitter.Emit(OpCodes.Dup);
				emitter.Emit(OpCodes.Ldc_I4, arrayIdx++);
				emitter.Emit(OpCodes.Ldarg, argIndex);
				if (paramByRef)
				{
					if (AccessTools.IsStruct(pType))
						emitter.Emit(OpCodes.Ldobj, pType);
					else
						emitter.Emit(LoadIndOpCodeFor(pType));
				}
				if (pType.IsValueType)
					emitter.Emit(OpCodes.Box, pType);
				emitter.Emit(OpCodes.Stelem_Ref);
			}
		}

		void RestoreArgumentArray(Dictionary<string, LocalBuilder> variables)
		{
			var parameters = original.GetParameters();
			var i = 0;
			var arrayIdx = 0;
			foreach (var pInfo in parameters)
			{
				var argIndex = i++ + (original.IsStatic ? 0 : 1);
				var pType = pInfo.ParameterType;
				if (pType.IsByRef)
				{
					pType = pType.GetElementType();

					emitter.Emit(OpCodes.Ldarg, argIndex);
					emitter.Emit(OpCodes.Ldloc, variables[ARGS_ARRAY_VAR]);
					emitter.Emit(OpCodes.Ldc_I4, arrayIdx);
					emitter.Emit(OpCodes.Ldelem_Ref);

					if (pType.IsValueType)
					{
						emitter.Emit(OpCodes.Unbox_Any, pType);
						if (AccessTools.IsStruct(pType))
							emitter.Emit(OpCodes.Stobj, pType);
						else
							emitter.Emit(StoreIndOpCodeFor(pType));
					}
					else
					{
						emitter.Emit(OpCodes.Castclass, pType);
						emitter.Emit(OpCodes.Stind_Ref);
					}
				}
				arrayIdx++;
			}
		}
	}
}
