using Harmony;
using HarmonyTests.Assets;
using Microsoft.VisualStudio.TestTools.UnitTesting;
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
			var prefix = patchClass.GetMethod("Prefix");
			Assert.IsNotNull(prefix);
			
			var instance = HarmonyInstance.Create("test");
			Assert.IsNotNull(instance);

			var patcher = new PatchProcessor(instance, new List<MethodBase> { originalMethod }, new HarmonyMethod(prefix), null);
			Assert.IsNotNull(patcher);

			patcher.Patch();

			var instance6 = new Class6
			{
				someFloat = 999,
				someString = "original",
				someStruct = new Class6Struct() { d1 = 1, d2 = 2, d3 = 3 }
			};
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
			var postfix = patchClass.GetMethod("Postfix");
			Assert.IsNotNull(postfix);
			
			var instance = HarmonyInstance.Create("test");
			Assert.IsNotNull(instance);

			var patcher = new PatchProcessor(instance, new List<MethodBase> { originalMethod }, null, new HarmonyMethod(postfix));
			Assert.IsNotNull(patcher);

			patcher.Patch();
			
			var instance7 = new Class7();
			var result = instance7.Method7("patched");
			
			Assert.IsTrue(instance7.mainRun);
			Assert.AreEqual(10, result.a);
			Assert.AreEqual(20, result.b);
		}

		[TestMethod]
		public void TestMethod8()
		{
			var originalClass = typeof(Class8);
			Assert.IsNotNull(originalClass);
			var originalMethod = originalClass.GetMethod("Method8");
			Assert.IsNotNull(originalMethod);

			var patchClass = typeof(Class8Patch);
			var postfix = patchClass.GetMethod("Postfix");
			Assert.IsNotNull(postfix);
			
			var instance = HarmonyInstance.Create("test");
			Assert.IsNotNull(instance);

			var patcher = new PatchProcessor(instance, new List<MethodBase> { originalMethod }, null, new HarmonyMethod(postfix));
			Assert.IsNotNull(patcher);

			patcher.Patch();
		
			var result = Class8.Method8("patched");

			Assert.IsTrue(Class8.mainRun);
			Assert.AreEqual(10, result.a);
			Assert.AreEqual(20, result.b);
		}
	}
}