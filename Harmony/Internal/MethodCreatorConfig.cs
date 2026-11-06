using MonoMod.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace HarmonyLib
{

	// Keep registration-specific options beside the resolved callback, including factory results.
	internal readonly struct PatchCall(MethodInfo method, bool uncheckedReferenceBinding = false, bool candidate = true, bool unchangedPassthrough = false, MethodBase instantiation = null)
	{
		internal readonly MethodInfo method = method;
		internal readonly bool uncheckedReferenceBinding = uncheckedReferenceBinding;
		internal readonly bool candidate = candidate;
		internal readonly bool unchangedPassthrough = unchangedPassthrough;
		// The registration target, retained in closed helpers for state ownership. Only definition bodies emit a guard.
		internal readonly MethodBase instantiation = instantiation;
		// Only unchanged callbacks inherit their accepted reference bindings; factory outputs are candidates on every rebuild.
		internal bool UncheckedReferences => uncheckedReferenceBinding || !candidate;
	}

	internal class MethodCreatorConfig

	{
		internal readonly MethodBase original;
		internal readonly MethodBase source; // for reverse patch
		internal readonly List<PatchCall> prefixes;
		internal readonly List<PatchCall> postfixes;
		internal readonly List<MethodInfo> transpilers;
		internal readonly List<PatchCall> finalizers;
		internal readonly List<Infix> innerprefixes;
		internal readonly List<Infix> innerpostfixes;
		internal readonly List<Infix> innerfinalizers;
		internal readonly bool debug;

		internal MethodCreatorConfig(
			MethodBase original,
			MethodBase source,
			List<PatchCall> prefixes,
			List<PatchCall> postfixes,
			List<MethodInfo> transpilers,
			List<PatchCall> finalizers,
			List<Infix> innerprefixes,
			List<Infix> innerpostfixes,
			List<Infix> innerfinalizers,
			bool debug)
		{
			this.original = original;
			this.source = source;
			this.prefixes = prefixes;
			this.postfixes = postfixes;
			this.transpilers = transpilers;
			this.finalizers = finalizers;
			this.innerprefixes = innerprefixes;
			this.innerpostfixes = innerpostfixes;
			this.innerfinalizers = innerfinalizers;
			this.debug = debug;
		}

		internal MethodCreatorConfig(MethodBase original, MethodBase source, List<PatchCall> prefixes, List<PatchCall> postfixes,
			List<MethodInfo> transpilers, List<PatchCall> finalizers, List<Infix> innerprefixes, List<Infix> innerpostfixes, bool debug)
			: this(original, source, prefixes, postfixes, transpilers, finalizers, innerprefixes, innerpostfixes, [], debug) { }

		internal MethodCreatorConfig(MethodCreatorConfig parent, string name, Type returnType)
			: this(parent.original, null, [], [], [], [], parent.innerprefixes, parent.innerpostfixes, parent.innerfinalizers, parent.debug)
		{
			patch = MethodBodyPatching.IsDefinitionWide(original)
				? GenericMethodContext.Create(original, name, returnType, []) : new DynamicMethodDefinition(name, returnType, []);
			il = patch.GetILGenerator();
			this.returnType = returnType;
			injections = parent.injections;
			instructions = [];
			originalVariables = parent.originalVariables;
			localVariables = new VariableState();
			persistence = parent.persistence;
		}

		internal bool Prepare()
		{
			var patchInfo = HarmonySharedState.GetPatchInfo(original) ?? new PatchInfo();
			patchIndex = patchInfo.VersionCount + 1;
			patch = MethodPatcherTools.CreateDynamicMethod(original, $"_Patch{patchIndex}", debug);
			if (patch == null) return false;
			injections = Fixes.Union(InnerFixes.Select(fix => fix.OuterMethod)).ToDictionary(fix => fix, fix => fix.GetParameters().Select(p => new InjectedParameter(fix, p)).ToList());
			returnType = AccessTools.GetReturnedType(original);
			il = patch.GetILGenerator();
			instructions = [];
			return true;
		}

		internal void AddCode(CodeInstruction code) => instructions.Add(code);
		internal void AddCodes(IEnumerable<CodeInstruction> codes) => instructions.AddRange(codes);
		internal void AddLocal(InjectionType type, LocalBuilder local) => localVariables.Add(type, local);
		internal void AddLocal(object name, LocalBuilder local) => localVariables.Add(name, local);

		internal LocalBuilder DeclareLocal(Type type, bool isPinned = false) => il.DeclareLocal(type, isPinned);
		internal Label DefineLabel() => il.DefineLabel();
		internal MethodInfo GenerateMethod(bool structuredHelper = false)
		{
			// The installer writes a definition-wide body into the open definition itself. It has no callable copy.
			if (!structuredHelper && MethodBodyPatching.IsDefinitionWide(original)) return null;
			var generated = MethodBodyGenerator.Generate(patch, structuredHelper, original);
			return structuredHelper && MethodBodyPatching.IsDefinitionWide(original)
				? GenericMethodContext.Construct(generated, GenericMethodContext.Arguments(original)) : generated;
		}

		// prepared by Prepare()
		internal int patchIndex;
		internal DynamicMethodDefinition patch;
		internal Dictionary<MethodInfo, List<InjectedParameter>> injections;
		internal Type returnType;
		internal ILGenerator il;
		internal List<CodeInstruction> instructions;

		// added by MethodCreator
		internal LocalBuilder[] originalVariables;
		internal MethodBody originalBody; // Read once by CreateReplacement before declaring locals or constructing its copier.
		internal VariableState localVariables;
		internal PatchBindingContext bindingContext;
		internal PersistentStatePlan persistence;
		internal LocalBuilder resultVariable;
		internal Label? skipOriginalLabel;
		internal LocalBuilder runOriginalVariable;
		internal LocalBuilder exceptionVariable;
		internal LocalBuilder finalizedVariable;
		// The instantiation of the callback call being emitted, if that callback was registered on a closed instantiation.
		internal MethodBase instantiation;
		// Instantiations of a definition-wide target that own a closed body. The family body enters each helper under a type guard.
		internal List<(MethodBase instantiation, MethodInfo helper, PersistentStatePlan persistence)> closedBodies = [];

		internal MethodBase MethodBase => source ?? original;
		internal IEnumerable<MethodInfo> Fixes => prefixes.Concat(postfixes).Concat(finalizers).Select(call => call.method).Distinct();
		internal IEnumerable<Infix> InnerFixes => innerprefixes.Union(innerpostfixes).Union(innerfinalizers);
		internal IEnumerable<InjectedParameter> InjectionsFor(MethodInfo fix, InjectionType type = InjectionType.Unknown, bool skipFirst = false)
		{
			if (injections.TryGetValue(fix, out var list))
			{
				var parameters = skipFirst ? list.Skip(1) : list;
				if (type != InjectionType.Unknown)
					return parameters.Where(pair => pair.injectionType == type);
				return parameters;
			}
			return [];
		}
		internal bool AnyFixHas(InjectionType type) => Fixes.SelectMany(fix => InjectionsFor(fix, type)).Any();
		internal IEnumerable<InjectedParameter> OuterInjectionsFor(MethodInfo fix, InjectionType type = InjectionType.Unknown)
			=> Fixes.Contains(fix) ? InjectionsFor(fix, type) : InfixInjectionsFor(fix, type).Where(injection => injection.outer);
		IEnumerable<InjectedParameter> InfixInjectionsFor(MethodInfo fix, InjectionType type)
			=> InjectionsFor(fix, type, fix.ReturnType != typeof(void) && innerpostfixes.Any(postfix => postfix.OuterMethod == fix)
				&& !innerprefixes.Any(prefix => prefix.OuterMethod == fix) && !innerfinalizers.Any(finalizer => finalizer.OuterMethod == fix));
		internal bool AnyInfixHasOuter(InjectionType type) => InnerFixes.Any(fix => InfixInjectionsFor(fix.OuterMethod, type).Any(injection => injection.outer));
		// State belongs to a patch class and its registration target, including inside closed helpers.
		internal static object StateKey(MethodInfo fix, MethodBase instantiation) => instantiation is null ? (object)fix.DeclaringType ?? "null" : (fix.DeclaringType, instantiation);
		internal IEnumerable<(MethodInfo fix, MethodBase instantiation)> StateOwners
			=> prefixes.Concat(postfixes).Concat(finalizers).Select(call => (call.method, call.instantiation)).Distinct()
				.Concat(InnerFixes.Select(fix => (fix.OuterMethod, (MethodBase)null)));
	}
}
