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
			try
			{
			}
			catch (Exception)
			{
				throw;
			}
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		public string Method2(string str) => str;

		[MethodImpl(MethodImplOptions.NoInlining)]
		public void Method3(int n)
		{
			try
			{
			}
			catch (Exception)
			{
				throw;
			}
		}
	}

	[HarmonyPatch]
	public class CombinedPatchClass_Patch_1
	{
		public static int counter = 0;

		[HarmonyPrefix]
		[HarmonyPatch(typeof(CombinedPatchClass), "Method1")]
		static bool Prefix1()
		{
			counter += 1;
			return false;
		}

		[HarmonyPostfix]
		[HarmonyPatch(typeof(CombinedPatchClass), "Method2")]
		static void Postfix2(ref string __result)
		{
			counter += 10;
			__result = "tested";
		}

		[HarmonyFinalizer]
		[HarmonyPatch(typeof(CombinedPatchClass), "Method3")]
		static void Finalizer3() => counter += 100;

		[HarmonyFinalizer]
		[HarmonyPatch(typeof(CombinedPatchClass), "Method3")]
		static Exception Finalizer4()
		{
			counter += 1000;
			return null;
		}
	}
}
