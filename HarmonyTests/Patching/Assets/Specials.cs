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

	public struct SomeStruct
	{
		public string reasonTextInt;
		public bool acceptedInt;
		public string Reason => reasonTextInt;
		public bool Accepted => acceptedInt;

		public static SomeStruct WasAccepted
		{
			get
			{
				var result = new SomeStruct
				{
					acceptedInt = true
				};
				return result;
			}
		}

		public static implicit operator SomeStruct(bool value)
		{
			if (value)
			{
				return WasAccepted;
			}
			return WasAccepted;
		}

		public static implicit operator SomeStruct(string value)
		{
			return new SomeStruct();
		}
	}

	public struct AnotherStruct
	{
		public int x;
		public int y;
		public int z;
	}

	public abstract class AbstractClass
	{
		public virtual SomeStruct Method(string checkingDef, AnotherStruct loc)
		{
			return SomeStruct.WasAccepted;
		}
	}

	public class ConcreteClass : AbstractClass
	{
		public override SomeStruct Method(string def, AnotherStruct loc)
		{
			return true;
		}
	}

	[HarmonyPatch(typeof(ConcreteClass))]
	[HarmonyPatch(nameof(ConcreteClass.Method))]
	public static class ConcreteClass_Patch
	{
		static void Prefix(ConcreteClass __instance, string def, AnotherStruct loc)
		{
			Console.WriteLine("ConcreteClass_Patch.Prefix");
		}
	}
}