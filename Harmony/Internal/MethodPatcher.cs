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
		readonly bool debug;
		readonly MethodBase original;
		readonly MethodBase source;
		readonly List<MethodInfo> prefixes;
		readonly List<MethodInfo> postfixes;
		readonly List<MethodInfo> transpilers;
		readonly List<MethodInfo> finalizers;
		readonly List<MethodInfo> infixes;
		readonly int patchIdx;
		readonly Type returnType;
		readonly DynamicMethodDefinition patch;
		readonly ILGenerator il;
		readonly Emitter emitter;

		internal MethodPatcher(MethodBase original, MethodBase source, List<MethodInfo> prefixes, List<MethodInfo> postfixes, List<MethodInfo> transpilers, List<MethodInfo> finalizers, List<MethodInfo> infixes, bool debug)
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
			this.infixes = infixes;

			if (debug)
			{
				FileLog.LogBuffered($"### Patch: {original.FullDescription()}");
				FileLog.FlushBuffer();
			}

			patchIdx = prefixes.Count + postfixes.Count + finalizers.Count + infixes.Count;
			returnType = AccessTools.GetReturnedType(original);
			patch = MethodPatcherTools.CreateDynamicMethod(original, $"_Patch{patchIdx}", debug);
			if (patch is null)
				throw new Exception("Could not create replacement method");

			il = patch.GetILGenerator();
			emitter = new Emitter(il); // TODO: remove Emitter and only create it in the last step
		}

		internal (MethodInfo, Dictionary<int, CodeInstruction>) CreateReplacement()
		{
			var originalVariables = MethodPatcherTools.DeclareOriginalLocalVariables(il, source ?? original);
			var localState = new LocalBuilderState();
			var fixes = prefixes.Union(postfixes).Union(finalizers).Union(infixes).ToList();
			var parameterNames = fixes.ToDictionary(fix => fix, fix => new HashSet<(ParameterInfo info, string realName)>(MethodPatcherTools.OriginalParameters(fix)));

			LocalBuilder resultVariable = null;
			if (patchIdx > 0)
			{
				resultVariable = emitter.DeclareLocalVariable(returnType, true);
				localState[MethodPatcherTools.RESULT_VAR] = resultVariable;
			}

			if (fixes.Any(fix => parameterNames[fix].Any(pair => pair.realName == MethodPatcherTools.RESULT_REF_VAR)))
			{
				if (returnType.IsByRef)
				{
					var resultRefVariable = il.DeclareLocal(
						 typeof(RefResult<>).MakeGenericType(returnType.GetElementType())
					);
					emitter.Emit(OpCodes.Ldnull);
					emitter.Emit(OpCodes.Stloc, resultRefVariable);
					localState[MethodPatcherTools.RESULT_REF_VAR] = resultRefVariable;
				}
			}

			LocalBuilder argsArrayVariable = null;
			if (fixes.Any(fix => parameterNames[fix].Any(pair => pair.realName == MethodPatcherTools.ARGS_ARRAY_VAR)))
			{
				emitter.PrepareArgumentArray(original);
				argsArrayVariable = il.DeclareLocal(typeof(object[]));
				emitter.Emit(OpCodes.Stloc, argsArrayVariable);
				localState[MethodPatcherTools.ARGS_ARRAY_VAR] = argsArrayVariable;
			}

			Label? skipOriginalLabel = null;
			LocalBuilder runOriginalVariable = null;
			var prefixAffectsOriginal = prefixes.Any(MethodPatcherTools.PrefixAffectsOriginal);
			var anyFixHasRunOriginalVar = fixes.Any(fix => parameterNames[fix].Any(pair => pair.realName == MethodPatcherTools.RUN_ORIGINAL_VAR));
			if (prefixAffectsOriginal || anyFixHasRunOriginalVar)
			{
				runOriginalVariable = emitter.DeclareLocalVariable(typeof(bool));
				emitter.Emit(OpCodes.Ldc_I4_1);
				emitter.Emit(OpCodes.Stloc, runOriginalVariable);

				if (prefixAffectsOriginal)
					skipOriginalLabel = il.DefineLabel();
			}

			fixes.ForEach(fix =>
			{
				if (fix.DeclaringType is not null && localState.TryGetValue(fix.DeclaringType.AssemblyQualifiedName, out _) is false)
				{
					parameterNames[fix].Where(pair => pair.realName == MethodPatcherTools.STATE_VAR).Select(pair => pair.info).Do(patchParam =>
					{
						var privateStateVariable = emitter.DeclareLocalVariable(patchParam.ParameterType);
						localState[fix.DeclaringType.AssemblyQualifiedName] = privateStateVariable;
					});
				}
			});

			LocalBuilder finalizedVariable = null;
			if (finalizers.Count > 0)
			{
				finalizedVariable = emitter.DeclareLocalVariable(typeof(bool));
				localState[MethodPatcherTools.EXCEPTION_VAR] = emitter.DeclareLocalVariable(typeof(Exception));

				// begin try
				emitter.MarkBlockBefore(new ExceptionBlock(ExceptionBlockType.BeginExceptionBlock), out _);
			}

			AddPrefixes(localState, runOriginalVariable);
			if (skipOriginalLabel.HasValue)
			{
				emitter.Emit(OpCodes.Ldloc, runOriginalVariable);
				emitter.Emit(OpCodes.Brfalse, skipOriginalLabel.Value);
			}

			var copier = new MethodCopier(source ?? original, il, originalVariables);
			copier.SetDebugging(debug);

			foreach (var transpiler in transpilers)
				copier.AddTranspiler(transpiler);
			copier.AddTranspiler(PatchTools.m_GetExecutingAssemblyReplacementTranspiler);

			var endLabels = new List<Label>();
			var replacementCodes = copier.Finalize(out var hasReturnCode, out var methodEndsInDeadCode, endLabels);
			//copier.LogCodes(emitter, replacementCodes);
			copier.EmitCodes(emitter, replacementCodes, endLabels);

			foreach (var label in endLabels)
				emitter.MarkLabel(label);
			if (resultVariable is not null && hasReturnCode)
				emitter.Emit(OpCodes.Stloc, resultVariable);
			if (skipOriginalLabel.HasValue)
				emitter.MarkLabel(skipOriginalLabel.Value);

			_ = AddPostfixes(localState, runOriginalVariable, false);

			if (resultVariable is not null && (hasReturnCode || (methodEndsInDeadCode && skipOriginalLabel is not null)))
				emitter.Emit(OpCodes.Ldloc, resultVariable);
			FileLog.LogILComment(emitter.CurrentPos(), "end original" + (methodEndsInDeadCode ? " (has dead code end)" : ""));

			var needsToStorePassthroughResult = AddPostfixes(localState, runOriginalVariable, true);

			var hasFinalizers = finalizers.Count > 0;
			if (hasFinalizers)
			{
				if (needsToStorePassthroughResult)
				{
					emitter.Emit(OpCodes.Stloc, resultVariable);
					emitter.Emit(OpCodes.Ldloc, resultVariable);
				}

				_ = AddFinalizers(localState, runOriginalVariable, false);
				emitter.Emit(OpCodes.Ldc_I4_1);
				emitter.Emit(OpCodes.Stloc, finalizedVariable);
				var noExceptionLabel1 = il.DefineLabel();
				emitter.Emit(OpCodes.Ldloc, localState[MethodPatcherTools.EXCEPTION_VAR]);
				emitter.Emit(OpCodes.Brfalse, noExceptionLabel1);
				emitter.Emit(OpCodes.Ldloc, localState[MethodPatcherTools.EXCEPTION_VAR]);
				emitter.Emit(OpCodes.Throw);
				emitter.MarkLabel(noExceptionLabel1);

				// end try, begin catch
				emitter.MarkBlockBefore(new ExceptionBlock(ExceptionBlockType.BeginCatchBlock), out var label_off);
				emitter.Emit(OpCodes.Stloc, localState[MethodPatcherTools.EXCEPTION_VAR]);

				emitter.Emit(OpCodes.Ldloc, finalizedVariable);
				var endFinalizerLabel = il.DefineLabel();
				emitter.Emit(OpCodes.Brtrue, endFinalizerLabel);

				var rethrowPossible = AddFinalizers(localState, runOriginalVariable, true);

				emitter.MarkLabel(endFinalizerLabel);

				var noExceptionLabel2 = il.DefineLabel();
				emitter.Emit(OpCodes.Ldloc, localState[MethodPatcherTools.EXCEPTION_VAR]);
				emitter.Emit(OpCodes.Brfalse, noExceptionLabel2);
				if (rethrowPossible)
					emitter.Emit(OpCodes.Rethrow);
				else
				{
					emitter.Emit(OpCodes.Ldloc, localState[MethodPatcherTools.EXCEPTION_VAR]);
					emitter.Emit(OpCodes.Throw);
				}
				emitter.MarkLabel(noExceptionLabel2);

				// end catch
				emitter.MarkBlockAfter(new ExceptionBlock(ExceptionBlockType.EndExceptionBlock));

				if (resultVariable is not null)
					emitter.Emit(OpCodes.Ldloc, resultVariable);
			}

			if (methodEndsInDeadCode == false || skipOriginalLabel is not null || hasFinalizers || postfixes.Count > 0)
				emitter.Emit(OpCodes.Ret);

			if (debug)
			{
				FileLog.LogBuffered("DONE");
				FileLog.LogBuffered("");
				FileLog.FlushBuffer();
			}

			return (patch.Generate(), emitter.GetInstructions());
		}

		void AddPrefixes(LocalBuilderState localState, LocalBuilder runOriginalVariable)
		{
			prefixes.Do(fix =>
			{
				var skipLabel = MethodPatcherTools.PrefixAffectsOriginal(fix) ? il.DefineLabel() : (Label?)null;
				if (skipLabel.HasValue)
				{
					emitter.Emit(OpCodes.Ldloc, runOriginalVariable);
					emitter.Emit(OpCodes.Brfalse, skipLabel.Value);
				}

				var tmpBoxVars = new List<KeyValuePair<LocalBuilder, Type>>();
				EmitCallParameter(fix, localState, runOriginalVariable, false, out var tmpInstanceBoxingVar, out var tmpObjectVar, out var refResultUsed, tmpBoxVars);
				emitter.Emit(OpCodes.Call, fix);
				if (MethodPatcherTools.OriginalParameters(fix).Any(pair => pair.realName == MethodPatcherTools.ARGS_ARRAY_VAR))
					emitter.RestoreArgumentArray(original, localState);
				if (tmpInstanceBoxingVar != null)
				{
					emitter.Emit(OpCodes.Ldarg_0);
					emitter.Emit(OpCodes.Ldloc, tmpInstanceBoxingVar);
					emitter.Emit(OpCodes.Unbox_Any, original.DeclaringType);
					emitter.Emit(OpCodes.Stobj, original.DeclaringType);
				}
				if (refResultUsed)
				{
					var label = il.DefineLabel();
					emitter.Emit(OpCodes.Ldloc, localState[MethodPatcherTools.RESULT_REF_VAR]);
					emitter.Emit(OpCodes.Brfalse_S, label);

					emitter.Emit(OpCodes.Ldloc, localState[MethodPatcherTools.RESULT_REF_VAR]);
					emitter.Emit(OpCodes.Callvirt, AccessTools.Method(localState[MethodPatcherTools.RESULT_REF_VAR].LocalType, "Invoke"));
					emitter.Emit(OpCodes.Stloc, localState[MethodPatcherTools.RESULT_VAR]);
					emitter.Emit(OpCodes.Ldnull);
					emitter.Emit(OpCodes.Stloc, localState[MethodPatcherTools.RESULT_REF_VAR]);

					emitter.MarkLabel(label);
					emitter.Emit(OpCodes.Nop);
				}
				else if (tmpObjectVar != null)
				{
					emitter.Emit(OpCodes.Ldloc, tmpObjectVar);
					emitter.Emit(OpCodes.Unbox_Any, AccessTools.GetReturnedType(original));
					emitter.Emit(OpCodes.Stloc, localState[MethodPatcherTools.RESULT_VAR]);
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

		bool AddPostfixes(LocalBuilderState localState, LocalBuilder runOriginalVariable, bool passthroughPatches)
		{
			var result = false;
			foreach (var fix in postfixes.Where(fix => passthroughPatches == (fix.ReturnType != typeof(void))))
			{
				var tmpBoxVars = new List<KeyValuePair<LocalBuilder, Type>>();
				EmitCallParameter(fix, localState, runOriginalVariable, true, out var tmpInstanceBoxingVar, out var tmpObjectVar, out var refResultUsed, tmpBoxVars);
				emitter.Emit(OpCodes.Call, fix);
				if (MethodPatcherTools.OriginalParameters(fix).Any(pair => pair.realName == MethodPatcherTools.ARGS_ARRAY_VAR))
					emitter.RestoreArgumentArray(original, localState);
				if (tmpInstanceBoxingVar != null)
				{
					emitter.Emit(OpCodes.Ldarg_0);
					emitter.Emit(OpCodes.Ldloc, tmpInstanceBoxingVar);
					emitter.Emit(OpCodes.Unbox_Any, original.DeclaringType);
					emitter.Emit(OpCodes.Stobj, original.DeclaringType);
				}
				if (refResultUsed)
				{
					var label = il.DefineLabel();
					emitter.Emit(OpCodes.Ldloc, localState[MethodPatcherTools.RESULT_REF_VAR]);
					emitter.Emit(OpCodes.Brfalse_S, label);

					emitter.Emit(OpCodes.Ldloc, localState[MethodPatcherTools.RESULT_REF_VAR]);
					emitter.Emit(OpCodes.Callvirt, AccessTools.Method(localState[MethodPatcherTools.RESULT_REF_VAR].LocalType, "Invoke"));
					emitter.Emit(OpCodes.Stloc, localState[MethodPatcherTools.RESULT_VAR]);
					emitter.Emit(OpCodes.Ldnull);
					emitter.Emit(OpCodes.Stloc, localState[MethodPatcherTools.RESULT_REF_VAR]);

					emitter.MarkLabel(label);
					emitter.Emit(OpCodes.Nop);
				}
				else if (tmpObjectVar != null)
				{
					emitter.Emit(OpCodes.Ldloc, tmpObjectVar);
					emitter.Emit(OpCodes.Unbox_Any, AccessTools.GetReturnedType(original));
					emitter.Emit(OpCodes.Stloc, localState[MethodPatcherTools.RESULT_VAR]);
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
					var hasPassThroughResultParam = firstFixParam is not null && fix.ReturnType == firstFixParam.ParameterType;
					if (hasPassThroughResultParam)
						result = true;
					else
					{
						if (firstFixParam is not null)
							throw new Exception($"Return type of pass through postfix {fix} does not match type of its first parameter");

						throw new Exception($"Postfix patch {fix} must have a \"void\" return type");
					}
				}
			}
			return result;
		}

		bool AddFinalizers(LocalBuilderState localState, LocalBuilder runOriginalVariable, bool catchExceptions)
		{
			var rethrowPossible = true;
			finalizers.Do(fix =>
			{
				if (catchExceptions)
					emitter.MarkBlockBefore(new ExceptionBlock(ExceptionBlockType.BeginExceptionBlock), out var label);

				var tmpBoxVars = new List<KeyValuePair<LocalBuilder, Type>>();
				EmitCallParameter(fix, localState, runOriginalVariable, false, out var tmpInstanceBoxingVar, out var tmpObjectVar, out var refResultUsed, tmpBoxVars);
				emitter.Emit(OpCodes.Call, fix);
				if (MethodPatcherTools.OriginalParameters(fix).Any(pair => pair.realName == MethodPatcherTools.ARGS_ARRAY_VAR))
					emitter.RestoreArgumentArray(original, localState);
				if (tmpInstanceBoxingVar != null)
				{
					emitter.Emit(OpCodes.Ldarg_0);
					emitter.Emit(OpCodes.Ldloc, tmpInstanceBoxingVar);
					emitter.Emit(OpCodes.Unbox_Any, original.DeclaringType);
					emitter.Emit(OpCodes.Stobj, original.DeclaringType);
				}
				if (refResultUsed)
				{
					var label = il.DefineLabel();
					emitter.Emit(OpCodes.Ldloc, localState[MethodPatcherTools.RESULT_REF_VAR]);
					emitter.Emit(OpCodes.Brfalse_S, label);

					emitter.Emit(OpCodes.Ldloc, localState[MethodPatcherTools.RESULT_REF_VAR]);
					emitter.Emit(OpCodes.Callvirt, AccessTools.Method(localState[MethodPatcherTools.RESULT_REF_VAR].LocalType, "Invoke"));
					emitter.Emit(OpCodes.Stloc, localState[MethodPatcherTools.RESULT_VAR]);
					emitter.Emit(OpCodes.Ldnull);
					emitter.Emit(OpCodes.Stloc, localState[MethodPatcherTools.RESULT_REF_VAR]);

					emitter.MarkLabel(label);
					emitter.Emit(OpCodes.Nop);
				}
				else if (tmpObjectVar != null)
				{
					emitter.Emit(OpCodes.Ldloc, tmpObjectVar);
					emitter.Emit(OpCodes.Unbox_Any, AccessTools.GetReturnedType(original));
					emitter.Emit(OpCodes.Stloc, localState[MethodPatcherTools.RESULT_VAR]);
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
					emitter.Emit(OpCodes.Stloc, localState[MethodPatcherTools.EXCEPTION_VAR]);
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

		void EmitCallParameter(MethodInfo patch, LocalBuilderState localState, LocalBuilder runOriginalVariable, bool allowFirsParamPassthrough,
			 out LocalBuilder tmpInstanceBoxingVar, out LocalBuilder tmpObjectVar, out bool refResultUsed, List<KeyValuePair<LocalBuilder, Type>> tmpBoxVars)
		{
			tmpInstanceBoxingVar = null;
			tmpObjectVar = null;
			refResultUsed = false;

			var isInstance = original.IsStatic is false;
			var originalParameters = original.GetParameters();
			var originalParameterNames = originalParameters.Select(p => p.Name).ToArray();
			var originalType = original.DeclaringType;

			var parameters = patch.GetParameters().ToList();
			if (allowFirsParamPassthrough && patch.ReturnType != typeof(void) && parameters.Count > 0 && parameters[0].ParameterType == patch.ReturnType)
				parameters.RemoveAt(0);

			var realNames = MethodPatcherTools.RealNames(patch);
			foreach (var patchParam in parameters)
			{
				var patchParamName = realNames[patchParam.Name];
				if (patchParamName == MethodPatcherTools.ORIGINAL_METHOD_PARAM)
				{
					if (MethodPatcherTools.EmitOriginalBaseMethod(original, emitter))
						continue;

					emitter.Emit(OpCodes.Ldnull);
					continue;
				}

				if (patchParamName == MethodPatcherTools.RUN_ORIGINAL_VAR)
				{
					if (runOriginalVariable != null)
						emitter.Emit(OpCodes.Ldloc, runOriginalVariable);
					else
						emitter.Emit(OpCodes.Ldc_I4_0);
					continue;
				}

				if (patchParamName == MethodPatcherTools.INSTANCE_PARAM)
				{
					if (original.IsStatic)
						emitter.Emit(OpCodes.Ldnull);
					else
					{
						var paramType = patchParam.ParameterType;

						var parameterIsRef = paramType.IsByRef;
						var parameterIsObject = paramType == typeof(object) || paramType == typeof(object).MakeByRefType();

						if (AccessTools.IsStruct(originalType))
						{
							if (parameterIsObject)
							{
								if (parameterIsRef)
								{
									emitter.Emit(OpCodes.Ldarg_0);
									emitter.Emit(OpCodes.Ldobj, originalType);
									emitter.Emit(OpCodes.Box, originalType);
									tmpInstanceBoxingVar = il.DeclareLocal(typeof(object));
									emitter.Emit(OpCodes.Stloc, tmpInstanceBoxingVar);
									emitter.Emit(OpCodes.Ldloca, tmpInstanceBoxingVar);
								}
								else
								{
									emitter.Emit(OpCodes.Ldarg_0);
									emitter.Emit(OpCodes.Ldobj, originalType);
									emitter.Emit(OpCodes.Box, originalType);
								}
							}
							else
							{
								if (parameterIsRef)
									emitter.Emit(OpCodes.Ldarg_0);
								else
								{
									emitter.Emit(OpCodes.Ldarg_0);
									emitter.Emit(OpCodes.Ldobj, originalType);
								}
							}
						}
						else
						{
							if (parameterIsRef)
								emitter.Emit(OpCodes.Ldarga, 0);
							else
								emitter.Emit(OpCodes.Ldarg_0);
						}
					}
					continue;
				}

				if (patchParamName == MethodPatcherTools.ARGS_ARRAY_VAR)
				{
					if (localState.TryGetValue(MethodPatcherTools.ARGS_ARRAY_VAR, out var argsArrayVar))
						emitter.Emit(OpCodes.Ldloc, argsArrayVar);
					else
						emitter.Emit(OpCodes.Ldnull);
					continue;
				}

				if (patchParamName.StartsWith(MethodPatcherTools.INSTANCE_FIELD_PREFIX, StringComparison.Ordinal))
				{
					var fieldName = patchParamName.Substring(MethodPatcherTools.INSTANCE_FIELD_PREFIX.Length);
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
						emitter.Emit(patchParam.ParameterType.IsByRef ? OpCodes.Ldsflda : OpCodes.Ldsfld, fieldInfo);
					else
					{
						emitter.Emit(OpCodes.Ldarg_0);
						emitter.Emit(patchParam.ParameterType.IsByRef ? OpCodes.Ldflda : OpCodes.Ldfld, fieldInfo);
					}
					continue;
				}

				if (patchParamName == MethodPatcherTools.STATE_VAR)
				{
					var ldlocCode = patchParam.ParameterType.IsByRef ? OpCodes.Ldloca : OpCodes.Ldloc;
					if (localState.TryGetValue(patch.DeclaringType?.AssemblyQualifiedName ?? "null", out var stateVar))
						emitter.Emit(ldlocCode, stateVar);
					else
						emitter.Emit(OpCodes.Ldnull);
					continue;
				}

				if (patchParamName == MethodPatcherTools.RESULT_VAR)
				{
					if (returnType == typeof(void))
						throw new Exception($"Cannot get result from void method {original.FullDescription()}");
					var resultType = patchParam.ParameterType;
					if (resultType.IsByRef && returnType.IsByRef is false)
						resultType = resultType.GetElementType();
					if (resultType.IsAssignableFrom(returnType) is false)
						throw new Exception($"Cannot assign method return type {returnType.FullName} to {MethodPatcherTools.RESULT_VAR} type {resultType.FullName} for method {original.FullDescription()}");
					var ldlocCode = patchParam.ParameterType.IsByRef && returnType.IsByRef is false ? OpCodes.Ldloca : OpCodes.Ldloc;
					if (returnType.IsValueType && patchParam.ParameterType == typeof(object).MakeByRefType()) ldlocCode = OpCodes.Ldloc;
					emitter.Emit(ldlocCode, localState[MethodPatcherTools.RESULT_VAR]);
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

				if (patchParamName == MethodPatcherTools.RESULT_REF_VAR)
				{
					if (!returnType.IsByRef)
						throw new Exception(
							 $"Cannot use {MethodPatcherTools.RESULT_REF_VAR} with non-ref return type {returnType.FullName} of method {original.FullDescription()}");

					var resultType = patchParam.ParameterType;
					var expectedTypeRef = typeof(RefResult<>).MakeGenericType(returnType.GetElementType()).MakeByRefType();
					if (resultType != expectedTypeRef)
						throw new Exception(
							 $"Wrong type of {MethodPatcherTools.RESULT_REF_VAR} for method {original.FullDescription()}. Expected {expectedTypeRef.FullName}, got {resultType.FullName}");

					emitter.Emit(OpCodes.Ldloca, localState[MethodPatcherTools.RESULT_REF_VAR]);

					refResultUsed = true;
					continue;
				}

				if (localState.TryGetValue(patchParamName, out var localBuilder))
				{
					var ldlocCode = patchParam.ParameterType.IsByRef ? OpCodes.Ldloca : OpCodes.Ldloc;
					emitter.Emit(ldlocCode, localBuilder);
					continue;
				}

				int argumentIdx;
				if (patchParamName.StartsWith(MethodPatcherTools.PARAM_INDEX_PREFIX, StringComparison.Ordinal))
				{
					var val = patchParamName.Substring(MethodPatcherTools.PARAM_INDEX_PREFIX.Length);
					if (!int.TryParse(val, out argumentIdx))
						throw new Exception($"Parameter {patchParamName} does not contain a valid index");
					if (argumentIdx < 0 || argumentIdx >= originalParameters.Length)
						throw new Exception($"No parameter found at index {argumentIdx}");
				}
				else
				{
					argumentIdx = patch.GetArgumentIndex(originalParameterNames, patchParam);
					if (argumentIdx == -1)
					{
						var harmonyMethod = HarmonyMethodExtensions.GetMergedFromType(patchParam.ParameterType);
						harmonyMethod.methodType ??= MethodType.Normal;
						var delegateOriginal = harmonyMethod.GetOriginalMethod();
						if (delegateOriginal is MethodInfo methodInfo)
						{
							var delegateConstructor = patchParam.ParameterType.GetConstructor([typeof(object), typeof(IntPtr)]);
							if (delegateConstructor is not null)
							{
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

						throw new Exception($"Parameter \"{patchParamName}\" not found in method {original.FullDescription()}");
					}
				}

				var originalParamType = originalParameters[argumentIdx].ParameterType;
				var originalParamElementType = originalParamType.IsByRef ? originalParamType.GetElementType() : originalParamType;
				var patchParamType = patchParam.ParameterType;
				var patchParamElementType = patchParamType.IsByRef ? patchParamType.GetElementType() : patchParamType;
				var originalIsNormal = originalParameters[argumentIdx].IsOut is false && originalParamType.IsByRef is false;
				var patchIsNormal = patchParam.IsOut is false && patchParamType.IsByRef is false;
				var needsBoxing = originalParamElementType.IsValueType && patchParamElementType.IsValueType is false;
				var patchArgIndex = argumentIdx + (isInstance ? 1 : 0);

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
						emitter.Emit(MethodPatcherTools.LoadIndOpCodeFor(originalParameters[argumentIdx].ParameterType));
				}
			}
		}
	}
}
