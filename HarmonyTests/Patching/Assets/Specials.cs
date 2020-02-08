using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace HarmonyLibTests.Assets
{
	/*public class HttpWebRequestPatches
	{
		public static bool prefixCalled = false;
		public static bool postfixCalled = false;

		public static void Prefix()
		{
			prefixCalled = true;
		}

		public static void Postfix()
		{
			postfixCalled = true;
		}

		public static void ResetTest()
		{
			prefixCalled = false;
			postfixCalled = false;
		}
	}*/

	public class DeadEndCode
	{
		public string Method()
		{
			throw new Exception();
		}
	}

	[HarmonyPatch(typeof(DeadEndCode), nameof(DeadEndCode.Method))]
	public class DeadEndCode_Patch1
	{
		static void Prefix()
		{
		}

		static void Postfix()
		{
		}
	}

	[HarmonyPatch(typeof(DeadEndCode), nameof(DeadEndCode.Method))]
	public class DeadEndCode_Patch2
	{
		public static MethodBase original = null;
		public static Exception exception = null;

		static void Nop() { }

		static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			for (var i = 1; i <= 10; i++)
				yield return new CodeInstruction(OpCodes.Call, SymbolExtensions.GetMethodInfo(() => Nop()));
			yield return new CodeInstruction(OpCodes.Call, SymbolExtensions.GetMethodInfo(() => Transpiler(null)));
			yield return new CodeInstruction(OpCodes.Ret);
		}

		static void Cleanup(MethodBase original, Exception ex)
		{
			if (original != null)
			{
				DeadEndCode_Patch2.original = original;
				exception = ex;
			}
		}
	}

	[HarmonyPatch(typeof(DeadEndCode), nameof(DeadEndCode.Method))]
	public class DeadEndCode_Patch3
	{
		static void Nop() { }

		static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			for (var i = 1; i <= 10; i++)
				yield return new CodeInstruction(OpCodes.Call, SymbolExtensions.GetMethodInfo(() => Nop()));
			yield return new CodeInstruction(OpCodes.Call, SymbolExtensions.GetMethodInfo(() => Transpiler(null)));
			yield return new CodeInstruction(OpCodes.Ret);
		}

		static Exception Cleanup(Exception ex)
		{
			return ex == null ? null : new ArgumentException("Test", ex);
		}
	}

	[HarmonyPatch(typeof(DeadEndCode), nameof(DeadEndCode.Method))]
	public class DeadEndCode_Patch4
	{
		static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			yield return new CodeInstruction(OpCodes.Call, null);
		}

		static Exception Cleanup()
		{
			return null;
		}
	}
}