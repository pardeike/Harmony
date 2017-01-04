using Harmony;
using System;

namespace HarmonyTests.Assets
{
	[HarmonyPatch]
	public class NoAttributesClass
	{
		[HarmonyPrepare]
		public void Method1() { }
	}

	[HarmonyPatch(typeof(string))]
	[HarmonyPatch("foobar")]
	[HarmonyPatch(new Type[] { typeof(float), typeof(string) })]
	public class AllAttributesClass
	{
		[HarmonyPrepare]
		public void Method1() { }

		[HarmonyPrefix]
		public void Method2() { }

		[HarmonyPostfix]
		public void Method3() { }
	}
}