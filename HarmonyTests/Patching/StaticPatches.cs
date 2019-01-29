using Harmony;
using HarmonyTests.Assets;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace HarmonyTests
{
	[TestClass]
	public class StaticPatches
	{
		[TestMethod]
		public void TestMethod0()
		{
			var originalClass = typeof(Class0);
			Assert.IsNotNull(originalClass);
			var originalMethod = originalClass.GetMethod("Method0");
			Assert.IsNotNull(originalMethod);

			var patchClass = typeof(Class0Patch);
			var postfix = patchClass.GetMethod("Postfix");
			Assert.IsNotNull(postfix);

			var instance = HarmonyInstance.Create("test");
			Assert.IsNotNull(instance);

			var patcher = new PatchProcessor(instance, new List<MethodBase> { originalMethod }, null, new HarmonyMethod(postfix), null);
			Assert.IsNotNull(patcher);
			patcher.Patch();

			var result = new Class0().Method0();
			Assert.AreEqual("patched", result);
		}

		[TestMethod]
		public void TestMethod1()
		{
			var originalClass = typeof(Class1);
			Assert.IsNotNull(originalClass);
			var originalMethod = originalClass.GetMethod("Method1");
			Assert.IsNotNull(originalMethod);

			var patchClass = typeof(Class1Patch);
			var prefix = patchClass.GetMethod("Prefix");
			var postfix = patchClass.GetMethod("Postfix");
			var transpiler = patchClass.GetMethod("Transpiler");
			Assert.IsNotNull(prefix);
			Assert.IsNotNull(postfix);
			Assert.IsNotNull(transpiler);

			Class1Patch._reset();

			var instance = HarmonyInstance.Create("test");
			Assert.IsNotNull(instance);

			var patcher = new PatchProcessor(instance, new List<MethodBase> { originalMethod }, new HarmonyMethod(prefix), new HarmonyMethod(postfix), new HarmonyMethod(transpiler));
			Assert.IsNotNull(patcher);

			var originalMethodStartPre = Memory.GetMethodStart(originalMethod, out var exception);
			patcher.Patch();
			var originalMethodStartPost = Memory.GetMethodStart(originalMethod, out exception);
			Assert.AreEqual(originalMethodStartPre, originalMethodStartPost);
			unsafe
			{
				var patchedCode = *(byte*)originalMethodStartPre;
				if (IntPtr.Size == sizeof(long))
					Assert.IsTrue(patchedCode == 0x48);
				else
					Assert.IsTrue(patchedCode == 0x68);
			}

			Class1.Method1();
			Assert.IsTrue(Class1Patch.prefixed, "Prefix was not executed");
			Assert.IsTrue(Class1Patch.originalExecuted, "Original was not executed");
			Assert.IsTrue(Class1Patch.postfixed, "Postfix was not executed");
		}

		[TestMethod]
		public void TestMethod2()
		{
			var originalClass = typeof(Class2);
			Assert.IsNotNull(originalClass);
			var originalMethod = originalClass.GetMethod("Method2");
			Assert.IsNotNull(originalMethod);

			var patchClass = typeof(Class2Patch);
			var prefix = patchClass.GetMethod("Prefix");
			var postfix = patchClass.GetMethod("Postfix");
			var transpiler = patchClass.GetMethod("Transpiler");
			Assert.IsNotNull(prefix);
			Assert.IsNotNull(postfix);
			Assert.IsNotNull(transpiler);

			Class2Patch._reset();

			var instance = HarmonyInstance.Create("test");
			Assert.IsNotNull(instance);

			var patcher = new PatchProcessor(instance, new List<MethodBase> { originalMethod }, new HarmonyMethod(prefix), new HarmonyMethod(postfix), new HarmonyMethod(transpiler));
			Assert.IsNotNull(patcher);

			var originalMethodStartPre = Memory.GetMethodStart(originalMethod, out var exception);
			patcher.Patch();
			var originalMethodStartPost = Memory.GetMethodStart(originalMethod, out exception);
			Assert.AreEqual(originalMethodStartPre, originalMethodStartPost);
			unsafe
			{
				var patchedCode = *(byte*)originalMethodStartPre;
				if (IntPtr.Size == sizeof(long))
					Assert.IsTrue(patchedCode == 0x48);
				else
					Assert.IsTrue(patchedCode == 0x68);
			}

			new Class2().Method2();
			Assert.IsTrue(Class2Patch.prefixed, "Prefix was not executed");
			Assert.IsTrue(Class2Patch.originalExecuted, "Original was not executed");
			Assert.IsTrue(Class2Patch.postfixed, "Postfix was not executed");
		}

		[TestMethod]
		public void TestMethod4()
		{
			var originalClass = typeof(Class4);
			Assert.IsNotNull(originalClass);
			var originalMethod = originalClass.GetMethod("Method4");
			Assert.IsNotNull(originalMethod);

			var patchClass = typeof(Class4Patch);
			var prefix = patchClass.GetMethod("Prefix");
			Assert.IsNotNull(prefix);

			Class4Patch._reset();

			var instance = HarmonyInstance.Create("test");
			Assert.IsNotNull(instance);

			var patcher = new PatchProcessor(instance, new List<MethodBase> { originalMethod }, new HarmonyMethod(prefix), null, null);
			Assert.IsNotNull(patcher);

			var originalMethodStartPre = Memory.GetMethodStart(originalMethod, out var exception);
			patcher.Patch();
			var originalMethodStartPost = Memory.GetMethodStart(originalMethod, out exception);
			Assert.AreEqual(originalMethodStartPre, originalMethodStartPost);
			unsafe
			{
				var patchedCode = *(byte*)originalMethodStartPre;
				if (IntPtr.Size == sizeof(long))
					Assert.IsTrue(patchedCode == 0x48);
				else
					Assert.IsTrue(patchedCode == 0x68);
			}

			(new Class4()).Method4("foo");
			Assert.IsTrue(Class4Patch.prefixed, "Prefix was not executed");
			Assert.IsTrue(Class4Patch.originalExecuted, "Original was not executed");
			Assert.AreEqual(Class4Patch.senderValue, "foo");
		}

		[TestMethod]
		public void TestMethod5()
		{
			var originalClass = typeof(Class5);
			Assert.IsNotNull(originalClass);
			var originalMethod = originalClass.GetMethod("Method5");
			Assert.IsNotNull(originalMethod);

			var patchClass = typeof(Class5Patch);
			var prefix = patchClass.GetMethod("Prefix");
			Assert.IsNotNull(prefix);
			var postfix = patchClass.GetMethod("Postfix");
			Assert.IsNotNull(postfix);

			Class5Patch._reset();

			var instance = HarmonyInstance.Create("test");
			Assert.IsNotNull(instance);

			var patcher = new PatchProcessor(instance, new List<MethodBase> { originalMethod }, new HarmonyMethod(prefix), new HarmonyMethod(postfix));
			Assert.IsNotNull(patcher);
			patcher.Patch();

			(new Class5()).Method5("foo");
			Assert.IsTrue(Class5Patch.prefixed, "Prefix was not executed");
			Assert.IsTrue(Class5Patch.postfixed, "Prefix was not executed");
		}

		[TestMethod]
		public void TestPatchUnpatch()
		{
			var originalClass = typeof(Class9);
			Assert.IsNotNull(originalClass);
			var originalMethod = originalClass.GetMethod("ToString");
			Assert.IsNotNull(originalMethod);

			var patchClass = typeof(Class9Patch);
			var prefix = patchClass.GetMethod("Prefix");
			Assert.IsNotNull(prefix);
			var postfix = patchClass.GetMethod("Postfix");
			Assert.IsNotNull(postfix);

			var instanceA = HarmonyInstance.Create("test");
			Assert.IsNotNull(instanceA);

			var patcher = new PatchProcessor(instanceA, new List<MethodBase> { originalMethod }, new HarmonyMethod(prefix), new HarmonyMethod(postfix));
			Assert.IsNotNull(patcher);
			patcher.Patch();

			var instanceB = HarmonyInstance.Create("test");
			Assert.IsNotNull(instanceB);

			instanceB.UnpatchAll("test");
		}
	}
}