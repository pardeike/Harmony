using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace TestLibrary;

public static class TestPatch
{
	private static void Method(ref Assembly prefix, ref Assembly original, ref Assembly postfix)
	{
		_ = prefix;
		_ = postfix;
		original = Assembly.GetExecutingAssembly();
	}

	private static void Prefix(ref Assembly prefix) => prefix = Assembly.GetExecutingAssembly();

	private static void Postfix(ref Assembly postfix) => postfix = Assembly.GetExecutingAssembly();

	private static void Finalizer(Exception __exception) => _ = __exception;

	private static IEnumerable<CodeInstruction> EmptyTranspiler(IEnumerable<CodeInstruction> instructions) => instructions;

	/// <summary>Checks that Assembly.GetExecutingAssembly is not broken for non patched methods</summary>
	public static void TestGlobalPatch(out Assembly original, out Assembly patched)
	{
		original = Assembly.GetExecutingAssembly();

		var instance = new Harmony("test");
		_ = instance.Patch(SymbolExtensions.GetMethodInfo((Assembly x) => Method(ref x, ref x, ref x)),
			transpiler: SymbolExtensions.GetMethodInfo(() => EmptyTranspiler(null))
		);

		patched = Assembly.GetExecutingAssembly();
	}

	/// <summary>Checks that Assembly.GetExecutingAssembly is not broken for patched methods</summary>
	public static void TestPatching(out Assembly prefix, out Assembly original, out Assembly postfix)
	{
		var instance = new Harmony("test");
		_ = instance.Patch(SymbolExtensions.GetMethodInfo((Assembly x) => Method(ref x, ref x, ref x)),
			prefix: SymbolExtensions.GetMethodInfo((Assembly x) => Prefix(ref x)),
			postfix: SymbolExtensions.GetMethodInfo((Assembly x) => Postfix(ref x)),
			transpiler: SymbolExtensions.GetMethodInfo(() => EmptyTranspiler(null)),
			finalizer: SymbolExtensions.GetMethodInfo(() => Finalizer(null))
		);

		prefix = original = postfix = null;
		Method(ref prefix, ref original, ref postfix);
	}
}
