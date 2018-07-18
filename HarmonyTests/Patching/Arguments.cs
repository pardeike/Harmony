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
	public class Arguments
	{
		[TestMethod]
		public void TestMethod6()
		{
			var originalClass = typeof(Class6);
			Assert.IsNotNull(originalClass);
			var originalMethod = originalClass.GetMethod("Method6");
			Assert.IsNotNull(originalMethod);

			var patchClass = typeof(Class6Patch);
			var realPrefix = patchClass.GetMethod("Prefix");
			Assert.IsNotNull(realPrefix);

			var instance6 = new Class6();

			MethodInfo prefixMethod;
			MethodInfo postfixMethod;
			MethodInfo transpilerMethod;
			PatchTools.GetPatches(typeof(Class6Patch), out prefixMethod, out postfixMethod, out transpilerMethod);

			Assert.AreSame(realPrefix, prefixMethod);

			var instance = HarmonyInstance.Create("test");
			Assert.IsNotNull(instance);

			var patcher = new PatchProcessor(instance, new List<MethodBase> { originalMethod }, new HarmonyMethod(prefixMethod), null);
			Assert.IsNotNull(patcher);

			patcher.Patch();

			instance6.someFloat = 999;
			instance6.someString = "original";
			instance6.someStruct = new Class6Struct() { d1 = 1, d2 = 2, d3 = 3 };
			var res = instance6.Method6();
			Assert.AreEqual(123, res.Item1);
			Assert.AreEqual("patched", res.Item2);
			Assert.AreEqual(10.0, res.Item3.d1);
		}

		[TestMethod]
		public void TestMethod7()
		{
			var originalClass = typeof(Class7);
			Assert.IsNotNull(originalClass);
			var originalMethod = originalClass.GetMethod("Method7");
			Assert.IsNotNull(originalMethod);

			var patchClass = typeof(Class7Patch);
			var realPrefix = patchClass.GetMethod("Prefix");
			Assert.IsNotNull(realPrefix);

			var instance6 = new Class6();

			MethodInfo prefixMethod;
			MethodInfo postfixMethod;
			MethodInfo transpilerMethod;
			PatchTools.GetPatches(typeof(Class7Patch), out prefixMethod, out postfixMethod, out transpilerMethod);

			Assert.AreSame(realPrefix, prefixMethod);

			var instance = HarmonyInstance.Create("test");
			Assert.IsNotNull(instance);

			var patcher = new PatchProcessor(instance, new List<MethodBase> { originalMethod }, new HarmonyMethod(prefixMethod), null);
			Assert.IsNotNull(patcher);

			patcher.Patch();

			var instance7 = new Class7();
			var result = instance7.Method7();

			Assert.AreEqual(1, result.a);
			Assert.AreEqual(2, result.b);
		}
	}
}