using HarmonyLib;
using HarmonyLibTests.Assets;
using NUnit.Framework;
using System.Collections.Generic;

namespace HarmonyLibTests.Patching
{
	[TestFixture]
	public class CombinedPatches : TestLogger
	{
		[Test]
		public void Test_ManyFinalizers()
		{
			var originalClass = typeof(CombinedPatchClass);
			Assert.NotNull(originalClass);
			var patchClass = typeof(CombinedPatchClass_Patch_1);
			Assert.NotNull(patchClass);

			var harmonyInstance = new Harmony("test");
			Assert.NotNull(harmonyInstance);

			var processor = harmonyInstance.CreateClassProcessor(patchClass);
			Assert.NotNull(processor);
			Assert.NotNull(processor.Patch());

			CombinedPatchClass_Patch_1.counter = 0;
			var instance = new CombinedPatchClass();
			instance.Method1();
			Assert.AreEqual("tested", instance.Method2("test"));
			instance.Method3(123);
			Assert.AreEqual(4, CombinedPatchClass_Patch_1.counter);
		}

		[Test]
		public static void Test_Method11()
		{
			var originalClass = typeof(Class14);
			Assert.NotNull(originalClass);
			var patchClass = typeof(Class14Patch);
			Assert.NotNull(patchClass);

			var harmonyInstance = new Harmony("test");
			Assert.NotNull(harmonyInstance);

			var processor = harmonyInstance.CreateClassProcessor(patchClass);
			Assert.NotNull(processor);
			Assert.NotNull(processor.Patch());

			_ = new Class14().Test("Test1", new KeyValuePair<string, int>("1", 1));
			_ = new Class14().Test("Test2", new KeyValuePair<string, int>("1", 1), new KeyValuePair<string, int>("2", 2));

			Assert.AreEqual("Prefix0", Class14.state[0]);
			Assert.AreEqual("Test1", Class14.state[1]);
			Assert.AreEqual("Postfix0", Class14.state[2]);
			Assert.AreEqual("Prefix1", Class14.state[3]);
			Assert.AreEqual("Test2", Class14.state[4]);
			Assert.AreEqual("Postfix1", Class14.state[5]);
		}
	}
}
