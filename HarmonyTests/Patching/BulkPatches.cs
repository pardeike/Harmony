using HarmonyLib;
using NUnit.Framework;

namespace HarmonyLibTests.Patching
{
	[TestFixture, NonParallelizable]
	public class BulkPatches : TestLogger
	{
		[Test]
		public void Test_HarmonyPatchAll()
		{
			var harmony = new Harmony("test");
			var processor = new PatchClassProcessor(harmony, typeof(Assets.BulkPatchClassPatch));
			Assets.BulkPatchClassPatch.transpileCount = 0;
			var patches = processor.Patch();
			Assert.NotNull(patches, "patches");
			Assert.AreEqual(3, patches.Count);
			Assert.AreEqual(3, Assets.BulkPatchClassPatch.transpileCount, "transpileCount");

			var instance = new Assets.BulkPatchClass();
			Assert.AreEqual("TEST1+", instance.Method1());
			Assert.AreEqual("TEST2+", instance.Method2());
		}
	}
}
