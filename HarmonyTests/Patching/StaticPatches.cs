﻿using Harmony;
using HarmonyTests.Assets;
using Microsoft.VisualStudio.TestTools.UnitTesting;
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
			PatchTools.GetPatches(typeof(Class1Patch), originalMethod, out prefixMethod, out postfixMethod, out transpilerMethod);

			Assert.AreSame(realPrefix, prefixMethod);
			Assert.AreSame(realPostfix, postfixMethod);
			Assert.AreSame(realTranspiler, transpilerMethod);

			var instance = HarmonyInstance.Create("test");
			Assert.IsNotNull(instance);

			var patcher = new PatchProcessor(instance, originalMethod, new HarmonyMethod(prefixMethod), new HarmonyMethod(postfixMethod), new HarmonyMethod(transpilerMethod));
			Assert.IsNotNull(patcher);

			patcher.Patch();
			Class1.Method1();

			Assert.IsTrue(Class1Patch.prefixed);
			Assert.IsTrue(Class1Patch.originalExecuted);
			Assert.IsTrue(Class1Patch.postfixed);
		}
	}
}