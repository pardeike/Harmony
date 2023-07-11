using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;

namespace TestLibrary;

public static class TestPatch
{
	private static void Method() { }

	private static IEnumerable<CodeInstruction> EmptyTranspiler(IEnumerable<CodeInstruction> instructions) => instructions;


	public static void TestPatching(out Assembly original, out Assembly patched)
	{
		original = Assembly.GetExecutingAssembly();

		var instance = new Harmony("test");
		instance.Patch(SymbolExtensions.GetMethodInfo(() => Method()), transpiler: SymbolExtensions.GetMethodInfo(() => EmptyTranspiler(null)));

		patched = Assembly.GetExecutingAssembly();
	}
}