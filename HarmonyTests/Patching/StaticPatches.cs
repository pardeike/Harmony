using HarmonyLib;
using HarmonyLibTests.Assets;
using NUnit.Framework;

namespace HarmonyLibTests
{
	[TestFixture]
	public class StaticPatches
	{
		[Test]
		public void Test_Method0()
		{
			var originalClass = typeof(Class0);
			Assert.NotNull(originalClass);
			var originalMethod = originalClass.GetMethod("Method0");
			Assert.NotNull(originalMethod);

			var patchClass = typeof(Class0Patch);
			var postfix = patchClass.GetMethod("Postfix");
			Assert.NotNull(postfix);

			var instance = new Harmony("test");
			Assert.NotNull(instance);

			var patcher = instance.CreateProcessor(originalMethod);
			Assert.NotNull(patcher);
			_ = patcher.AddPostfix(postfix);
			_ = patcher.Patch();

			var result = new Class0().Method0();
			Assert.AreEqual("patched", result);
		}

		[Test]
		public void Test_Method1()
		{
			var originalClass = typeof(Class1);
			Assert.NotNull(originalClass);
			var originalMethod = originalClass.GetMethod("Method1");
			Assert.NotNull(originalMethod);

			var patchClass = typeof(Class1Patch);
			var prefix = patchClass.GetMethod("Prefix");
			var postfix = patchClass.GetMethod("Postfix");
			var transpiler = patchClass.GetMethod("Transpiler");
			Assert.NotNull(prefix);
			Assert.NotNull(postfix);
			Assert.NotNull(transpiler);

			Class1Patch.ResetTest();

			var instance = new Harmony("test");
			Assert.NotNull(instance);

			var patcher = instance.CreateProcessor(originalMethod);
			Assert.NotNull(patcher);
			_ = patcher.AddPrefix(prefix);
			_ = patcher.AddPostfix(postfix);
			_ = patcher.AddTranspiler(transpiler);

			var originalMethodStartPre = Memory.GetMethodStart(originalMethod, out _);
			_ = patcher.Patch();
			var originalMethodStartPost = Memory.GetMethodStart(originalMethod, out _);
			Assert.AreEqual(originalMethodStartPre, originalMethodStartPost);

			Class1.Method1();
			Assert.True(Class1Patch.prefixed, "Prefix was not executed");
			Assert.True(Class1Patch.originalExecuted, "Original was not executed");
			Assert.True(Class1Patch.postfixed, "Postfix was not executed");
		}

		[Test]
		public void Test_Method2()
		{
			var originalClass = typeof(Class2);
			Assert.NotNull(originalClass);
			var originalMethod = originalClass.GetMethod("Method2");
			Assert.NotNull(originalMethod);

			var patchClass = typeof(Class2Patch);
			var prefix = patchClass.GetMethod("Prefix");
			var postfix = patchClass.GetMethod("Postfix");
			var transpiler = patchClass.GetMethod("Transpiler");
			Assert.NotNull(prefix);
			Assert.NotNull(postfix);
			Assert.NotNull(transpiler);

			Class2Patch.ResetTest();

			var instance = new Harmony("test");
			Assert.NotNull(instance);

			var patcher = instance.CreateProcessor(originalMethod);
			Assert.NotNull(patcher);
			_ = patcher.AddPrefix(prefix);
			_ = patcher.AddPostfix(postfix);
			_ = patcher.AddTranspiler(transpiler);

			var originalMethodStartPre = Memory.GetMethodStart(originalMethod, out _);
			_ = patcher.Patch();
			var originalMethodStartPost = Memory.GetMethodStart(originalMethod, out _);
			Assert.AreEqual(originalMethodStartPre, originalMethodStartPost);

			new Class2().Method2();
			Assert.True(Class2Patch.prefixed, "Prefix was not executed");
			Assert.True(Class2Patch.originalExecuted, "Original was not executed");
			Assert.True(Class2Patch.postfixed, "Postfix was not executed");
		}

		[Test]
		public void Test_Method4()
		{
			var originalClass = typeof(Class4);
			Assert.NotNull(originalClass);
			var originalMethod = originalClass.GetMethod("Method4");
			Assert.NotNull(originalMethod);

			var patchClass = typeof(Class4Patch);
			var prefix = patchClass.GetMethod("Prefix");
			Assert.NotNull(prefix);

			Class4Patch.ResetTest();

			var instance = new Harmony("test");
			Assert.NotNull(instance);

			var patcher = instance.CreateProcessor(originalMethod);
			Assert.NotNull(patcher);
			_ = patcher.AddPrefix(prefix);

			var originalMethodStartPre = Memory.GetMethodStart(originalMethod, out _);
			_ = patcher.Patch();
			var originalMethodStartPost = Memory.GetMethodStart(originalMethod, out _);
			Assert.AreEqual(originalMethodStartPre, originalMethodStartPost);

			(new Class4()).Method4("foo");
			Assert.True(Class4Patch.prefixed, "Prefix was not executed");
			Assert.True(Class4Patch.originalExecuted, "Original was not executed");
			Assert.AreEqual(Class4Patch.senderValue, "foo");
		}

		[Test]
		public void Test_Method5()
		{
			var originalClass = typeof(Class5);
			Assert.NotNull(originalClass);
			var originalMethod = originalClass.GetMethod("Method5");
			Assert.NotNull(originalMethod);

			var patchClass = typeof(Class5Patch);
			var prefix = patchClass.GetMethod("Prefix");
			Assert.NotNull(prefix);
			var postfix = patchClass.GetMethod("Postfix");
			Assert.NotNull(postfix);

			Class5Patch.ResetTest();

			var instance = new Harmony("test");
			Assert.NotNull(instance);

			var patcher = instance.CreateProcessor(originalMethod);
			Assert.NotNull(patcher);
			_ = patcher.AddPrefix(prefix);
			_ = patcher.AddPostfix(postfix);
			_ = patcher.Patch();

			(new Class5()).Method5("foo");
			Assert.True(Class5Patch.prefixed, "Prefix was not executed");
			Assert.True(Class5Patch.postfixed, "Postfix was not executed");
		}

		[Test]
		public void Test_PatchUnpatch()
		{
			var originalClass = typeof(Class9);
			Assert.NotNull(originalClass);
			var originalMethod = originalClass.GetMethod("ToString");
			Assert.NotNull(originalMethod);

			var patchClass = typeof(Class9Patch);
			var prefix = patchClass.GetMethod("Prefix");
			Assert.NotNull(prefix);
			var postfix = patchClass.GetMethod("Postfix");
			Assert.NotNull(postfix);

			var instance = new Harmony("test");
			Assert.NotNull(instance);

			var patcher = instance.CreateProcessor(originalMethod);
			Assert.NotNull(patcher);
			_ = patcher.AddPrefix(prefix);
			_ = patcher.AddPostfix(postfix);
			_ = patcher.Patch();

			var instanceB = new Harmony("test");
			Assert.NotNull(instanceB);

			instanceB.UnpatchAll("test");
		}

		[Test]
		public void Test_Attributes()
		{
			var originalClass = typeof(AttributesClass);
			Assert.NotNull(originalClass);

			var originalMethod = originalClass.GetMethod("Method");
			Assert.NotNull(originalMethod);
			Assert.AreEqual(originalMethod, AttributesPatch.Patch0());

			var instance = new Harmony("test");
			Assert.NotNull(instance);

			var patchClass = typeof(AttributesPatch);
			Assert.NotNull(patchClass);

			AttributesPatch.ResetTest();

			var patcher = instance.CreateClassProcessor(patchClass);
			Assert.NotNull(patcher);
			Assert.NotNull(patcher.Patch());

			(new AttributesClass()).Method("foo");
			Assert.True(AttributesPatch.targeted, "TargetMethod was not executed");
			Assert.True(AttributesPatch.postfixed, "Prefix was not executed");
			Assert.True(AttributesPatch.postfixed, "Postfix was not executed");
		}

		[Test]
		public void Test_Method10()
		{
			var originalClass = typeof(Class10);
			Assert.NotNull(originalClass);
			var originalMethod = originalClass.GetMethod("Method10");
			Assert.NotNull(originalMethod);

			var patchClass = typeof(Class10Patch);
			var postfix = patchClass.GetMethod("Postfix");
			Assert.NotNull(postfix);

			var instance = new Harmony("test");
			Assert.NotNull(instance);

			var patcher = instance.CreateProcessor(originalMethod);
			Assert.NotNull(patcher);
			_ = patcher.AddPostfix(postfix);
			_ = patcher.Patch();

			_ = new Class10().Method10();
			Assert.True(Class10Patch.postfixed);
			Assert.True(Class10Patch.originalResult);
		}

		[Test]
		public void Test_MultiplePatches_One_Class()
		{
			var originalClass = typeof(MultiplePatches1);
			Assert.NotNull(originalClass);
			var originalMethod = originalClass.GetMethod("TestMethod");
			Assert.NotNull(originalMethod);

			var instance = new Harmony("test");
			Assert.NotNull(instance);
			var patchClass = typeof(MultiplePatches1Patch);
			Assert.NotNull(patchClass);

			MultiplePatches1.result = "before";

			var patcher = instance.CreateClassProcessor(patchClass);
			Assert.NotNull(patcher);
			Assert.NotNull(patcher.Patch());

			var result = (new MultiplePatches1()).TestMethod("after");
			Assert.AreEqual("after,prefix2,prefix1", MultiplePatches1.result);
			Assert.AreEqual("ok,postfix", result);
		}

		[Test]
		public void Test_MultiplePatches_Multiple_Classes()
		{
			var originalClass = typeof(MultiplePatches2);
			Assert.NotNull(originalClass);
			var originalMethod = originalClass.GetMethod("TestMethod");
			Assert.NotNull(originalMethod);

			var instance = new Harmony("test");
			Assert.NotNull(instance);
			var patchClass1 = typeof(MultiplePatchesPatch2_Part1);
			Assert.NotNull(patchClass1);
			var patchClass2 = typeof(MultiplePatchesPatch2_Part2);
			Assert.NotNull(patchClass2);
			var patchClass3 = typeof(MultiplePatchesPatch2_Part3);
			Assert.NotNull(patchClass3);

			MultiplePatches2.result = "before";

			var patcher1 = instance.CreateClassProcessor(patchClass1);
			Assert.NotNull(patcher1);
			Assert.NotNull(patcher1.Patch());
			var patcher2 = instance.CreateClassProcessor(patchClass2);
			Assert.NotNull(patcher2);
			Assert.NotNull(patcher2.Patch());
			var patcher3 = instance.CreateClassProcessor(patchClass3);
			Assert.NotNull(patcher3);
			Assert.NotNull(patcher3.Patch());

			var result = (new MultiplePatches2()).TestMethod("hey");
			Assert.AreEqual("hey,prefix2,prefix1", MultiplePatches2.result);
			Assert.AreEqual("patched", result);
		}
	}
}