using HarmonyLib;
using NUnit.Framework;

namespace HarmonyLibTests.Patching
{
	[TestFixture, NonParallelizable]
	public class DynamicMethodPatches : TestLogger
	{
		[Test]
		public void Test_ByRefResultPrefix()
		{
			var originalClass = typeof(Assets.Class11);
			Assert.NotNull(originalClass);

			var originalMethod = originalClass.GetMethod(nameof(Assets.Class11.TestMethod));
			Assert.NotNull(originalMethod);

			var patchClass = typeof(Assets.Class11Patch);
			Assert.NotNull(patchClass);

			var prefix = patchClass.GetMethod(nameof(Assets.Class11Patch.Prefix));
			Assert.NotNull(prefix);

			var harmonyInstance = new Harmony("test");
			Assert.NotNull(harmonyInstance);

			var patchResult = harmonyInstance.Patch(
				original: originalMethod,
				prefix: new HarmonyMethod(prefix));

			Assert.NotNull(patchResult);

			var instance = new Assets.Class11();
			var result = instance.TestMethod(0);

			Assert.False(instance.originalMethodRan);
			Assert.True(Assets.Class11Patch.prefixed);

			Assert.AreEqual("patched", result);
		}
	}
}
