using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

using static HarmonyLib.Code;

namespace HarmonyLib
{
	internal class Infix
	{
		internal Patch patch;

		internal Infix(Patch patch) => this.patch = patch;

		internal MethodInfo OuterMethod => patch.PatchMethod;
		internal MethodBase InnerMethod => patch.Target.Member as MethodBase;
		internal int[] Positions => patch.Target.Positions; // multiple 1-based positions, or empty array for all positions

		internal bool Matches(MethodBase method, int index, int total) // index is 1-based
		{
			if (method is not MethodInfo operand || !patch.Target.Matches(new CodeInstruction(OpCodes.Call, operand))) return false;
			if (Positions.Length == 0) return true;
			foreach (var pos in Positions)
			{
				if (pos > 0 && pos == index) return true;
				if (pos < 0 && index == (long)total + pos + 1) return true;
			}
			return false;
		}

		internal static HashSet<int> ResolvePositions(int count, int[] positions)
		{
			if (positions is null) throw new ArgumentNullException(nameof(positions));
			if (count == 0) throw new ArgumentException("The inner target has no matching operation after transpilers.");
			if (positions.Length == 0) return new HashSet<int>(Enumerable.Range(0, count));
			var selected = new HashSet<int>();
			foreach (var position in positions)
			{
				var index = position > 0 ? (long)position - 1 : (long)count + position;
				if (position == 0 || index < 0 || index >= count)
					throw new ArgumentException($"Position {position} does not select one of the {count} matching inner calls. Positions start at 1; -1 is the last call.");
				_ = selected.Add((int)index);
			}
			return selected;
		}

		internal static IEnumerable<CodeInstruction> Rewrite(MethodCreator creator, IEnumerable<CodeInstruction> source)
		{
			var config = creator.config;
			if (!config.InnerFixes.Any()) return source;
			// Instruction identity is its position, even when a transpiler reuses the same object.
			var instructions = source.Select(instruction => new CodeInstruction(instruction)).ToList();
			var sites = new Dictionary<int, (List<Patch> prefixes, List<Patch> postfixes, List<Patch> finalizers)>();
			Collect(config.innerprefixes, HarmonyPatchType.InnerPrefix);
			Collect(config.innerpostfixes, HarmonyPatchType.InnerPostfix);
			Collect(config.innerfinalizers, HarmonyPatchType.InnerFinalizer);
			var branches = new HashSet<Label>(instructions.SelectMany(instruction => instruction.operand is Label label
				? [label] : instruction.operand as Label[] ?? []));
			var replacements = new Dictionary<int, (int end, List<CodeInstruction> codes)>();
			foreach (var pair in sites.OrderBy(pair => pair.Key))
			{
				var index = pair.Key;
				var start = index;
				while (start > 0 && instructions[start - 1].opcode.OpCodeType == OpCodeType.Prefix) start--;
				try
				{
					config.persistence?.ValidateSite(instructions[index]);
					if (start < index && (instructions.Skip(start + 1).Take(index - start).Any(instruction => instruction.labels.Any(branches.Contains))
						|| instructions.Skip(start + 1).Take(index - start).Any(instruction => instruction.blocks.Any(block => block.blockType != ExceptionBlockType.EndExceptionBlock))
						|| instructions[start].blocks.Any(block => block.blockType == ExceptionBlockType.EndExceptionBlock)))
						throw new ArgumentException("A branch or exception boundary enters the call without its preceding call prefix.");
					var prefixes = Sort(pair.Value.prefixes);
					var postfixes = Sort(pair.Value.postfixes);
					var finalizers = Sort(pair.Value.finalizers);
					var codes = finalizers.Count == 0 ? EmitSite(creator, instructions, start, index, prefixes, postfixes, finalizers)
						: EmitHelperSite(creator, instructions, start, index, prefixes, postfixes, finalizers);
					codes[0].labels.AddRange(instructions[start].labels);
					if (start != index) codes[0].labels.AddRange(instructions[index].labels);
					for (var i = start; i <= index; i++)
						foreach (var block in instructions[i].blocks)
							(block.blockType == ExceptionBlockType.EndExceptionBlock ? codes[codes.Count - 1] : codes[0]).blocks.Add(block);
					replacements.Add(start, (index, codes));
				}
				catch (Exception ex)
				{
					var records = pair.Value.prefixes.Concat(pair.Value.postfixes).Concat(pair.Value.finalizers);
					throw new HarmonyException($"Cannot wrap inner call {instructions[index].operand} at instruction {index} in {MethodCreatorTools.InfixMethodIdentity(config.original)} "
						+ $"for {string.Join(", ", records.Select(patch => patch.owner + ":" + MethodCreatorTools.InfixMethodIdentity(patch.PatchMethod)).ToArray())}: {ex.Message}", ex);
				}
			}
			var result = new List<CodeInstruction>();
			for (var i = 0; i < instructions.Count; i++)
				if (replacements.TryGetValue(i, out var replacement))
				{
					result.AddRange(replacement.codes);
					i = replacement.end;
				}
				else result.Add(instructions[i]);
			return result;

			List<MethodInfo> Sort(List<Patch> patches) => [.. new PatchSorter([.. patches], config.debug, true).Sort().Select(patch => patch.PatchMethod)];

			void Collect(List<Infix> fixes, HarmonyPatchType role)
			{
				foreach (var fix in fixes)
				{
					var description = "unresolved target";
					var positions = "unknown";
					int? matchCount = null;
					try
					{
						var target = fix.patch.Target ?? throw new ArgumentException("The inner patch has no target. Remove it before rebuilding this method.");
						positions = target.Positions is null ? "null" : string.Join(", ", target.Positions.Select(position => position.ToString()).ToArray());
						target.Validate();
						description = target.Kind == InnerTargetKind.Constant
							? $"Constant {target.ConstantType} \"{target.ConstantData.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n")}\""
							: $"{target.Kind} " + (target.Member is MethodBase method ? MethodCreatorTools.InfixMethodIdentity(method)
								: $"{target.Member.DeclaringType?.FullName}::{target.Member.Name}");
						var matches = Enumerable.Range(0, instructions.Count).Where(index => target.Matches(instructions[index])).ToList();
						matchCount = matches.Count;
						foreach (var position in ResolvePositions(matches.Count, fix.Positions))
						{
							var index = matches[position];
							if (!sites.TryGetValue(index, out var site)) sites.Add(index, site = ([], [], []));
							(role == HarmonyPatchType.InnerPrefix ? site.prefixes : role == HarmonyPatchType.InnerPostfix ? site.postfixes : site.finalizers).Add(fix.patch);
						}
					}
					catch (Exception ex)
					{
						throw new HarmonyException($"Cannot select {description}, positions [{positions}], in {MethodCreatorTools.InfixMethodIdentity(config.original)}"
							+ $" after ordinary transpilers for {fix.patch.owner}:{MethodCreatorTools.InfixMethodIdentity(fix.OuterMethod)}."
							+ (matchCount.HasValue ? $" Found {matchCount.Value} matching operations. " : " ") + ex.Message, ex);
					}
				}
			}
		}

		static List<CodeInstruction> EmitSite(MethodCreator creator, List<CodeInstruction> instructions, int start, int index,
			List<MethodInfo> prefixes, List<MethodInfo> postfixes, List<MethodInfo> finalizers,
			PatchBindingContext context = null, PatchBindingContext outer = null)
		{
			var config = creator.config;
			var fixes = prefixes.Concat(postfixes).Concat(finalizers).Distinct().ToList();
			foreach (var fix in fixes) MethodCreatorTools.ValidateInfixSignature(fix, "patch");
			var inputsOnStack = context is null;
			context ??= CreateContext(config, instructions, start, index);
			var returnType = context.returnType;
			var arguments = context.arguments;
			var receiver = context.receiver;
			var variables = context.variables;
			outer ??= new PatchBindingContext(config.original, new VariableState(config.localVariables))
			{
				originalLocals = config.bindingContext.originalLocals
			};
			var codes = new List<CodeInstruction> { Nop["start inner call"] };
			if (inputsOnStack)
			{
				for (var i = arguments.Length - 1; i >= 0; i--) codes.Add(arguments[i].Store());
				if (receiver.HasValue) codes.Add(receiver.Value.Store());
			}

			IEnumerable<InjectedParameter> Injections(MethodInfo fix, InjectionType type)
				=> config.InjectionsFor(fix, type, fix.ReturnType != typeof(void) && postfixes.Contains(fix)
					&& !prefixes.Contains(fix) && !finalizers.Contains(fix));
			bool Has(InjectionType injectionType) => fixes.Any(fix => Injections(fix, injectionType).Any(injection => !injection.outer));
			var canSkip = prefixes.Any(fix => fix.ReturnType == typeof(bool));
			LocalBuilder result = null;
			if (returnType != typeof(void))
			{
				result = config.DeclareLocal(returnType);
				variables.Add(InjectionType.Result, result);
				var needsDefault = canSkip || finalizers.Any(fix => fix.ReturnType != typeof(void))
					|| prefixes.Concat(finalizers).Any(fix => config.InjectionsFor(fix).Any(injection => !injection.outer
					&& (injection.injectionType == InjectionType.Result || injection.injectionType == InjectionType.ResultRef)));
				if (needsDefault)
				{
					var elementType = returnType.GetElementType();
					if (returnType.IsByRef && !MethodCreatorTools.CanStoreInObjectArray(elementType))
						throw new ArgumentException($"Skipping or exposing a pre-call result requires a default reference, which cannot hold {elementType}.");
					codes.AddRange(creator.GenerateVariableInit(result, true));
				}
			}
			if (Has(InjectionType.ResultRef) && returnType.IsByRef)
			{
				var resultRef = config.DeclareLocal(typeof(RefResult<>).MakeGenericType(returnType.GetElementType()));
				variables.Add(InjectionType.ResultRef, resultRef);
				codes.AddRange([Ldnull, Stloc[resultRef]]);
			}
			LocalBuilder run = null;
			if (prefixes.Any(fix => creator.AffectsOriginal(fix, true)) || Has(InjectionType.RunOriginal))
			{
				run = config.DeclareLocal(typeof(bool));
				variables.Add(InjectionType.RunOriginal, run);
				codes.AddRange([Ldc_I4_1, Stloc[run]]);
			}
			foreach (var fix in fixes)
				foreach (var injection in Injections(fix, InjectionType.State).Where(injection => !injection.outer))
				{
					var name = fix.DeclaringType;
					var type = injection.parameterInfo.ParameterType;
					if (type.IsByRef) type = type.GetElementType();
					if (variables.TryGetValue(name, out var state))
					{
						if (state.type != type) throw new ArgumentException($"Inner __state for {fix.DeclaringType} has conflicting types {state.type} and {type}.");
						continue;
					}
					var stateLocal = config.DeclareLocal(type);
					variables.Add(name, stateLocal);
					codes.AddRange(creator.GenerateVariableInit(stateLocal));
				}
			LocalBuilder finalized = null;
			if (finalizers.Count != 0)
			{
				finalized = config.DeclareLocal(typeof(bool));
				var exception = config.DeclareLocal(typeof(Exception));
				variables.Add(InjectionType.Exception, exception);
				codes.AddRange([Ldc_I4_0, Stloc[finalized], Ldnull, Stloc[exception]]);
			}
			codes.AddRange(creator.SetupInfixBindings(context, outer, prefixes, postfixes, finalizers));
			if (finalized != null) codes.Add(creator.MarkBlock(ExceptionBlockType.BeginExceptionBlock));
			codes.AddRange(creator.EmitPrefixes(prefixes, context, outer));
			var afterCall = config.DefineLabel();
			if (canSkip) codes.AddRange([Ldloc[run], Brfalse[afterCall]]);
			if (receiver.HasValue) codes.Add(receiver.Value.Load());
			codes.AddRange(arguments.Select(argument => argument.Load()));
			for (var i = start; i <= index; i++) codes.Add(instructions[i].Clone());
			if (result is not null) codes.Add(Stloc[result]);
			codes.Add(Nop.WithLabels(afterCall));
			codes.AddRange(creator.EmitPostfixes(postfixes, context, false, outer));
			if (result is not null) codes.Add(Ldloc[result]);
			else if (postfixes.Any(fix => fix.ReturnType != typeof(void))) throw new ArgumentException("A void inner call cannot have a passthrough postfix.");
			codes.AddRange(creator.EmitPostfixes(postfixes, context, true, outer));
			if (finalized != null)
			{
				// Match ordinary Harmony's phase-wide passthrough result commit before finalization.
				if (result != null) codes.Add(Stloc[result]);
				codes.AddRange(creator.EmitFinalization(finalizers, context, finalized, outer));
				if (result != null) codes.Add(Ldloc[result]);
			}
			codes.Add(Nop["end inner call"]);
			return codes;
		}

		static List<CodeInstruction> EmitHelperSite(MethodCreator creator, List<CodeInstruction> instructions, int start, int index,
			List<MethodInfo> prefixes, List<MethodInfo> postfixes, List<MethodInfo> finalizers)
		{
			var parent = creator.config;
			var inner = CreateContext(parent, instructions, start, index, true);
			var outer = new PatchBindingContext(parent.original, new VariableState(parent.localVariables))
			{
				originalLocals = parent.bindingContext.originalLocals
			};
			creator.PrepareInfixOuterLocals(prefixes.Concat(finalizers), outer);
			creator.PrepareInfixOuterLocals(postfixes, outer, true);
			var helperConfig = new MethodCreatorConfig(parent, parent.patch.Definition.Name + "_Infix" + index, inner.returnType);
			var helper = new MethodCreator(helperConfig, false);
			var parameterTypes = new List<Type>();
			if (inner.receiver is InjectionStorage receiver) parameterTypes.Add(receiver.type);
			parameterTypes.AddRange(inner.parameters.Select(parameter => parameter.ParameterType));
			var callerLoads = new List<CodeInstruction>();
			var variables = new VariableState();
			foreach (var pair in outer.variables.Named) variables[pair.Key] = Transport(pair.Value);
			var transported = new PatchBindingContext(outer.member, outer.returnType, outer.parameters, outer.receiverType,
				outer.receiver is InjectionStorage originalReceiver ? Transport(originalReceiver) : null,
				[.. outer.arguments.Select(Transport)], variables)
			{
				receiverParameterType = outer.receiverParameterType,
				originalLocals = [.. outer.originalLocals.Select(Transport)]
			};
			var codes = EmitSite(helper, instructions, start, index, prefixes, postfixes, finalizers, inner, transported);
			codes.Add(Ret);
			foreach (var type in parameterTypes)
				helperConfig.patch.Definition.Parameters.Add(new Mono.Cecil.ParameterDefinition(helperConfig.patch.Definition.Module.ImportReference(type)));
			if (parent.debug) helper.LogCodes(new Emitter(helperConfig.il), codes);
			helper.EmitCodes(new Emitter(helperConfig.il), codes);
			var method = helperConfig.GenerateMethod(structuredHelper: true);
			return [Nop["start inner helper"], .. callerLoads, Call[method], Nop["end inner helper"]];

			InjectionStorage Transport(InjectionStorage storage)
			{
				var type = storage.type.IsByRef ? storage.type : storage.type.MakeByRefType();
				InjectionStorage? parameter = null;
				return new InjectionStorage(type, () =>
				{
					if (parameter is null)
					{
						parameter = new InjectionStorage(type, parameterTypes.Count);
						parameterTypes.Add(type);
						callerLoads.Add(storage.type.IsByRef ? storage.Load() : storage.LoadAddress());
					}
					return parameter.Value;
				});
			}
		}

		// Describe the operands of the original instruction, then use the same binder and scheduling for every kind.
		static PatchBindingContext CreateContext(MethodCreatorConfig config, List<CodeInstruction> instructions, int start, int index, bool parameterStorage = false)
		{
			var instruction = instructions[index];
			var member = instruction.operand as MemberInfo;
			Type returnType;
			Type receiverStorage = null;
			var receiverType = member?.DeclaringType;
			BindingParameter[] parameters;
			if (member is MethodBase method)
			{
				MethodCreatorTools.ValidateInfixSignature(method, "selected inner call");
				if ((method.CallingConvention & CallingConventions.VarArgs) != 0)
					throw new ArgumentException("Varargs inner calls are not supported.");
				if (method.ContainsGenericParameters) throw new ArgumentException("The selected call has unresolved generic storage.");
				var construction = instruction.opcode == OpCodes.Newobj;
				returnType = construction ? method.DeclaringType : AccessTools.GetReturnedType(method);
				parameters = BindingParameter.From(method);
				if (!construction && !method.IsStatic)
					receiverStorage = receiverType.IsValueType ? receiverType.MakeByRefType() : receiverType;
				if (method.IsStatic && instruction.opcode == OpCodes.Callvirt)
					throw new ArgumentException("A static method cannot be called with callvirt.");
				if (start != index)
				{
					if (index - start != 1 || instructions[start].opcode != OpCodes.Constrained || instruction.opcode != OpCodes.Callvirt
						|| instructions[start].operand is not Type type || method.IsStatic)
						throw new ArgumentException("Only a concrete constrained. prefix on an instance callvirt is supported; tail. and other call prefixes cannot be wrapped.");
					receiverType = type;
					receiverStorage = type.MakeByRefType();
				}
			}
			else if (member is FieldInfo field)
			{
				MethodCreatorTools.ValidateInfixField(field);
				if (field.IsStatic != (instruction.opcode == OpCodes.Ldsfld || instruction.opcode == OpCodes.Stsfld))
					throw new ArgumentException("Field targets require ldsfld/stsfld for static fields and ldfld/stfld for instance fields.");
				var write = instruction.opcode == OpCodes.Stfld || instruction.opcode == OpCodes.Stsfld;
				parameters = write ? [new BindingParameter("value", field.FieldType)] : [];
				returnType = write ? typeof(void) : field.FieldType;
				if (!field.IsStatic)
				{
					if (receiverType.IsValueType) throw new ArgumentException("Instance fields on structs require distinguishing value and address receivers and are not supported.");
					receiverStorage = receiverType;
				}
				var prefixes = instructions.Skip(start).Take(index - start).ToArray();
				if (prefixes.Any(prefix => prefix.opcode != OpCodes.Volatile && prefix.opcode != OpCodes.Unaligned)
					|| prefixes.Select(prefix => prefix.opcode).Distinct().Count() != prefixes.Length
					|| prefixes.Any(prefix => prefix.opcode == OpCodes.Unaligned && (field.IsStatic || Convert.ToInt32(prefix.operand) is not (1 or 2 or 4))))
					throw new ArgumentException("A field access supports only one volatile. and one valid unaligned. prefix.");
			}
			else
			{
				if (start != index) throw new ArgumentException("A constant load cannot have instruction prefixes.");
				parameters = [];
				returnType = instruction.opcode == OpCodes.Ldstr ? typeof(string)
					: instruction.opcode == OpCodes.Ldc_I8 ? typeof(long) : instruction.opcode == OpCodes.Ldc_R4 ? typeof(float)
					: instruction.opcode == OpCodes.Ldc_R8 ? typeof(double) : typeof(int);
			}
			if (receiverType?.ContainsGenericParameters == true || returnType.ContainsGenericParameters || parameters.Any(parameter => parameter.ParameterType.ContainsGenericParameters))
				throw new ArgumentException("The selected operation has unresolved generic storage. Select a closed outer method and concrete operands.");
			var arguments = parameters.Select((parameter, argumentIndex) => parameterStorage
				? new InjectionStorage(parameter.ParameterType, argumentIndex + (receiverStorage is null ? 0 : 1))
				: new InjectionStorage(config.DeclareLocal(parameter.ParameterType))).ToArray();
			InjectionStorage? receiver = receiverStorage is null ? null : parameterStorage
				? new InjectionStorage(receiverStorage, 0) : new InjectionStorage(config.DeclareLocal(receiverStorage));
			return new PatchBindingContext(member, returnType, parameters, receiverType, receiver, arguments, new VariableState());
		}
	}
}
