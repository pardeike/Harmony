using HarmonyLib;
using NUnit.Framework;

namespace HarmonyLibTests.Patching
{
	[TestFixture, NonParallelizable]
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
		public void Test_StructInstanceNoRef()
		{
			var originalClass = typeof(Assets.Struct2NoRef);
			Assert.NotNull(originalClass);
			var originalMethod = originalClass.GetMethod("TestMethod");
			Assert.NotNull(originalMethod);

			var patchClass = typeof(Assets.Struct2NoRefObjectPatch);

			Assert.NotNull(patchClass);
			var postfix = patchClass.GetMethod("Postfix");
			Assert.NotNull(postfix);

			var harmonyInstance = new Harmony("test");
			Assert.NotNull(harmonyInstance);

			var result = harmonyInstance.Patch(originalMethod, null, new HarmonyMethod(postfix));
			Assert.NotNull(result);

			var instance = new Assets.Struct2NoRef() { s = "before" };
			Assets.Struct2NoRefObjectPatch.s = "";
			instance.TestMethod("original");
			Assert.AreEqual("original", Assets.Struct2NoRefObjectPatch.s);
		}

		[Test]
		public void Test_StructInstanceByRef()
		{
			var originalClass = typeof(Assets.Struct2Ref);
			Assert.NotNull(originalClass);
			var originalMethod = originalClass.GetMethod("TestMethod");
			Assert.NotNull(originalMethod);

			var patchClass = typeof(Assets.Struct2RefPatch);

			Assert.NotNull(patchClass);
			var postfix = patchClass.GetMethod("Postfix");
			Assert.NotNull(postfix);

			var harmonyInstance = new Harmony("test");
			Assert.NotNull(harmonyInstance);

			var result = harmonyInstance.Patch(originalMethod, null, new HarmonyMethod(postfix));
			Assert.NotNull(result);

			var instance = new Assets.Struct2Ref() { s = "before" };
			instance.TestMethod("original");
			Assert.AreEqual("patched", instance.s);
		}

		[Test]
		public void Test_StructInstanceNoRefObject()
		{
			var originalClass = typeof(Assets.Struct3NoRefObject);
			Assert.NotNull(originalClass);
			var originalMethod = originalClass.GetMethod("TestMethod");
			Assert.NotNull(originalMethod);

			var patchClass = typeof(Assets.Struct3NoRefObjectPatch);

			Assert.NotNull(patchClass);
			var postfix = patchClass.GetMethod("Postfix");
			Assert.NotNull(postfix);

			var harmonyInstance = new Harmony("test");
			Assert.NotNull(harmonyInstance);

			var result = harmonyInstance.Patch(originalMethod, null, new HarmonyMethod(postfix));
			Assert.NotNull(result);

			var instance = new Assets.Struct3NoRefObject() { s = "before" };
			Assets.Struct3NoRefObjectPatch.s = "";
			instance.TestMethod("original");
			Assert.AreEqual("original", Assets.Struct3NoRefObjectPatch.s);
		}

		[Test]
		public void Test_StructInstanceByRefObject()
		{
			var originalClass = typeof(Assets.Struct3RefObject);
			Assert.NotNull(originalClass);
			var originalMethod = originalClass.GetMethod("TestMethod");
			Assert.NotNull(originalMethod);

			var patchClass = typeof(Assets.Struct3RefObjectPatch);

			Assert.NotNull(patchClass);
			var postfix = patchClass.GetMethod("Postfix");
			Assert.NotNull(postfix);

			var harmonyInstance = new Harmony("test");
			Assert.NotNull(harmonyInstance);

			var result = harmonyInstance.Patch(originalMethod, null, new HarmonyMethod(postfix));
			Assert.NotNull(result);

			var instance = new Assets.Struct3RefObject() { s = "before" };
			instance.TestMethod("original");
			Assert.AreEqual("patched", instance.s);
		}
	}
}
