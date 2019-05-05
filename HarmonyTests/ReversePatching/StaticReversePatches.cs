using HarmonyLib;
using HarmonyLibTests.Assets;
using NUnit.Framework;

namespace HarmonyLibTests
{
	[TestFixture]
	public class StaticReversePatches
	{
		[Test]
		public void TestReversePatching()
		{
			var originalClass = typeof(Class0Reverse);
			Assert.IsNotNull(originalClass);
			var originalMethod = originalClass.GetMethod("Method");
			Assert.IsNotNull(originalMethod);

			var patchClass = typeof(Class0ReversePatch);
			var postfix = patchClass.GetMethod("Postfix");
			Assert.IsNotNull(postfix);

			var instance = new Harmony("test");
			Assert.IsNotNull(instance);

			var patcher = instance.CreateProcessor(originalMethod);
			patcher.AddPostfix(new HarmonyMethod(postfix));
			patcher.Patch();

			var standin = patchClass.GetMethod("StringOperation");
			Assert.IsNotNull(standin);

			var reversePatcher = instance.CreateReversePatcher(originalMethod, standin);
			reversePatcher.Patch();

			var class0 = new Class0Reverse();

			var result1 = class0.Method("al-gin-Ori", 123);
			Assert.AreEqual("Original123Prolog", result1);

			var result2 = class0.Method("al-gin-Ori", 456);
			Assert.AreEqual("EpilogOriginal", result2);
		}
	}
}