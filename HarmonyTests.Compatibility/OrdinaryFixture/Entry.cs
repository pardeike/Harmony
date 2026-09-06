using HarmonyLib;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace HarmonyCompatibility.Ordinary;

public static class Entry
{
	public static string Label = "unset";

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static Assembly Provider() => typeof(Harmony).Assembly;
	public static void SetLabel(string label) => Label = label;

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static void AddPrefix(MethodBase target, string owner) => new Harmony(owner).CreateProcessor(target)
		.AddPrefix(typeof(Entry).GetMethod(nameof(Prefix))).Patch();

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static void AddPostfix(MethodBase target, string owner) => new Harmony(owner).CreateProcessor(target)
		.AddPostfix(typeof(Entry).GetMethod(nameof(Postfix))).Patch();

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static void AddTranspiler(MethodBase target, string owner) => new Harmony(owner).CreateProcessor(target)
		.AddTranspiler(new HarmonyMethod(typeof(Entry).GetMethod(nameof(Transpiler))) { priority = Priority.First }).Patch();

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static void Rebuild(MethodBase target) => new Harmony("compat.rebuild").CreateProcessor(target).Patch();

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static void UnpatchOwner(MethodBase target, string owner) => new Harmony("compat.remove").Unpatch(target, HarmonyPatchType.All, owner);

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static void UnpatchMethod(MethodBase target, MethodInfo patch) => new Harmony("compat.remove").Unpatch(target, patch);

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static void UnpatchAll(MethodBase target) => new Harmony("compat.remove").Unpatch(target, HarmonyPatchType.All, "*");

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static string[] Owners(MethodBase target) => Harmony.GetPatchInfo(target)?.Owners.OrderBy(x => x).ToArray() ?? [];

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static void AddClass(string owner) => new Harmony(owner).CreateClassProcessor(typeof(DeclaredPatch)).Patch();

	public static void Prefix(ref int value)
	{
		Targets.PatchCalls++;
		Targets.Trace.Add("prefix:" + Label);
		value += 10;
	}

	public static void Postfix(ref int __result)
	{
		Targets.PatchCalls++;
		Targets.Trace.Add("postfix:" + Label);
		__result += 100;
	}

	public static void StatePrefix(int value, ref string? __state)
	{
		if (__state is not null) throw new InvalidOperationException("Inner state leaked from another class, site, or loop iteration: " + __state);
		if (value % 2 != 0) __state = Label + ":" + value;
	}

	public static void StatePostfix(string? __state) => Targets.Trace.Add(Label + ".state1:" + (__state ?? "null"));
	public static void StatePostfixSecond(string? __state) => Targets.Trace.Add(Label + ".state2:" + (__state ?? "null"));

	public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
	{
		Targets.TranspilerEntries++;
		if (Interlocked.CompareExchange(ref Targets.PauseNextTranspiler, 0, 1) == 1)
		{
			Targets.TranspilerPaused.Set();
			if (!Targets.ResumeTranspiler.Wait(TimeSpan.FromSeconds(20))) throw new TimeoutException("Concurrent-update diagnostic did not resume its old transpiler.");
		}
		return Enumerate(instructions);
	}

	private static IEnumerable<CodeInstruction> Enumerate(IEnumerable<CodeInstruction> instructions)
	{
		Targets.TranspilerEnumerations++;
		foreach (var instruction in instructions) yield return instruction;
	}
}

[HarmonyPatch(typeof(Targets), nameof(Targets.Run))]
public static class DeclaredPatch
{
	[HarmonyPrefix]
	public static void Prefix(ref int value)
	{
		Targets.PatchCalls++;
		Targets.Trace.Add("class");
		value += 10;
	}
}
