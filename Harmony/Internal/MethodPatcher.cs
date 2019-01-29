using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace Harmony
{
	internal static class MethodPatcher
	{
		/// special parameter names that can be used in prefix and postfix methods
		
		public static string INSTANCE_PARAM = "__instance";
		public static string ORIGINAL_METHOD_PARAM = "__originalMethod";
		public static string RESULT_VAR = "__result";
		public static string STATE_VAR = "__state";
		public static string PARAM_INDEX_PREFIX = "__";
		public static string INSTANCE_FIELD_PREFIX = "___";
		
		[UpgradeToLatestVersion(1)]
		public static DynamicMethod CreatePatchedMethod(MethodBase original, List<MethodInfo> prefixes, List<MethodInfo> postfixes, List<MethodInfo> transpilers)
		{
			return CreatePatchedMethod(original, "HARMONY_PATCH_1.1.1", prefixes, postfixes, transpilers);
		}
		
		[UpgradeToLatestVersion(1)]
		public static DynamicMethod CreatePatchedMethod(MethodBase original, string harmonyInstanceID, List<MethodInfo> prefixes, List<MethodInfo> postfixes, List<MethodInfo> transpilers)
		{
			Memory.MarkForNoInlining(original);

			if (original == null)
				throw new ArgumentNullException(nameof(original), "Original method is null. Did you specify it correctly?");

			try
			{
				if (HarmonyInstance.DEBUG)
				{
					FileLog.LogBuffered("### Patch " + original.DeclaringType + ", " + original);
					FileLog.FlushBuffer();
				}

				var idx = prefixes.Count() + postfixes.Count();
				var firstArgIsReturnBuffer = NativeThisPointer.NeedsNativeThisPointerFix(original);
				var returnType = AccessTools.GetReturnedType(original);
				var patch = DynamicTools.CreateDynamicMethod(original, "_Patch" + idx);
				if (patch == null)
					return null;

				var il = patch.GetILGenerator();

				var originalVariables = DynamicTools.DeclareLocalVariables(original, il);
				var privateVars = new Dictionary<string, LocalBuilder>();

				LocalBuilder resultVariable = null;
				if (idx > 0)
				{
					resultVariable = DynamicTools.DeclareLocalVariable(il, returnType);
					privateVars[RESULT_VAR] = resultVariable;
				}

				prefixes.ForEach(prefix =>
				{
					prefix.GetParameters()
						.Where(patchParam => patchParam.Name == STATE_VAR)
						.Do(patchParam =>
						{
							var privateStateVariable = DynamicTools.DeclareLocalVariable(il, patchParam.ParameterType);
							privateVars[prefix.DeclaringType.FullName] = privateStateVariable;
						});
				});

				if (firstArgIsReturnBuffer)
					Emitter.Emit(il, original.IsStatic ? OpCodes.Ldarg_0 : OpCodes.Ldarg_1);

				var skipOriginalLabel = il.DefineLabel();
				var canHaveJump = AddPrefixes(il, original, prefixes, privateVars, skipOriginalLabel);

				var copier = new MethodCopier(original, il, originalVariables);
				foreach (var transpiler in transpilers)
					copier.AddTranspiler(transpiler);

				var endLabels = new List<Label>();
				var endBlocks = new List<ExceptionBlock>();
				copier.Finalize(endLabels, endBlocks);

				foreach (var label in endLabels)
					Emitter.MarkLabel(il, label);
				foreach (var block in endBlocks)
					Emitter.MarkBlockAfter(il, block);
				if (resultVariable != null)
					Emitter.Emit(il, OpCodes.Stloc, resultVariable);
				if (canHaveJump)
					Emitter.MarkLabel(il, skipOriginalLabel);

				AddPostfixes(il, original, postfixes, privateVars, false);

				if (resultVariable != null)
					Emitter.Emit(il, OpCodes.Ldloc, resultVariable);

				AddPostfixes(il, original, postfixes, privateVars, true);

				if (firstArgIsReturnBuffer)
					Emitter.Emit(il, OpCodes.Stobj, returnType);

				Emitter.Emit(il, OpCodes.Ret);

				if (HarmonyInstance.DEBUG)
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
				var exceptionString = "Exception from HarmonyInstance \"" + harmonyInstanceID + "\" patching " + original.FullDescription();
				if (HarmonyInstance.DEBUG)
					FileLog.Log("Exception: " + exceptionString);
				throw new Exception(exceptionString, ex);
			}
			finally
			{
				if (HarmonyInstance.DEBUG)
					FileLog.FlushBuffer();
			}
		}

		static OpCode LoadIndOpCodeFor(Type type)
		{
			if (type.IsEnum) return OpCodes.Ldind_I4;

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

		[UpgradeToLatestVersion(1)]
		static HarmonyArgument GetArgumentAttribute(this ParameterInfo parameter)
		{
			var attributes = parameter.GetCustomAttributes(false);
			return AllHarmonyArguments(attributes).FirstOrDefault();
		}

		[UpgradeToLatestVersion(1)]
		static HarmonyArgument[] GetArgumentAttributes(this MethodInfo method)
		{
			if (method == null) return new HarmonyArgument[0];
			var attributes = method.GetCustomAttributes(false);
			return AllHarmonyArguments(attributes);
		}

		[UpgradeToLatestVersion(1)]
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

			argumentName = GetOriginalArgumentName(method?.DeclaringType.GetArgumentAttributes(), name, originalParameterNames);
			if (argumentName != null)
				return argumentName;

			return name;
		}

		static int GetArgumentIndex(MethodInfo patch, string[] originalParameterNames, ParameterInfo patchParam)
		{
			var originalName = patchParam.GetOriginalArgumentName(originalParameterNames);
			if (originalName != null)
				return Array.IndexOf(originalParameterNames, originalName);

			var patchParamName = patchParam.Name;
			originalName = patch.GetOriginalArgumentName(originalParameterNames, patchParamName);
			if (originalName != null)
				return Array.IndexOf(originalParameterNames, originalName);

			return -1;
		}

		static readonly MethodInfo getMethodMethod = typeof(MethodBase).GetMethod("GetMethodFromHandle", new[] { typeof(RuntimeMethodHandle) });

		[UpgradeToLatestVersion(1)]
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
					var constructorInfo = original as ConstructorInfo;
					if (constructorInfo != null)
					{
						Emitter.Emit(il, OpCodes.Ldtoken, constructorInfo);
						Emitter.Emit(il, OpCodes.Call, getMethodMethod);
						continue;
					}
					var methodInfo = original as MethodInfo;
					if (methodInfo != null)
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
							Emitter.Emit(il, firstArgIsReturnBuffer ? OpCodes.Ldarg_1 : OpCodes.Ldarg_0);
						}
						if (instanceIsRef && parameterIsRef == false)
						{
							Emitter.Emit(il, firstArgIsReturnBuffer ? OpCodes.Ldarg_1 : OpCodes.Ldarg_0);
							Emitter.Emit(il, OpCodes.Ldobj, original.DeclaringType);
						}
						if (instanceIsRef == false && parameterIsRef)
						{
							Emitter.Emit(il, OpCodes.Ldarga, firstArgIsReturnBuffer ? 1 : 0);
						}
					}
					continue;
				}

				if (patchParam.Name.StartsWith(INSTANCE_FIELD_PREFIX))
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
						Emitter.Emit(il, firstArgIsReturnBuffer ? OpCodes.Ldarg_1 : OpCodes.Ldarg_0);
						Emitter.Emit(il, patchParam.ParameterType.IsByRef ? OpCodes.Ldflda : OpCodes.Ldfld, fieldInfo);
					}
					continue;
				}

				if (patchParam.Name == STATE_VAR)
				{
					var ldlocCode = patchParam.ParameterType.IsByRef ? OpCodes.Ldloca : OpCodes.Ldloc;
					if (variables.TryGetValue(patch.DeclaringType.FullName, out var stateVar))
						Emitter.Emit(il, ldlocCode, stateVar);
					else
						Emitter.Emit(il, OpCodes.Ldnull);
					continue;
				}

				if (patchParam.Name == RESULT_VAR)
				{
					var returnType = AccessTools.GetReturnedType(original);
					if (returnType == typeof(void))
						throw new Exception("Cannot get result from void method " + original.FullDescription());
					var resultType = patchParam.ParameterType.GetElementType();
					if (resultType.IsAssignableFrom(returnType) == false)
						throw new Exception("Cannot assign method return type " + returnType.FullName + " to " + RESULT_VAR + " type " + resultType.FullName + " for method " + original.FullDescription());
					var ldlocCode = patchParam.ParameterType.IsByRef ? OpCodes.Ldloca : OpCodes.Ldloc;
					Emitter.Emit(il, ldlocCode, variables[RESULT_VAR]);
					continue;
				}

				int idx;
				if (patchParam.Name.StartsWith(PARAM_INDEX_PREFIX))
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
								throw new Exception("Return type of postfix patch " + fix + " does match type of its first parameter");

							throw new Exception("Postfix patch " + fix + " must have a \"void\" return type");
						}
					}
				});
		}
	}
}
 