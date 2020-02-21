extern alias mmc;

using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Collections;

namespace HarmonyLibTests.Assets
{
	public class Class0
	{
		[MethodImpl(MethodImplOptions.NoInlining)]
		public string Method0()
		{
			return "original";
		}
	}

	public class Class0Patch
	{
		public static void Postfix(ref string __result)
		{
			__result = "patched";
		}
	}

	public class Class1
	{
		[MethodImpl(MethodImplOptions.NoInlining)]
		public static void Method1()
		{
			try
			{
				Class1Patch.originalExecuted = true;
			}
			finally
			{
			}
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

		public static IEnumerable<CodeInstruction> Transpiler(ILGenerator il, IEnumerable<CodeInstruction> instructions)
		{
			var localVar = il.DeclareLocal(typeof(int));
			yield return new CodeInstruction(OpCodes.Ldc_I4, 123);
			yield return new CodeInstruction(OpCodes.Stloc, localVar);

			foreach (var instruction in instructions)
				yield return instruction;
		}

		public static void ResetTest()
		{
			prefixed = false;
			originalExecuted = false;
			postfixed = false;
		}
	}

	public class Class2
	{
		[MethodImpl(MethodImplOptions.NoInlining)]
		public void Method2()
		{
			try
			{
				Class2Patch.originalExecuted = true;
			}
			finally
			{
			}
		}
	}

	public class Class2Patch
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

		public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			// no-op / passthrough
			return instructions;
		}

		public static void ResetTest()
		{
			prefixed = false;
			originalExecuted = false;
			postfixed = false;
		}
	}

	public class Class3
	{
		public string log = "-";
		public string GetLog => log;

		public void TestMethod(string s)
		{
			log = s;
			try
			{
				log += ",test";
				var z = 0;
				var n = 1 / z;
				if (n == 0)
					log += ",zero";
				else
					log += ",!zero";
				goto ending;
			}
			catch (Exception ex)
			{
				log = log + ",ex:" + ex.GetType().Name;
			}
			finally
			{
				log += ",finally";
			}
			log += ",end";
			return;
		ending:
			log += ",fail";
		}
	}

	public class Class4
	{
#pragma warning disable IDE0060
		[MethodImpl(MethodImplOptions.NoInlining)]
		public void Method4(object sender)
#pragma warning restore IDE0060
		{
			_ = DateTime.Now;
			Class4Patch.originalExecuted = true;
		}
	}

	public class Class4Patch
	{
		public static bool prefixed = false;
		public static object senderValue = null;
		public static bool originalExecuted = false;

#pragma warning disable IDE0060
		public static bool Prefix(Class4 __instance, object sender)
#pragma warning restore IDE0060
		{
			prefixed = true;
			senderValue = sender;
			return true;
		}

		public static void ResetTest()
		{
			prefixed = false;
			senderValue = null;
			originalExecuted = false;
		}
	}

	public class Class5
	{
#pragma warning disable IDE0060
		[MethodImpl(MethodImplOptions.NoInlining)]
		public void Method5(object xxxyyy)
#pragma warning restore IDE0060
		{
			_ = DateTime.Now;
		}
	}

	public class Class5Patch
	{
		public static bool prefixed = false;
		public static bool postfixed = false;

		[HarmonyArgument("xxxyyy", "bar")]
#pragma warning disable IDE0060
		public static void Prefix(object bar)
#pragma warning restore IDE0060
		{
			prefixed = true;
		}

		public static void Postfix(
#pragma warning disable IDE0060
			[HarmonyArgument("xxxyyy")] object bar
#pragma warning restore IDE0060
		)
		{
			postfixed = true;
		}

		public static void ResetTest()
		{
			prefixed = false;
			postfixed = false;
		}
	}

	public struct Class6Struct
	{
		public double d1;
		public double d2;
		public double d3;
	}

	public class Class6
	{
		public float someFloat;
		public string someString;
		public Class6Struct someStruct;

		[MethodImpl(MethodImplOptions.NoInlining)]
		public List<object> Method6()
		{
			_ = DateTime.Now;
			return new List<object>() { someFloat, someString, someStruct };
		}
	}

	public class Class6Patch
	{
		public static void Prefix(ref float ___someFloat, ref string ___someString, ref Class6Struct ___someStruct)
		{
			___someFloat = 123;
			___someString = "patched";
			___someStruct = new Class6Struct()
			{
				d1 = 10.0,
				d2 = 20.0,
				d3 = 30.0
			};
		}
	}

	public struct TestStruct
	{
		public long a;
		public long b;
	}

	public class Class7
	{
		public object state1 = "-";

		[MethodImpl(MethodImplOptions.NoInlining)]
		public TestStruct Method7(string test)
		{
			state1 = test;
			return new TestStruct() { a = 333, b = 666 };
		}
	}

	public static class Class7Patch
	{
		public static void Postfix(ref TestStruct __result)
		{
			__result = new TestStruct() { a = 10, b = 20 };
		}

		/*
		public static IEnumerable<CodeInstruction> Transpiler(ILGenerator il, IEnumerable<CodeInstruction> instructions)
		{
			var f_state1 = typeof(Class7).GetField("state1");
			if (f_state1 == null) throw new NullReferenceException();

			var f_a = typeof(TestStruct).GetField("a");
			var f_b = typeof(TestStruct).GetField("b");

			var local = il.DeclareLocal(typeof(TestStruct));

			var list = new List<CodeInstruction>()
			{
				// arguments
				// 0 - this
				// 1 - pointer to valuetype (simulate return value)
				// 2 - parameter 1
				// 3 - parameter 2
				// 4 - parameter 3
				// ...

				new CodeInstruction(OpCodes.Ldarg_0),
				new CodeInstruction(OpCodes.Ldarg_2),
				new CodeInstruction(OpCodes.Stfld, f_state1),

				new CodeInstruction(OpCodes.Ldarg_1), // load ref to return value

				new CodeInstruction(OpCodes.Ldloca, local),
				new CodeInstruction(OpCodes.Initobj, typeof(TestStruct)),
				new CodeInstruction(OpCodes.Ldloca, local),
				new CodeInstruction(OpCodes.Ldc_I4_S, 10),
				new CodeInstruction(OpCodes.Conv_I8),
				new CodeInstruction(OpCodes.Stfld, f_a),
				new CodeInstruction(OpCodes.Ldloca, local),
				new CodeInstruction(OpCodes.Ldc_I4_S, 20),
				new CodeInstruction(OpCodes.Conv_I8),
				new CodeInstruction(OpCodes.Stfld, f_b),
				new CodeInstruction(OpCodes.Ldloc, local), // here, result is on the stack

				new CodeInstruction(OpCodes.Stobj, typeof(TestStruct)), // store it into ref

				new CodeInstruction(OpCodes.Ret),
			};

			foreach (var item in list)
				yield return item;
		}
		*/
	}

	public class Class8
	{
		public static bool mainRun = false;

		[MethodImpl(MethodImplOptions.NoInlining)]
		public static TestStruct Method8(string test)
		{
			mainRun = true;
			return new TestStruct() { a = 1, b = 2 };
		}
	}

	public class Class8Patch
	{
		public static void Postfix(ref TestStruct __result)
		{
			__result = new TestStruct() { a = 10, b = 20 };
		}
	}

	public class Class9
	{
		public override string ToString()
		{
			return string.Format("foobar");
		}
	}

	public class Class9Patch
	{
		public static void Prefix(out object __state)
		{
			__state = null;
		}

#pragma warning disable IDE0060
		public static void Postfix(object __state)
#pragma warning restore IDE0060
		{

		}
	}

	public struct Struct1
	{
		public int n;
		public string s;
		public long l1;
		public long l2;
		public long l3;
		public long l4;

		public static bool prefixed = false;
		public static bool originalExecuted = false;
		public static bool postfixed = false;

		public void TestMethod(string val)
		{
			s = val;
			n++;
			originalExecuted = true;
		}

		public static void Reset()
		{
			prefixed = false;
			originalExecuted = false;
			postfixed = false;
		}
	}

	public class Struct1Patch
	{
		public static void Prefix()
		{
			Struct1.prefixed = true;
		}

		public static void Postfix()
		{
			Struct1.postfixed = true;
		}
	}

	public struct Struct2
	{
		public string s;

		[MethodImpl(MethodImplOptions.NoInlining)]
		public void TestMethod(string val)
		{
			s = val;
		}
	}

	public class Struct2Patch
	{
		public static void Postfix(ref Struct2 __instance)
		{
			__instance.s = "patched";
		}
	}

	public class AttributesClass
	{
		[MethodImpl(MethodImplOptions.NoInlining)]
		public void Method(string foo)
		{
			_ = DateTime.Now;
		}
	}

	[HarmonyPatch]
	public class AttributesPatch
	{
		public static bool targeted = false;
		public static bool prefixed = false;
		public static bool postfixed = false;

		[HarmonyTargetMethod]
		public static MethodBase Patch0()
		{
			targeted = true;
			return AccessTools.Method(typeof(AttributesClass), "Method");
		}

		[HarmonyPrefix]
		public static void Patch1()
		{
			prefixed = true;
		}

		[HarmonyPostfix]
		public static void Patch2()
		{
			postfixed = true;
		}

		public static void ResetTest()
		{
			targeted = false;
			prefixed = false;
			postfixed = false;
		}
	}

	public class Class10
	{
		[MethodImpl(MethodImplOptions.NoInlining)]
		public bool Method10()
		{
			_ = DateTime.Now;
			return true;
		}
	}

	public class Class10Patch
	{
		public static bool originalResult = false;
		public static bool postfixed = false;

		public static void Postfix(bool __result)
		{
			originalResult = __result;
			postfixed = true;
		}
	}

	public class Class11
	{
		public bool originalMethodRan = false;

		[MethodImpl(MethodImplOptions.NoInlining)]
		public string TestMethod(int dummy)
		{
			originalMethodRan = true;
			return "original" + dummy;
		}
	}

	public static class Class11Patch
	{
		public static bool prefixed = false;

#if NETCOREAPP2_0
		public static bool Prefix(ref string __result,  int dummy)
		{
			__result = "patched";
			prefixed = true;
			return false;
		}
#else
		public static MethodInfo Prefix(MethodBase method)
		{
			var dynamicMethod = new mmc::MonoMod.Utils.DynamicMethodDefinition(method.Name + "_Class11Patch_Prefix",
				typeof(bool),
				new[] { typeof(string).MakeByRefType(), typeof(int) });

			dynamicMethod.Definition.Parameters[0].Name = "__result";
			dynamicMethod.Definition.Parameters[1].Name = "dummy";

			var il = dynamicMethod.GetILGenerator();

			//load "patched" into __result
			il.Emit(OpCodes.Ldarg_0);
			il.Emit(OpCodes.Ldstr, "patched");
			il.Emit(OpCodes.Stind_Ref);

			//set prefixed to true
			il.Emit(OpCodes.Ldnull);
			il.Emit(OpCodes.Ldc_I4_1);
			il.Emit(OpCodes.Stfld, typeof(Class11Patch).GetField(nameof(prefixed)));

			//return false
			il.Emit(OpCodes.Ldc_I4_0);
			il.Emit(OpCodes.Ret);

			return dynamicMethod.Generate();
		}
#endif
	}

	public class Class12
	{
		readonly int count;

		public Class12(int count)
		{
			this.count = count;
		}

		public List<string> FizzBuzz()
		{
			var output = new List<string>(count);
			for (var i = 1; i <= count; i++)
			{
				if (i % 15 == 0)
					output.Add("FizzBuzz");
				else if (i % 3 == 0)
					output.Add("Fizz");
				else if (i % 5 == 0)
					output.Add("Buzz");
				else
					output.Add(i.ToString());
			}
			return output;
		}
	}

	public class Class13<T> : IEnumerable<T>
	{
		readonly List<T> store = new List<T>();

		[MethodImpl(MethodImplOptions.NoInlining)]
		public void Add(T item)
		{
			store.Add(item);
		}

		public IEnumerator<T> GetEnumerator()
		{
			return store.GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return store.GetEnumerator();
		}
	}

	public class Class13Patch
	{
		public static object method = null;
		public static int result = 0;

		public static void Prefix(object __originalMethod, ref int item)
		{
			method = __originalMethod;
			result = item;
			item = 999;
		}
	}

	public class Class14
	{
		public static List<string> state = new List<string>();

		[MethodImpl(MethodImplOptions.NoInlining)]
		public bool Test(string s, KeyValuePair<string, int> p)
		{
			state.Add(s);
			try { return true; }
			finally { }
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		public bool Test(string s, KeyValuePair<string, int> p1, KeyValuePair<string, int> p2)
		{
			state.Add(s);
			try { return true; }
			finally { }
		}
	}

	[HarmonyPatch]
	public static class Class14Patch
	{
		[HarmonyPatch(typeof(Class14), "Test", new Type[] { typeof(string), typeof(KeyValuePair<string, int>) })]
		[HarmonyPrefix]
		static bool Prefix0()
		{
			Class14.state.Add("Prefix0");
			return true;
		}
		[HarmonyPatch(typeof(Class14), "Test", new Type[] { typeof(string), typeof(KeyValuePair<string, int>) })]
		[HarmonyPostfix]
		static void Postfix0()
		{
			Class14.state.Add("Postfix0");
		}

		[HarmonyPatch(typeof(Class14), "Test", new Type[] { typeof(string), typeof(KeyValuePair<string, int>), typeof(KeyValuePair<string, int>) })]
		[HarmonyPrefix]
		static bool Prefix1()
		{
			Class14.state.Add("Prefix1");
			return true;
		}
		[HarmonyPatch(typeof(Class14), "Test", new Type[] { typeof(string), typeof(KeyValuePair<string, int>), typeof(KeyValuePair<string, int>) })]
		[HarmonyPostfix]
		static void Postfix1()
		{
			Class14.state.Add("Postfix1");
		}
	}

	[HarmonyPatch]
	public static class Class15Patch
	{
		static MethodBase TargetMethod()
		{
			return null;
		}

		static void Postfix()
		{
		}
	}

	[HarmonyPatch]
	public static class Class16Patch
	{
		static string TargetMethod()
		{
			return null;
		}

		static void Postfix()
		{
		}
	}

	[HarmonyPatch]
	public static class Class17Patch
	{
		static IEnumerable<MethodBase> TargetMethods()
		{
			yield return AccessTools.Method(typeof(Class16Patch), "TargetMethods");
			yield return AccessTools.Method(typeof(Class16Patch), "Dummy");
			yield return AccessTools.Method(typeof(Class16Patch), "Postfix");
		}

		static void Postfix()
		{
		}
	}

	public class MultiplePatches1
	{
		public static string result;

		[MethodImpl(MethodImplOptions.NoInlining)]
		public string TestMethod(string val)
		{
			result = val;
			return "ok";
		}
	}

	public class MultiplePatches2
	{
		public static string result;

		[MethodImpl(MethodImplOptions.NoInlining)]
		public string TestMethod(string val)
		{
			result = val;
			return "ok";
		}
	}

	[HarmonyPatch]
	public class MultiplePatches1Patch
	{
		[HarmonyPatch(typeof(MultiplePatches1), "TestMethod")]
		[HarmonyPrefix]
		public static void Fix1(ref string val)
		{
			val += ",prefix1";
		}

		[HarmonyPatch(typeof(MultiplePatches1), "TestMethod")]
		[HarmonyPrefix]
		[HarmonyPriority(Priority.High)]
		public static void Fix2(ref string val)
		{
			val += ",prefix2";
		}

		[HarmonyPatch(typeof(MultiplePatches1), "TestMethod")]
		[HarmonyPostfix]
		public static void Fix3(ref string __result)
		{
			__result += ",postfix";
		}
	}

	[HarmonyPatch(typeof(MultiplePatches2), "TestMethod")]
	public class MultiplePatchesPatch2_Part1
	{
		[HarmonyPriority(Priority.Low)]
		public static void Prefix(ref string val)
		{
			val += ",prefix1";
		}
	}

	[HarmonyPatch(typeof(MultiplePatches2), "TestMethod")]
	public class MultiplePatchesPatch2_Part2
	{
		public static void Prefix(ref string val)
		{
			val += ",prefix2";
		}
	}

	[HarmonyPatch(typeof(MultiplePatches2), "TestMethod")]
	public class MultiplePatchesPatch2_Part3
	{
		public static void Postfix(ref string __result)
		{
			__result = "patched";
		}
	}

	public class InjectFieldBase
	{
		internal string testField;
	}

	public class InjectFieldSubClass : InjectFieldBase
	{
		public string TestValue => testField;

		public void Method(string val)
		{
			try
			{
				testField = val;
			}
			catch (Exception)
			{
				throw;
			}
		}
	}

	[HarmonyPatch(typeof(InjectFieldSubClass), nameof(InjectFieldSubClass.Method))]
	public class InjectFieldSubClass_Patch
	{
		public static void Postfix(ref string ___testField)
		{
			___testField = "patched";
		}
	}

	public class Finalizer_Patch_Order_Class
	{
		[MethodImpl(MethodImplOptions.NoInlining)]
		public IEnumerable<int> Method()
		{
			yield return 1;
			yield return 2;
			yield return 3;
		}
	}

	[HarmonyPatch(typeof(Finalizer_Patch_Order_Class))]
	[HarmonyPatch(nameof(Finalizer_Patch_Order_Class.Method))]
	public class Finalizer_Patch_Order_Patch
	{
		public static List<string> events = new List<string>();

		public static List<string> GetEvents()
		{
			return events;
		}

		public static void ResetTest()
		{
			events = new List<string>();
		}

		[HarmonyPrefix]
		[HarmonyPriority(200)]
		public static bool Bool_Prefix()
		{
			events.Add(nameof(Bool_Prefix));
			return true;
		}

		[HarmonyPrefix]
		[HarmonyPriority(100)]
		public static void Void_Prefix()
		{
			events.Add(nameof(Void_Prefix));
		}

		[HarmonyPostfix]
		[HarmonyPriority(200)]
		public static void Simple_Postfix()
		{
			events.Add(nameof(Simple_Postfix));
		}

		[HarmonyPostfix]
		[HarmonyPriority(200)]
		public static IEnumerable<int> Passthrough_Postfix1(IEnumerable<int> items)
		{
			events.Add($"{nameof(Passthrough_Postfix1)} start");
			var e = items.GetEnumerator();
			while (e.MoveNext())
			{
				var oldValue = e.Current;
				var newValue = oldValue * 10;
				events.Add($"Yield {newValue} [old={oldValue}]");
				yield return newValue;
			}
			events.Add($"{nameof(Passthrough_Postfix1)} end");
		}

		[HarmonyPostfix]
		[HarmonyPriority(100)]
		public static IEnumerable<int> Passthrough_Postfix2(IEnumerable<int> items)
		{
			events.Add($"{nameof(Passthrough_Postfix2)} start");
			var e = items.GetEnumerator();
			while (e.MoveNext())
			{
				var oldValue = e.Current;
				var newValue = oldValue + 1;
				events.Add($"Yield {newValue} [old={oldValue}]");
				yield return newValue;
			}
			events.Add($"{nameof(Passthrough_Postfix2)} end");
		}

		[HarmonyFinalizer]
		[HarmonyPriority(300)]
		public static Exception NonModifying_Finalizer(Exception __exception)
		{
			events.Add(nameof(NonModifying_Finalizer));
			return __exception;
		}

		[HarmonyFinalizer]
		[HarmonyPriority(200)]
		public static Exception ClearException_Finalizer()
		{
			events.Add(nameof(ClearException_Finalizer));
			return null;
		}

		[HarmonyFinalizer]
		[HarmonyPriority(100)]
		public static void Void_Finalizer(Exception __exception)
		{
			events.Add(nameof(Void_Finalizer));
		}
	}

	// disabled - see test case
	/*
	public class ClassExceptionFilter
	{
		[MethodImpl(MethodImplOptions.NoInlining)]
		public static int Method(Exception exception)
		{
			var result = 0;
			try
			{
				if (exception != null)
					throw exception;
			}
			catch (Exception e) when (e.Message == "test")
			{
				result += 1;
			}
			catch (ArithmeticException)
			{
				result += 10;
			}
			finally
			{
				result += 100;
			}
			return result;
		}
	}
	*/
}