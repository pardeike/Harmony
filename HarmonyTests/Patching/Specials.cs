extern alias mmc;

using HarmonyLib;
using HarmonyLibTests.Assets;
using HarmonyLibTests.Assets.Methods;
using mmc::MonoMod.Utils;
using NUnit.Framework;
using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Text;

namespace HarmonyLibTests.Patching
{
	[TestFixture, NonParallelizable]
	public class Specials : TestLogger
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
			StructReturnBuffer.ResetCaches();

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
				var result = original.Invoke(type == "S" ? null : clsInstance, new object[] { "test" });
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
			if (AccessTools.IsMonoRuntime is false)
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
				Assert.IsInstanceOf(typeof(ArgumentException), ex.InnerException);
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

		/* 
		These tests are really a pain, so for now they are disabled
		//
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
		//
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
		//
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
		//
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
		//
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
		//
		[Test]
		public void Test_NativeMethodPatchingSimple()
		{
			var res1 = NativeMethodPatchingSimple.AllocConsole();
			Assert.IsFalse(res1);

			var original = SymbolExtensions.GetMethodInfo(() => NativeMethodPatchingSimple.AllocConsole());
			var transpiler = SymbolExtensions.GetMethodInfo(() => NativeMethodPatchingSimple.Transpiler(default));

			var instance = new Harmony("test");
			NativeMethodPatchingSimple.instructions = null;
			var patched = instance.Patch(original, transpiler: new HarmonyMethod(transpiler));
			Assert.NotNull(patched);
			Assert.IsNotNull(NativeMethodPatchingSimple.instructions);
			Assert.AreEqual(2, NativeMethodPatchingSimple.instructions.Count);

			var res2 = NativeMethodPatchingSimple.AllocConsole();
			Assert.IsTrue(res2);
		}
		//
		[Test]
		public void Test_NativeMethodPatchingPostfix()
		{
			var patchClass = typeof(NativeMethodPatchingPostfix);
			Assert.NotNull(patchClass);

			var instance = new Harmony("test");
			Assert.NotNull(instance, "Harmony instance");
			var patcher = instance.CreateClassProcessor(patchClass);
			Assert.NotNull(patcher, "Patch processor");
			var patched = patcher.Patch();
			Assert.AreEqual(1, patched.Count);
			Assert.NotNull(patched[0]);

			var builder = new StringBuilder(256);
			var res = NativeMethodPatchingPostfix.gethostname(builder, 256);
			Assert.AreEqual(0, res);
			var host = builder.ToString();
			Console.WriteLine($"host={host}");
			Assert.IsTrue(host.Length > 0);
			Assert.IsTrue(host.EndsWith("-postfix"));
		}
		//
		[Test]
		public void Test_RunAllocConsole()
		{
			var method = SymbolExtensions.GetMethodInfo(() => NativeMethodPatchingSimple.AllocConsole());
			var att = method.GetCustomAttributes(false).OfType<DllImportAttribute>().FirstOrDefault();
			var name = att.Value;
			var asmname = new AssemblyName("Test");
#if NET35
			var dynamicAsm = AppDomain.CurrentDomain.DefineDynamicAssembly(asmname, AssemblyBuilderAccess.Run);
#else
			var dynamicAsm = AssemblyBuilder.DefineDynamicAssembly(asmname, AssemblyBuilderAccess.Run);
#endif

			var dynamicMod = dynamicAsm.DefineDynamicModule(asmname.Name);
			var tb = dynamicMod.DefineType("MyType", TypeAttributes.Public | TypeAttributes.UnicodeClass);
			var mb = tb.DefinePInvokeMethod(method.Name, name,
					  MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.PinvokeImpl,
					  CallingConventions.Standard, method.ReturnType, method.GetParameters().Select(x => x.ParameterType).ToArray(),
					  att.CallingConvention, att.CharSet
			);
			mb.SetImplementationFlags(mb.GetMethodImplementationFlags() | MethodImplAttributes.PreserveSig);
			var t = tb.CreateType();
			var proxyMethod = t.GetMethod(method.Name);

			var res = proxyMethod.Invoke(null, null);
			Console.WriteLine($"res = {res}");
		}
		*/
	}
}
