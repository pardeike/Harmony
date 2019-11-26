using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace HarmonyLib
{
	internal static class MethodPatcher
	{
		/// special parameter names that can be used in prefix and postfix methods

		public static string INSTANCE_PARAM = "__instance";
		public static string ORIGINAL_METHOD_PARAM = "__originalMethod";
		public static string RESULT_VAR = "__result";
		public static string STATE_VAR = "__state";
		public static string EXCEPTION_VAR = "__exception";
		public static string PARAM_INDEX_PREFIX = "__";
		public static string INSTANCE_FIELD_PREFIX = "___";

		public static DynamicMethod CreatePatchedMethod(MethodBase original, MethodBase source, string harmonyInstanceID, List<MethodInfo> prefixes, List<MethodInfo> postfixes, List<MethodInfo> transpilers, List<MethodInfo> finalizers)
		{
			try
			{
				if (original == null)
					throw new ArgumentNullException(nameof(original));

				Memory.MarkForNoInlining(original);

				if (Harmony.DEBUG)
				{
					FileLog.LogBuffered("### Patch " + original.FullDescription());
					FileLog.FlushBuffer();
				}

				var idx = prefixes.Count() + postfixes.Count() + finalizers.Count();
				var firstArgIsReturnBuffer = NativeThisPointer.NeedsNativeThisPointerFix(original);
				var returnType = AccessTools.GetReturnedType(original);
				var hasFinalizers = finalizers.Any();
				var patch = DynamicTools.CreateDynamicMethod(original, "_Patch" + idx);
				if (patch == null)
					return null;

				var il = patch.GetILGenerator();

				var originalVariables = DynamicTools.DeclareLocalVariables(source ?? original, il);
				var privateVars = new Dictionary<string, LocalBuilder>();

				LocalBuilder resultVariable = null;
				if (idx > 0)
				{
					resultVariable = DynamicTools.DeclareLocalVariable(il, returnType);
					privateVars[RESULT_VAR] = resultVariable;
				}

				prefixes.Union(postfixes).Union(finalizers).ToList().ForEach(fix =>
				{
					if (fix.DeclaringType != null && privateVars.ContainsKey(fix.DeclaringType.FullName) == false)
					{
						fix.GetParameters()
						.Where(patchParam => patchParam.Name == STATE_VAR)
						.Do(patchParam =>
						{
							var privateStateVariable = DynamicTools.DeclareLocalVariable(il, patchParam.ParameterType);
							privateVars[fix.DeclaringType.FullName] = privateStateVariable;
						});
					}
				});

				LocalBuilder finalizedVariable = null;
				if (hasFinalizers)
				{
					finalizedVariable = DynamicTools.DeclareLocalVariable(il, typeof(bool));

					privateVars[EXCEPTION_VAR] = DynamicTools.DeclareLocalVariable(il, typeof(Exception));

					// begin try
					Emitter.MarkBlockBefore(il, new ExceptionBlock(ExceptionBlockType.BeginExceptionBlock), out _);
				}

				if (firstArgIsReturnBuffer)
					Emitter.Emit(il, OpCodes.Ldarg_1); // load ref to return value

				var skipOriginalLabel = il.DefineLabel();
				var canHaveJump = AddPrefixes(il, original, prefixes, privateVars, skipOriginalLabel);

				var copier = new MethodCopier(source ?? original, il, originalVariables);
				foreach (var transpiler in transpilers)
					copier.AddTranspiler(transpiler);
				if (firstArgIsReturnBuffer)
					copier.AddTranspiler(NativeThisPointer.m_ArgumentShiftTranspiler);

				var endLabels = new List<Label>();
				copier.Finalize(endLabels);

				foreach (var label in endLabels)
					Emitter.MarkLabel(il, label);
				if (resultVariable != null)
					Emitter.Emit(il, OpCodes.Stloc, resultVariable);
				if (canHaveJump)
					Emitter.MarkLabel(il, skipOriginalLabel);

				AddPostfixes(il, original, postfixes, privateVars, false);

				if (resultVariable != null)
					Emitter.Emit(il, OpCodes.Ldloc, resultVariable);

				AddPostfixes(il, original, postfixes, privateVars, true);

				if (hasFinalizers)
				{
					AddFinalizers(il, original, finalizers, privateVars, false);
					Emitter.Emit(il, OpCodes.Ldc_I4_1);
					Emitter.Emit(il, OpCodes.Stloc, finalizedVariable);
					var noExceptionLabel1 = il.DefineLabel();
					Emitter.Emit(il, OpCodes.Ldloc, privateVars[EXCEPTION_VAR]);
					Emitter.Emit(il, OpCodes.Brfalse, noExceptionLabel1);
					Emitter.Emit(il, OpCodes.Ldloc, privateVars[EXCEPTION_VAR]);
					Emitter.Emit(il, OpCodes.Throw);
					Emitter.MarkLabel(il, noExceptionLabel1);

					// end try, begin catch
					Emitter.MarkBlockBefore(il, new ExceptionBlock(ExceptionBlockType.BeginCatchBlock), out var label);
					Emitter.Emit(il, OpCodes.Stloc, privateVars[EXCEPTION_VAR]);

					Emitter.Emit(il, OpCodes.Ldloc, finalizedVariable);
					var endFinalizerLabel = il.DefineLabel();
					Emitter.Emit(il, OpCodes.Brtrue, endFinalizerLabel);

					var rethrowPossible = AddFinalizers(il, original, finalizers, privateVars, true);

					Emitter.MarkLabel(il, endFinalizerLabel);

					var noExceptionLabel2 = il.DefineLabel();
					Emitter.Emit(il, OpCodes.Ldloc, privateVars[EXCEPTION_VAR]);
					Emitter.Emit(il, OpCodes.Brfalse, noExceptionLabel2);
					if (rethrowPossible)
						Emitter.Emit(il, OpCodes.Rethrow);
					else
					{
						Emitter.Emit(il, OpCodes.Ldloc, privateVars[EXCEPTION_VAR]);
						Emitter.Emit(il, OpCodes.Throw);
					}
					Emitter.MarkLabel(il, noExceptionLabel2);

					// end catch
					Emitter.MarkBlockAfter(il, new ExceptionBlock(ExceptionBlockType.EndExceptionBlock));

					if (resultVariable != null)
						Emitter.Emit(il, OpCodes.Ldloc, resultVariable);
				}

				if (firstArgIsReturnBuffer)
					Emitter.Emit(il, OpCodes.Stobj, returnType); // store result into ref

				Emitter.Emit(il, OpCodes.Ret);

				if (Harmony.DEBUG)
				{
					FileLog.LogBuffered("DONE");
					FileLog.LogBuffered("");
					FileLog.FlushBuffer();
				}

				DynamicTools.PrepareDynamicMethod(patch);
				return patch;
			}
			catch (Exception ex)
			{
				var exceptionString = "Exception from HarmonyInstance \"" + harmonyInstanceID + "\" patching " + original.FullDescription() + ": " + ex;
				if (Harmony.DEBUG)
				{
					var savedIndentLevel = FileLog.indentLevel;
					FileLog.indentLevel = 0;
					FileLog.Log(exceptionString);
					FileLog.indentLevel = savedIndentLevel;
				}

				throw new Exception(exceptionString, ex);
			}
			finally
			{
				if (Harmony.DEBUG)
					FileLog.FlushBuffer();
			}
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

		static HarmonyArgument[] AllHarmonyArguments(object[] attributes)
		{
			return attributes.Select(attr =>
			{
				if (attr.GetType().Name != nameof(HarmonyArgument)) return null;
				return AccessTools.MakeDeepCopy<HarmonyArgument>(attr);
			})
			.Where(harg => harg != null)
			.ToArray();
		}

		static HarmonyArgument GetArgumentAttribute(this ParameterInfo parameter)
		{
			var attributes = parameter.GetCustomAttributes(false);
			return AllHarmonyArguments(attributes).FirstOrDefault();
		}

		static HarmonyArgument[] GetArgumentAttributes(this MethodInfo method)
		{
			if (method == null || method is DynamicMethod)
				return default;

			var attributes = method.GetCustomAttributes(false);
			return AllHarmonyArguments(attributes);
		}

		static HarmonyArgument[] GetArgumentAttributes(this Type type)
		{
			var attributes = type.GetCustomAttributes(false);
			return AllHarmonyArguments(attributes);
		}

		static string GetOriginalArgumentName(this ParameterInfo parameter, string[] originalParameterNames)
		{
			var attribute = parameter.GetArgumentAttribute();

			if (attribute == null)
				return null;

			if (string.IsNullOrEmpty(attribute.OriginalName) == false)
				return attribute.OriginalName;

			if (attribute.Index >= 0 && attribute.Index < originalParameterNames.Length)
				return originalParameterNames[attribute.Index];

			return null;
		}

		static string GetOriginalArgumentName(HarmonyArgument[] attributes, string name, string[] originalParameterNames)
		{
			if ((attributes?.Length ?? 0) <= 0)
				return null;

			var attribute = attributes.SingleOrDefault(p => p.NewName == name);
			if (attribute == null)
				return null;

			if (string.IsNullOrEmpty(attribute.OriginalName) == false)
				return attribute.OriginalName;

			if (originalParameterNames != null && attribute.Index >= 0 && attribute.Index < originalParameterNames.Length)
				return originalParameterNames[attribute.Index];

			return null;
		}

		static string GetOriginalArgumentName(this MethodInfo method, string[] originalParameterNames, string name)
		{
			string argumentName;

			argumentName = GetOriginalArgumentName(method?.GetArgumentAttributes(), name, originalParameterNames);
			if (argumentName != null)
				return argumentName;

			argumentName = GetOriginalArgumentName(method?.DeclaringType?.GetArgumentAttributes(), name, originalParameterNames);
			if (argumentName != null)
				return argumentName;

			return name;
		}

		static int GetArgumentIndex(MethodInfo patch, string[] originalParameterNames, ParameterInfo patchParam)
		{
			if (patch is DynamicMethod)
				return Array.IndexOf(originalParameterNames, patchParam.Name);

			var originalName = patchParam.GetOriginalArgumentName(originalParameterNames);
			if (originalName != null)
				return Array.IndexOf(originalParameterNames, originalName);

			originalName = patch.GetOriginalArgumentName(originalParameterNames, patchParam.Name);
			if (originalName != null)
				return Array.IndexOf(originalParameterNames, originalName);

			return -1;
		}

		static readonly MethodInfo getMethodMethod = typeof(MethodBase).GetMethod("GetMethodFromHandle", new[] { typeof(RuntimeMethodHandle) });

		static void EmitCallParameter(ILGenerator il, MethodBase original, MethodInfo patch, Dictionary<string, LocalBuilder> variables, bool allowFirsParamPassthrough)
		{
			var isInstance = original.IsStatic == false;
			var originalParameters = original.GetParameters();
			var originalParameterNames = originalParameters.Select(p => p.Name).ToArray();
			var firstArgIsReturnBuffer = NativeThisPointer.NeedsNativeThisPointerFix(original);

			// check for passthrough using first parameter (which must have same type as return type)
			var parameters = patch.GetParameters().ToList();
			if (allowFirsParamPassthrough && patch.ReturnType != typeof(void) && parameters.Count > 0 && parameters[0].ParameterType == patch.ReturnType)
				parameters.RemoveRange(0, 1);

			foreach (var patchParam in parameters)
			{
				if (patchParam.Name == ORIGINAL_METHOD_PARAM)
				{
					if (original is ConstructorInfo constructorInfo)
					{
						Emitter.Emit(il, OpCodes.Ldtoken, constructorInfo);
						Emitter.Emit(il, OpCodes.Call, getMethodMethod);
						continue;
					}

					if (original is MethodInfo methodInfo)
					{
						Emitter.Emit(il, OpCodes.Ldtoken, methodInfo);
						Emitter.Emit(il, OpCodes.Call, getMethodMethod);
						continue;
					}
					Emitter.Emit(il, OpCodes.Ldnull);
					continue;
				}

				if (patchParam.Name == INSTANCE_PARAM)
				{
					if (original.IsStatic)
						Emitter.Emit(il, OpCodes.Ldnull);
					else
					{
						var instanceIsRef = AccessTools.IsStruct(original.DeclaringType);
						var parameterIsRef = patchParam.ParameterType.IsByRef;
						if (instanceIsRef == parameterIsRef)
						{
							Emitter.Emit(il, OpCodes.Ldarg_0);
						}
						if (instanceIsRef && parameterIsRef == false)
						{
							Emitter.Emit(il, OpCodes.Ldarg_0);
							Emitter.Emit(il, OpCodes.Ldobj, original.DeclaringType);
						}
						if (instanceIsRef == false && parameterIsRef)
						{
							Emitter.Emit(il, OpCodes.Ldarga, 0);
						}
					}
					continue;
				}

				if (patchParam.Name.StartsWith(INSTANCE_FIELD_PREFIX, StringComparison.Ordinal))
				{
					var fieldName = patchParam.Name.Substring(INSTANCE_FIELD_PREFIX.Length);
					FieldInfo fieldInfo;
					if (fieldName.All(char.IsDigit))
					{
						fieldInfo = AccessTools.DeclaredField(original.DeclaringType, int.Parse(fieldName));
						if (fieldInfo == null)
							throw new ArgumentException("No field found at given index in class " + original.DeclaringType.FullName, fieldName);
					}
					else
					{
						fieldInfo = AccessTools.DeclaredField(original.DeclaringType, fieldName);
						if (fieldInfo == null)
							throw new ArgumentException("No such field defined in class " + original.DeclaringType.FullName, fieldName);
					}

					if (fieldInfo.IsStatic)
						Emitter.Emit(il, patchParam.ParameterType.IsByRef ? OpCodes.Ldsflda : OpCodes.Ldsfld, fieldInfo);
					else
					{
						Emitter.Emit(il, OpCodes.Ldarg_0);
						Emitter.Emit(il, patchParam.ParameterType.IsByRef ? OpCodes.Ldflda : OpCodes.Ldfld, fieldInfo);
					}
					continue;
				}

				// state is special too since each patch has its own local var
				if (patchParam.Name == STATE_VAR)
				{
					var ldlocCode = patchParam.ParameterType.IsByRef ? OpCodes.Ldloca : OpCodes.Ldloc;
					if (variables.TryGetValue(patch.DeclaringType.FullName, out var stateVar))
						Emitter.Emit(il, ldlocCode, stateVar);
					else
						Emitter.Emit(il, OpCodes.Ldnull);
					continue;
				}

				// treat __result var special
				if (patchParam.Name == RESULT_VAR)
				{
					var returnType = AccessTools.GetReturnedType(original);
					if (returnType == typeof(void))
						throw new Exception("Cannot get result from void method " + original.FullDescription());
					var resultType = patchParam.ParameterType;
					if (resultType.IsByRef)
						resultType = resultType.GetElementType();
					if (resultType.IsAssignableFrom(returnType) == false)
						throw new Exception("Cannot assign method return type " + returnType.FullName + " to " + RESULT_VAR + " type " + resultType.FullName + " for method " + original.FullDescription());
					var ldlocCode = patchParam.ParameterType.IsByRef ? OpCodes.Ldloca : OpCodes.Ldloc;
					Emitter.Emit(il, ldlocCode, variables[RESULT_VAR]);
					continue;
				}

				// any other declared variables
				if (variables.TryGetValue(patchParam.Name, out var localBuilder))
				{
					var ldlocCode = patchParam.ParameterType.IsByRef ? OpCodes.Ldloca : OpCodes.Ldloc;
					Emitter.Emit(il, ldlocCode, localBuilder);
					continue;
				}

				int idx;
				if (patchParam.Name.StartsWith(PARAM_INDEX_PREFIX, StringComparison.Ordinal))
				{
					var val = patchParam.Name.Substring(PARAM_INDEX_PREFIX.Length);
					if (!int.TryParse(val, out idx))
						throw new Exception("Parameter " + patchParam.Name + " does not contain a valid index");
					if (idx < 0 || idx >= originalParameters.Length)
						throw new Exception("No parameter found at index " + idx);
				}
				else
				{
					idx = GetArgumentIndex(patch, originalParameterNames, patchParam);
					if (idx == -1) throw new Exception("Parameter \"" + patchParam.Name + "\" not found in method " + original.FullDescription());
				}

				//   original -> patch     opcode
				// --------------------------------------
				// 1 normal   -> normal  : LDARG
				// 2 normal   -> ref/out : LDARGA
				// 3 ref/out  -> normal  : LDARG, LDIND_x
				// 4 ref/out  -> ref/out : LDARG
				//
				var originalIsNormal = originalParameters[idx].IsOut == false && originalParameters[idx].ParameterType.IsByRef == false;
				var patchIsNormal = patchParam.IsOut == false && patchParam.ParameterType.IsByRef == false;
				var patchArgIndex = idx + (isInstance ? 1 : 0) + (firstArgIsReturnBuffer ? 1 : 0);

				// Case 1 + 4
				if (originalIsNormal == patchIsNormal)
				{
					Emitter.Emit(il, OpCodes.Ldarg, patchArgIndex);
					continue;
				}

				// Case 2
				if (originalIsNormal && patchIsNormal == false)
				{
					Emitter.Emit(il, OpCodes.Ldarga, patchArgIndex);
					continue;
				}

				// Case 3
				Emitter.Emit(il, OpCodes.Ldarg, patchArgIndex);
				Emitter.Emit(il, LoadIndOpCodeFor(originalParameters[idx].ParameterType));
			}
		}

		static bool AddPrefixes(ILGenerator il, MethodBase original, List<MethodInfo> prefixes, Dictionary<string, LocalBuilder> variables, Label label)
		{
			var canHaveJump = false;
			prefixes.ForEach(fix =>
			{
				EmitCallParameter(il, original, fix, variables, false);
				Emitter.Emit(il, OpCodes.Call, fix);

				if (fix.ReturnType != typeof(void))
				{
					if (fix.ReturnType != typeof(bool))
						throw new Exception("Prefix patch " + fix + " has not \"bool\" or \"void\" return type: " + fix.ReturnType);
					Emitter.Emit(il, OpCodes.Brfalse, label);
					canHaveJump = true;
				}
			});
			return canHaveJump;
		}

		static void AddPostfixes(ILGenerator il, MethodBase original, List<MethodInfo> postfixes, Dictionary<string, LocalBuilder> variables, bool passthroughPatches)
		{
			postfixes
				.Where(fix => passthroughPatches == (fix.ReturnType != typeof(void)))
				.Do(fix =>
				{
					EmitCallParameter(il, original, fix, variables, true);
					Emitter.Emit(il, OpCodes.Call, fix);

					if (fix.ReturnType != typeof(void))
					{
						var firstFixParam = fix.GetParameters().FirstOrDefault();
						var hasPassThroughResultParam = firstFixParam != null && fix.ReturnType == firstFixParam.ParameterType;
						if (!hasPassThroughResultParam)
						{
							if (firstFixParam != null)
								throw new Exception("Return type of pass through postfix " + fix + " does not match type of its first parameter");

							throw new Exception("Postfix patch " + fix + " must have a \"void\" return type");
						}
					}
				});
		}

		static bool AddFinalizers(ILGenerator il, MethodBase original, List<MethodInfo> finalizers, Dictionary<string, LocalBuilder> variables, bool catchExceptions)
		{
			var rethrowPossible = true;
			finalizers
				.Do(fix =>
				{
					if (catchExceptions)
						Emitter.MarkBlockBefore(il, new ExceptionBlock(ExceptionBlockType.BeginExceptionBlock), out var label);

					EmitCallParameter(il, original, fix, variables, false);
					Emitter.Emit(il, OpCodes.Call, fix);
					if (fix.ReturnType != typeof(void))
					{
						Emitter.Emit(il, OpCodes.Stloc, variables[EXCEPTION_VAR]);
						rethrowPossible = false;
					}

					if (catchExceptions)
					{
						Emitter.MarkBlockBefore(il, new ExceptionBlock(ExceptionBlockType.BeginCatchBlock), out _);
						Emitter.Emit(il, OpCodes.Pop);
						Emitter.MarkBlockAfter(il, new ExceptionBlock(ExceptionBlockType.EndExceptionBlock));
					}
				});
			return rethrowPossible;
		}
	}
}
