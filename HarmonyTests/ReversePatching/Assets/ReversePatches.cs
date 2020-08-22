using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

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
			// This inner transpiler will be applied to the original and
			// the result will replace this method
			//
			// That will allow this method to have a different signature
			// than the original and it must match the transpiled result
			//
			IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
			{
				var list = Transpilers.Manipulator(instructions,
					item => item.opcode == OpCodes.Ldarg_1,
					item => item.opcode = OpCodes.Ldarg_0
				).ToList();
				var mJoin = SymbolExtensions.GetMethodInfo(() => string.Join(null, null));
				var idx = list.FindIndex(item => item.opcode == OpCodes.Call && item.operand as MethodInfo == mJoin);
				list.RemoveRange(idx + 1, list.Count - (idx + 1));
				list.Add(new CodeInstruction(OpCodes.Ret));
				return list.AsEnumerable();
			}

			// make compiler happy
			_ = Transpiler(null);
			return original;
		}

		public static void Postfix(string original, ref string __result)
		{
			__result = "Epilog" + StringOperation(original);
		}
	}

	public class Class1Reverse
	{
		[MethodImpl(MethodImplOptions.NoInlining)]
		public string Method(string original, int n)
		{
			return original + GetExtra(n);
		}

		private static string GetExtra(int n)
		{
			return "Extra" + n;
		}
	}

	[HarmonyPatch(typeof(Class1Reverse), "Method")]
	[HarmonyPatch(MethodType.Normal)]
	public class Class1ReversePatch
	{
		[HarmonyReversePatch]
		[HarmonyPatch("GetExtra")]
		[MethodImpl(MethodImplOptions.NoInlining)]
		public static string GetExtra(int n)
		{
			// this will be replaced by reverse patching

			// using a fake while loop to force non-inlining
			while (DateTime.Now.Ticks > 0)
				throw new NotImplementedException();
			return null;
		}

		public static bool Prefix(string original, int n, ref string __result)
		{
			if (n != 456) return true;

			__result = "Prefixed" + GetExtra(n) + original;
			return false;
		}
	}
}
