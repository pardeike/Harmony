using HarmonyLib;
using HarmonyLibTests.Assets;
using NUnit.Framework;
using System;

namespace HarmonyLibTests.Patching
{
	public class MethodTypes : TestLogger
	{
		private void Patch(Type patchClass)
		{
			Assert.NotNull(patchClass);
			var instance = new Harmony("test");
			var patcher = instance.CreateClassProcessor(patchClass);
			var patched = patcher.Patch();
			Assert.AreEqual(1, patched.Count);
		}

		[Test] public void Test_MethodType_Normal() => Patch(typeof(MethodTypes_Normal_Patch));
		[Test] public void Test_MethodType_Getter() => Patch(typeof(MethodTypes_Getter_Patch));
		[Test] public void Test_MethodType_Setter() => Patch(typeof(MethodTypes_Setter_Patch));
	}
}