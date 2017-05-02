using Harmony;
using System.Collections.Generic;
using System.Reflection;

namespace HarmonyTests.Assets
{
	public class Class1
	{
		public static void Method1()
		{
			Class1Patch.originalExecuted = true;
		}
	}

	public class Class1Patch
	{
		public static bool prefixed = false;
		public static bool originalExecuted = false;
		public static bool postfixed = false;

		public static bool Prefix()
		{
			prefixed = true;
			return true;
		}

		public static void Postfix()
		{
			postfixed = true;
		}

		public static IEnumerable<CodeInstruction> Transpiler(MethodBase original, IEnumerable<CodeInstruction> instructions)
		{
			// no-op / passthrough
			return instructions;
		}

		public static void _reset()
		{
			prefixed = false;
			originalExecuted = false;
			postfixed = false;
		}
	}
}