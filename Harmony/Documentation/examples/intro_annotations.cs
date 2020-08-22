namespace Intro_Annotations
{
	// extra-file: intro_somegame.cs

	// <example>
	// your code, most likely in your own dll

	using HarmonyLib;
	using Intro_SomeGame;

	public class MyPatcher
	{
		// make sure DoPatching() is called at start either by
		// the mod loader or by your injector

		public static void DoPatching()
		{
			var harmony = new Harmony("com.example.patch");
			harmony.PatchAll();
		}
	}

	[HarmonyPatch(typeof(SomeGameClass))]
	[HarmonyPatch("DoSomething")]
	class Patch01
	{
		static AccessTools.FieldRef<SomeGameClass, bool> isRunningRef =
			AccessTools.FieldRefAccess<SomeGameClass, bool>("isRunning");

		static bool Prefix(SomeGameClass __instance, ref int ___counter)
		{
			isRunningRef(__instance) = true;
			if (___counter > 100)
				return false;
			___counter = 0;
			return true;
		}

		static void Postfix(ref int __result)
		{
			__result *= 2;
		}
	}
	// </example>
}
