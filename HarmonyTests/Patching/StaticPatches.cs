using Harmony;
using Harmony.ILCopying;
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
		public void TestMethod1()
		{
			var originalClass = typeof(Class1);
			Assert.IsNotNull(originalClass);
			var originalMethod = originalClass.GetMethod("Method1");
			Assert.IsNotNull(originalMethod);

			var patchClass = typeof(Class1Patch);
			var realPrefix = patchClass.GetMethod("Prefix");
			var realPostfix = patchClass.GetMethod("Postfix");
			var realTranspiler = patchClass.GetMethod("Transpiler");
			Assert.IsNotNull(realPrefix);
			Assert.IsNotNull(realPostfix);
			Assert.IsNotNull(realTranspiler);

			Class1Patch._reset();

			MethodInfo prefixMethod;
			MethodInfo postfixMethod;
			MethodInfo transpilerMethod;
			PatchTools.GetPatches(typeof(Class1Patch), out prefixMethod, out postfixMethod, out transpilerMethod);

			Assert.AreSame(realPrefix, prefixMethod);
			Assert.AreSame(realPostfix, postfixMethod);
			Assert.AreSame(realTranspiler, transpilerMethod);

			var instance = HarmonyInstance.Create("test");
			Assert.IsNotNull(instance);

			var patcher = new PatchProcessor(instance, new List<MethodBase> { originalMethod }, new HarmonyMethod(prefixMethod), new HarmonyMethod(postfixMethod), new HarmonyMethod(transpilerMethod));
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
			var realPrefix = patchClass.GetMethod("Prefix");
			var realPostfix = patchClass.GetMethod("Postfix");
			var realTranspiler = patchClass.GetMethod("Transpiler");
			Assert.IsNotNull(realPrefix);
			Assert.IsNotNull(realPostfix);
			Assert.IsNotNull(realTranspiler);

			Class2Patch._reset();

			MethodInfo prefixMethod;
			MethodInfo postfixMethod;
			MethodInfo transpilerMethod;
			PatchTools.GetPatches(typeof(Class2Patch), out prefixMethod, out postfixMethod, out transpilerMethod);

			Assert.AreSame(realPrefix, prefixMethod);
			Assert.AreSame(realPostfix, postfixMethod);
			Assert.AreSame(realTranspiler, transpilerMethod);

			var instance = HarmonyInstance.Create("test");
			Assert.IsNotNull(instance);

			var patcher = new PatchProcessor(instance, new List<MethodBase> { originalMethod }, new HarmonyMethod(prefixMethod), new HarmonyMethod(postfixMethod), new HarmonyMethod(transpilerMethod));
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
			Assert.IsTrue(Class1Patch.prefixed, "Prefix was not executed");
			Assert.IsTrue(Class1Patch.originalExecuted, "Original was not executed");
			Assert.IsTrue(Class1Patch.postfixed, "Postfix was not executed");
		}

		[TestMethod]
		public void TestMethod4()
		{
			var originalClass = typeof(Class4);
			Assert.IsNotNull(originalClass);
			var originalMethod = originalClass.GetMethod("Method4");
			Assert.IsNotNull(originalMethod);

			var patchClass = typeof(Class4Patch);
			var realPrefix = patchClass.GetMethod("Prefix");
			Assert.IsNotNull(realPrefix);

			Class4Patch._reset();

			MethodInfo prefixMethod;
			MethodInfo postfixMethod;
			MethodInfo transpilerMethod;
			PatchTools.GetPatches(typeof(Class4Patch), out prefixMethod, out postfixMethod, out transpilerMethod);

			Assert.AreSame(realPrefix, prefixMethod);

			var instance = HarmonyInstance.Create("test");
			Assert.IsNotNull(instance);

			var patcher = new PatchProcessor(instance, new List<MethodBase> { originalMethod }, new HarmonyMethod(prefixMethod), null, null);
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
			var realPrefix = patchClass.GetMethod("Prefix");
			Assert.IsNotNull(realPrefix);
			var realPostfix = patchClass.GetMethod("Postfix");
			Assert.IsNotNull(realPostfix);

			Class5Patch._reset();

			var instance = HarmonyInstance.Create("test");
			Assert.IsNotNull(instance);

			MethodInfo prefixMethod;
			MethodInfo postfixMethod;
			MethodInfo transpilerMethod;
			PatchTools.GetPatches(typeof(Class5Patch), out prefixMethod, out postfixMethod, out transpilerMethod);

			var patcher = new PatchProcessor(instance, new List<MethodBase> { originalMethod }, new HarmonyMethod(prefixMethod), new HarmonyMethod(postfixMethod), null);
			Assert.IsNotNull(patcher);
			patcher.Patch();

			(new Class5()).Method5("foo");
			Assert.IsTrue(Class5Patch.prefixed, "Prefix was not executed");
			Assert.IsTrue(Class5Patch.postfixed, "Prefix was not executed");
		}
	}
}