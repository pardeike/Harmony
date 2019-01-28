using Harmony;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HarmonyTests
{
	[TestClass]
	public class ValueTypes
	{
		[TestMethod]
		public void ValueTypeInstance()
		{
			var originalClass = typeof(Assets.Struct1);
			Assert.IsNotNull(originalClass);
			var originalMethod = originalClass.GetMethod("TestMethod");
			Assert.IsNotNull(originalMethod);

			var patchClass = typeof(Assets.Struct1Patch);

			Assert.IsNotNull(patchClass);
			var prefix = patchClass.GetMethod("Prefix");
			Assert.IsNotNull(prefix);

			Assert.IsNotNull(patchClass);
			var postfix = patchClass.GetMethod("Postfix");
			Assert.IsNotNull(postfix);

			var instance = new Assets.Struct1() { s = "before", n = 1 };

			var harmonyInstance = HarmonyInstance.Create("test");
			Assert.IsNotNull(harmonyInstance);

			var result = harmonyInstance.Patch(originalMethod, new HarmonyMethod(prefix), new HarmonyMethod(postfix));
			Assert.IsNotNull(result);

			Assets.Struct1.Reset();
			instance.TestMethod("new");
			Assert.AreEqual(2, instance.n);
			Assert.AreEqual("new", instance.s);
		}

		[TestMethod]
		public void StructInstanceByRef()
		{
			var originalClass = typeof(Assets.Struct2);
			Assert.IsNotNull(originalClass);
			var originalMethod = originalClass.GetMethod("TestMethod");
			Assert.IsNotNull(originalMethod);

			var patchClass = typeof(Assets.Struct2Patch);

			Assert.IsNotNull(patchClass);
			var postfix = patchClass.GetMethod("Postfix");
			Assert.IsNotNull(postfix);

			var harmonyInstance = HarmonyInstance.Create("test");
			Assert.IsNotNull(harmonyInstance);

			var result = harmonyInstance.Patch(originalMethod, null, new HarmonyMethod(postfix));
			Assert.IsNotNull(result);

			var instance = new Assets.Struct2() { s = "before" };
			instance.TestMethod("original");
			Assert.AreEqual("patched", instance.s);
		}
	}
}