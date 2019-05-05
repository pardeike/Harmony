using HarmonyLib;
using HarmonyLibTests.Assets;
using NUnit.Framework;

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace HarmonyLibTests
{
	public class ClassExceptionFilter
	{
		[MethodImpl(MethodImplOptions.NoInlining)]
		public string Method0()
		{
            try {
                throw new Exception("123456");
            } catch(Exception e) when(e.Message == "123456") {
            }

            return "original";
		}
	}

    public class ClassExceptionFilterPatch
	{
		public static void Postfix(ref string __result)
		{
			__result = "patched_exception_filter";
		}
	}

	[TestFixture]
	public class TestExceptionFilterBlock
	{
		[Test]
		public void TestMethodExceptionFilterBlock()
		{
            Harmony.DEBUG = true;
			var originalClass = typeof(ClassExceptionFilter);
			Assert.IsNotNull(originalClass);
			var originalMethod = originalClass.GetMethod("Method0");
			Assert.IsNotNull(originalMethod);

			var patchClass = typeof(ClassExceptionFilterPatch);
			var postfix = patchClass.GetMethod("Postfix");
			Assert.IsNotNull(postfix);

			var instance = new Harmony("test");
			Assert.IsNotNull(instance);

			var patcher = new PatchProcessor(instance, new List<MethodBase> { originalMethod }, null, new HarmonyMethod(postfix), null);
			Assert.IsNotNull(patcher);
			patcher.Patch();

			var result = new ClassExceptionFilter().Method0();
			Assert.AreEqual("patched_exception_filter", result);
		}

	}
}
