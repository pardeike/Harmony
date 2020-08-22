using HarmonyLib;
using NUnit.Framework;

namespace HarmonyLibTests.Patching
{
	[TestFixture]
	public class ValueTypes : TestLogger
	{
		[Test]
		public void ValueTypeInstance()
		{
			var originalClass = typeof(Assets.Struct1);
			Assert.NotNull(originalClass);
			var originalMethod = originalClass.GetMethod("TestMethod");
			Assert.NotNull(originalMethod);

			var patchClass = typeof(Assets.Struct1Patch);

			Assert.NotNull(patchClass);
			var prefix = patchClass.GetMethod("Prefix");
			Assert.NotNull(prefix);

			Assert.NotNull(patchClass);
			var postfix = patchClass.GetMethod("Postfix");
			Assert.NotNull(postfix);

			var instance = new Assets.Struct1() { s = "before", n = 1 };

			var harmonyInstance = new Harmony("test");
			Assert.NotNull(harmonyInstance);

			var result = harmonyInstance.Patch(originalMethod, new HarmonyMethod(prefix), new HarmonyMethod(postfix));
			Assert.NotNull(result);

			Assets.Struct1.Reset();
			instance.TestMethod("new");
			Assert.AreEqual(2, instance.n);
			Assert.AreEqual("new", instance.s);
		}

		[Test]
		public void Test_StructInstanceByRef()
		{
			var originalClass = typeof(Assets.Struct2);
			Assert.NotNull(originalClass);
			var originalMethod = originalClass.GetMethod("TestMethod");
			Assert.NotNull(originalMethod);

			var patchClass = typeof(Assets.Struct2Patch);

			Assert.NotNull(patchClass);
			var postfix = patchClass.GetMethod("Postfix");
			Assert.NotNull(postfix);

			var harmonyInstance = new Harmony("test");
			Assert.NotNull(harmonyInstance);

			var result = harmonyInstance.Patch(originalMethod, null, new HarmonyMethod(postfix));
			Assert.NotNull(result);

			var instance = new Assets.Struct2() { s = "before" };
			instance.TestMethod("original");
			Assert.AreEqual("patched", instance.s);
		}
	}
}
