using HarmonyLib;
using HarmonyLibTests.Assets;
using NUnit.Framework;

namespace HarmonyLibTests.Patching
{
	[TestFixture]
	public class Arguments : TestLogger
	{
		[Test]
		public void Test_Method6()
		{
			var originalClass = typeof(Class6);
			Assert.NotNull(originalClass);
			var originalMethod = originalClass.GetMethod("Method6");
			Assert.NotNull(originalMethod);

			var patchClass = typeof(Class6Patch);
			var prefix = patchClass.GetMethod("Prefix");
			Assert.NotNull(prefix);

			var instance = new Harmony("test");
			Assert.NotNull(instance);

			var patcher = instance.CreateProcessor(originalMethod);
			Assert.NotNull(patcher);
			_ = patcher.AddPrefix(prefix);
			Assert.NotNull(patcher);

			_ = patcher.Patch();

			var instance6 = new Class6
			{
				someFloat = 999,
				someString = "original",
				someStruct = new Class6Struct() { d1 = 1, d2 = 2, d3 = 3 }
			};
			var res = instance6.Method6();
			Assert.AreEqual(res[0], 123);
			Assert.AreEqual(res[1], "patched");
			Assert.AreEqual(((Class6Struct)res[2]).d1, 10.0);
		}

		[Test]
		public void Test_Method7()
		{
			var originalClass = typeof(Class7);
			Assert.NotNull(originalClass);
			var originalMethod = originalClass.GetMethod("Method7");
			Assert.NotNull(originalMethod);

			var patchClass = typeof(Class7Patch);
			var postfix = patchClass.GetMethod("Postfix");
			Assert.NotNull(postfix);

			var instance = new Harmony("test");
			Assert.NotNull(instance);

			var patcher = instance.CreateProcessor(originalMethod);
			Assert.NotNull(patcher);
			_ = patcher.AddPostfix(postfix);

			_ = patcher.Patch();

			var instance7 = new Class7();
			var result = instance7.Method7("parameter");

			Assert.AreEqual("parameter", instance7.state1);
			Assert.AreEqual(10, result.a);
			Assert.AreEqual(20, result.b);
		}

		[Test]
		public void Test_Method8()
		{
			var originalClass = typeof(Class8);
			Assert.NotNull(originalClass);
			var originalMethod = originalClass.GetMethod("Method8");
			Assert.NotNull(originalMethod);

			var patchClass = typeof(Class8Patch);
			var postfix = patchClass.GetMethod("Postfix");
			Assert.NotNull(postfix);

			var instance = new Harmony("test");
			Assert.NotNull(instance);

			var patcher = instance.CreateProcessor(originalMethod);
			Assert.NotNull(patcher);
			_ = patcher.AddPostfix(postfix);
			Assert.NotNull(patcher);

			_ = patcher.Patch();

			var result = Class8.Method8("patched");

			Assert.True(Class8.mainRun);
			Assert.AreEqual(10, result.a);
			Assert.AreEqual(20, result.b);
		}

		[Test]
		public void Test_InjectingBaseClassField()
		{
			var testInstance = new InjectFieldSubClass();
			testInstance.Method("foo");
			Assert.AreEqual("foo", testInstance.TestValue);

			var originalClass = testInstance.GetType();
			Assert.NotNull(originalClass);
			var originalMethod = originalClass.GetMethod("Method");
			Assert.NotNull(originalMethod);

			var patchClass = typeof(InjectFieldSubClass_Patch);
			var postfix = patchClass.GetMethod("Postfix");
			Assert.NotNull(postfix);

			var instance = new Harmony("test");
			Assert.NotNull(instance);

			var patcher = instance.CreateProcessor(originalMethod);
			Assert.NotNull(patcher);
			_ = patcher.AddPostfix(postfix);
			Assert.NotNull(patcher);

			_ = patcher.Patch();

			testInstance.Method("bar");
			Assert.AreEqual("patched", testInstance.TestValue);
		}

		[Test]
		public void Test_InjectBaseDelegateForClass()
		{
			var instance = new InjectDelegateClass() { pre = "{", post = "}" };
			instance.Method(123);
			Assert.AreEqual("[{test:123}]", instance.result);

			var harmony = new Harmony("test");
			var processor = new PatchClassProcessor(harmony, typeof(InjectDelegateClassPatch));
			var patches = processor.Patch();
			Assert.NotNull(patches, "patches");
			Assert.AreEqual(1, patches.Count);

			instance.Method(123);
			Assert.AreEqual("{patch:456} | [{patch:456}]", InjectDelegateClassPatch.result);
		}

		[Test]
		public void Test_InjectDelegateForStaticClass()
		{
			Assert.AreEqual("[1999]", InjectDelegateStaticClass.Method(999));

			var harmony = new Harmony("test");
			var processor = new PatchClassProcessor(harmony, typeof(InjectDelegateStaticClassPatch));
			var patches = processor.Patch();
			Assert.NotNull(patches, "patches");
			Assert.AreEqual(1, patches.Count);
			Assert.AreEqual("[123]/[456]", InjectDelegateStaticClass.Method(4444));
		}

		[Test]
		public void Test_InjectDelegateForValueType()
		{
			var instance = new InjectDelegateStruct() { pre = "{", post = "}" };
			Assert.AreEqual("{1999}", instance.Method(999));

			var harmony = new Harmony("test");
			var processor = new PatchClassProcessor(harmony, typeof(InjectDelegateStructPatch));
			var patches = processor.Patch();
			Assert.NotNull(patches, "patches");
			Assert.AreEqual(1, patches.Count);
			Assert.AreEqual("{123}/{456}", instance.Method(4444));
		}

		[Test]
		public void Test_RefResults()
		{
			var intRef1 = Class19.Method19();
			Assert.AreEqual(123, intRef1);

			var harmony = new Harmony("test");
			var processor = new PatchClassProcessor(harmony, typeof(Class19Patch));
			var patches = processor.Patch();
			Assert.NotNull(patches, "patches");
			Assert.AreEqual(1, patches.Count);

			var intRef2 = Class19.Method19();
			Assert.AreEqual(456, intRef2);
		}

		[Test]
		public void Test_BoxingValueResults()
		{
			var struct1 = Class20.Method20();
			Assert.AreEqual(123, struct1.value);

			var harmony = new Harmony("test");
			var processor = new PatchClassProcessor(harmony, typeof(Class20Patch));
			var patches = processor.Patch();
			Assert.NotNull(patches, "patches");
			Assert.AreEqual(1, patches.Count);

			_ = Class20.Method20();
			var result = (Class20.Struct20)Class20Patch.theResult;
			Assert.AreEqual(123, result.value);
		}

		[Test]
		public void Test_BoxingRefValueResults()
		{
			var struct1 = Class21.Method21();
			Assert.AreEqual(123, struct1.value);

			var harmony = new Harmony("test");
			var processor = new PatchClassProcessor(harmony, typeof(Class21Patch));
			var patches = processor.Patch();
			Assert.NotNull(patches, "patches");
			Assert.AreEqual(1, patches.Count);

			var result = Class21.Method21();
			Assert.AreEqual(456, result.value);
		}
	}
}
