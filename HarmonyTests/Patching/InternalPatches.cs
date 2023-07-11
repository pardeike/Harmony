using HarmonyLib;
using NUnit.Framework;
using System.Collections.Generic;
using System.Reflection;
using TestLibrary;

namespace HarmonyLibTests.Patching
{
	[TestFixture, NonParallelizable]
	public class InternalPatches : TestLogger
	{
		private static void Method() { }

		private static IEnumerable<CodeInstruction> EmptyTranspiler(IEnumerable<CodeInstruction> instructions) => instructions;


		[Test]
		public void Test_This_Assembly_Correct_GetExecutingAssembly()
		{
			var assemblyOriginal = Assembly.GetExecutingAssembly();

			var instance = new Harmony("test");
			instance.Patch(SymbolExtensions.GetMethodInfo(() => Method()), transpiler: SymbolExtensions.GetMethodInfo(() => EmptyTranspiler(null)));

			var assemblyPatched = Assembly.GetExecutingAssembly();

			Assert.AreEqual(assemblyOriginal, assemblyPatched);
		}

		[Test]
		public void Test_External_Assembly_Correct_GetExecutingAssembly()
		{
			TestPatch.TestPatching(out var assemblyOriginal, out var assemblyPatched);

			Assert.AreEqual(assemblyOriginal, assemblyPatched);
		}
	}
}
