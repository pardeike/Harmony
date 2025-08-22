using HarmonyLib;
using NUnit.Framework;
using System.Runtime.CompilerServices;

namespace HarmonyLibTests.Patching
{
	[TestFixture, NonParallelizable]
	public class CategoryPatches : TestLogger
	{
		[Test]
		public void Test_HarmonyPatchAll()
		{
			var harmony = new Harmony("test");
			harmony.PatchCategory("CategoryA");

			Assert.AreEqual(2, Get1());
			Assert.AreEqual(false, GetTrue());
			Assert.AreEqual("Hello World", GetHelloWorld());
			Assert.AreEqual(18, Multiply(3, 6));


			harmony.PatchCategory("CategoryB");

			Assert.AreEqual(2, Get1());
			Assert.AreEqual(false, GetTrue());
			Assert.AreEqual("Hello World!", GetHelloWorld());
			Assert.AreEqual(36, Multiply(3, 6));

			harmony.UnpatchCategory("CategoryA");

			Assert.AreEqual(1, Get1());
			Assert.AreEqual(true, GetTrue());
			Assert.AreEqual("Hello World!", GetHelloWorld());
			Assert.AreEqual(36, Multiply(3, 6));

			harmony.UnpatchCategory("CategoryB");

			Assert.AreEqual(1, Get1());
			Assert.AreEqual(true, GetTrue());
			Assert.AreEqual("Hello World", GetHelloWorld());
			Assert.AreEqual(18, Multiply(3, 6));
		}
		[MethodImpl(MethodImplOptions.NoInlining)]
		public static int Get1() => 1;

		[MethodImpl(MethodImplOptions.NoInlining)]
		public static bool GetTrue() => true;

		[MethodImpl(MethodImplOptions.NoInlining)]
		public static string GetHelloWorld() => "Hello World";

		[MethodImpl(MethodImplOptions.NoInlining)]
		public static int Multiply(int a, int b) => a * b;

		[HarmonyPatch]
		[HarmonyPatch(typeof(CategoryPatches))]
		[HarmonyPatchCategory("CategoryA")]
		static class CategoryAPatches
		{
			[HarmonyPatch(nameof(Get1)), HarmonyPrefix]
			public static bool Get1Patch(ref int __result)
			{
				__result = 2;
				return false;
			}
			[HarmonyPatch(nameof(GetTrue)), HarmonyPostfix]
			public static void GetTruePatch(ref bool __result)
			{
				__result = false;
			}
		}

		[HarmonyPatch]
		[HarmonyPatchCategory("CategoryB")]
		static class CategoryBPatches
		{
			[HarmonyPatch(typeof(CategoryPatches), nameof(GetHelloWorld)), HarmonyPostfix]
			public static void GetHelloWorldPatch(ref string __result)
			{
				__result = __result + "!";
			}
			[HarmonyPatch(typeof(CategoryPatches), nameof(Multiply)), HarmonyPrefix]
			public static void Multiply(ref int a)
			{
				a *= 2;
			}
		}
	}
}
