using HarmonyLib;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;

public static partial class Program
{
	static void Part2SharedState(string firstPath, string secondPath, bool concurrent)
	{
		int Count() => AppDomain.CurrentDomain.GetAssemblies().Count(a => a.GetName().Name == "HarmonySharedState");
		Require(Count() == 0, "no shared state before the startup probe");
		var types = new[] { firstPath, secondPath }.Select((path, index) => new AssemblyLoadContext("part2-" + index)
			.LoadFromAssemblyPath(Path.GetFullPath(path)).GetType("HarmonyLib.HarmonySharedState")!).ToArray();
		observations["engines"] = types.Select(type => type.Assembly.FullName).ToArray();
		var errors = new string?[2];
		using var ready = new Barrier(2);
		void Initialize(int index)
		{
			if (concurrent) ready.SignalAndWait();
			try { RuntimeHelpers.RunClassConstructor(types[index].TypeHandle); }
			catch (Exception error) { errors[index] = error.ToString(); }
		}
		if (concurrent)
		{
			var threads = Enumerable.Range(0, 2).Select(index => new Thread(() => Initialize(index))).ToArray();
			foreach (var thread in threads) thread.Start();
			foreach (var thread in threads) Require(thread.Join(TimeSpan.FromSeconds(30)), "startup thread completed");
		}
		else { Initialize(0); Initialize(1); }
		observations["initializationErrors"] = errors;
		observations["sharedStateAssemblyCount"] = Count();
		if (errors.All(error => error is null))
			observations["samePatchDictionary"] = ReferenceEquals(types[0].GetField("state", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null),
				types[1].GetField("state", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null));
		Observe("thirdCurrentEngine", () =>
		{
			RuntimeHelpers.RunClassConstructor(typeof(Harmony).Assembly.GetType("HarmonyLib.HarmonySharedState")!.TypeHandle);
			return true;
		});
	}

	[HarmonyPatch(typeof(Program), nameof(Outer)), HarmonyPatch(MethodType.Normal)]
	public static class Part2AnnotatedPatch
	{
		[HarmonyPrefix, HarmonyInfix(typeof(Program), nameof(Inner), typeof(int))]
		public static void Before() { }
	}

	static void Part2Annotations()
	{
		var method = typeof(Part2AnnotatedPatch).GetMethod(nameof(Part2AnnotatedPatch.Before))!;
		var parent = HarmonyMethodExtensions.GetMergedFromType(typeof(Part2AnnotatedPatch));
		var raw = HarmonyMethodExtensions.GetMergedFromMethod(method);
		var merged = parent.Merge(raw);
		var imported = parent.Merge(new HarmonyMethod(method));
		observations["rawMethodType"] = (int?)raw.methodType;
		observations["mergedMethodType"] = (int?)merged.methodType;
		observations["importedMethodType"] = (int?)imported.methodType;
		var resolve = typeof(Harmony).Assembly.GetType("HarmonyLib.PatchTools")!.GetMethod("GetOriginalMethod", BindingFlags.Static | BindingFlags.NonPublic)!;
		observations["mergedResolvesOuter"] = Equals(resolve.Invoke(null, [merged]), Method(nameof(Outer)));
		observations["importedResolvesOuter"] = Equals(resolve.Invoke(null, [imported]), Method(nameof(Outer)));
		var harmony = new Harmony("review.part2.annotations");
		try { observations["classInstallationCount"] = harmony.CreateClassProcessor(typeof(Part2AnnotatedPatch)).Patch().Count; }
		finally { harmony.UnpatchAll(harmony.Id); }
	}

	static void Part2PatchEquality()
	{
		var callback = LoadPlugin(pluginPath).GetType("Patches")!.GetMethod("Prefix")!;
		var outer = Method(nameof(Outer));
		var harmony = new Harmony("review.part2.patch-equality");
		harmony.CreateProcessor(outer).AddInnerPrefix(new HarmonyMethod(callback) { innerMethod = new InnerMethod(Method(nameof(Inner))) }).Patch();
		var held = Harmony.GetPatchInfo(outer).InnerPrefixes.Single();
		var hash = held.GetHashCode();
		var duplicate = LoadPlugin(pluginPath);
		Require(duplicate != callback.Module.Assembly && duplicate.ManifestModule.ModuleVersionId == callback.Module.ModuleVersionId, "duplicate real callback module");
		var fresh = Harmony.GetPatchInfo(outer).InnerPrefixes.Single();
		Observe("cachedHashStable", () => held.GetHashCode() == hash);
		Observe("freshSelfEquality", () => fresh.Equals(fresh));
		Observe("freshHashCode", () => fresh.GetHashCode());
		Observe("freshHashSet", () => new HashSet<Patch> { fresh }.Count);
		Observe("removeByHeldCallback", () => { harmony.Unpatch(outer, callback); return Outer(10); });
	}

	public static int Part2InvalidPostfix(string value) => value.Length;
	static void Part2Passthrough()
	{
		var harmony = new Harmony("review.part2.passthrough");
		var outer = Method(nameof(Outer));
		try
		{
			harmony.CreateProcessor(outer).AddInnerPostfix(new HarmonyMethod(Method(nameof(PlainPostfix))) { innerMethod = new InnerMethod(Method(nameof(Inner))) }).Patch();
			observations["before"] = Outer(10);
			Observe("invalidRegistration", () => harmony.CreateProcessor(outer).AddInnerPostfix(new HarmonyMethod(Method(nameof(Part2InvalidPostfix)))
			{ innerMethod = new InnerMethod(Method(nameof(Inner))) }).Patch()?.Name);
			observations["after"] = Outer(10);
			observations["survivingPostfixes"] = Harmony.GetPatchInfo(outer).InnerPostfixes.Count;
		}
		finally { harmony.UnpatchAll(harmony.Id); }
	}

	static readonly Dictionary<string, int> validationCalls = [];
	public static void CountPart2Validation(MethodBase __originalMethod)
		=> validationCalls[__originalMethod.Name] = validationCalls.TryGetValue(__originalMethod.Name, out var count) ? count + 1 : 1;
	static void Part2Validation()
	{
		var harmony = new Harmony("review.part2.validation");
		foreach (var name in new[] { "ValidateSurvivingMetadata", "RequiresInfixV2", "RequiresInfixV3", "RequiresInfixV4" })
			harmony.Patch(typeof(PatchInfo).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!, prefix: new HarmonyMethod(Method(nameof(CountPart2Validation))));
		harmony.Patch(typeof(InnerMethod).GetMethod("ResolveModule", BindingFlags.Static | BindingFlags.NonPublic)!, prefix: new HarmonyMethod(Method(nameof(CountPart2Validation))));
		validationCalls.Clear();
		try
		{
			harmony.CreateProcessor(Method(nameof(Outer))).AddInnerPrefix(new HarmonyMethod(Method(nameof(Noop))) { innerMethod = new InnerMethod(Method(nameof(Inner))) }).Patch();
			observations["callsForOneInfixRegistration"] = new Dictionary<string, int>(validationCalls);
			observations["result"] = Outer(10);
		}
		finally { harmony.UnpatchAll(harmony.Id); }
	}

	static void Part2ConstantAllocation()
	{
		var target = InnerTarget.Constant(1d);
		var match = typeof(InnerTarget).GetMethod("Matches", BindingFlags.Instance | BindingFlags.NonPublic)!
			.CreateDelegate<Func<CodeInstruction, bool>>(target);
		long Allocations(CodeInstruction instruction)
		{
			for (var i = 0; i < 100; i++) Require(!match(instruction), "the candidate must not match");
			var before = GC.GetAllocatedBytesForCurrentThread();
			for (var i = 0; i < 10000; i++) _ = match(instruction);
			return GC.GetAllocatedBytesForCurrentThread() - before;
		}
		observations["candidateCount"] = 10000;
		observations["nonnumericControlBytes"] = Allocations(new CodeInstruction(OpCodes.Nop));
		observations["nonmatchingDoubleBytes"] = Allocations(new CodeInstruction(OpCodes.Ldc_R8, 2d));
	}
}
