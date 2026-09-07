using HarmonyLib;
using System.Reflection;

namespace HarmonyCompatibility.Feature;

public static partial class Entry
{
	public sealed class CompletionState { public int Value; }
	public static string CompletionIdentity() => typeof(Entry).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
		.Single(attribute => attribute.Key == "CompletionFixtureIdentity").Value!;

	public static void InstallCompletionIdentity(MethodBase outer, string owner) => new Harmony(owner).CreateProcessor(outer)
		.AddInnerFinalizer(new HarmonyMethod(typeof(Entry).GetMethod(nameof(CompleteIdentity))!)
		{ innerMethod = new InnerMethod(typeof(Targets).GetMethod(nameof(Targets.Called))!) }).Patch();

	public static void InstallOrdinaryIdentity(MethodBase outer, string owner) => new Harmony(owner).CreateProcessor(outer)
		.AddFinalizer(typeof(Entry).GetMethod(nameof(CompleteIdentity))!).Patch();

	public static Exception? CompleteIdentity(Exception? __exception, ref int __result)
	{
		var identity = CompletionIdentity();
		Targets.Trace.Add("identity:" + identity);
		Targets.PatchCalls++;
		__result += identity == "second" ? 100 : 10;
		return __exception;
	}

	public static void InstallCompletionState(MethodBase outer, string owner) => new Harmony(owner).CreateProcessor(outer)
		.AddPrefix(typeof(Entry).GetMethod(nameof(PrepareCompletionState))!)
		.AddPostfix(typeof(Entry).GetMethod(nameof(FinishCompletionState))!)
		.AddInnerFinalizer(new HarmonyMethod(typeof(Entry).GetMethod(nameof(CompleteState))!)
		{ innerMethod = new InnerMethod(typeof(Targets).GetMethod(nameof(Targets.Called))!) }).Patch();

	public static void PrepareCompletionState(out CompletionState __state) => __state = new CompletionState { Value = 17 };
	public static int FinishCompletionState(int __result, CompletionState __state)
	{
		Targets.Trace.Add("state-postfix:" + __state.Value);
		return __result + __state.Value;
	}
	public static Exception? CompleteState(Exception? __exception, [HarmonyOuter] CompletionState __state)
	{
		Targets.Trace.Add("state-finalizer:" + __state.Value);
		__state.Value++;
		return __exception;
	}

	public static MethodBase AutoBody() => AccessTools.StateMachineMoveNext(typeof(Targets).GetMethod(nameof(Targets.AutoFactory))!)!;

	public static void InstallCompletion(MethodBase outer, string kind, string owner)
	{
		var method = typeof(Entry).GetMethod(kind == "Finalizer" ? nameof(CompleteFinalizer) : nameof(CapturedBefore))!;
		var patch = new HarmonyMethod(method) { innerMethod = new InnerMethod(typeof(Targets).GetMethod(nameof(Targets.Called))!) };
		var processor = new Harmony(owner).CreateProcessor(outer);
		if (kind == "Finalizer") processor.AddInnerFinalizer(patch); else processor.AddInnerPrefix(patch);
		processor.Patch();
	}

	public static void InstallCompletionV2(MethodBase outer) => new Harmony("completion-v2").CreateProcessor(outer)
		.AddInnerPostfix(new HarmonyMethod(typeof(Entry).GetMethod(nameof(AfterConstant))!) { innerTarget = InnerTarget.Constant(7) }).Patch();

	public static void CapturedBefore([HarmonyOuter, HarmonyArgument("value", ArgumentMode.Captured)] int capturedValue)
	{
		Targets.PatchCalls++;
		Targets.Trace.Add("captured:" + capturedValue);
		if (capturedValue != 1) throw new InvalidOperationException("The generated-body capture was not preserved.");
	}

	public static Exception? CompleteFinalizer(Exception? __exception)
	{
		Targets.PatchCalls++;
		Targets.Trace.Add("inner-finalizer");
		return __exception;
	}
}

[HarmonyPatch(typeof(Targets), nameof(Targets.AutoFactory))]
public static class AutoMethod
{
	[HarmonyInfix(typeof(Targets), nameof(Targets.Called), typeof(int), OuterBody = InfixOuterBody.Auto)]
	[HarmonyPrefix]
	public static void Before(ref int value) => Entry.InnerPrefix(ref value);
}

[HarmonyPatch(typeof(Targets), nameof(Targets.AutoFactory))]
public static class AutoConstant
{
	[HarmonyInfix(7, OuterBody = InfixOuterBody.Auto)]
	[HarmonyPostfix]
	public static int After(int value) => Entry.AfterConstant(value);
}

[HarmonyPatch]
public static class FinalizerDeclaration
{
	[HarmonyTargetMethod]
	public static MethodBase Target() => Targets.CapturedFactory(1).Method;

	[HarmonyInfix(typeof(Targets), nameof(Targets.Called), typeof(int)), HarmonyFinalizer]
	public static Exception? After(Exception? __exception) => Entry.CompleteFinalizer(__exception);
}

[HarmonyPatch]
public static class CapturedDeclaration
{
	[HarmonyTargetMethod]
	public static MethodBase Target() => Targets.CapturedFactory(1).Method;

	[HarmonyInfix(typeof(Targets), nameof(Targets.Called), typeof(int)), HarmonyPrefix]
	public static void Before([HarmonyOuter, HarmonyArgument("value", ArgumentMode.Captured)] int value) => Entry.CapturedBefore(value);
}
