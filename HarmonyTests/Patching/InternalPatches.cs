using HarmonyLib;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Reflection;
using TestLibrary;

namespace HarmonyLibTests.Patching
{
	[TestFixture, NonParallelizable]
	public class InternalPatches : TestLogger
	{
		private static void Method(ref Assembly assembly)
		{
			var thisAssembly = Assembly.GetExecutingAssembly();

			Assert.AreEqual(assembly, thisAssembly);
		}

		private static void Prefix(ref Assembly assembly) => assembly = Assembly.GetExecutingAssembly();

		private static void Postfix(ref Assembly assembly)
		{
			var thisAssembly = Assembly.GetExecutingAssembly();

			Assert.AreEqual(assembly, thisAssembly);
		}

		private static void Finalizer(Exception __exception) { }

		private static IEnumerable<CodeInstruction> EmptyTranspiler(IEnumerable<CodeInstruction> instructions) => instructions;


		[Test]
		public void Test_Global_This_Assembly_Correct_GetExecutingAssembly()
		{
			var assemblyOriginal = Assembly.GetExecutingAssembly();

			var instance = new Harmony("test");
			_ = instance.Patch(SymbolExtensions.GetMethodInfo((Assembly x) => Method(ref x)),
				prefix: SymbolExtensions.GetMethodInfo((Assembly x) => Prefix(ref x)),
				postfix: SymbolExtensions.GetMethodInfo((Assembly x) => Postfix(ref x)),
				transpiler: SymbolExtensions.GetMethodInfo(() => EmptyTranspiler(null)),
				finalizer: SymbolExtensions.GetMethodInfo(() => Finalizer(null))
			);
			// Check that it still work with Harmony's global patch
			var assemblyPatched = Assembly.GetExecutingAssembly();
			Assert.AreEqual(assemblyOriginal, assemblyPatched);
		}

		[Test]
		public void Test_This_Assembly_Correct_GetExecutingAssembly()
		{
			var assemblyOriginal = Assembly.GetExecutingAssembly();

			var instance = new Harmony("test");
			_ = instance.Patch(SymbolExtensions.GetMethodInfo((Assembly x) => Method(ref x)),
				prefix: SymbolExtensions.GetMethodInfo((Assembly x) => Prefix(ref x)),
				postfix: SymbolExtensions.GetMethodInfo((Assembly x) => Postfix(ref x)),
				transpiler: SymbolExtensions.GetMethodInfo(() => EmptyTranspiler(null))
			);

			// Check that the patched method correctly returns the assembly
			Assembly methodAssembly = null;
			Method(ref methodAssembly);
			Assert.AreEqual(assemblyOriginal, methodAssembly);
		}

		[Test]
		public void Test_External_Global_Assembly_Correct_GetExecutingAssembly()
		{
			TestPatch.TestGlobalPatch(out var assemblyOriginal, out var assemblyPatched);

			Assert.AreEqual(assemblyOriginal, assemblyPatched);
		}

		[Test]
		public void Test_External_Assembly_Correct_GetExecutingAssembly()
		{
			TestPatch.TestPatching(out var prefix, out var original, out var postfix);

			Assert.AreEqual(original, prefix);
			Assert.AreEqual(original, postfix);
		}
	}
}
