namespace Patching_Infix
{
	using HarmonyLib;
	using System;
	using System.Collections.Generic;
	using System.Runtime.CompilerServices;

	// <example>
	public static class Helper
	{
		[MethodImpl(MethodImplOptions.NoInlining)]
		public static bool Decide(string value)
		{
			DecidePatch.Events.Add("call: " + value);
			return value.Length > 0;
		}
	}

	public static class Outer
	{
		[MethodImpl(MethodImplOptions.NoInlining)]
		public static bool Run(string value, int mode) => Helper.Decide(value);
	}

	[HarmonyPatch(typeof(Outer), nameof(Outer.Run))]
	public static class DecidePatch
	{
		public static readonly List<string> Events = [];

		[HarmonyInfix(typeof(Helper), nameof(Helper.Decide), typeof(string))]
		[HarmonyPrefix, HarmonyPriority(Priority.High)]
		public static bool Before(ref string value, ref bool __result, [HarmonyOuter] int mode)
		{
			Events.Add("high priority");
			if (mode == 0)
			{
				__result = false;
				return false;
			}
			value += ".";
			return true;
		}

		[HarmonyInfix(typeof(Helper), nameof(Helper.Decide), typeof(string))]
		[HarmonyPrefix, HarmonyPriority(Priority.Low)]
		static void Observe(bool __runOriginal) => Events.Add("low priority: " + __runOriginal);

		[HarmonyInfix(typeof(Helper), nameof(Helper.Decide), typeof(string))]
		[HarmonyPostfix]
		static void After(bool __result, bool __runOriginal) => Events.Add("postfix: " + __result + ", " + __runOriginal);
	}
	// </example>

	public static class Installation
	{
		// <install>
		public static void Install(Harmony harmony) => harmony.CreateClassProcessor(typeof(DecidePatch)).Patch();
		// </install>

		// <manual>
		public static void InstallManual(Harmony harmony)
		{
			var prefix = new HarmonyMethod(typeof(DecidePatch), nameof(DecidePatch.Before))
			{
				innerMethod = new InnerMethod(AccessTools.Method(typeof(Helper), nameof(Helper.Decide)))
			};
			harmony.CreateProcessor(AccessTools.Method(typeof(Outer), nameof(Outer.Run))).AddInnerPrefix(prefix).Patch();
		}
		// </manual>
	}

	// <capture>
	public static class Prices
	{
		[MethodImpl(MethodImplOptions.NoInlining)]
		public static int Lookup(string item) => item == "apple" ? 2 : 5;
	}

	public static class Receipt
	{
		[MethodImpl(MethodImplOptions.NoInlining)]
		public static string Line(string item, int quantity) => Format(item, Prices.Lookup(item) * quantity);

		[MethodImpl(MethodImplOptions.NoInlining)]
		public static string Format(string item, int total) => item + ": " + total;
	}

	[HarmonyPatch(typeof(Receipt), nameof(Receipt.Line))]
	public static class UnitPricePatch
	{
		[HarmonyPostfix, HarmonyInfix(typeof(Prices), nameof(Prices.Lookup), typeof(string))]
		static void RememberPrice(int __result, [HarmonyOuter] out int __var_unitPrice)
			=> __var_unitPrice = __result;

		[HarmonyPostfix, HarmonyInfix(typeof(Receipt), nameof(Receipt.Format), typeof(string), typeof(int))]
		static void ShowUnitPrice(ref string __result, [HarmonyOuter] int quantity, [HarmonyOuter] int __var_unitPrice)
		{
			if (quantity > 1)
				__result += " (" + quantity + " x " + __var_unitPrice + ")";
		}
	}
	// </capture>

	// <finalizer>
	public static class Parser
	{
		[MethodImpl(MethodImplOptions.NoInlining)]
		public static int Parse(string text) => int.Parse(text);
	}

	public static class RecoveringOuter
	{
		[MethodImpl(MethodImplOptions.NoInlining)]
		public static int Total(string text) => 5 + Parser.Parse(text);
	}

	[HarmonyPatch(typeof(RecoveringOuter), nameof(RecoveringOuter.Total))]
	public static class RecoverPatch
	{
		[HarmonyFinalizer, HarmonyInfix(typeof(Parser), nameof(Parser.Parse), typeof(string))]
		static Exception Recover(Exception __exception, ref int __result)
		{
			if (__exception is not FormatException) return __exception;
			__result = 0;
			return null;
		}
	}
	// </finalizer>

	// <generated>
	public static class Sequence
	{
		public static IEnumerable<int> Count(int limit)
		{
			while (limit > 0) yield return Visit(limit--);
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		public static int Visit(int value) => value;
	}

	[HarmonyPatch(typeof(Sequence), nameof(Sequence.Count))]
	public static class SequencePatch
	{
		[HarmonyPrefix, HarmonyInfix(typeof(Sequence), nameof(Sequence.Visit), typeof(int), OuterBody = InfixOuterBody.Auto)]
		static void LimitRemaining(
			[HarmonyOuter, HarmonyArgument("limit", ArgumentMode.Captured)] ref int remaining)
			=> remaining = Math.Min(remaining, 1);
	}
	// </generated>

	// <persistent>
	[HarmonyPatch(typeof(Sequence), nameof(Sequence.Count))]
	public static class SequencePersistentPatch
	{
		[HarmonyPrefix, HarmonyInfix(typeof(Sequence), nameof(Sequence.Visit), typeof(int), OuterBody = InfixOuterBody.Auto)]
		static void NumberVisit(ref int value,
			[HarmonyOuter, HarmonyArgument("visits", ArgumentMode.Persistent)] ref int visits)
			=> value = ++visits;
	}
	// </persistent>
}
