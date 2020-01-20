using HarmonyLib;
using NUnit.Framework;

namespace HarmonyLibTests
{
	[TestFixture]
	public class DynamicMethodPatches
	{
		[Test]
		public void ByRefResultPrefix()
		{
			var originalClass = typeof(Assets.Class11);
			Assert.IsNotNull(originalClass);

			var originalMethod = originalClass.GetMethod(nameof(Assets.Class11.TestMethod));
			Assert.IsNotNull(originalMethod);

			var patchClass = typeof(Assets.Class11Patch);
			Assert.IsNotNull(patchClass);

			var prefix = patchClass.GetMethod(nameof(Assets.Class11Patch.Prefix));
			Assert.IsNotNull(prefix);

			var harmonyInstance = new Harmony("test");
			Assert.IsNotNull(harmonyInstance);

			var patchResult = harmonyInstance.Patch(
				original: originalMethod,
				prefix: new HarmonyMethod(prefix));

			Assert.IsNotNull(patchResult);

			var instance = new Assets.Class11();
			var result = instance.TestMethod(0);

			Assert.IsFalse(instance.originalMethodRan);
			Assert.IsTrue(Assets.Class11Patch.prefixed);

			Assert.AreEqual("patched", result);
		}
	}
}