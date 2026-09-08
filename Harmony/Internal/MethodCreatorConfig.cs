using MonoMod.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace HarmonyLib
{

	internal class MethodCreatorConfig

	{
		internal readonly MethodBase original;
		internal readonly MethodBase source; // for reverse patch
		internal readonly List<MethodInfo> prefixes;
		internal readonly List<MethodInfo> postfixes;
		internal readonly List<MethodInfo> transpilers;
		internal readonly List<MethodInfo> finalizers;
		internal readonly List<Infix> innerprefixes;
		internal readonly List<Infix> innerpostfixes;
		internal readonly List<Infix> innerfinalizers;
		internal readonly bool debug;

		internal MethodCreatorConfig(
			MethodBase original,
			MethodBase source,
			List<MethodInfo> prefixes,
			List<MethodInfo> postfixes,
			List<MethodInfo> transpilers,
			List<MethodInfo> finalizers,
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

		internal MethodCreatorConfig(MethodBase original, MethodBase source, List<MethodInfo> prefixes, List<MethodInfo> postfixes,
			List<MethodInfo> transpilers, List<MethodInfo> finalizers, List<Infix> innerprefixes, List<Infix> innerpostfixes, bool debug)
			: this(original, source, prefixes, postfixes, transpilers, finalizers, innerprefixes, innerpostfixes, [], debug) { }

		internal MethodCreatorConfig(MethodCreatorConfig parent, string name, Type returnType)
			: this(parent.original, null, [], [], [], [], parent.innerprefixes, parent.innerpostfixes, parent.innerfinalizers, parent.debug)
		{
			patch = new DynamicMethodDefinition(name, returnType, []);
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
		internal LocalBuilder GetLocal(InjectionType type) => localVariables[type];
		internal InjectionStorage GetLocal(string name) => localVariables[name];
		internal bool HasLocal(string name) => localVariables.TryGetValue(name, out _);

		internal LocalBuilder DeclareLocal(Type type, bool isPinned = false) => il.DeclareLocal(type, isPinned);
		internal Label DefineLabel() => il.DefineLabel();
		internal MethodInfo GenerateMethod(bool structuredHelper = false)
		{
			var body = patch.Definition.Body;
			// Match DynamicMethod's default for transpiler-declared and Harmony-generated locals on either backend.
			body.InitLocals = true;
			// Synthetic helpers contain only Harmony's structured finalizer regions, not imported outer handlers.
			// DynamicMethod tokens retain the exact runtime members, including callbacks in private load contexts.
			if (structuredHelper)
			{
				var method = (DynamicMethod)DMDEmitDynamicMethodGenerator.Generate(patch);
				// Helpers explicitly initialize every local that can be read before assignment.
				// Older optimized JITs otherwise expose an uninitialized result after a caught operation fault.
				method.InitLocals = false;
				return method;
			}
			// MonoMod's DynamicMethod calli emitter subtracts the arguments but omits the returned stack value.
			// Cecil calculates the complete stack depth, which older JITs require even when newer JITs accept the undercount.
			var returnsFromCalli = body.Instructions.Any(instruction => instruction.OpCode == Mono.Cecil.Cil.OpCodes.Calli
				&& instruction.Operand is Mono.Cecil.CallSite call && ReturnsValue(call));
			if (body.ExceptionHandlers.Count == 0 && !returnsFromCalli) return patch.Generate();
			var proxies = new Dictionary<MethodInfo, Mono.Cecil.MethodReference>();
			var proxyAssemblies = new List<Assembly>();
			// Legacy runtimes can bind emitted assemblies directly and must retain their competing-identity checks.
			var proxyEmittedMethods = typeof(object).Assembly.GetType("System.Runtime.Loader.AssemblyLoadContext") is not null;
			foreach (var instruction in body.Instructions)
			{
				var method = instruction.Operand is DynamicMethodReference dynamicReference ? dynamicReference.DynamicMethod : null;
				if (proxyEmittedMethods && method is null && instruction.Operand is Mono.Cecil.MethodReference reference && !reference.HasThis
					&& reference != patch.Definition && reference.ResolveReflection() is MethodInfo target)
				{
#if NET35
					if (target.Module.Assembly is AssemblyBuilder) method = target;
#else
					if (target.Module.Assembly.IsDynamic) method = target;
#endif
				}
				if (method is null) continue;
				if (!proxies.TryGetValue(method, out var proxy))
				{
					var proxyMethod = DynamicMethodProxy.Create(method);
					proxy = patch.Definition.Module.ImportReference(proxyMethod);
					proxyAssemblies.Add(proxyMethod.Module.Assembly);
					proxies.Add(method, proxy);
				}
				instruction.Operand = proxy;
			}
			// Preserve the emitted exception table instead of reconstructing ranges and handler-entry labels through reflection emission.
			return GeneratedAssemblyLoader.Generate(patch, proxyAssemblies);

			static bool ReturnsValue(Mono.Cecil.CallSite call)
			{
				var type = call.ReturnType;
				while (type is Mono.Cecil.RequiredModifierType or Mono.Cecil.OptionalModifierType)
					type = ((Mono.Cecil.TypeSpecification)type).ElementType;
				return type.MetadataType != Mono.Cecil.MetadataType.Void;
			}
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
		internal VariableState localVariables;
		internal PatchBindingContext bindingContext;
		internal PersistentStatePlan persistence;
		internal LocalBuilder resultVariable;
		internal Label? skipOriginalLabel;
		internal LocalBuilder runOriginalVariable;
		internal LocalBuilder exceptionVariable;
		internal LocalBuilder finalizedVariable;

		internal MethodBase MethodBase => source ?? original;
		internal IEnumerable<MethodInfo> Fixes => prefixes.Union(postfixes).Union(finalizers);
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
		internal void WithFixes(Action<MethodInfo> action)
		{
			foreach (var fix in Fixes)
				action(fix);
			foreach (var fix in InnerFixes)
				action(fix.OuterMethod);
		}
	}
}
