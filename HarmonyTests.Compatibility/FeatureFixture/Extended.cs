using HarmonyLib;
using System.Reflection;

namespace HarmonyCompatibility.Feature;

public static partial class Entry
{
	public static void InstallMemberBinding(bool outer)
	{
		var patch = new HarmonyMethod(typeof(Entry).GetMethod(outer ? nameof(ObserveOuterMember) : nameof(ObserveInnerMember))!)
		{
			innerMethod = new InnerMethod(typeof(Targets).GetMethod(nameof(Targets.Called))!)
		};
		new Harmony("member-binding").CreateProcessor(typeof(Targets).GetMethod(nameof(Targets.OperationRun))!).AddInnerPrefix(patch).Patch();
	}

	public static void ObserveInnerMember(MethodInfo __originalMember) => ObserveMember(__originalMember, nameof(Targets.Called));
	public static void ObserveOuterMember([HarmonyOuter] MethodInfo __originalMember) => ObserveMember(__originalMember, nameof(Targets.OperationRun));
	private static void ObserveMember(MethodInfo member, string expected)
	{
		if (member != typeof(Targets).GetMethod(expected)) throw new InvalidOperationException("Wrong original member: " + member);
		Targets.PatchCalls++;
		Targets.Trace.Add("member:" + expected);
	}

	public static void InstallExtended(string kind, string owner)
	{
		var selector = kind switch
		{
			"Constructor" => new InnerTarget(typeof(OperationBox).GetConstructor([typeof(int)])!, 1),
			"Field" => new InnerTarget(typeof(OperationBox).GetField(nameof(OperationBox.Value))!, InnerTargetKind.FieldRead, 1),
			"Constant" => InnerTarget.Constant(7, 1),
			_ => throw new ArgumentException("Unknown extended selector: " + kind)
		};
		var patch = new HarmonyMethod(typeof(Entry).GetMethod("After" + kind)!) { innerTarget = selector };
		new Harmony(owner).CreateProcessor(typeof(Targets).GetMethod(nameof(Targets.OperationRun))!).AddInnerPostfix(patch).Patch();
		selector.positions[0] = 99; // Registration must retain its own selector snapshot.
	}

	public static OperationBox AfterConstructor(OperationBox value)
	{
		Observe("Constructor");
		value.Value += 100;
		return value;
	}

	public static int AfterField(int value) { Observe("Field"); return value + 100; }
	public static int AfterConstant(int value) { Observe("Constant"); return value + 100; }

	private static void Observe(string kind)
	{
		Targets.PatchCalls++;
		Targets.Trace.Add("extended:" + kind);
	}
}

[HarmonyPatch(typeof(Targets), nameof(Targets.OperationRun))]
public static class ExtendedConstructor
{
	[HarmonyInfix(typeof(OperationBox), InnerTargetKind.Constructor, typeof(int))]
	[HarmonyPostfix]
	public static OperationBox After(OperationBox value) => Entry.AfterConstructor(value);
}

[HarmonyPatch(typeof(Targets), nameof(Targets.OperationRun))]
public static class ExtendedField
{
	[HarmonyInfix(typeof(OperationBox), nameof(OperationBox.Value), InnerTargetKind.FieldRead)]
	[HarmonyPostfix]
	public static int After(int value) => Entry.AfterField(value);
}

[HarmonyPatch(typeof(Targets), nameof(Targets.OperationRun))]
public static class ExtendedConstant
{
	[HarmonyInfix(7)]
	[HarmonyPostfix]
	public static int After(int value) => Entry.AfterConstant(value);
}
