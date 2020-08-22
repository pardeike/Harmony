using HarmonyLib;
using System;
using System.Runtime.CompilerServices;

namespace HarmonyLibTests.Assets
{
	public class CombinedPatchClass
	{
		[MethodImpl(MethodImplOptions.NoInlining)]
		public void Method1()
		{
			throw new Exception();
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		public string Method2(string str)
		{
			return str;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		public void Method3(int n)
		{
			throw new Exception("" + n);
		}
	}

	[HarmonyPatch]
	public class CombinedPatchClass_Patch_1
	{
		public static int counter = 0;

		[HarmonyFinalizer]
		[HarmonyPatch(typeof(CombinedPatchClass), "Method1")]
		static bool Prefix1()
		{
			counter++;
			return false;
		}

		[HarmonyFinalizer]
		[HarmonyPatch(typeof(CombinedPatchClass), "Method2")]
		static void Postfix(ref string __result)
		{
			counter++;
			__result = "tested";
		}

		[HarmonyFinalizer]
		[HarmonyPatch(typeof(CombinedPatchClass), "Method3")]
		static void Finalizer3()
		{
			counter++;
		}

		[HarmonyFinalizer]
		[HarmonyPatch(typeof(CombinedPatchClass), "Method3")]
		static Exception Finalizer4()
		{
			counter++;
			return null;
		}
	}
}
