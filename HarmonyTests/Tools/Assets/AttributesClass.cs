using Harmony;
using System;
using System.Collections.Generic;

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

		[HarmonyTargetMethod]
		public void Method2() { }

		[HarmonyPrefix]
		[HarmonyPriority(Priority.High)]
		public void Method3() { }

		[HarmonyPostfix]
		[HarmonyBefore("foo", "bar")]
		[HarmonyAfter("test")]
		public void Method4() { }
	}

	public class NoAnnotationsClass
	{
		[HarmonyPatch(typeof(List<string>), "TestMethod")]
		[HarmonyPatch(new Type[] { typeof(string), typeof(string), typeof(string) }, new ArgumentType[] { ArgumentType.Normal, ArgumentType.Ref, ArgumentType.Normal })]
		static void Patch() { }
	}
}