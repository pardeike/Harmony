using Harmony;
using HarmonyTests.Assets;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace HarmonyTests
{
	[TestFixture]
	public class FinalizerPatches
	{
		[Test]
		public void TestSimpleFinalizer()
		{
			var originalClass = typeof(SimpleFinalizerClass);
			Assert.IsNotNull(originalClass);
			var originalMethod = originalClass.GetMethod("Method");
			Assert.IsNotNull(originalMethod);

			var patchClass = typeof(SimpleFinalizerPatch);
			var finalizer = patchClass.GetMethod("Finalizer");
			Assert.IsNotNull(finalizer);

			var instance = HarmonyInstance.Create("test");
			Assert.IsNotNull(instance);
			var patcher = new PatchProcessor(instance, new List<MethodBase> { originalMethod }, null, null, null, new HarmonyMethod(finalizer));
			Assert.IsNotNull(patcher);
			patcher.Patch();

			new SimpleFinalizerClass().Method();
			Assert.IsTrue(SimpleFinalizerPatch.finalized);
		}
	}
}