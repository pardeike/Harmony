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

		internal MethodCreator(MethodCreatorConfig config, bool prepare = true)
		{
			if (config.original is null)
				throw new ArgumentNullException("config.original");
			this.config = config;
			if (config.debug)
			{
				FileLog.LogBuffered($"### Patch: {config.original.FullDescription()}");
				FileLog.FlushBuffer();
			}
			if (prepare && config.Prepare() == false)
				throw new Exception("Could not create replacement method");
		}

		internal (MethodInfo, Dictionary<int, CodeInstruction>) CreateReplacement()
		{
			config.originalBody = MethodBodyPatching.GetMethodBody(config.MethodBase);
			config.originalVariables = MethodPatcherTools.DeclareOriginalLocalVariables(config.il, config.originalBody);
			config.localVariables = new VariableState();
			config.bindingContext = new PatchBindingContext(config.original, config.localVariables);
			config.bindingContext.originalLocals = [.. config.originalVariables.Select(local => new InjectionStorage(local))];
			config.persistence = PersistentStatePlan.Create(config);

			foreach (var (instantiation, helper, _) in config.closedBodies)
				config.AddCodes(EmitClosedBody(instantiation, helper));

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
			var prefixAffectsOriginal = config.prefixes.Any(call => this.AffectsOriginal(call.method));
			var anyFixHasRunOriginal = config.AnyFixHas(InjectionType.RunOriginal);
			if (prefixAffectsOriginal || anyFixHasRunOriginal)
			{
				config.runOriginalVariable = config.DeclareLocal(typeof(bool));
				config.AddLocal(InjectionType.RunOriginal, config.runOriginalVariable);
				config.AddCodes([Ldc_I4_1, Stloc[config.runOriginalVariable]]);
				if (prefixAffectsOriginal)
					config.skipOriginalLabel = config.DefineLabel();
			}

			foreach (var (fix, instantiation) in config.StateOwners)
			{
				var declaringType = fix.DeclaringType;
				if (declaringType is null)
					continue;
				var varName = MethodCreatorConfig.StateKey(fix, instantiation);
				var hasLocal = config.localVariables.TryGetValue(varName, out var maybeLocal);
				foreach (var injection in config.OuterInjectionsFor(fix, InjectionType.State))
				{
					var parameterType = injection.parameterInfo.ParameterType;
					var type = parameterType.IsByRef ? parameterType.GetElementType() : parameterType;
					if (hasLocal)
					{
						if (injection.outer ? type != maybeLocal.type : !type.IsAssignableFrom(maybeLocal.type))
						{
							var message = $"__state type mismatch in patch \"{fix.DeclaringType.FullName}.{fix.Name}\": " +
								$"previous __state was declared as \"{maybeLocal.type.FullName}\" but this patch expects \"{type.FullName}\"; " +
								$"original {config.original.FullDescription()}, parameter {injection.parameterInfo.Name}, supplied {maybeLocal.type.FullDescription()}, requested {parameterType.FullDescription()}";
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
					maybeLocal = new InjectionStorage(privateStateVariable);
					hasLocal = true;
				}
			}

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
			if (config.persistence is not null) replacement = config.persistence.RewriteOperations(replacement);

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
				if (needsToStorePassthroughResult)
				{
					config.AddCode(Stloc[config.resultVariable]);
					config.AddCode(Ldloc[config.resultVariable]);
				}

				config.AddCodes(EmitFinalization(config.finalizers, config.bindingContext, config.finalizedVariable));

				if (config.resultVariable is not null)
					config.AddCode(Ldloc[config.resultVariable]);
			}

			if (methodEndsInDeadCode == false || config.skipOriginalLabel is not null || config.finalizers.Count > 0 || config.postfixes.Count > 0)
				config.AddCode(Ret);

			config.persistence?.ProtectLifetime(this);

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

		// An instantiation with its own closed body leaves the family body at its start. Its arguments and result bridge the definition's open types.
		List<CodeInstruction> EmitClosedBody(MethodBase instantiation, MethodInfo helper)
		{
			var skip = config.DefineLabel();
			var codes = new List<CodeInstruction>(GenericFamily.Guard(config.original, instantiation, skip));
			var open = MethodPatcherTools.ReplacementParameterTypes(config.original);
			var closed = helper.GetParameters().Types();
			for (var i = 0; i < open.Count; i++)
			{
				codes.Add(Ldarg[i]);
				if (open[i] != closed[i] && !closed[i].IsByRef && !closed[i].IsPointer) codes.Add(GenericFamily.Convert(open[i], closed[i]));
			}
			codes.Add(Call[helper]);
			if (helper.ReturnType != config.returnType && !config.returnType.IsByRef && !config.returnType.IsPointer)
				codes.Add(GenericFamily.Convert(helper.ReturnType, config.returnType));
			codes.AddRange([Ret, Nop.WithLabels(skip)]);
			return codes;
		}

		internal void AddPrefixes() => config.AddCodes(EmitPrefixes(config.prefixes, config.bindingContext));

		internal List<CodeInstruction> EmitPrefixes(IEnumerable<PatchCall> prefixes, PatchBindingContext context, PatchBindingContext outerContext = null)
		{
			var codes = new List<CodeInstruction>();
			foreach (var call in prefixes)
			{
				var fix = call.method;
				var returnType = fix.ReturnType;
				if (returnType != typeof(void) && returnType != typeof(bool))
					throw this.InvalidBinding(fix, context, "prefix return value", returnType, typeof(bool), "Prefixes must return bool or void");
				var guard = BeginGuard(call, codes);
				var skipLabel = this.AffectsOriginal(fix, outerContext != null) ? config.DefineLabel() : (Label?)null;
				if (skipLabel.HasValue)
					codes.AddRange([Ldloc[context.variables[InjectionType.RunOriginal]], Brfalse[skipLabel.Value]]);

				codes.AddRange(this.EmitPatchCall(fix, context, false, outerContext, call.UncheckedReferences, call.instantiation));

				if (returnType != typeof(void))
				{
					codes.Add(Stloc[context.variables[InjectionType.RunOriginal]]);
				}

				if (skipLabel.HasValue)
					codes.Add(Nop.WithLabels(skipLabel.Value));
				EndGuard(guard, codes);
			}
			return codes;
		}

		// A callback registered on a closed instantiation runs only when the definition's type parameters match it.
		Label? BeginGuard(PatchCall call, List<CodeInstruction> codes)
		{
			if (call.instantiation is null) return null;
			var skip = config.DefineLabel();
			codes.AddRange(GenericFamily.Guard(config.original, call.instantiation, skip));
			return skip;
		}

		static void EndGuard(Label? skip, List<CodeInstruction> codes)
		{
			if (skip.HasValue) codes.Add(Nop.WithLabels(skip.Value));
		}

		internal bool AddPostfixes(bool passthroughPatches)
		{
			config.AddCodes(EmitPostfixes(config.postfixes, config.bindingContext, passthroughPatches));
			return passthroughPatches && config.postfixes.Any(call => call.method.ReturnType != typeof(void));
		}

		internal List<CodeInstruction> EmitPostfixes(IEnumerable<PatchCall> postfixes, PatchBindingContext context, bool passthroughPatches, PatchBindingContext outerContext = null)
		{
			var codes = new List<CodeInstruction>();
			var previousResultType = context.returnType;
			foreach (var call in postfixes.Where(call => passthroughPatches == (call.method.ReturnType != typeof(void))))
			{
				var fix = call.method;
				if (outerContext != null && passthroughPatches && (fix.ReturnType != context.returnType
					|| fix.GetParameters().FirstOrDefault()?.ParameterType != context.returnType))
					throw new ArgumentException($"Infix passthrough postfix {fix.FullDescription()} must return and take a first parameter of exactly {context.returnType.FullDescription()}, "
						+ $"the result type of {context.Description}; actual return is {fix.ReturnType.FullDescription()} and first parameter is {fix.GetParameters().FirstOrDefault()?.ParameterType.FullDescription() ?? "missing"}.");
				var guard = BeginGuard(call, codes);
				var bridged = call.instantiation is not null && !context.returnType.IsByRef;
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
					// New connections are checked even between survivors. Only the consumer can opt out of its input binding.
					var pairUnchecked = call.uncheckedReferenceBinding || call.unchangedPassthrough;
					this.ValidateReturnBinding(fix, context, previousResultType, firstFixParam.ParameterType, $"passthrough parameter {firstFixParam.Name}, previous return value", pairUnchecked);
					this.ValidateReturnBinding(fix, context, fix.ReturnType, context.returnType, uncheckedReferenceBinding: call.UncheckedReferences);
					// A passthrough on an instantiation takes and returns its closed result type. The stack keeps the definition's open type.
					if (bridged && firstFixParam.ParameterType != context.returnType)
						codes.Add(GenericFamily.Convert(context.returnType, firstFixParam.ParameterType));
					previousResultType = bridged ? context.returnType : fix.ReturnType;
				}
				codes.AddRange(this.EmitPatchCall(fix, context, true, outerContext, call.UncheckedReferences, call.instantiation));
				if (bridged && fix.ReturnType != typeof(void) && fix.ReturnType != context.returnType)
					codes.Add(GenericFamily.Convert(fix.ReturnType, context.returnType));
				EndGuard(guard, codes);
			}
			return codes;
		}

		internal List<CodeInstruction> EmitFinalization(IList<PatchCall> finalizers, PatchBindingContext context,
			LocalBuilder finalized, PatchBindingContext outerContext = null)
		{
			var exception = context.variables[InjectionType.Exception];
			var noException = config.DefineLabel();
			var endFinalizers = config.DefineLabel();
			var suppressed = config.DefineLabel();
			var (codes, _) = EmitFinalizers(finalizers, context, false, outerContext);
			codes.AddRange([Ldc_I4_1, Stloc[finalized], Ldloc[exception], Brfalse[noException], Ldloc[exception], Throw,
				Nop.WithLabels(noException), this.MarkBlock(ExceptionBlockType.BeginCatchBlock), Stloc[exception],
				Ldloc[finalized], Brtrue[endFinalizers]]);
			var (catchCodes, rethrowPossible) = EmitFinalizers(finalizers, context, true, outerContext);
			codes.AddRange(catchCodes);
			codes.AddRange([Nop.WithLabels(endFinalizers), Ldloc[exception], Brfalse[suppressed]]);
			if (rethrowPossible) codes.Add(Rethrow);
			else codes.AddRange([Ldloc[exception], Throw]);
			codes.AddRange([Nop.WithLabels(suppressed), this.MarkBlock(ExceptionBlockType.EndExceptionBlock)]);
			return codes;
		}

		internal (List<CodeInstruction> codes, bool rethrowPossible) EmitFinalizers(IEnumerable<PatchCall> finalizers,
			PatchBindingContext context, bool catchExceptions, PatchBindingContext outerContext = null)
		{
			var codes = new List<CodeInstruction>();
			var rethrowPossible = true;
			foreach (var call in finalizers)
			{
				var fix = call.method;
				if (outerContext != null && fix.ReturnType != typeof(void) && !typeof(Exception).IsAssignableFrom(fix.ReturnType))
					throw new ArgumentException($"Infix finalizer {fix.FullDescription()} must return void or an Exception.");
				if (fix.ReturnType != typeof(void)) this.ValidateReturnBinding(fix, context, fix.ReturnType, typeof(Exception), uncheckedReferenceBinding: call.UncheckedReferences);
				var guard = BeginGuard(call, codes);
				if (catchExceptions)
					codes.Add(this.MarkBlock(ExceptionBlockType.BeginExceptionBlock));

				codes.AddRange(this.EmitPatchCall(fix, context, false, outerContext, call.UncheckedReferences, call.instantiation));

				if (fix.ReturnType != typeof(void))
				{
					codes.Add(Stloc[context.variables[InjectionType.Exception]]);
					rethrowPossible = false;
				}

				if (catchExceptions)
				{
					codes.Add(this.MarkBlock(ExceptionBlockType.BeginCatchBlock));
					codes.Add(Pop);
					codes.Add(this.MarkBlock(ExceptionBlockType.EndExceptionBlock));
				}
				EndGuard(guard, codes);
			}

			return (codes, rethrowPossible);
		}

		IEnumerable<CodeInstruction> AddInfixes(IEnumerable<CodeInstruction> instructions)
			=> Infix.Rewrite(this, instructions);
	}
}
