namespace EdgeCases
{
	// <example>
	using HarmonyLib;
	using System;
	using System.Runtime.CompilerServices;

	[HarmonyPatch]
	class Patch
	{
		[HarmonyReversePatch]
		[HarmonyPatch(typeof(BaseClass), nameof(BaseClass.Method))]
		[MethodImpl(MethodImplOptions.NoInlining)]
		static string BaseMethodDummy(SubClass instance) => null;

		[HarmonyPatch(typeof(SubClass), nameof(SubClass.Method))]
		static void Prefix(SubClass __instance)
		{
			var str = BaseMethodDummy(__instance);
			Console.WriteLine(str);
		}
	}

	public class BaseClass
	{
		public virtual string Method() => "base";
	}

	public class SubClass : BaseClass
	{
		public override string Method() => "subclass";
	}
	// </example>

	class GameObject { }

	class UnityEngine
	{
		public static UnityEngineObject Object { get; set; }

		internal class SceneManagement
		{
			internal class Scene { }
			internal class LoadSceneMode { }
		}
	}

	class UnityEngineObject
	{
		internal void DontDestroyOnLoad(object gameObject) { }
	}

	class SceneManager
	{
		internal static Action<UnityEngine.SceneManagement.Scene, UnityEngine.SceneManagement.LoadSceneMode> sceneLoaded;
	}

#pragma warning disable CS0649
	// <early1>
	class SomeGameObject
	{
		GameObject gameObject;

		void SomeMethod() => UnityEngine.Object.DontDestroyOnLoad(gameObject);

		void SomeOtherMethod() => SomeMethod();
	}
	// </early1>
#pragma warning restore CS0649

	// <early2>
	public static class Patcher
	{
		private static bool patched = false;

		public static void Main() =>
			//DoPatch(); <-- Do not execute patching on assembly entry point
			SceneManager.sceneLoaded += SceneLoaded;

		private static void DoPatch()
		{
			var harmony = new Harmony("test");
			harmony.PatchAll();
			patched = true;
		}

		private static void SceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode loadSceneMode)
		{
			// Execute patching after unity has finished it's startup and loaded at least the first game scene
			if (!patched)
				DoPatch();
		}
	}
	// </early2>
}
