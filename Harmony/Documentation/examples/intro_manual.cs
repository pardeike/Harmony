namespace Intro_Manual
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

			//
			var mOriginal = AccessTools.Method(typeof(SomeGameClass), "DoSomething");
			var mPrefix = SymbolExtensions.GetMethodInfo(() => MyPrefix());
			var mPostfix = SymbolExtensions.GetMethodInfo(() => MyPostfix());
			// in general, add null checks here (new HarmonyMethod() does it for you too)

			harmony.Patch(mOriginal, new HarmonyMethod(mPrefix), new HarmonyMethod(mPostfix));
		}

		public static void MyPrefix()
		{
			// ...
		}

		public static void MyPostfix()
		{
			// ...
		}
	}
	// </example>
}
