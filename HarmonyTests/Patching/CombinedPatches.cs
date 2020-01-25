using HarmonyLib;
using HarmonyLibTests.Assets;
using NUnit.Framework;

namespace HarmonyLibTests
{
	[TestFixture]
	public class CombinedPatches
	{
		[Test]
		public void Test_ManyFinalizers()
		{
			var originalClass = typeof(Assets.CombinedPatchClass);
			Assert.IsNotNull(originalClass);
			var patchClass = typeof(Assets.CombinedPatchClass_Patch_1);
			Assert.IsNotNull(patchClass);

			var harmonyInstance = new Harmony("test");
			Assert.IsNotNull(harmonyInstance);

			var processor = harmonyInstance.ProcessorForAnnotatedClass(patchClass);
			Assert.IsNotNull(processor);
			Assert.IsNotNull(processor.Patch());

			CombinedPatchClass_Patch_1.counter = 0;
			var instance = new CombinedPatchClass();
			instance.Method1();
			Assert.AreEqual("tested", instance.Method2("test"));
			instance.Method3(123);
			Assert.AreEqual(4, CombinedPatchClass_Patch_1.counter);
		}
	}
}