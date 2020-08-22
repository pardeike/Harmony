namespace Reverse_Patching
{
	using HarmonyLib;
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Reflection;
	using System.Reflection.Emit;

	public class Example
	{
		// <example>
		private class OriginalCode
		{
			private void Test(int counter, string name)
			{
				// ...
			}
		}

		[HarmonyPatch]
		public class Patch
		{
			[HarmonyReversePatch]
			[HarmonyPatch(typeof(OriginalCode), "Test")]
			public static void MyTest(object instance, int counter, string name)
			{
				// its a stub so it has no initial content
				throw new NotImplementedException("It's a stub");
			}
		}

		class Main
		{
			void Test()
			{
				// here we call OriginalCode.Test()
				Patch.MyTest(originalInstance, 100, "hello");
			}
		}
		// </example>

		public static object originalInstance;
	}

	class TranspilerExample
	{
		// <transpiler>
		private class OriginalClass
		{
			private string SpecialCalculation(string original, int n)
			{
				var parts = original.Split('-').Reverse().ToArray();
				var str = string.Join("", parts) + n;
				return str + "Prolog";
			}
		}

		[HarmonyPatch]
		public class Patch
		{
			// When reverse patched, StringOperation will contain all the
			// code from the original including the Join() but not the +n
			//
			// Basically
			// var parts = original.Split('-').Reverse().ToArray();
			// return string.Join("", parts)
			//
			[HarmonyReversePatch]
			[HarmonyPatch(typeof(OriginalClass), "SpecialCalculation")]
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
					return list.AsEnumerable();
				}

				// make compiler happy
				_ = Transpiler(null);
				return original;
			}
		}
		// </transpiler>
	}
}
