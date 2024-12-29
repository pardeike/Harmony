using HarmonyLib;
using HarmonyLibTests.Assets;
using HarmonyLibTests.Assets.Methods;
using NUnit.Framework;
using System;
#if NET6_0_OR_GREATER
using System.Net.Http;
using System.Reflection.Emit;
#else
using System.Net;
using System.Reflection.Emit;
#endif

namespace HarmonyLibTests.Patching
{
	[TestFixture, NonParallelizable]
	public class Specials : TestLogger
	{
		[Test]
		public void Test_HttpWebRequestGetResponse()
		{
#if NET6_0_OR_GREATER
			var original = SymbolExtensions.GetMethodInfo(() => new HttpClient().Send(default));
#else
			var t_WebRequest = typeof(HttpWebRequest);
			Assert.NotNull(t_WebRequest);
			var original = AccessTools.DeclaredMethod(t_WebRequest, nameof(HttpWebRequest.GetResponse));
#endif
			Assert.NotNull(original);

			var prefix = SymbolExtensions.GetMethodInfo(() => HttpWebRequestPatches.Prefix());
			var postfix = SymbolExtensions.GetMethodInfo(() => HttpWebRequestPatches.Postfix());

			var instance = new Harmony("test");
			Assert.NotNull(instance);
			_ = instance.Patch(original, new HarmonyMethod(prefix), new HarmonyMethod(postfix));

			HttpWebRequestPatches.ResetTest();

#if NET6_0_OR_GREATER
			var client = new HttpClient();
			var webRequest = new HttpRequestMessage(HttpMethod.Get, "http://google.com");
			var response = client.Send(webRequest);
#else
			var request = WebRequest.Create("http://google.com");
			Assert.AreEqual(request.GetType(), t_WebRequest);
			var response = request.GetResponse();
#endif

			Assert.NotNull(response);
			Assert.True(HttpWebRequestPatches.prefixCalled, "Prefix not called");
			Assert.True(HttpWebRequestPatches.postfixCalled, "Postfix not called");
		}

		[Test]
		public void Test_PatchResultRef()
		{
			ResultRefStruct.numbersPrefix = [0, 0];
			ResultRefStruct.numbersPostfix = [0, 0];
			ResultRefStruct.numbersPostfixWithNull = [0];
			ResultRefStruct.numbersFinalizer = [0];
			ResultRefStruct.numbersMixed = [0, 0];

			var test = new ResultRefStruct();

			var instance = new Harmony("result-ref-test");
			Assert.NotNull(instance);
			var processor = instance.CreateClassProcessor(typeof(ResultRefStruct_Patch));
			Assert.NotNull(processor, "processor");

			test.ToPrefix() = 1;
			test.ToPostfix() = 2;
			test.ToPostfixWithNull() = 3;
			test.ToMixed() = 5;

			Assert.AreEqual(new[] { 1, 0 }, ResultRefStruct.numbersPrefix);
			Assert.AreEqual(new[] { 2, 0 }, ResultRefStruct.numbersPostfix);
			Assert.AreEqual(new[] { 3 }, ResultRefStruct.numbersPostfixWithNull);
			_ = Assert.Throws<Exception>(() => test.ToFinalizer(), "ToFinalizer method does not throw");
			Assert.AreEqual(new[] { 5, 0 }, ResultRefStruct.numbersMixed);

			var replacements = processor.Patch();
			Assert.NotNull(replacements, "replacements");

			test.ToPrefix() = -1;
			test.ToPostfix() = -2;
			test.ToPostfixWithNull() = -3;
			test.ToFinalizer() = -4;
			test.ToMixed() = -5;

			Assert.AreEqual(new[] { 1, -1 }, ResultRefStruct.numbersPrefix);
			Assert.AreEqual(new[] { 2, -2 }, ResultRefStruct.numbersPostfix);
			Assert.AreEqual(new[] { -3 }, ResultRefStruct.numbersPostfixWithNull);
			Assert.AreEqual(new[] { -4 }, ResultRefStruct.numbersFinalizer);
			Assert.AreEqual(new[] { 42, -5 }, ResultRefStruct.numbersMixed);
		}

		[Test]
		public void Test_Patch_ConcreteClass()
		{
			var instance = new Harmony("special-case-1");
			Assert.NotNull(instance, "instance");
			var processor = instance.CreateClassProcessor(typeof(ConcreteClass_Patch));
			Assert.NotNull(processor, "processor");

			var someStruct1 = new ConcreteClass().Method("test", new AnotherStruct());
			Assert.True(someStruct1.accepted, "someStruct1.accepted");

			TestTools.Log($"Patching ConcreteClass_Patch start");
			var replacements = processor.Patch();
			Assert.NotNull(replacements, "replacements");
			Assert.AreEqual(1, replacements.Count);
			TestTools.Log($"Patching ConcreteClass_Patch done");

			TestTools.Log($"Running patched ConcreteClass_Patch start");
			var someStruct2 = new ConcreteClass().Method("test", new AnotherStruct());
			Assert.True(someStruct2.accepted, "someStruct2.accepted");
			TestTools.Log($"Running patched ConcreteClass_Patch done");
		}

		[Test, NonParallelizable]
		public void Test_Patch_Returning_Structs([Values(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20)] int n, [Values("I", "S")] string type)
		{
			var name = $"{type}M{n:D2}";

			var patchClass = typeof(ReturningStructs_Patch);
			Assert.NotNull(patchClass);

			var prefix = SymbolExtensions.GetMethodInfo(() => ReturningStructs_Patch.Prefix(null));
			Assert.NotNull(prefix);

			var instance = new Harmony("returning-structs");
			Assert.NotNull(instance);

			var cls = AccessTools.TypeByName($"HarmonyLibTests.Assets.Methods.ReturningStructs_{type}{n:D2}");
			Assert.NotNull(cls, "type");
			var method = AccessTools.DeclaredMethod(cls, name);
			Assert.NotNull(method, "method");

			TestTools.Log($"Test_Returning_Structs: patching {name} start");
			try
			{
				var replacement = instance.Patch(method, new HarmonyMethod(prefix));
				Assert.NotNull(replacement, "replacement");
			}
			catch (Exception ex)
			{
				TestTools.Log($"Test_Returning_Structs: patching {name} exception: {ex}");
			}
			TestTools.Log($"Test_Returning_Structs: patching {name} done");

			var clsInstance = Activator.CreateInstance(cls);
			try
			{
				TestTools.Log($"Test_Returning_Structs: running patched {name}");

				var original = AccessTools.DeclaredMethod(cls, name);
				Assert.NotNull(original, $"{name}: original");
				var result = original.Invoke(type == "S" ? null : clsInstance, ["test"]);
				Assert.NotNull(result, $"{name}: result");
				Assert.AreEqual($"St{n:D2}", result.GetType().Name);

				TestTools.Log($"Test_Returning_Structs: running patched {name} done");
			}
			catch (Exception ex)
			{
				TestTools.Log($"Test_Returning_Structs: running {name} exception: {ex}");
			}
		}

		[Test]
		public void Test_PatchException()
		{
			var test = new DeadEndCode();

			var instance = new Harmony("test");
			Assert.NotNull(instance);
			var original = AccessTools.Method(typeof(DeadEndCode), nameof(DeadEndCode.Method));
			Assert.NotNull(original);
			var prefix = AccessTools.Method(typeof(DeadEndCode_Patch1), nameof(DeadEndCode_Patch1.Prefix));
			Assert.NotNull(prefix);
			var postfix = AccessTools.Method(typeof(DeadEndCode_Patch1), nameof(DeadEndCode_Patch1.Postfix));
			Assert.NotNull(postfix);
			var prefixWithControl =
				AccessTools.Method(typeof(DeadEndCode_Patch1), nameof(DeadEndCode_Patch1.PrefixWithControl));
			Assert.NotNull(postfix);

			// run original
			try
			{
				_ = test.Method();
				Assert.Fail("expecting format exception");
			}
			catch (FormatException ex)
			{
				Assert.NotNull(ex);
			}

			// patch: +prefix
			var newMethod = instance.Patch(original, prefix: new HarmonyMethod(prefix));
			Assert.NotNull(newMethod);

			// run original with prefix
			DeadEndCode_Patch1.prefixCalled = false;
			try
			{
				_ = test.Method();
				Assert.Fail("expecting format exception");
			}
			catch (Exception ex)
			{
				Assert.NotNull(ex as FormatException);
			}
			Assert.True(DeadEndCode_Patch1.prefixCalled);


			var isMonoRuntime = AccessTools.IsMonoRuntime;
			var harmonyMethodPostfix = new HarmonyMethod(postfix);
			// patch: +postfix
			if (isMonoRuntime)
			{
				// mono should fail
				try
				{
					_ = instance.Patch(original, postfix: harmonyMethodPostfix);
					Assert.Fail("expecting patch exception");
				}
				catch (Exception ex)
				{
					Assert.NotNull(ex as InvalidProgramException);
				}
			}
			else
			{
				// non-mono can add postfix to methods ending in dead code

				_ = instance.Patch(original, postfix: harmonyMethodPostfix);
				DeadEndCode_Patch1.prefixCalled = false;
				DeadEndCode_Patch1.postfixCalled = false;
				// run original
				try
				{
					_ = test.Method();
					Assert.Fail("expecting format exception");
				}
				catch (FormatException ex)
				{
					Assert.NotNull(ex);
					Assert.True(DeadEndCode_Patch1.prefixCalled);
					Assert.False(DeadEndCode_Patch1.postfixCalled);
				}
			}

			_ = instance.Patch(original,
				prefix: new HarmonyMethod(prefixWithControl),
				postfix: isMonoRuntime ? harmonyMethodPostfix : null
			);
			DeadEndCode_Patch1.prefixCalled = false;
			DeadEndCode_Patch1.postfixCalled = false;
			_ = test.Method();
			Assert.True(DeadEndCode_Patch1.prefixCalled);
			Assert.True(DeadEndCode_Patch1.postfixCalled);
		}

		[Test]
		public void Test_PatchingLateThrow1()
		{
			var patchClass = typeof(LateThrowClass_Patch1);
			Assert.NotNull(patchClass);

			new LateThrowClass1().Method("AB");
			try
			{
				new LateThrowClass1().Method("");
				Assert.Fail("expecting exception");
			}
			catch (ArgumentException ex)
			{
				Assert.AreEqual(ex.Message, "fail");
			}

			var instance = new Harmony("test");
			Assert.NotNull(instance);
			var patcher = instance.CreateClassProcessor(patchClass);
			Assert.NotNull(patcher);
			Assert.NotNull(patcher.Patch());

			LateThrowClass_Patch1.prefixCalled = false;
			LateThrowClass_Patch1.postfixCalled = false;
			new LateThrowClass1().Method("AB");
			Assert.True(LateThrowClass_Patch1.prefixCalled);
			Assert.True(LateThrowClass_Patch1.postfixCalled);

			LateThrowClass_Patch1.prefixCalled = false;
			LateThrowClass_Patch1.postfixCalled = false;
			try
			{
				new LateThrowClass1().Method("");
				Assert.Fail("expecting exception");
			}
			catch (ArgumentException ex)
			{
				Assert.AreEqual(ex.Message, "fail");
			}
			Assert.True(LateThrowClass_Patch1.prefixCalled);
			Assert.False(LateThrowClass_Patch1.postfixCalled);

			LateThrowClass_Patch1.prefixCalled = false;
			LateThrowClass_Patch1.postfixCalled = false;
			new LateThrowClass1().Method("AB");
			Assert.True(LateThrowClass_Patch1.prefixCalled);
			Assert.True(LateThrowClass_Patch1.postfixCalled);
		}

		[Test]
		public void Test_PatchingLateThrow2()
		{
			var patchClass = typeof(LateThrowClass_Patch2);
			Assert.NotNull(patchClass);

			new LateThrowClass2().Method(0);

			var instance = new Harmony("test");
			Assert.NotNull(instance);
			var patcher = instance.CreateClassProcessor(patchClass);
			Assert.NotNull(patcher);
			Assert.NotNull(patcher.Patch());

			LateThrowClass_Patch2.prefixCalled = false;
			LateThrowClass_Patch2.postfixCalled = false;
			new LateThrowClass2().Method(0);
			Assert.True(LateThrowClass_Patch2.prefixCalled);
			Assert.True(LateThrowClass_Patch2.postfixCalled);
		}

		[Test]
		public void Test_PatchExceptionWithCleanup2()
		{
			if (AccessTools.IsMonoRuntime is false)
				return; // Assert.Ignore("Only mono allows for detailed IL exceptions. Test ignored.");

			var patchClass = typeof(DeadEndCode_Patch3);
			Assert.NotNull(patchClass);

			var instance = new Harmony("test");
			Assert.NotNull(instance, "Harmony instance");
			var patcher = instance.CreateClassProcessor(patchClass);
			Assert.NotNull(patcher, "Patch processor");
			try
			{
				_ = patcher.Patch();
			}
			catch (HarmonyException ex)
			{
				Assert.NotNull(ex.InnerException);
				Assert.IsInstanceOf<ArgumentException>(ex.InnerException);
				Assert.AreEqual("Test", ex.InnerException.Message);
				return;
			}
			Assert.Fail("Patch should throw HarmonyException");
		}

		[Test]
		public void Test_PatchExceptionWithCleanup3()
		{
			if (AccessTools.IsMonoRuntime is false)
				return; // Assert.Ignore("Only mono allows for detailed IL exceptions. Test ignored.");

			var patchClass = typeof(DeadEndCode_Patch4);
			Assert.NotNull(patchClass);

			var instance = new Harmony("test");
			Assert.NotNull(instance, "Harmony instance");
			var patcher = instance.CreateClassProcessor(patchClass);
			Assert.NotNull(patcher, "Patch processor");
			_ = patcher.Patch();
		}

		[Test]
		public void Test_PatchExternalMethod()
		{
			var patchClass = typeof(ExternalMethod_Patch);
			Assert.NotNull(patchClass);

			var instance = new Harmony("test");
			Assert.NotNull(instance, "Harmony instance");
			var patcher = instance.CreateClassProcessor(patchClass);
			Assert.NotNull(patcher, "Patch processor");
			_ = patcher.Patch();
		}

		[Test]
		public void Test_PatchEventHandler()
		{
			Console.WriteLine($"### EventHandlerTestClass TEST");

			var patchClass = typeof(EventHandlerTestClass_Patch);
			Assert.NotNull(patchClass);

			var instance = new Harmony("test");
			Assert.NotNull(instance, "Harmony instance");
			var patcher = instance.CreateClassProcessor(patchClass);
			Assert.NotNull(patcher, "Patch processor");
			var patched = patcher.Patch();
			Assert.AreEqual(1, patched.Count);
			Assert.NotNull(patched[0]);

			Console.WriteLine($"### EventHandlerTestClass BEFORE");
			new EventHandlerTestClass().Run();
			Console.WriteLine($"### EventHandlerTestClass AFTER");
		}

		[Test]
		public void Test_PatchMarshalledClass()
		{
			Console.WriteLine($"### MarshalledTestClass TEST");

			var patchClass = typeof(MarshalledTestClass_Patch);
			Assert.NotNull(patchClass);

			var instance = new Harmony("test");
			Assert.NotNull(instance, "Harmony instance");
			var patcher = instance.CreateClassProcessor(patchClass);
			Assert.NotNull(patcher, "Patch processor");
			var patched = patcher.Patch();
			Assert.AreEqual(1, patched.Count);
			Assert.NotNull(patched[0]);

			Console.WriteLine($"### MarshalledTestClass BEFORE");
			new MarshalledTestClass().Run();
			Console.WriteLine($"### MarshalledTestClass AFTER");
		}

		[Test]
		public void Test_MarshalledWithEventHandler1()
		{
			Console.WriteLine($"### MarshalledWithEventHandlerTest1 TEST");

			var patchClass = typeof(MarshalledWithEventHandlerTest1Class_Patch);
			Assert.NotNull(patchClass);

			var instance = new Harmony("test");
			Assert.NotNull(instance, "Harmony instance");
			var patcher = instance.CreateClassProcessor(patchClass);
			Assert.NotNull(patcher, "Patch processor");
			var patched = patcher.Patch();
			Assert.AreEqual(1, patched.Count);
			Assert.NotNull(patched[0]);

			Console.WriteLine($"### MarshalledWithEventHandlerTest1 BEFORE");
			new MarshalledWithEventHandlerTest1Class().Run();
			Console.WriteLine($"### MarshalledWithEventHandlerTest1 AFTER");
		}

		[Test]
		public void Test_MarshalledWithEventHandler2()
		{
			Console.WriteLine($"### MarshalledWithEventHandlerTest2 TEST");

			var patchClass = typeof(MarshalledWithEventHandlerTest2Class_Patch);
			Assert.NotNull(patchClass);

			var instance = new Harmony("test");
			Assert.NotNull(instance, "Harmony instance");
			var patcher = instance.CreateClassProcessor(patchClass);
			Assert.NotNull(patcher, "Patch processor");
			var patched = patcher.Patch();
			Assert.AreEqual(1, patched.Count);
			Assert.NotNull(patched[0]);

			Console.WriteLine($"### MarshalledWithEventHandlerTest2 BEFORE");
			new MarshalledWithEventHandlerTest2Class().Run();
			Console.WriteLine($"### MarshalledWithEventHandlerTest2 AFTER");
		}

		[Test]
		public void Test_CallClosure()
		{
			CodeInstruction.State.closureCache.Clear();
			var instance = new ClassTestingCallClosure
			{
				field1 = "test",
				field2 = "tobereplaced"
			};

			var code1 = instance.WIthoutContext();
			var action1 = code1.operand as DynamicMethod;
			Assert.NotNull(action1);
			var result = action1.Invoke(null, ["TEST"]);
			Assert.AreEqual(result, "[TEST]");
			Assert.AreEqual(CodeInstruction.State.closureCache.Count, 0);

			var code2 = instance.WithContext();
			Assert.AreEqual(instance.field1, "test");
			Assert.AreEqual(instance.field2, "tobereplaced");
			var action2 = code2.operand as DynamicMethod;
			Assert.NotNull(action2);
			_ = action2.Invoke(null, []);
			Assert.AreEqual(instance.field1, "test");
			Assert.AreEqual(instance.field2, "test");
			Assert.AreEqual(CodeInstruction.State.closureCache.Count, 1);

			_ = instance.WithContext();
			Assert.AreEqual(CodeInstruction.State.closureCache.Count, 2);
		}
	}
}
