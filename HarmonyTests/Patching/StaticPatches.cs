using Harmony;
using HarmonyTests.Assets;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Reflection;

namespace HarmonyTests
{
	[TestClass]
	public class StaticPatches
	{
		[TestMethod]
		public void TestMethod1()
		{
			// TODO: this test fails within VisualStudio when the patch tries to emit the
			// call to the original method (the delegate to the copy of it).
			//
			// This actually works fine from within a Unity application and probably has
			// something to do with the fact that VS does not allow .NET 2.0 targets to
			// run unit tests. Or something else.

			/*
			Class1Patch._reset();
			PatchTools.GetPatches(typeof(Class1Patch), typeof(Class1), "Method1", new Type[] { });

			var patcher = new Patcher(delegate (MethodInfo original, MethodInfo prefix, MethodInfo postfix)
			{
			});
			patcher.Patch(patch.original, patch.prepatch, patch.postpatch);

			Class1.Method1();
			Assert.IsTrue(Class1Patch.prefixed);
			Assert.IsTrue(Class1Patch.originalExecuted);
			Assert.IsTrue(Class1Patch.postfixed);
			*/
		}
	}
}