namespace Patching_Infix
{
	using HarmonyLib;
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
}
