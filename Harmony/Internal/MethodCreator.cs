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
			config.bindingContext = new PatchBindingContext(config.original, config.localVariables);

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
				config.AddCodes(this.PrepareArgumentArray(config.bindingContext));
				config.AddCode(Stloc[argsArrayVariable]);
			}
			else if (config.AnyInfixHasOuter(InjectionType.ArgsArray))
				config.AddCodes(this.InitializeOutArguments(config.bindingContext));

			config.skipOriginalLabel = null;
			var prefixAffectsOriginal = config.prefixes.Any(this.AffectsOriginal);
			var anyFixHasRunOriginal = config.AnyFixHas(InjectionType.RunOriginal);
			if (prefixAffectsOriginal || anyFixHasRunOriginal)
			{
				config.runOriginalVariable = config.DeclareLocal(typeof(bool));
				config.AddLocal(InjectionType.RunOriginal, config.runOriginalVariable);
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
				foreach (var injection in config.OuterInjectionsFor(fix, InjectionType.State))
				{
					var parameterType = injection.parameterInfo.ParameterType;
					var type = parameterType.IsByRef ? parameterType.GetElementType() : parameterType;
					if (maybeLocal != null)
					{
						if (injection.outer ? type != maybeLocal.LocalType : !type.IsAssignableFrom(maybeLocal.LocalType))
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
					maybeLocal = privateStateVariable;
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

			if (config.debug)
			{
				var logEmitter = new Emitter(config.il);
				this.LogCodes(logEmitter, config.instructions);
			}

			var codeEmitter = new Emitter(config.il);
			this.EmitCodes(codeEmitter, config.instructions);
			var replacementMethod = config.GenerateMethod();

			if (config.debug)
			{
				FileLog.LogBuffered("DONE");
				FileLog.LogBuffered("");
				FileLog.FlushBuffer();
			}

			return (replacementMethod, codeEmitter.GetInstructions());
		}

		internal void AddPrefixes() => config.AddCodes(EmitPrefixes(config.prefixes, config.bindingContext));

		internal List<CodeInstruction> EmitPrefixes(IEnumerable<MethodInfo> prefixes, PatchBindingContext context, PatchBindingContext outerContext = null)
		{
			var codes = new List<CodeInstruction>();
			foreach (var fix in prefixes)
			{
				var skipLabel = this.AffectsOriginal(fix, outerContext != null) ? config.DefineLabel() : (Label?)null;
				if (skipLabel.HasValue)
					codes.AddRange([Ldloc[context.variables[InjectionType.RunOriginal]], Brfalse[skipLabel.Value]]);

				codes.AddRange(this.EmitPatchCall(fix, context, false, outerContext));

				var returnType = fix.ReturnType;
				if (returnType != typeof(void))
				{
					if (returnType != typeof(bool))
						throw new Exception($"Prefix patch {fix} has not \"bool\" or \"void\" return type: {fix.ReturnType}");
					codes.Add(Stloc[context.variables[InjectionType.RunOriginal]]);
				}

				if (skipLabel.HasValue)
					codes.Add(Nop.WithLabels(skipLabel.Value));
			}
			return codes;
		}

		internal bool AddPostfixes(bool passthroughPatches)
		{
			config.AddCodes(EmitPostfixes(config.postfixes, config.bindingContext, passthroughPatches));
			return passthroughPatches && config.postfixes.Any(fix => fix.ReturnType != typeof(void));
		}

		internal List<CodeInstruction> EmitPostfixes(IEnumerable<MethodInfo> postfixes, PatchBindingContext context, bool passthroughPatches, PatchBindingContext outerContext = null)
		{
			var codes = new List<CodeInstruction>();
			foreach (var fix in postfixes.Where(fix => passthroughPatches == (fix.ReturnType != typeof(void))))
			{
				if (outerContext != null && passthroughPatches && (fix.ReturnType != context.returnType
					|| fix.GetParameters().FirstOrDefault()?.ParameterType != context.returnType))
					throw new ArgumentException($"Infix passthrough postfix {fix.FullDescription()} must return and take a first parameter of exactly {context.returnType.FullDescription()}, "
						+ $"the result type of {context.Description}; actual return is {fix.ReturnType.FullDescription()} and first parameter is {fix.GetParameters().FirstOrDefault()?.ParameterType.FullDescription() ?? "missing"}.");
				codes.AddRange(this.EmitPatchCall(fix, context, true, outerContext));

				if (fix.ReturnType != typeof(void))
				{
					var firstFixParam = fix.GetParameters().FirstOrDefault();
					var hasPassThroughResultParam = firstFixParam is not null && fix.ReturnType == firstFixParam.ParameterType;
					if (!hasPassThroughResultParam)
					{
						if (firstFixParam is not null)
							throw new Exception($"Return type of pass through postfix {fix} does not match type of its first parameter");

						throw new Exception($"Postfix patch {fix} must have a \"void\" return type");
					}
				}
			}
			return codes;
		}

		internal bool AddFinalizers(bool catchExceptions)
		{
			var rethrowPossible = true;
			config.finalizers.Do(fix =>
			{
				if (catchExceptions)
					config.AddCode(this.MarkBlock(ExceptionBlockType.BeginExceptionBlock));

				config.AddCodes(this.EmitPatchCall(fix, config.bindingContext, false));

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
			=> Infix.Rewrite(this, instructions);
	}
}
