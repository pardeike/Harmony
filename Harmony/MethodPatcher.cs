using Harmony.ILCopying;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace Harmony
{
	public static class MethodPatcher
	{
		// special parameter names that can be used in prefix and postfix methods
		//
		public static string INSTANCE_PARAM = "__instance";
		public static string ORIGINAL_METHOD_PARAM = "__originalMethod";
		public static string RESULT_VAR = "__result";
		public static string STATE_VAR = "__state";
		public static string PARAM_INDEX_PREFIX = "__";
		public static string INSTANCE_FIELD_PREFIX = "___";

		// in case of trouble, set to true to write dynamic method to desktop as a dll
		// won't work for all methods because of the inability to extend a type compared
		// to the way DynamicTools.CreateDynamicMethod works
		//
		static readonly bool DEBUG_METHOD_GENERATION_BY_DLL_CREATION = false;

		// for fixing old harmony bugs
		[UpgradeToLatestVersion(1)]
		public static DynamicMethod CreatePatchedMethod(MethodBase original, List<MethodInfo> prefixes, List<MethodInfo> postfixes, List<MethodInfo> transpilers)
		{
			return CreatePatchedMethod(original, "HARMONY_PATCH_1.1.1", prefixes, postfixes, transpilers);
		}

		public static DynamicMethod CreatePatchedMethod(MethodBase original, string harmonyInstanceID, List<MethodInfo> prefixes, List<MethodInfo> postfixes, List<MethodInfo> transpilers)
		{
			try
			{
				if (HarmonyInstance.DEBUG) FileLog.LogBuffered("### Patch " + original.DeclaringType + ", " + original);

				var idx = prefixes.Count() + postfixes.Count();
				var patch = DynamicTools.CreateDynamicMethod(original, "_Patch" + idx);
				if (patch == null)
					return null;

				var il = patch.GetILGenerator();

				// for debugging
				AssemblyBuilder assemblyBuilder = null;
				TypeBuilder typeBuilder = null;
				if (DEBUG_METHOD_GENERATION_BY_DLL_CREATION)
					il = DynamicTools.CreateSaveableMethod(original, "_Patch" + idx, out assemblyBuilder, out typeBuilder);

				var originalVariables = DynamicTools.DeclareLocalVariables(original, il);
				var privateVars = new Dictionary<string, LocalBuilder>();

				LocalBuilder resultVariable = null;
				if (idx > 0)
				{
					resultVariable = DynamicTools.DeclareLocalVariable(il, AccessTools.GetReturnedType(original));
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

				Emitter.Emit(il, OpCodes.Ret);

				if (HarmonyInstance.DEBUG)
				{
					FileLog.LogBuffered("DONE");
					FileLog.LogBuffered("");
					FileLog.FlushBuffer();
				}

				// for debugging
				if (DEBUG_METHOD_GENERATION_BY_DLL_CREATION)
				{
					DynamicTools.SaveMethod(assemblyBuilder, typeBuilder);
					return null;
				}

				DynamicTools.PrepareDynamicMethod(patch);
				return patch;
			}
			catch (Exception ex)
			{
				throw new Exception("Exception from HarmonyInstance \"" + harmonyInstanceID + "\"", ex);
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

		static HarmonyArgument GetArgumentAttribute(this ParameterInfo parameter)
		{
			return parameter.GetCustomAttributes(false).FirstOrDefault(attr => attr is HarmonyArgument) as HarmonyArgument;
		}

		static HarmonyArgument[] GetArgumentAttributes(this MethodInfo method)
		{
			return method.GetCustomAttributes(false).Where(attr => attr is HarmonyArgument).Cast<HarmonyArgument>().ToArray();
		}

		static HarmonyArgument[] GetArgumentAttributes(this Type type)
		{
			return type.GetCustomAttributes(false).Where(attr => attr is HarmonyArgument).Cast<HarmonyArgument>().ToArray();
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
			if (attributes.Length <= 0)
				return null;

			var attribute = attributes.SingleOrDefault(p => p.NewName == name);
			if (attribute == null)
				return null;

			if (string.IsNullOrEmpty(attribute.OriginalName) == false)
				return attribute.OriginalName;

			if (attribute.Index >= 0 && attribute.Index < originalParameterNames.Length)
				return originalParameterNames[attribute.Index];

			return null;
		}

		static string GetOriginalArgumentName(this MethodInfo method, string[] originalParameterNames, string name)
		{
			string argumentName;

			argumentName = GetOriginalArgumentName(method.GetArgumentAttributes(), name, originalParameterNames);
			if (argumentName != null)
				return argumentName;

			argumentName = GetOriginalArgumentName(method.DeclaringType.GetArgumentAttributes(), name, originalParameterNames);
			if (argumentName != null)
				return argumentName;

			return name;
		}

		private static int GetArgumentIndex(MethodInfo patch, string[] originalParameterNames, ParameterInfo patchParam)
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

		static MethodInfo getMethodMethod = typeof(MethodBase).GetMethod("GetMethodFromHandle", new[] { typeof(RuntimeMethodHandle) });

		static void EmitCallParameter(ILGenerator il, MethodBase original, MethodInfo patch, Dictionary<string, LocalBuilder> variables, bool allowFirsParamPassthrough)
		{
			var isInstance = original.IsStatic == false;
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
					else if (patchParam.ParameterType.IsByRef)
						Emitter.Emit(il, OpCodes.Ldarga, 0); // probably won't work or will be useless
					else
						Emitter.Emit(il, OpCodes.Ldarg_0);
					continue;
				}

				if (patchParam.Name.StartsWith(INSTANCE_FIELD_PREFIX))
				{
					var fieldName = patchParam.Name.Substring(INSTANCE_FIELD_PREFIX.Length);
					FieldInfo fieldInfo;
					if (fieldName.All(char.IsDigit))
					{
						fieldInfo = AccessTools.Field(original.DeclaringType, int.Parse(fieldName));
						if (fieldInfo == null)
							throw new ArgumentException("No field found at given index in class " + original.DeclaringType.FullName, fieldName);
					}
					else
					{
						fieldInfo = AccessTools.Field(original.DeclaringType, fieldName);
						if (fieldInfo == null)
							throw new ArgumentException("No such field defined in class " + original.DeclaringType.FullName, fieldName);
					}

					if (fieldInfo.IsStatic)
					{
						if (patchParam.ParameterType.IsByRef)
							Emitter.Emit(il, OpCodes.Ldsflda, fieldInfo);
						else
							Emitter.Emit(il, OpCodes.Ldsfld, fieldInfo);
					}
					else
					{
						if (patchParam.ParameterType.IsByRef)
						{
							Emitter.Emit(il, OpCodes.Ldarg_0);
							Emitter.Emit(il, OpCodes.Ldflda, fieldInfo);
						}
						else
						{
							Emitter.Emit(il, OpCodes.Ldarg_0);
							Emitter.Emit(il, OpCodes.Ldfld, fieldInfo);
						}
					}
					continue;
				}

				if (patchParam.Name == STATE_VAR)
				{
					var ldlocCode = patchParam.ParameterType.IsByRef ? OpCodes.Ldloca : OpCodes.Ldloc;
					Emitter.Emit(il, ldlocCode, variables[patch.DeclaringType.FullName]);
					continue;
				}

				if (patchParam.Name == RESULT_VAR)
				{
					if (AccessTools.GetReturnedType(original) == typeof(void))
						throw new Exception("Cannot get result from void method " + original.FullDescription());
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
				var patchArgIndex = idx + (isInstance ? 1 : 0);

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