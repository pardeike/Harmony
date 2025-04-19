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
			if (config.original is null) // TODO move this to the caller of MethodCreator()
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
				if (declaringType is null) return;
				var varName = declaringType.AssemblyQualifiedName;
				if (config.HasLocal(varName)) return;
				foreach (var injection in config.InjectionsFor(fix, InjectionType.State))
				{
					var privateStateVariable = config.DeclareLocal(injection.parameterInfo.ParameterType);
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
			var replacement = copier.Finalize(out var hasReturnCode, out var methodEndsInDeadCode, endLabels);
			config.AddCode(Nop["start original"]);
			config.AddCodes(this.CleanupCodes(replacement, endLabels));
			config.AddCode(Nop["end original" + (methodEndsInDeadCode ? " (has dead code end)" : "")]);
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

			var logEmitter = new Emitter(config.il);
			this.LogCodes(new Emitter(config.il), config.instructions);

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
	}
}
