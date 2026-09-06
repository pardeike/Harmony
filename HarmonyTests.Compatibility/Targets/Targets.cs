using System.Reflection;
using System.Runtime.CompilerServices;

namespace HarmonyCompatibility;

public static class Targets
{
	public static readonly List<string> Trace = [];
	public static int TranspilerEntries;
	public static int TranspilerEnumerations;
	public static int PatchCalls;
	public static int PrepareCalls;
	public static int PauseNextTranspiler;
	public static readonly ManualResetEventSlim TranspilerPaused = new(false);
	public static readonly ManualResetEventSlim ResumeTranspiler = new(false);
	public static readonly List<MethodBase> ObservedInnerMethods = [];

	[MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
	public static int Run(int value) => Called(value) + 1;

	[MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
	public static int LegacyRun(int value) => Called(value) + 1;

	[MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
	public static int ScopedRun(int value)
	{
		var result = Called(value);
		return result + value;
	}

	[MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
	public static int StateRun()
	{
		var result = 0;
		for (var value = 1; value <= 2; value++) result += Called(value) + Called(value + 2);
		return result;
	}

	[MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
	public static int Called(int value)
	{
		Trace.Add("called:" + value);
		return value * 2;
	}

	[MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
	public static string GenericRun()
	{
		return Container<int>.Use<int>(1, 2) + Container<string>.Use<int>("s", 3)
			+ Container<int>.Use<string>(4, "t") + Container<string>.Use<string>("u", "v");
	}

	public static (int Result, string[] Trace) Execute()
	{
		Trace.Clear();
		var result = Run(1);
		return (result, Trace.ToArray());
	}
}

public class Container<T>
{
	[MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
	public static string Use<U>(T first, U second)
	{
		Targets.Trace.Add(typeof(T).Name + "/" + typeof(U).Name);
		return "x";
	}

	public class Nested<U>
	{
		[MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
		public static void Touch<V>() { }
	}
}
