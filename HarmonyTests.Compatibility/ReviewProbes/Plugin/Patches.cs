using HarmonyLib;
using System.Runtime.CompilerServices;

public static class Patches
{
	public static int Field;
#if SECOND
    const int value = 2;
#else
	const int value = 1;
#endif
	public static void Before(out int __state) => __state = value;
	public static void After(int __state, ref int __result) => __result += __state;
	public static void NamedBefore([HarmonyOuter] out int __var_named) => __var_named = value;
	public static void NamedAfter([HarmonyOuter] int __var_named, ref int __result) => __result += __var_named;
	public static void Prefix(ref int value) => value++;
	public static void Postfix(ref int __result) => __result++;
	public static void Finalizer(ref int __result) => __result++;
	public static void OrdinaryCallback() { }
	[MethodImpl(MethodImplOptions.NoInlining)]
	public static int Target(int value) => value;
}
