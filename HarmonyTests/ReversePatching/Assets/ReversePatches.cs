using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;

namespace HarmonyLibTests.Assets
{
	public class Class0Reverse
	{
		public string Method(string original, int n)
		{
			var parts = original.Split('-').Reverse().ToArray();
			var str = string.Join("", parts) + n;
			return str + "Prolog";
		}
	}

	public class Class0ReversePatch
	{
		public static string StringOperation(string original)
		{
			IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
			{
				var mJoin = SymbolExtensions.GetMethodInfo(() => string.Join(null, null));
				var list = Transpilers.Manipulator(instructions,
					item => item.opcode == OpCodes.Ldarg_1,
					item => item.opcode = OpCodes.Ldarg_0
				).ToList();
				var idx = list.FindIndex(item => item.opcode == OpCodes.Call && item.operand == mJoin);
				list.RemoveRange(idx + 1, list.Count - (idx + 1));
				return list.AsEnumerable();
			}
			Transpiler(null);
			return original;
		}

		public static void Postfix(string original, int n, ref string __result)
		{
			if (n == 456)
				__result = "Epilog" + StringOperation(original);
		}
	}
}