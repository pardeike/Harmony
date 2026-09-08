using HarmonyLib;
using System.Reflection;

namespace HarmonyCompatibility.Feature;

public static partial class Entry
{
	public static MethodBase PersistentBody(string name) => AccessTools.StateMachineMoveNext(typeof(Targets).GetMethod(name)!)!;
	public static void InstallPersistent(MethodBase body, string owner) => new Harmony(owner).CreateProcessor(body)
		.AddInnerPrefix(new HarmonyMethod(typeof(Entry).GetMethod(nameof(PersistentBefore))!)
		{ innerMethod = new InnerMethod(typeof(Targets).GetMethod(nameof(Targets.Called))!) }).Patch();
	public static void PersistentBefore(ref int value, [HarmonyOuter, HarmonyArgument("count", ArgumentMode.Persistent)] ref int count)
	{
		Targets.PatchCalls++;
		value = ++count;
	}
}

[HarmonyPatch]
public static class PersistentDeclaration
{
	[HarmonyTargetMethod]
	public static MethodBase Target() => Entry.PersistentBody(nameof(Targets.PersistentAsync));
	[HarmonyInfix(typeof(Targets), nameof(Targets.Called), typeof(int)), HarmonyPrefix]
	public static void Before(ref int value, [HarmonyOuter, HarmonyArgument("count", ArgumentMode.Persistent)] ref int count)
		=> Entry.PersistentBefore(ref value, ref count);
}
