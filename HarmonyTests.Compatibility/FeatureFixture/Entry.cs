using HarmonyLib;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace HarmonyCompatibility.Feature;

public static class Entry
{
	private static InnerMethod? input;

	public static Assembly Provider() => typeof(Harmony).Assembly;
	public static Type Declaration(string name) => typeof(Entry).Assembly.GetType("HarmonyCompatibility.Feature." + name, true)!;

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static object MaterializeAttribute() => new HarmonyInfix(typeof(Targets), nameof(Targets.Called), typeof(int));

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static object DirectNewMember()
	{
		var method = new HarmonyMethod();
		method.innerMethod = new InnerMethod(typeof(Targets).GetMethod(nameof(Targets.Called))!);
		return method.innerMethod;
	}

	public static void Install(MethodBase outer, string owner, bool postfix)
	{
		var method = typeof(Entry).GetMethod(postfix ? nameof(InnerPostfix) : nameof(InnerPrefix))!;
		var patch = new HarmonyMethod(method) { innerMethod = new InnerMethod(typeof(Targets).GetMethod(nameof(Targets.Called))!) };
		var processor = new Harmony(owner).CreateProcessor(outer);
		if (postfix) processor.AddInnerPostfix(patch); else processor.AddInnerPrefix(patch);
		processor.Patch();
	}

	public static object IdentityRecord(MethodInfo selector, int[] positions)
	{
		input = new InnerMethod(selector, positions);
		return new PatchInfo { innerprefixes = [new Patch(new HarmonyMethod(typeof(Entry).GetMethod(nameof(GenericPrefix))!) { innerMethod = input }, 0, "identity")] };
	}

	public static void InstallGeneric(MethodInfo selector, string owner, int[] positions)
	{
		input = new InnerMethod(selector, positions);
		new Harmony(owner).CreateProcessor(typeof(Targets).GetMethod(nameof(Targets.GenericRun))!)
			.AddInnerPrefix(new HarmonyMethod(typeof(Entry).GetMethod(nameof(GenericPrefix))!) { innerMethod = input }).Patch();
	}

	public static void MutateInput()
	{
		input!.Method = typeof(Targets).GetMethod(nameof(Targets.Called))!;
		if (input.positions.Length > 0) input.positions[0] = 100;
		input.positions = [200];
	}

	public static void InstallOuterScope()
	{
		new Harmony("outer-scope").CreateProcessor(typeof(Targets).GetMethod(nameof(Targets.ScopedRun))!)
			.AddInnerPrefix(new HarmonyMethod(typeof(Entry).GetMethod(nameof(OuterPrefix))!)
			{
				innerMethod = new InnerMethod(typeof(Targets).GetMethod(nameof(Targets.Called))!)
			}).Patch();
	}

	public static void InstallState(Type callbacks, string owner)
	{
		HarmonyMethod Metadata(string name) => new(callbacks.GetMethod(name)!)
		{
			innerMethod = new InnerMethod(typeof(Targets).GetMethod(nameof(Targets.Called))!)
		};
		new Harmony(owner).CreateProcessor(typeof(Targets).GetMethod(nameof(Targets.StateRun))!)
			.AddInnerPrefix(Metadata("StatePrefix"))
			.AddInnerPostfix(Metadata("StatePostfix")).Patch();
		new Harmony(owner).CreateProcessor(typeof(Targets).GetMethod(nameof(Targets.StateRun))!)
			.AddInnerPostfix(Metadata("StatePostfixSecond")).Patch();
	}

	public static void OuterPrefix(int value, [HarmonyOuter, HarmonyArgument("value")] ref int outerValue)
	{
		Targets.Trace.Add("scope:" + value + "/" + outerValue);
		outerValue += 10;
	}

	public static void GenericPrefix(MethodBase __originalMethod)
	{
		Targets.PatchCalls++;
		Targets.ObservedInnerMethods.Add(__originalMethod);
	}

	public static void InnerPrefix(ref int value)
	{
		Targets.PatchCalls++;
		Targets.Trace.Add("infix-prefix");
		value += 10;
	}

	public static void InnerPostfix(ref int __result)
	{
		Targets.PatchCalls++;
		Targets.Trace.Add("infix-postfix");
		__result += 100;
	}
}

[HarmonyPatch(typeof(Targets), nameof(Targets.Run))]
public static class PrefixAttribute
{
	[HarmonyInfix(typeof(Targets), nameof(Targets.Called), typeof(int))]
	[HarmonyPrefix]
	public static void Before(ref int value) => Entry.InnerPrefix(ref value);
}

[HarmonyPatch(typeof(Targets), nameof(Targets.Run))]
public static class PostfixAttribute
{
	[HarmonyInfix(typeof(Targets), nameof(Targets.Called), typeof(int))]
	[HarmonyPostfix]
	public static void After(ref int __result) => Entry.InnerPostfix(ref __result);
}

[HarmonyPatch(typeof(Targets), nameof(Targets.Run))]
public static class PrefixName
{
	[HarmonyInfix(typeof(Targets), nameof(Targets.Called), typeof(int))]
	public static void Prefix(ref int value) => Entry.InnerPrefix(ref value);
}

[HarmonyPatch(typeof(Targets), nameof(Targets.Run))]
public static class PostfixName
{
	[HarmonyInfix(typeof(Targets), nameof(Targets.Called), typeof(int))]
	public static void Postfix(ref int __result) => Entry.InnerPostfix(ref __result);
}

[HarmonyPatch(typeof(Targets), nameof(Targets.Run))]
public static class InnerPrefixName
{
	[HarmonyInfix(typeof(Targets), nameof(Targets.Called), typeof(int))]
	public static void InnerPrefix(ref int value) => Entry.InnerPrefix(ref value);
}

[HarmonyPatch(typeof(Targets), nameof(Targets.Run))]
public static class InnerPostfixName
{
	[HarmonyInfix(typeof(Targets), nameof(Targets.Called), typeof(int))]
	public static void InnerPostfix(ref int __result) => Entry.InnerPostfix(ref __result);
}

[HarmonyPatch(typeof(Targets), nameof(Targets.Run))]
public static class EquivalentRoles
{
	[HarmonyPrefix]
	[HarmonyInfix(typeof(Targets), nameof(Targets.Called), typeof(int))]
	public static void Prefix(ref int value) => Entry.InnerPrefix(ref value);
}

[HarmonyPatch]
public static class TargetMethodDeclaration
{
	public static MethodBase TargetMethod() => typeof(Targets).GetMethod(nameof(Targets.Run))!;
	[HarmonyPrefix]
	[HarmonyInfix(typeof(Targets), nameof(Targets.Called), typeof(int))]
	[HarmonyPriority(Priority.High)]
	[HarmonyBefore("ordering-before")]
	[HarmonyAfter("ordering-after")]
	public static void Before(ref int value) => Entry.InnerPrefix(ref value);
}

[HarmonyPatch]
public static class TargetMethodsDeclaration
{
	public static IEnumerable<MethodBase> TargetMethods() => [typeof(Targets).GetMethod(nameof(Targets.Run))!];
	[HarmonyInfix(typeof(Targets), nameof(Targets.Called), typeof(int))]
	[HarmonyPostfix]
	[HarmonyPriority(Priority.Low)]
	[HarmonyAfter("ordering-after")]
	[HarmonyBefore("ordering-before")]
	public static void After(ref int __result) => Entry.InnerPostfix(ref __result);
}

[HarmonyPatch(typeof(Targets), nameof(Targets.Run))]
public static class PrepareFalseMissing
{
	[HarmonyPrepare]
	public static bool Prepare() { Targets.PrepareCalls++; return false; }
	[HarmonyInfix(typeof(Targets), "MissingInnerMethod")]
	[HarmonyPrefix]
	public static void Before() => throw new InvalidOperationException("A skipped declaration ran.");
}
