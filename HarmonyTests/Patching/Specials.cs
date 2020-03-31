using HarmonyLib;
using HarmonyLibTests.Assets;
using HarmonyLibTests.Assets.Methods;
using NUnit.Framework;
using System;

namespace HarmonyLibTests
{
	public static class TestLog
	{
		public static void Log(string str)
		{
			TestContext.Progress.WriteLine(str);
		}
	}

	[TestFixture]
	public class Specials
	{
		/* TODO - patching HttpWebRequest.GetResponse does not work
		 * 
		[Test]
		public void Test_HttpWebRequestGetResponse()
		{
			Assert.Ignore("Someone patching HttpWebRequest does not work");

			var t_WebRequest = typeof(HttpWebRequest);
			Assert.NotNull(t_WebRequest);
			var original = AccessTools.DeclaredMethod(t_WebRequest, nameof(HttpWebRequest.GetResponse));
			Assert.NotNull(original);

			var t_HttpWebRequestPatches = typeof(HttpWebRequestPatches);
			var prefix = t_HttpWebRequestPatches.GetMethod("Prefix");
			Assert.NotNull(prefix);
			var postfix = t_HttpWebRequestPatches.GetMethod("Postfix");
			Assert.NotNull(postfix);

			var instance = new Harmony("test");
			Assert.NotNull(instance);
			_ = instance.Patch(original, new HarmonyMethod(prefix, debug: true), new HarmonyMethod(postfix, debug: true));

			HttpWebRequestPatches.ResetTest();
			var request = WebRequest.Create("http://google.com");
			Assert.AreEqual(request.GetType(), t_WebRequest);
			var response = request.GetResponse();
			Assert.NotNull(response);
			Assert.True(HttpWebRequestPatches.prefixCalled, "Prefix not called");
			Assert.True(HttpWebRequestPatches.postfixCalled, "Postfix not called");
		}
		*/

		// TODO: this test might crash in certain environments
		[Test, Order(1000)]
		public void Test_Special_Case1()
		{
			TestLog.Log($"Test_Special_Case1 started");

			var instance = new Harmony("special-case-1");
			Assert.NotNull(instance, "instance");
			var processor = instance.CreateClassProcessor(typeof(ConcreteClass_Patch));
			Assert.NotNull(processor, "processor");

			TestLog.Log($"Patching ConcreteClass_Patch started");
			var replacements = processor.Patch();
			Assert.NotNull(replacements, "replacements");
			Assert.AreEqual(1, replacements.Count);
			TestLog.Log($"Patching ConcreteClass_Patch done");

			TestLog.Log($"Running patched ConcreteClass_Patch");
			var someStruct = new ConcreteClass().Method("test", new AnotherStruct());
			Assert.True(someStruct.acceptedInt);
			TestLog.Log($"Running patched ConcreteClass_Patch done");
		}

		// TODO: this test might crash in certain environments
		[Test, Order(1001)] public void Test_Returning_Structs_01_I() { Returning_Structs(01, false); }
		[Test, Order(1002)] public void Test_Returning_Structs_02_I() { Returning_Structs(02, false); }
		[Test, Order(1003)] public void Test_Returning_Structs_03_I() { Returning_Structs(03, false); }
		[Test, Order(1004)] public void Test_Returning_Structs_04_I() { Returning_Structs(04, false); }
		[Test, Order(1005)] public void Test_Returning_Structs_05_I() { Returning_Structs(05, false); }
		[Test, Order(1006)] public void Test_Returning_Structs_06_I() { Returning_Structs(06, false); }
		[Test, Order(1007)] public void Test_Returning_Structs_07_I() { Returning_Structs(07, false); }
		[Test, Order(1008)] public void Test_Returning_Structs_08_I() { Returning_Structs(08, false); }
		[Test, Order(1009)] public void Test_Returning_Structs_09_I() { Returning_Structs(09, false); }
		[Test, Order(1010)] public void Test_Returning_Structs_10_I() { Returning_Structs(10, false); }
		[Test, Order(1011)] public void Test_Returning_Structs_11_I() { Returning_Structs(11, false); }
		[Test, Order(1012)] public void Test_Returning_Structs_12_I() { Returning_Structs(12, false); }
		[Test, Order(1013)] public void Test_Returning_Structs_13_I() { Returning_Structs(13, false); }
		[Test, Order(1014)] public void Test_Returning_Structs_14_I() { Returning_Structs(14, false); }
		[Test, Order(1015)] public void Test_Returning_Structs_15_I() { Returning_Structs(15, false); }
		[Test, Order(1016)] public void Test_Returning_Structs_16_I() { Returning_Structs(16, false); }
		[Test, Order(1017)] public void Test_Returning_Structs_17_I() { Returning_Structs(17, false); }
		[Test, Order(1018)] public void Test_Returning_Structs_18_I() { Returning_Structs(18, false); }
		[Test, Order(1019)] public void Test_Returning_Structs_19_I() { Returning_Structs(19, false); }
		[Test, Order(1020)] public void Test_Returning_Structs_20_I() { Returning_Structs(20, false); }
		[Test, Order(1021)] public void Test_Returning_Structs_01_S() { Returning_Structs(01, true); }
		[Test, Order(1022)] public void Test_Returning_Structs_02_S() { Returning_Structs(02, true); }
		[Test, Order(1023)] public void Test_Returning_Structs_03_S() { Returning_Structs(03, true); }
		[Test, Order(1024)] public void Test_Returning_Structs_04_S() { Returning_Structs(04, true); }
		[Test, Order(1025)] public void Test_Returning_Structs_05_S() { Returning_Structs(05, true); }
		[Test, Order(1026)] public void Test_Returning_Structs_06_S() { Returning_Structs(06, true); }
		[Test, Order(1027)] public void Test_Returning_Structs_07_S() { Returning_Structs(07, true); }
		[Test, Order(1028)] public void Test_Returning_Structs_08_S() { Returning_Structs(08, true); }
		[Test, Order(1029)] public void Test_Returning_Structs_09_S() { Returning_Structs(09, true); }
		[Test, Order(1030)] public void Test_Returning_Structs_10_S() { Returning_Structs(10, true); }
		[Test, Order(1031)] public void Test_Returning_Structs_11_S() { Returning_Structs(11, true); }
		[Test, Order(1032)] public void Test_Returning_Structs_12_S() { Returning_Structs(12, true); }
		[Test, Order(1033)] public void Test_Returning_Structs_13_S() { Returning_Structs(13, true); }
		[Test, Order(1034)] public void Test_Returning_Structs_14_S() { Returning_Structs(14, true); }
		[Test, Order(1035)] public void Test_Returning_Structs_15_S() { Returning_Structs(15, true); }
		[Test, Order(1036)] public void Test_Returning_Structs_16_S() { Returning_Structs(16, true); }
		[Test, Order(1037)] public void Test_Returning_Structs_17_S() { Returning_Structs(17, true); }
		[Test, Order(1038)] public void Test_Returning_Structs_18_S() { Returning_Structs(18, true); }
		[Test, Order(1039)] public void Test_Returning_Structs_19_S() { Returning_Structs(19, true); }
		[Test, Order(1040)] public void Test_Returning_Structs_20_S() { Returning_Structs(20, true); }
		public void Returning_Structs(int n, bool useStatic)
		{
			TestLog.Log($"Test_Returning_Structs{n} started");

			var patchClass = typeof(ReturningStructs_Patch);
			Assert.NotNull(patchClass);

			var prefix = SymbolExtensions.GetMethodInfo(() => ReturningStructs_Patch.Prefix(null));
			Assert.NotNull(prefix);

			var instance = new Harmony("returning-structs");
			Assert.NotNull(instance);

			var cls = typeof(ReturningStructs);
			var name = $"{(useStatic ? "S" : "I")}M{n.ToString("D2")}";
			var method = AccessTools.DeclaredMethod(cls, name);
			Assert.NotNull(method, "method");

			TestLog.Log($"Test_Returning_Structs{n}, patching {name} started");
			try
			{
				var replacement = instance.Patch(method, new HarmonyMethod(prefix));
				Assert.NotNull(replacement, "replacement");
			}
			catch (Exception ex)
			{
				TestLog.Log($"Test_Returning_Structs{n}, patching {name} exception: {ex}");
			}
			TestLog.Log($"Test_Returning_Structs{n}, patching {name} done");

			var clsInstance = new ReturningStructs();
			try
			{
				var sn = n.ToString("D2");
				name = $"{(useStatic ? "S" : "I")}M{sn}";

				TestLog.Log($"Test_Returning_Structs{n}, running patched {name}");

				var original = AccessTools.DeclaredMethod(cls, name);
				Assert.NotNull(original, $"{name}: original");
				var result = original.Invoke(useStatic ? null : clsInstance, new object[] { "test" });
				Assert.NotNull(result, $"{name}: result");
				var resultType = result.GetType();
				Assert.AreEqual($"St{sn}", resultType.Name);
			}
			catch (Exception ex)
			{
				TestLog.Log($"Test_Returning_Structs{n}, running exception: {ex}");
			}
		}

		[Test]
		public void Test_PatchException()
		{
			var patchClass = typeof(DeadEndCode_Patch1);
			Assert.NotNull(patchClass);

			var instance = new Harmony("test");
			Assert.NotNull(instance);
			var patcher = instance.CreateClassProcessor(patchClass);
			Assert.NotNull(patcher);

			Exception exception = null;
			try
			{
				Assert.NotNull(patcher.Patch());
			}
			catch (Exception ex)
			{
				exception = ex;
			}
			Assert.NotNull(exception);
		}

		[Test]
		public void Test_PatchExceptionWithCleanup1()
		{
			if (AccessTools.IsMonoRuntime == false)
				return; // Assert.Ignore("Only mono allows for detailed IL exceptions. Test ignored.");

			var patchClass = typeof(DeadEndCode_Patch2);
			Assert.NotNull(patchClass);

			DeadEndCode_Patch2.original = null;
			DeadEndCode_Patch2.exception = null;

			var instance = new Harmony("test");
			Assert.NotNull(instance, "Harmony instance");
			var patcher = instance.CreateClassProcessor(patchClass);
			Assert.NotNull(patcher, "Patch processor");
			try
			{
				_ = patcher.Patch();
				Assert.Fail("Patch should throw exception");
			}
			catch (Exception)
			{
			}

			Assert.AreSame(typeof(DeadEndCode).GetMethod("Method"), DeadEndCode_Patch2.original, "Patch should save original method");
			Assert.NotNull(DeadEndCode_Patch2.exception, "Patch should save exception");

			var harmonyException = DeadEndCode_Patch2.exception as HarmonyException;
			Assert.NotNull(harmonyException, $"Exception should be a HarmonyException (is: {DeadEndCode_Patch2.exception.GetType()}");

			var instructions = harmonyException.GetInstructions();
			Assert.NotNull(instructions, "HarmonyException should have instructions");
			Assert.AreEqual(12, instructions.Count);

			var errorIndex = harmonyException.GetErrorIndex();
			Assert.AreEqual(10, errorIndex);

			var errorOffset = harmonyException.GetErrorOffset();
			Assert.AreEqual(50, errorOffset);
		}

		[Test]
		public void Test_PatchExceptionWithCleanup2()
		{
			if (AccessTools.IsMonoRuntime == false)
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
				Assert.IsInstanceOf(typeof(ArgumentException), ex.InnerException);
				Assert.AreEqual("Test", ex.InnerException.Message);
				return;
			}
			Assert.Fail("Patch should throw HarmonyException");
		}

		[Test]
		public void Test_PatchExceptionWithCleanup3()
		{
			if (AccessTools.IsMonoRuntime == false)
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
	}
}