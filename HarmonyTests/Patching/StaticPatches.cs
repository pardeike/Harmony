using HarmonyLib;
using HarmonyLibTests.Assets;
using NUnit.Framework;
using System.Linq;

namespace HarmonyLibTests.Patching
{
	[TestFixture, NonParallelizable]
	public class StaticPatches : TestLogger
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

			var instance = new Harmony("unpatch-all-test");
			Assert.NotNull(instance);

			var patcher = instance.CreateProcessor(originalMethod);
			Assert.NotNull(patcher);
			_ = patcher.AddPrefix(prefix);
			_ = patcher.AddPostfix(postfix);
			_ = patcher.Patch();

			var instanceB = new Harmony("test");
			Assert.NotNull(instanceB);

			instanceB.UnpatchAll("unpatch-all-test");
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

		[Test]
		public void Test_Finalizer_Patch_Order()
		{
			var instance = new Harmony("test");
			Assert.NotNull(instance, "instance");
			var processor = instance.CreateClassProcessor(typeof(Finalizer_Patch_Order_Patch));
			Assert.NotNull(processor, "processor");

			var methods = processor.Patch();
			Assert.NotNull(methods, "methods");
			Assert.AreEqual(1, methods.Count);

			Finalizer_Patch_Order_Patch.ResetTest();
			var test = new Finalizer_Patch_Order_Class();
			Assert.NotNull(test, "test");
			var values = test.Method().ToList();

			Assert.NotNull(values, "values");
			Assert.AreEqual(3, values.Count);
			Assert.AreEqual(11, values[0]);
			Assert.AreEqual(21, values[1]);
			Assert.AreEqual(31, values[2]);

			// note that since passthrough postfixes are async, they run AFTER any finalizer
			//
			var actualEvents = Finalizer_Patch_Order_Patch.GetEvents();
			var correctEvents = new string[] {
				"Bool_Prefix",
				"Void_Prefix",
				"Simple_Postfix",
				"NonModifying_Finalizer",
				"ClearException_Finalizer",
				"Void_Finalizer",
				"Passthrough_Postfix2 start",
				"Passthrough_Postfix1 start",
				"Yield 10 [old=1]", "Yield 11 [old=10]",
				"Yield 20 [old=2]", "Yield 21 [old=20]",
				"Yield 30 [old=3]", "Yield 31 [old=30]",
				"Passthrough_Postfix1 end",
				"Passthrough_Postfix2 end"
			};

			Assert.True(actualEvents.SequenceEqual(correctEvents), "events");
		}

		[Test]
		public void Test_Affecting_Original_Prefixes()
		{
			var instance = new Harmony("test");
			Assert.NotNull(instance, "instance");
			var processor = instance.CreateClassProcessor(typeof(Affecting_Original_Prefixes_Patch));
			Assert.NotNull(processor, "processor");

			var methods = processor.Patch();
			Assert.NotNull(methods, "methods");
			Assert.AreEqual(1, methods.Count);

			Affecting_Original_Prefixes_Patch.ResetTest();
			var test = new Affecting_Original_Prefixes_Class();
			Assert.NotNull(test, "test");
			var value = test.Method(100);

			Assert.AreEqual("patched", value);

			// note that since passthrough postfixes are async, they run AFTER any finalizer
			//
			var events = Affecting_Original_Prefixes_Patch.GetEvents();
			Assert.AreEqual(4, events.Count, "event count");
			Assert.AreEqual("Prefix1", events[0]);
			Assert.AreEqual("Prefix2", events[1]);
			Assert.AreEqual("Prefix3", events[2]);
			Assert.AreEqual("Prefix5", events[3]);
		}

		[Test]
		public void Test_Class18()
		{
			var instance = new Harmony("test");
			Assert.NotNull(instance, "instance");
			var processor = instance.CreateClassProcessor(typeof(Class18Patch));
			Assert.NotNull(processor, "processor");

			var methods = processor.Patch();
			Assert.NotNull(methods, "methods");
			Assert.AreEqual(1, methods.Count);

			Class18Patch.prefixExecuted = false;
			var color = Class18.GetDefaultNameplateColor(new APIUser());

			Assert.IsTrue(Class18Patch.prefixExecuted, "prefixExecuted");
			Assert.AreEqual((float)1, color.r);
		}

		[Test]
		public void Test_Class22()
		{
			var instance = new Harmony("test");
			Assert.NotNull(instance, "instance");

			var processor = new PatchClassProcessor(instance, typeof(Class22));
			Assert.NotNull(processor, "processor");
			_ = processor.Patch();

			Class22.bool1 = null;
			Class22.bool2 = null;
			Class22.bool3 = null;
			Class22.bool4 = null;
			Class22.Method22();

			Assert.NotNull(Class22.bool1, "bool1");
			Assert.IsTrue(Class22.bool1.Value, "bool1.Value");

			Assert.NotNull(Class22.bool2, "bool2");
			Assert.IsTrue(Class22.bool2.Value, "bool2.Value");

			Assert.NotNull(Class22.bool3, "bool3");
			Assert.IsFalse(Class22.bool3.Value, "bool3.Value");

			Assert.NotNull(Class22.bool4, "bool3");
			Assert.IsFalse(Class22.bool4.Value, "bool4.Value");
		}

		[Test]
		public void Test_Class22b()
		{
			var instance = new Harmony("test");
			Assert.NotNull(instance, "instance");

			var processor = new PatchClassProcessor(instance, typeof(Class22b));
			Assert.NotNull(processor, "processor");
			_ = processor.Patch();

			Class22b.prefixResult = false;
			Class22b.originalExecuted = false;
			Class22b.runOriginalPre = null;
			Class22b.runOriginalPost = null;
			Class22b.Method22b();

			Assert.IsFalse(Class22b.originalExecuted, "originalExecuted 1");
			Assert.IsTrue(Class22b.runOriginalPre.Value, "runOriginalPre.Value 1");
			Assert.IsFalse(Class22b.runOriginalPost.Value, "runOriginalPre.Value 1");

			Class22b.prefixResult = true;
			Class22b.originalExecuted = false;
			Class22b.runOriginalPre = null;
			Class22b.runOriginalPost = null;
			Class22b.Method22b();

			Assert.IsTrue(Class22b.originalExecuted, "originalExecuted 2");
			Assert.IsTrue(Class22b.runOriginalPre.Value, "runOriginalPre.Value 2");
			Assert.IsTrue(Class22b.runOriginalPost.Value, "runOriginalPre.Value 2");
		}

		[Test]
		public void Test_Class23()
		{
			var instance = new Harmony("test");
			Assert.NotNull(instance, "instance");

			var processor = new PatchClassProcessor(instance, typeof(Class23));
			Assert.NotNull(processor, "processor");
			_ = processor.Patch();

			Class23.bool1 = null;
			Class23.Method23();

			Assert.NotNull(Class23.bool1, "bool1");
			Assert.IsTrue(Class23.bool1.Value, "bool1.Value");
		}

		[Test]
		public void Test_Class24()
		{
			var instance = new Harmony("test");
			Assert.NotNull(instance, "instance");

			var processor = new PatchClassProcessor(instance, typeof(Class24));
			Assert.NotNull(processor, "processor");
			_ = processor.Patch();

			Class24.bool1 = null;
			Class24.bool2 = null;
			Class24.Method24();

			Assert.NotNull(Class24.bool1, "bool1");
			Assert.IsTrue(Class24.bool1.Value, "bool1.Value");

			Assert.NotNull(Class24.bool2, "bool2");
			Assert.IsTrue(Class24.bool2.Value, "bool2.Value");
		}
	}
}
