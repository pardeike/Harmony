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
			Assert.IsTrue(Class1Patch.prefixed);
			Assert.IsTrue(Class1Patch.originalExecuted);
			Assert.IsTrue(Class1Patch.postfixed);
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
			Assert.IsTrue(Class2Patch.prefixed);
			Assert.IsTrue(Class2Patch.originalExecuted);
			Assert.IsTrue(Class2Patch.postfixed);
		}
	}
}