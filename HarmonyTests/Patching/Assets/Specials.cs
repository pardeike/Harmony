using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace HarmonyLibTests.Assets
{
	public class HttpWebRequestPatches
	{
		public static bool prefixCalled = false;
		public static bool postfixCalled = false;

		public static void Prefix() => prefixCalled = true;

		public static void Postfix() => postfixCalled = true;

		public static void ResetTest()
		{
			prefixCalled = false;
			postfixCalled = false;
		}
	}

	// -----------------------------------------------------

	public class ResultRefStruct
	{
		// ReSharper disable FieldCanBeMadeReadOnly.Global
		public static int[] numbersPrefix = [0, 0];
		public static int[] numbersPostfix = [0, 0];
		public static int[] numbersPostfixWithNull = [0];
		public static int[] numbersFinalizer = [0];
		public static int[] numbersMixed = [0, 0];
		// ReSharper restore FieldCanBeMadeReadOnly.Global

		[MethodImpl(MethodImplOptions.NoInlining)]
		public ref int ToPrefix() => ref numbersPrefix[0];

		[MethodImpl(MethodImplOptions.NoInlining)]
		public ref int ToPostfix() => ref numbersPostfix[0];

		[MethodImpl(MethodImplOptions.NoInlining)]
		public ref int ToPostfixWithNull() => ref numbersPostfixWithNull[0];

		[MethodImpl(MethodImplOptions.NoInlining)]
		public ref int ToFinalizer() => throw new Exception();

		[MethodImpl(MethodImplOptions.NoInlining)]
		public ref int ToMixed() => ref numbersMixed[0];
	}

	[HarmonyPatch(typeof(ResultRefStruct))]
	public class ResultRefStruct_Patch
	{
		[HarmonyPatch(nameof(ResultRefStruct.ToPrefix))]
		[HarmonyPrefix]
		public static bool Prefix(ref RefResult<int> __resultRef)
		{
			__resultRef = () => ref ResultRefStruct.numbersPrefix[1];
			return false;
		}

		[HarmonyPatch(nameof(ResultRefStruct.ToPostfix))]
		[HarmonyPostfix]
		public static void Postfix(ref RefResult<int> __resultRef) => __resultRef = () => ref ResultRefStruct.numbersPostfix[1];

		[HarmonyPatch(nameof(ResultRefStruct.ToPostfixWithNull))]
		[HarmonyPostfix]
		public static void PostfixWithNull(ref RefResult<int> __resultRef) => __resultRef = null;

		[HarmonyPatch(nameof(ResultRefStruct.ToFinalizer))]
		[HarmonyFinalizer]
		public static Exception Finalizer(ref RefResult<int> __resultRef)
		{
			__resultRef = () => ref ResultRefStruct.numbersFinalizer[0];
			return null;
		}

		[HarmonyPatch(nameof(ResultRefStruct.ToMixed))]
		[HarmonyPostfix]
		public static void PostfixMixed(ref int __result, ref RefResult<int> __resultRef)
		{
			__result = 42;
			__resultRef = () => ref ResultRefStruct.numbersMixed[1];
		}
	}

	// -----------------------------------------------------

	public class DeadEndCode
	{
		[MethodImpl(MethodImplOptions.NoInlining)]
		public string Method() => throw new FormatException();
	}

	// not using attributes here because we apply prefix first, then postfix
	public class DeadEndCode_Patch1
	{
		public static bool prefixCalled = false;
		public static bool postfixCalled = false;

		public static void Prefix() => prefixCalled = true;

		public static void Postfix() => postfixCalled = true;

		public static bool PrefixWithControl() => false;
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
			if (original is not null)
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

		static Exception Cleanup(Exception ex) => ex is null ? null : new ArgumentException("Test", ex);
	}

	[HarmonyPatch(typeof(DeadEndCode), nameof(DeadEndCode.Method))]
	public class DeadEndCode_Patch4
	{
		static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			yield return new CodeInstruction(OpCodes.Call, null);
		}

		static Exception Cleanup() => null;
	}

	// -----------------------------------------------------

	public class LateThrowClass1
	{
		[MethodImpl(MethodImplOptions.NoInlining)]
		public void Method(string str)
		{
			if (str.Length == 2)
				return;

			// this throw is the last IL code before 'ret' in this method
			throw new ArgumentException("fail");
		}
	}

	[HarmonyPatch(typeof(LateThrowClass1), nameof(LateThrowClass1.Method))]
	public class LateThrowClass_Patch1
	{
		public static bool prefixCalled = false;
		public static bool postfixCalled = false;

		static void Prefix() => prefixCalled = true;

		static void Postfix() => postfixCalled = true;
	}

	// -----------------------------------------------------

	public class LateThrowClass2
	{
		[MethodImpl(MethodImplOptions.NoInlining)]
		public void Method(int i)
		{
			switch (i)
			{
				case 0:
					Console.WriteLine("Test");
					break;
				default:
					throw new ArgumentOutOfRangeException();
			}
		}
	}

	[HarmonyPatch(typeof(LateThrowClass2), nameof(LateThrowClass2.Method))]
	public class LateThrowClass_Patch2
	{
		public static bool prefixCalled = false;
		public static bool postfixCalled = false;

		static void Prefix() => prefixCalled = true;

		static void Postfix() => postfixCalled = true;
	}

	// -----------------------------------------------------

	public struct SomeStruct
	{
		public bool accepted;

		public static SomeStruct WasAccepted => new() { accepted = true };
		public static SomeStruct WasNotAccepted => new() { accepted = false };

		public static implicit operator SomeStruct(bool value) => value ? WasAccepted : WasNotAccepted;

		public static implicit operator SomeStruct(string value) => new();
	}

	public struct AnotherStruct
	{
		public int x;
		public int y;
		public int z;
	}

	public abstract class AbstractClass
	{
		public virtual SomeStruct Method(string originalDef, AnotherStruct loc) => SomeStruct.WasAccepted;
	}

	public class ConcreteClass : AbstractClass
	{
		[MethodImpl(MethodImplOptions.NoInlining)]
		public override SomeStruct Method(string def, AnotherStruct loc) => true;
	}

	[HarmonyPatch(typeof(ConcreteClass))]
	[HarmonyPatch(nameof(ConcreteClass.Method))]
	public static class ConcreteClass_Patch
	{
		static void Prefix(ConcreteClass __instance, string def, AnotherStruct loc) => TestTools.Log("ConcreteClass_Patch.Method.Prefix");
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

	public class ClassTestingCallClosure
	{
		public string field1 = "";
		public string field2 = "";

		public CodeInstruction WIthoutContext() => CodeInstruction.CallClosure<Func<string, string>>(input => { return $"[{input}]"; });
		public CodeInstruction WithContext() => CodeInstruction.CallClosure(() => { field2 = field1; });
	}
}
