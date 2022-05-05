using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

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
			if (original is object)
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
			return ex is null ? null : new ArgumentException("Test", ex);
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
		public bool accepted;

		public static SomeStruct WasAccepted => new() { accepted = true };
		public static SomeStruct WasNotAccepted => new() { accepted = false };

		public static implicit operator SomeStruct(bool value)
		{
			return value ? WasAccepted : WasNotAccepted;
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
		public virtual SomeStruct Method(string originalDef, AnotherStruct loc)
		{
			return SomeStruct.WasAccepted;
		}
	}

	public class ConcreteClass : AbstractClass
	{
		[MethodImpl(MethodImplOptions.NoInlining)]
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
			TestTools.Log("ConcreteClass_Patch.Method.Prefix");
		}
	}

	[HarmonyPatch(typeof(AppDomain), nameof(AppDomain.GetData))]
	public class ExternalMethod_Patch
	{
		static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			yield return new CodeInstruction(OpCodes.Ldnull);
			yield return new CodeInstruction(OpCodes.Ret);
		}
	}

	public class EventHandlerTestClass
	{
		public delegate void TestEvent();
		public event TestEvent OnTestEvent;

		public void Run()
		{
			Console.WriteLine("EventHandlerTestClass.Run called");
			OnTestEvent += Handler;
			_ = OnTestEvent.Method;
			Console.WriteLine("EventHandlerTestClass.Run done");
		}

		public void Handler()
		{
			try
			{
				Console.WriteLine("MarshalledTestClass.Handler called");
			}
			catch
			{
				Console.WriteLine("MarshalledTestClass.Handler exception");
			}
		}
	}

	[HarmonyPatch(typeof(EventHandlerTestClass), nameof(EventHandlerTestClass.Handler))]
	public class EventHandlerTestClass_Patch
	{
		static void Prefix()
		{
		}
	}

	public class MarshalledTestClass : MarshalByRefObject
	{
		public void Run()
		{
			Console.WriteLine("MarshalledTestClass.Run called");
			Handler();
			Console.WriteLine("MarshalledTestClass.Run called");
		}

		public void Handler()
		{
			try
			{
				Console.WriteLine("MarshalledTestClass.Handler called");
			}
			catch
			{
				Console.WriteLine("MarshalledTestClass.Handler exception");
			}
		}
	}

	[HarmonyPatch(typeof(MarshalledTestClass), nameof(MarshalledTestClass.Handler))]
	public class MarshalledTestClass_Patch
	{
		static void Prefix()
		{
		}
	}

	public class MarshalledWithEventHandlerTest1Class : MarshalByRefObject
	{
		public delegate void TestEvent();
#pragma warning disable CS0067
		public event TestEvent OnTestEvent;
#pragma warning restore CS0067

		public void Run()
		{
			Console.WriteLine("MarshalledWithEventHandlerTest1Class.Run called");
			OnTestEvent += Handler;
			Console.WriteLine("MarshalledWithEventHandlerTest1Class.Run called");
		}

		public void Handler()
		{
			try
			{
				Console.WriteLine("MarshalledWithEventHandlerTest1Class.Handler called");
			}
			catch
			{
				Console.WriteLine("MarshalledWithEventHandlerTest1Class.Handler exception");
			}
		}
	}

	[HarmonyPatch(typeof(MarshalledWithEventHandlerTest1Class), nameof(MarshalledWithEventHandlerTest1Class.Handler))]
	public class MarshalledWithEventHandlerTest1Class_Patch
	{
		static void Prefix()
		{
		}
	}

	public class MarshalledWithEventHandlerTest2Class : MarshalByRefObject
	{
		public delegate void TestEvent();
		public event TestEvent OnTestEvent;

		public void Run()
		{
			Console.WriteLine("MarshalledWithEventHandlerTest2Class.Run called");
			OnTestEvent += Handler;
			_ = OnTestEvent.Method;
			Console.WriteLine("MarshalledWithEventHandlerTest2Class.Run called");
		}

		public void Handler()
		{
			try
			{
				Console.WriteLine("MarshalledWithEventHandlerTest2Class.Handler called");
			}
			catch
			{
				Console.WriteLine("MarshalledWithEventHandlerTest2Class.Handler exception");
			}
		}
	}

	[HarmonyPatch(typeof(MarshalledWithEventHandlerTest2Class), nameof(MarshalledWithEventHandlerTest2Class.Handler))]
	public class MarshalledWithEventHandlerTest2Class_Patch
	{
		static void Prefix()
		{
		}
	}

	public static class NativeMethodPatchingSimple
	{
		[DllImport("kernel32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool AllocConsole();

		public static List<CodeInstruction> instructions;

		public static bool MyAllocConsole()
		{
			return true;
		}

		public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			NativeMethodPatchingSimple.instructions = instructions.ToList();

			foreach (var code in instructions)
			{
				if (code.opcode == OpCodes.Call)
					code.operand = SymbolExtensions.GetMethodInfo(() => MyAllocConsole());
				yield return code;
			}
		}
	}

	[HarmonyPatch(typeof(NativeMethodPatchingPostfix), nameof(NativeMethodPatchingPostfix.gethostname))]
	public static class NativeMethodPatchingPostfix
	{
		[DllImport("WSOCK32.DLL", SetLastError = true)]
		public static extern long gethostname(StringBuilder name, int nameLen);

		public static void Postfix(StringBuilder name)
		{
			_ = name.Append("-postfix");
		}
	}
}
