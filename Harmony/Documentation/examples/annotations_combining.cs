namespace Annotations_Combining
{
	// <example>
	using HarmonyLib;

	[HarmonyPatch(typeof(SomeType))]
	class MyPatches1
	{
		[HarmonyPostfix]
		[HarmonyPatch("SomeMethod1")]
		static void Postfix1() { }

		[HarmonyPostfix]
		[HarmonyPatch("SomeMethod2")]
		static void Postfix2() { }
	}

	[HarmonyPatch(typeof(TypeA))]
	class MyPatches2
	{
		[HarmonyPrefix]
		[HarmonyPatch("SomeMethod1")]
		static void Prefix1() { }

		[HarmonyPrefix]
		[HarmonyPatch("SomeMethod2")]
		static void Prefix2() { }

		[HarmonyPatch(typeof(TypeB), "SomeMethod1")]
		static void Postfix() { }
	}

	[HarmonyPatch]
	class MyPatches3
	{
		[HarmonyPatch(typeof(TypeA), "SomeMethod1")]
		static void Prefix() { }

		[HarmonyPatch(typeof(TypeB), "SomeMethod2")]
		static void Postfix() { }

		[HarmonyPatch(typeof(TypeC), "SomeMethod3")]
		static void Finalizer() { }
	}
	// </example>

	class SomeType { }
	class TypeA { }
	class TypeB { }
	class TypeC { }
}
