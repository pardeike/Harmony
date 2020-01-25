using HarmonyLib;
using HarmonyLibTests.Assets;
using NUnit.Framework;

namespace HarmonyLibTests
{
	[TestFixture]
	public class ReverseTranspiling
	{
		[Test]
		public void Test_ReverseTranspilerPatching()
		{
			var class0 = new Class0Reverse();

			var result1 = class0.Method("al-gin-Ori", 123);
			Assert.AreEqual("Original123Prolog", result1);

			var originalClass = typeof(Class0Reverse);
			Assert.IsNotNull(originalClass);
			var originalMethod = originalClass.GetMethod("Method");
			Assert.IsNotNull(originalMethod);

			var patchClass = typeof(Class0ReversePatch);
			var postfix = patchClass.GetMethod("Postfix");
			Assert.IsNotNull(postfix);

			//Harmony.DEBUG = true;
			var instance = new Harmony("test");
			Assert.IsNotNull(instance);

			var patcher = instance.CreateProcessor(originalMethod);
			_ = patcher.AddPostfix(new HarmonyMethod(postfix));
			_ = patcher.Patch();

			var standin = new HarmonyMethod(patchClass.GetMethod("StringOperation"));
			Assert.IsNotNull(standin);
			Assert.IsNotNull(standin.method);

			var reversePatcher = instance.CreateReversePatcher(originalMethod, standin);
			_ = reversePatcher.Patch();

			var result2 = class0.Method("al-gin-Ori", 456);
			Assert.AreEqual("EpilogOriginal", result2);
		}
	}
}