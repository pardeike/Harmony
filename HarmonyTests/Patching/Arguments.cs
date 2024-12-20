using HarmonyLib;
using HarmonyLibTests.Assets;
using NUnit.Framework;
using System;

namespace HarmonyLibTests.Patching
{
	[TestFixture, NonParallelizable]
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
			Assert.AreEqual("abc", intRef1);

			var harmony = new Harmony("test");
			var processor = new PatchClassProcessor(harmony, typeof(Class19Patch));
			var patches = processor.Patch();
			Assert.NotNull(patches, "patches");
			Assert.AreEqual(1, patches.Count);

			var intRef2 = Class19.Method19();
			Assert.AreEqual("def", intRef2);
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

		[Test]
		public void Test_ArgumentCases()
		{
			var harmony = new Harmony("test");
			typeof(ArgumentOriginalMethods).GetMethods().Do(original =>
			{
				var name = original.Name;
				var i = name.IndexOf("_2_");
				if (i > 0)
				{
					var typeName = name.Substring(i + 3);
					var replacementName = $"To_{typeName}";
					var replacement = typeof(ArgumentPatchMethods).GetMethod(replacementName);
					Assert.NotNull(replacement, $"replacement '{replacementName}'");
					try
					{
						var result = harmony.Patch(original, new HarmonyMethod(replacement));
						Assert.NotNull(result, "result");
					}
					catch (Exception ex)
					{
						Assert.Fail($"Patching {original.Name} failed:\n{ex}");
					}
				}
			});

			var instance = new ArgumentOriginalMethods();
			ArgumentPatchMethods.Reset();

			var obj = new ArgumentTypes.Object();
			instance.Object_2_Object(obj);
			instance.Object_2_ObjectRef(obj);
			instance.ObjectRef_2_Object(ref obj);
			instance.ObjectRef_2_ObjectRef(ref obj);

			var val = new ArgumentTypes.Value() { n = 100 };
			instance.Value_2_Value(val);
			instance.Value_2_Boxing(val);
			instance.Value_2_ValueRef(val);
			Assert.AreEqual(100, val.n);
			instance.Value_2_BoxingRef(val);
			instance.ValueRef_2_Value(ref val);
			instance.ValueRef_2_Boxing(ref val);
			instance.ValueRef_2_ValueRef(ref val);
			Assert.AreEqual(101, val.n);
			instance.ValueRef_2_BoxingRef(ref val);
			Assert.AreEqual(102, val.n);

			Assert.AreEqual("OOOOVVVVVVVV", ArgumentPatchMethods.result);
		}

		[Test]
		public void Test_SimpleArgumentArrayUsage()
		{
			var harmony = new Harmony("test");
			var processor = new PatchClassProcessor(harmony, typeof(SimpleArgumentArrayUsagePatch));
			var patches = processor.Patch();
			Assert.NotNull(patches, "patches");
			Assert.AreEqual(1, patches.Count);

			SimpleArgumentArrayUsage.n = 0;
			SimpleArgumentArrayUsage.s = "";
			SimpleArgumentArrayUsage.st = new SimpleArgumentArrayUsage.SomeStruct() { n = 0 };
			SimpleArgumentArrayUsage.f = [];

			var instance = new SimpleArgumentArrayUsage();
			instance.Method(
				100,
				"original",
				new SimpleArgumentArrayUsage.SomeStruct() { n = 200 },
				[10f, 20f, 30f]
			);

			Assert.AreEqual(123, SimpleArgumentArrayUsage.n);
			Assert.AreEqual("patched", SimpleArgumentArrayUsage.s);
			Assert.AreEqual(456, SimpleArgumentArrayUsage.st.n);
			Assert.AreEqual(3, SimpleArgumentArrayUsage.f.Length);
			Assert.AreEqual(1.2f, SimpleArgumentArrayUsage.f[0]);
			Assert.AreEqual(3.4f, SimpleArgumentArrayUsage.f[1]);
			Assert.AreEqual(5.6f, SimpleArgumentArrayUsage.f[2]);
		}

		[Test]
		public void Test_ArrayArguments()
		{
			var harmony = new Harmony("test");
			var processor = new PatchClassProcessor(harmony, typeof(ArgumentArrayPatches));
			var patches = processor.Patch();
			Assert.NotNull(patches, "patches");
			Assert.AreEqual(1, patches.Count);

			ArgumentArrayPatches.prefixInput = null;
			ArgumentArrayPatches.postfixInput = null;

			var instance = new ArgumentArrayMethods();
			var n1 = 8;
			var n2 = 9;
			var s1 = "A";
			var s2 = "B";
			var st1 = new ArgumentArrayMethods.SomeStruct() { n = 8 };
			var st2 = new ArgumentArrayMethods.SomeStruct() { n = 9 };
			var f1 = new float[] { 8f };
			var f2 = new float[] { 9f };

			instance.Method(
				n1, ref n2, out var n3,
				s1, ref s2, out var s3,
				st1, ref st2, out var st3,
				f1, ref f2, out var f3
			);

			// prefix input
			var r = ArgumentArrayPatches.prefixInput;
			var i = 0;
			Assert.AreEqual(8, r[i], $"prefix[{i++}]");
			Assert.AreEqual(9, r[i], $"prefix[{i++}]");
			Assert.AreEqual(0, r[i], $"prefix[{i++}]");

			Assert.AreEqual("A", r[i], $"prefix[{i++}]");
			Assert.AreEqual("B", r[i], $"prefix[{i++}]");
			Assert.AreEqual(null, r[i], $"prefix[{i++}]");

			Assert.AreEqual(8, ((ArgumentArrayMethods.SomeStruct)r[i]).n, $"prefix[{i++}]");
			Assert.AreEqual(9, ((ArgumentArrayMethods.SomeStruct)r[i]).n, $"prefix[{i++}]");
			Assert.AreEqual(0, ((ArgumentArrayMethods.SomeStruct)r[i]).n, $"prefix[{i++}]");

			Assert.AreEqual(8f, ((float[])r[i])[0], $"prefix[{i++}]");
			Assert.AreEqual(9f, ((float[])r[i])[0], $"prefix[{i++}]");
			Assert.AreEqual(null, (float[])r[i], $"prefix[{i++}]");

			// postfix input
			r = ArgumentArrayPatches.postfixInput;
			i = 0;
			Assert.AreEqual(8, r[i], $"postfix[{i++}]");
			Assert.AreEqual(123, r[i], $"postfix[{i++}]");
			Assert.AreEqual(456, r[i], $"postfix[{i++}]");

			Assert.AreEqual("A", r[i], $"postfix[{i++}]");
			Assert.AreEqual("abc", r[i], $"postfix[{i++}]");
			Assert.AreEqual("def", r[i], $"postfix[{i++}]");

			Assert.AreEqual(8, ((ArgumentArrayMethods.SomeStruct)r[i]).n, $"postfix[{i++}]");
			Assert.AreEqual(123, ((ArgumentArrayMethods.SomeStruct)r[i]).n, $"postfix[{i++}]");
			Assert.AreEqual(456, ((ArgumentArrayMethods.SomeStruct)r[i]).n, $"postfix[{i++}]");

			Assert.AreEqual(8f, ((float[])r[i])[0], $"postfix[{i++}]");
			Assert.AreEqual(5.6f, ((float[])r[i])[2], $"postfix[{i++}]");
			Assert.AreEqual(6.5f, ((float[])r[i])[2], $"postfix[{i++}]");

			// method output values
			Assert.AreEqual(123, n2, "n2");
			Assert.AreEqual(456, n3, "n3");
			Assert.AreEqual("abc", s2, "s2");
			Assert.AreEqual("def", s3, "s3");
			Assert.AreEqual(123, st2.n, "st2");
			Assert.AreEqual(456, st3.n, "st3");
			Assert.AreEqual(5.6f, f2[2], "f2");
			Assert.AreEqual(6.5f, f3[2], "f3");
		}

		[Test]
		public void Test_RenamedArguments()
		{
			var harmony = new Harmony("test");
			var processor = new PatchClassProcessor(harmony, typeof(RenamedArgumentsPatch));
			var patches = processor.Patch();
			Assert.NotNull(patches, "patches");
			Assert.AreEqual(1, patches.Count);
			RenamedArgumentsPatch.log.Clear();
			new RenamedArguments().Method("test");
			var log = RenamedArgumentsPatch.log.Join();
			Assert.AreEqual("val1, patched, val2, hello", log);
		}

		[Test, Explicit("Crashes and throws NRE in some configurations: see https://discord.com/channels/131466550938042369/674571535570305060/1319451813975687269")]
		public void Test_NullableResults()
		{
			var res1 = new NullableResults().Method();
			Assert.True(res1.HasValue);
			Assert.False(res1.Value);

			var harmony = new Harmony("test");
			var processor = new PatchClassProcessor(harmony, typeof(NullableResultsPatch));
			var patches = processor.Patch();
			Assert.NotNull(patches, "patches");
			Assert.AreEqual(1, patches.Count);

			var res2 = new NullableResults().Method();
			Assert.True(res2.HasValue);
			Assert.True(res2.Value);
		}
	}
}
