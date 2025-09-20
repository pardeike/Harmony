using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

using static HarmonyLib.Code;

namespace HarmonyLib
{
	internal class MethodCreator
	{
		internal MethodCreatorConfig config;

		internal MethodCreator(MethodCreatorConfig config)
		{
			if (config.original is null)
				throw new ArgumentNullException("config.original");
			this.config = config;
			if (config.debug)
			{
				FileLog.LogBuffered($"### Patch: {config.original.FullDescription()}");
				FileLog.FlushBuffer();
			}
			if (config.Prepare() == false)
				throw new Exception("Could not create replacement method");
		}

		internal (MethodInfo, Dictionary<int, CodeInstruction>) CreateReplacement()
		{
			config.originalVariables = this.DeclareOriginalLocalVariables(config.MethodBase);
			config.localVariables = new VariableState();

			if (config.Fixes.Any() && config.returnType != typeof(void))
			{
				config.resultVariable = config.DeclareLocal(config.returnType);
				config.AddLocal(InjectionType.Result, config.resultVariable);
				config.AddCodes(this.GenerateVariableInit(config.resultVariable, true));
			}

			if (config.AnyFixHas(InjectionType.ResultRef))
			{
				if (config.returnType.IsByRef)
				{
					var varType = typeof(RefResult<>).MakeGenericType(config.returnType.GetElementType());
					var resultRefVariable = config.DeclareLocal(varType);
					config.AddLocal(InjectionType.ResultRef, resultRefVariable);
					config.AddCodes([Ldnull, Stloc[resultRefVariable]]);
				}
			}

			if (config.AnyFixHas(InjectionType.ArgsArray))
			{
				var argsArrayVariable = config.DeclareLocal(typeof(object[]));
				config.AddLocal(InjectionType.ArgsArray, argsArrayVariable);
				config.AddCodes(this.PrepareArgumentArray());
				config.AddCode(Stloc[argsArrayVariable]);
			}

			config.skipOriginalLabel = null;
			var prefixAffectsOriginal = config.prefixes.Any(this.AffectsOriginal);
			var anyFixHasRunOriginal = config.AnyFixHas(InjectionType.RunOriginal);
			if (prefixAffectsOriginal || anyFixHasRunOriginal)
			{
				config.runOriginalVariable = config.DeclareLocal(typeof(bool));
				config.AddCodes([Ldc_I4_1, Stloc[config.runOriginalVariable]]);
				if (prefixAffectsOriginal)
					config.skipOriginalLabel = config.DefineLabel();
			}

			config.WithFixes(fix =>
			{
				var declaringType = fix.DeclaringType;
				if (declaringType is null)
					return;
				var varName = declaringType.AssemblyQualifiedName;
				_ = config.localVariables.TryGetValue(varName, out var maybeLocal);
				foreach (var injection in config.InjectionsFor(fix, InjectionType.State))
				{
					var parameterType = injection.parameterInfo.ParameterType;
					var type = parameterType.IsByRef ? parameterType.GetElementType() : parameterType;
					if (maybeLocal != null)
					{
						if (!type.IsAssignableFrom(maybeLocal.LocalType))
						{
							var message = $"__state type mismatch in patch \"{fix.DeclaringType.FullName}.{fix.Name}\": " +
							$"previous __state was declared as \"{maybeLocal.LocalType.FullName}\" but this patch expects \"{type.FullName}\"";
							throw new HarmonyException(message);
						}
						else
						{
							continue;
						}
					}
					var privateStateVariable = config.DeclareLocal(type);
					config.AddLocal(varName, privateStateVariable);
					config.AddCodes(this.GenerateVariableInit(privateStateVariable));
				}
			});

			config.finalizedVariable = null;
			if (config.finalizers.Count > 0)
			{
				config.finalizedVariable = config.DeclareLocal(typeof(bool));
				config.AddCodes(this.GenerateVariableInit(config.finalizedVariable));
				config.exceptionVariable = config.DeclareLocal(typeof(Exception));
				config.AddLocal(InjectionType.Exception, config.exceptionVariable);
				config.AddCodes(this.GenerateVariableInit(config.exceptionVariable));
				// begin try
				config.AddCode(this.MarkBlock(ExceptionBlockType.BeginExceptionBlock));
			}

			AddPrefixes();
			if (config.skipOriginalLabel.HasValue)
				config.AddCodes([Ldloc[config.runOriginalVariable], Brfalse[config.skipOriginalLabel.Value]]);

			var copier = new MethodCopier(config);
			foreach (var transpiler in config.transpilers)
				copier.AddTranspiler(transpiler);
			copier.AddTranspiler(PatchTools.m_GetExecutingAssemblyReplacementTranspiler);

			var endLabels = new List<Label>();
			var replacement = copier.Finalize(true, out var hasReturnCode, out var methodEndsInDeadCode, endLabels);

			replacement = [.. AddInfixes(replacement)];

			config.AddCode(Nop["start original"]);
			config.AddCodes(this.CleanupCodes(replacement, endLabels));
			config.AddCode(Nop["end original"]);
			if (endLabels.Count > 0)
				config.AddCode(Nop.WithLabels(endLabels));
			if (config.resultVariable is not null && hasReturnCode)
				config.AddCode(Stloc[config.resultVariable]);
			if (config.skipOriginalLabel.HasValue)
				config.AddCode(Nop.WithLabels(config.skipOriginalLabel.Value));

			_ = AddPostfixes(false);
			if (config.resultVariable is not null && (hasReturnCode || (methodEndsInDeadCode && config.skipOriginalLabel.HasValue)))
				config.AddCode(Ldloc[config.resultVariable]);

			var needsToStorePassthroughResult = AddPostfixes(true);

			if (config.finalizers.Count > 0)
			{
				var exceptionVariable = config.GetLocal(InjectionType.Exception);

				if (needsToStorePassthroughResult)
				{
					config.AddCode(Stloc[config.resultVariable]);
					config.AddCode(Ldloc[config.resultVariable]);
				}

				_ = AddFinalizers(false);
				config.AddCode(Ldc_I4_1);
				config.AddCode(Stloc[config.finalizedVariable]);
				var noExceptionLabel1 = config.DefineLabel();
				config.AddCode(Ldloc[exceptionVariable]);
				config.AddCode(Brfalse[noExceptionLabel1]);
				config.AddCode(Ldloc[exceptionVariable]);
				config.AddCode(Throw);
				config.AddCode(Nop.WithLabels(noExceptionLabel1));

				// end try, begin catch
				config.AddCode(this.MarkBlock(ExceptionBlockType.BeginCatchBlock));
				config.AddCode(Stloc[exceptionVariable]);

				config.AddCode(Ldloc[config.finalizedVariable]);
				var endFinalizerLabel = config.DefineLabel();
				config.AddCode(Brtrue[endFinalizerLabel]);

				var rethrowPossible = AddFinalizers(true);

				config.AddCode(Nop.WithLabels(endFinalizerLabel));

				var noExceptionLabel2 = config.DefineLabel();
				config.AddCode(Ldloc[exceptionVariable]);
				config.AddCode(Brfalse[noExceptionLabel2]);
				if (rethrowPossible)
					config.AddCode(Rethrow);
				else
				{
					config.AddCode(Ldloc[exceptionVariable]);
					config.AddCode(Throw);
				}
				config.AddCode(Nop.WithLabels(noExceptionLabel2));

				// end catch
				config.AddCode(this.MarkBlock(ExceptionBlockType.EndExceptionBlock));

				if (config.resultVariable is not null)
					config.AddCode(Ldloc[config.resultVariable]);
			}

			if (methodEndsInDeadCode == false || config.skipOriginalLabel is not null || config.finalizers.Count > 0 || config.postfixes.Count > 0)
				config.AddCode(Ret);

			config.instructions = FaultBlockRewriter.Rewrite(config.instructions, config.il);

			if (config.debug)
			{
				var logEmitter = new Emitter(config.il);
				this.LogCodes(logEmitter, config.instructions);
			}

			var codeEmitter = new Emitter(config.il);
			this.EmitCodes(codeEmitter, config.instructions);
			var replacementMethod = config.patch.Generate();

			if (config.debug)
			{
				FileLog.LogBuffered("DONE");
				FileLog.LogBuffered("");
				FileLog.FlushBuffer();
			}

			return (replacementMethod, codeEmitter.GetInstructions());
		}

		internal void AddPrefixes()
		{
			foreach (var fix in config.prefixes)
			{
				var skipLabel = this.AffectsOriginal(fix) ? config.DefineLabel() : (Label?)null;
				if (skipLabel.HasValue)
					config.AddCodes([Ldloc[config.runOriginalVariable], Brfalse[skipLabel.Value]]);

				var tmpBoxVars = new List<KeyValuePair<LocalBuilder, Type>>();
				config.AddCodes(this.EmitCallParameter(fix, false, out var tmpInstanceBoxingVar, out var tmpObjectVar, out var refResultUsed, tmpBoxVars));
				config.AddCode(Call[fix]);
				if (MethodPatcherTools.OriginalParameters(fix).Any(pair => pair.realName == MethodPatcherTools.ARGS_ARRAY_VAR))
					config.AddCodes(this.RestoreArgumentArray());
				if (tmpInstanceBoxingVar != null)
				{
					config.AddCode(Ldarg_0);
					config.AddCode(Ldloc[tmpInstanceBoxingVar]);
					config.AddCode(Unbox_Any[config.original.DeclaringType]);
					config.AddCode(Stobj[config.original.DeclaringType]);
				}
				if (refResultUsed)
				{
					var label = config.DefineLabel();
					config.AddCode(Ldloc[config.GetLocal(InjectionType.ResultRef)]);
					config.AddCode(Brfalse_S[label]);

					config.AddCode(Ldloc[config.GetLocal(InjectionType.ResultRef)]);
					config.AddCode(Callvirt[AccessTools.Method(config.GetLocal(InjectionType.ResultRef).LocalType, "Invoke")]);
					config.AddCode(Stloc[config.GetLocal(InjectionType.Result)]);
					config.AddCode(Ldnull);
					config.AddCode(Stloc[config.GetLocal(InjectionType.ResultRef)]);

					config.AddCode(Nop.WithLabels(label));
				}
				else if (tmpObjectVar != null)
				{
					config.AddCode(Ldloc[tmpObjectVar]);
					config.AddCode(Unbox_Any[AccessTools.GetReturnedType(config.original)]);
					config.AddCode(Stloc[config.GetLocal(InjectionType.Result)]);
				}
				tmpBoxVars.Do(tmpBoxVar =>
				{
					config.AddCode(new CodeInstruction(config.OriginalIsStatic ? OpCodes.Ldarg_0 : OpCodes.Ldarg_1));
					config.AddCode(Ldloc[tmpBoxVar.Key]);
					config.AddCode(Unbox_Any[tmpBoxVar.Value]);
					config.AddCode(Stobj[tmpBoxVar.Value]);
				});

				var returnType = fix.ReturnType;
				if (returnType != typeof(void))
				{
					if (returnType != typeof(bool))
						throw new Exception($"Prefix patch {fix} has not \"bool\" or \"void\" return type: {fix.ReturnType}");
					config.AddCode(Stloc[config.runOriginalVariable]);
				}

				if (skipLabel.HasValue)
					config.AddCode(Nop.WithLabels(skipLabel.Value));
			}
		}

		internal bool AddPostfixes(bool passthroughPatches)
		{
			var result = false;
			var original = config.original;
			var originalIsStatic = original.IsStatic;
			foreach (var fix in config.postfixes.Where(fix => passthroughPatches == (fix.ReturnType != typeof(void))))
			{
				var tmpBoxVars = new List<KeyValuePair<LocalBuilder, Type>>();
				config.AddCodes(this.EmitCallParameter(fix, true, out var tmpInstanceBoxingVar, out var tmpObjectVar, out var refResultUsed, tmpBoxVars));
				config.AddCode(Call[fix]);
				if (MethodPatcherTools.OriginalParameters(fix).Any(pair => pair.realName == MethodPatcherTools.ARGS_ARRAY_VAR))
					config.AddCodes(this.RestoreArgumentArray());
				if (tmpInstanceBoxingVar != null)
				{
					config.AddCode(Ldarg_0);
					config.AddCode(Ldloc[tmpInstanceBoxingVar]);
					config.AddCode(Unbox_Any[original.DeclaringType]);
					config.AddCode(Stobj[original.DeclaringType]);
				}
				if (refResultUsed)
				{
					var label = config.DefineLabel();
					config.AddCode(Ldloc[config.GetLocal(InjectionType.ResultRef)]);
					config.AddCode(Brfalse_S[label]);

					config.AddCode(Ldloc[config.GetLocal(InjectionType.ResultRef)]);
					config.AddCode(Callvirt[AccessTools.Method(config.GetLocal(InjectionType.ResultRef).LocalType, "Invoke")]);
					config.AddCode(Stloc[config.GetLocal(InjectionType.Result)]);
					config.AddCode(Ldnull);
					config.AddCode(Stloc[config.GetLocal(InjectionType.ResultRef)]);

					config.AddCode(Nop.WithLabels(label));
				}
				else if (tmpObjectVar != null)
				{
					config.AddCode(Ldloc[tmpObjectVar]);
					config.AddCode(Unbox_Any[AccessTools.GetReturnedType(original)]);
					config.AddCode(Stloc[config.GetLocal(InjectionType.Result)]);
				}
				tmpBoxVars.Do(tmpBoxVar =>
				{
					config.AddCode(new CodeInstruction(originalIsStatic ? OpCodes.Ldarg_0 : OpCodes.Ldarg_1));
					config.AddCode(Ldloc[tmpBoxVar.Key]);
					config.AddCode(Unbox_Any[tmpBoxVar.Value]);
					config.AddCode(Stobj[tmpBoxVar.Value]);
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

		internal bool AddFinalizers(bool catchExceptions)
		{
			var rethrowPossible = true;
			var original = config.original;
			var originalIsStatic = original.IsStatic;
			config.finalizers.Do(fix =>
			{
				if (catchExceptions)
					config.AddCode(this.MarkBlock(ExceptionBlockType.BeginExceptionBlock));

				var tmpBoxVars = new List<KeyValuePair<LocalBuilder, Type>>();
				config.AddCodes(this.EmitCallParameter(fix, false, out var tmpInstanceBoxingVar, out var tmpObjectVar, out var refResultUsed, tmpBoxVars));
				config.AddCode(Call[fix]);
				if (MethodPatcherTools.OriginalParameters(fix).Any(pair => pair.realName == MethodPatcherTools.ARGS_ARRAY_VAR))
					config.AddCodes(this.RestoreArgumentArray());
				if (tmpInstanceBoxingVar != null)
				{
					config.AddCode(Ldarg_0);
					config.AddCode(Ldloc[tmpInstanceBoxingVar]);
					config.AddCode(Unbox_Any[original.DeclaringType]);
					config.AddCode(Stobj[original.DeclaringType]);
				}
				if (refResultUsed)
				{
					var label = config.DefineLabel();
					config.AddCode(Ldloc[config.GetLocal(InjectionType.ResultRef)]);
					config.AddCode(Brfalse_S[label]);

					config.AddCode(Ldloc[config.GetLocal(InjectionType.ResultRef)]);
					config.AddCode(Callvirt[AccessTools.Method(config.GetLocal(InjectionType.ResultRef).LocalType, "Invoke")]);
					config.AddCode(Stloc[config.GetLocal(InjectionType.Result)]);
					config.AddCode(Ldnull);
					config.AddCode(Stloc[config.GetLocal(InjectionType.ResultRef)]);

					config.AddCode(Nop.WithLabels(label));
				}
				else if (tmpObjectVar != null)
				{
					config.AddCode(Ldloc[tmpObjectVar]);
					config.AddCode(Unbox_Any[AccessTools.GetReturnedType(original)]);
					config.AddCode(Stloc[config.GetLocal(InjectionType.Result)]);
				}
				tmpBoxVars.Do(tmpBoxVar =>
				{
					config.AddCode(new CodeInstruction(originalIsStatic ? OpCodes.Ldarg_0 : OpCodes.Ldarg_1));
					config.AddCode(Ldloc[tmpBoxVar.Key]);
					config.AddCode(Unbox_Any[tmpBoxVar.Value]);
					config.AddCode(Stobj[tmpBoxVar.Value]);
				});

				if (fix.ReturnType != typeof(void))
				{
					config.AddCode(Stloc[config.GetLocal(InjectionType.Exception)]);
					rethrowPossible = false;
				}

				if (catchExceptions)
				{
					config.AddCode(this.MarkBlock(ExceptionBlockType.BeginCatchBlock));
					config.AddCode(Pop);
					config.AddCode(this.MarkBlock(ExceptionBlockType.EndExceptionBlock));
				}
			});

			return rethrowPossible;
		}

		IEnumerable<CodeInstruction> AddInfixes(IEnumerable<CodeInstruction> instructions)
		{
			var instructionList = instructions.ToList();
			var callOccurrences = new Dictionary<MethodInfo, int>();
			
			// First pass: count call occurrences to determine indices
			foreach (var instruction in instructionList)
			{
				if ((instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt) && 
					instruction.operand is MethodInfo method)
				{
					callOccurrences[method] = callOccurrences.GetValueOrDefault(method, 0) + 1;
				}
			}

			var replacements = new Dictionary<CodeInstruction, List<CodeInstruction>>();
			callOccurrences.Clear(); // Reset for second pass

			// Second pass: generate infix replacements
			foreach (var instruction in instructionList)
			{
				if ((instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt) && 
					instruction.operand is MethodInfo innerMethod)
				{
					var currentIndex = callOccurrences[innerMethod] = callOccurrences.GetValueOrDefault(innerMethod, 0) + 1;
					var totalOccurrences = instructionList.Count(ins => 
						(ins.opcode == OpCodes.Call || ins.opcode == OpCodes.Callvirt) && 
						ins.operand is MethodInfo m && m == innerMethod);

					// Find matching infix patches for this call site
					var prefixes = config.innerprefixes.FilterAndSort(innerMethod, currentIndex, totalOccurrences, config.debug);
					var postfixes = config.innerpostfixes.FilterAndSort(innerMethod, currentIndex, totalOccurrences, config.debug);

					if (prefixes.Length > 0 || postfixes.Length > 0)
					{
						// Generate infix replacement for this call site
						var infixBlock = GenerateInfixBlock(instruction, innerMethod, prefixes, postfixes);
						replacements[instruction] = infixBlock.ToList();
					}
				}
			}

			// Apply replacements
			return instructionList.SelectMany(instruction =>
				replacements.TryGetValue(instruction, out var replacement) ? (IEnumerable<CodeInstruction>)replacement : new[] { instruction });
		}

		List<CodeInstruction> GenerateInfixBlock(CodeInstruction originalCall, MethodInfo innerMethod, Infix[] prefixes, Infix[] postfixes)
		{
			var codes = new List<CodeInstruction>();
			var parameters = innerMethod.GetParameters();
			var hasInstance = !innerMethod.IsStatic;
			var hasResult = innerMethod.ReturnType != typeof(void);

			// Step 1: Capture stack operands into locals (in reverse order)
			var capturedLocals = new List<LocalBuilder>();
			
			// Capture arguments in reverse order (argN, ..., arg1, instance?)
			for (int i = parameters.Length - 1; i >= 0; i--)
			{
				var param = parameters[i];
				var local = config.DeclareLocal(param.ParameterType);
				capturedLocals.Insert(0, local); // Insert at beginning to maintain order
				codes.Add(Stloc[local]);
			}

			// Capture instance if not static
			LocalBuilder instanceLocal = null;
			if (hasInstance)
			{
				instanceLocal = config.DeclareLocal(innerMethod.DeclaringType);
				codes.Add(Stloc[instanceLocal]);
			}

			// Step 2: Initialize per-site locals
			var runOriginalLocal = config.DeclareLocal(typeof(bool));
			codes.Add(Ldc_I4_1); // true
			codes.Add(Stloc[runOriginalLocal]);

			LocalBuilder resultLocal = null;
			if (hasResult)
			{
				resultLocal = config.DeclareLocal(innerMethod.ReturnType);
				codes.AddRange(this.GenerateVariableInit(resultLocal, true));
			}

			// Step 3: Inner prefixes
			var afterPrefixesLabel = config.DefineLabel();
			var canSkip = prefixes.Any(p => this.AffectsOriginal(p.OuterMethod));

			foreach (var prefix in prefixes)
			{
				if (canSkip)
				{
					codes.Add(Ldloc[runOriginalLocal]);
					codes.Add(Brfalse[afterPrefixesLabel]);
				}

				// Generate parameter loading for inner prefix with inner context
				codes.AddRange(GenerateInnerPatchParameters(prefix.OuterMethod, innerMethod, instanceLocal, capturedLocals, resultLocal, runOriginalLocal));
				codes.Add(Call[prefix.OuterMethod]);

				// Handle boolean return (skip semantics)
				if (prefix.OuterMethod.ReturnType == typeof(bool))
				{
					codes.Add(Stloc[runOriginalLocal]);
				}
				else if (prefix.OuterMethod.ReturnType != typeof(void))
				{
					codes.Add(Pop); // Discard non-bool return value
				}
			}

			codes.Add(Nop.WithLabels(afterPrefixesLabel));

			// Step 4: Conditional original call
			var afterCallLabel = config.DefineLabel();
			codes.Add(Ldloc[runOriginalLocal]);
			codes.Add(Brfalse[afterCallLabel]);

			// Reload instance and arguments for original call
			if (hasInstance)
			{
				codes.Add(Ldloc[instanceLocal]);
			}
			foreach (var argLocal in capturedLocals)
			{
				codes.Add(Ldloc[argLocal]);
			}

			// Emit the original call (preserving labels/blocks from original instruction)
			codes.Add(originalCall.Clone());

			// Store result if non-void
			if (hasResult)
			{
				codes.Add(Stloc[resultLocal]);
			}

			codes.Add(Nop.WithLabels(afterCallLabel));

			// Step 5: Inner postfixes
			foreach (var postfix in postfixes)
			{
				codes.AddRange(GenerateInnerPatchParameters(postfix.OuterMethod, innerMethod, instanceLocal, capturedLocals, resultLocal, runOriginalLocal));
				codes.Add(Call[postfix.OuterMethod]);

				// Handle result passthrough
				if (hasResult && postfix.OuterMethod.ReturnType == innerMethod.ReturnType)
				{
					var firstParam = postfix.OuterMethod.GetParameters().FirstOrDefault();
					if (firstParam?.ParameterType == innerMethod.ReturnType)
					{
						codes.Add(Stloc[resultLocal]);
					}
				}
				else if (postfix.OuterMethod.ReturnType != typeof(void))
				{
					codes.Add(Pop); // Discard return value
				}
			}

			// Step 6: Restore stack effect
			if (hasResult)
			{
				codes.Add(Ldloc[resultLocal]);
			}

			return codes;
		}

		List<CodeInstruction> GenerateInnerPatchParameters(MethodInfo patchMethod, MethodInfo innerMethod, 
			LocalBuilder instanceLocal, List<LocalBuilder> argumentLocals, LocalBuilder resultLocal, LocalBuilder runOriginalLocal)
		{
			var codes = new List<CodeInstruction>();
			var patchParams = patchMethod.GetParameters();
			var innerParams = innerMethod.GetParameters();
			
			foreach (var patchParam in patchParams)
			{
				var paramType = patchParam.ParameterType;
				var paramName = patchParam.Name;
				
				// Handle special injection parameters
				if (paramName == "__instance")
				{
					if (instanceLocal != null)
						codes.Add(paramType.IsByRef ? Ldloca[instanceLocal] : Ldloc[instanceLocal]);
					else
						codes.Add(Ldnull);
					continue;
				}

				if (paramName == "__result")
				{
					if (resultLocal != null)
						codes.Add(paramType.IsByRef ? Ldloca[resultLocal] : Ldloc[resultLocal]);
					else
						codes.Add(Ldnull);
					continue;
				}

				if (paramName == "__runOriginal")
				{
					codes.Add(paramType.IsByRef ? Ldloca[runOriginalLocal] : Ldloc[runOriginalLocal]);
					continue;
				}

				// Handle outer context parameters (o_ prefix)
				if (paramName.StartsWith("o_"))
				{
					// For now, throw an exception - this needs more complex implementation
					throw new NotImplementedException($"Outer context parameter '{paramName}' not yet implemented for infix patches");
				}

				// Handle synthetic locals (__var_ prefix)
				if (paramName.StartsWith("__var_"))
				{
					throw new NotImplementedException($"Synthetic local parameter '{paramName}' not yet implemented for infix patches");
				}

				// Try to match inner method parameter by name
				var paramIndex = -1;
				for (int i = 0; i < innerParams.Length; i++)
				{
					if (innerParams[i].Name == paramName)
					{
						paramIndex = i;
						break;
					}
				}
				
				if (paramIndex >= 0 && paramIndex < argumentLocals.Count)
				{
					var argLocal = argumentLocals[paramIndex];
					codes.Add(paramType.IsByRef ? Ldloca[argLocal] : Ldloc[argLocal]);
				}
				else
				{
					throw new Exception($"Cannot resolve parameter '{paramName}' for infix patch {patchMethod.FullDescription()}");
				}
			}

			return codes;
		}
	}
}
