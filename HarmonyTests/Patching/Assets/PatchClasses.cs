using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

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
		public static void Method1()
		{
			Class1Patch.originalExecuted = true;
			// some useless work to prevent inlining when testing Release builds
			for (var i = 0; i < "abcd".Length; i++)
				if (i > 4)
					Console.WriteLine("");
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
		public void Method2()
		{
			Class2Patch.originalExecuted = true;
			// some useless work to prevent inlining when testing Release builds
			for (var i = 0; i < "abcd".Length; i++)
				if (i > 4)
					Console.WriteLine("");
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
		public void Method4(object sender)
#pragma warning restore IDE0060
		{
			Console.WriteLine("In Class4.Method4");
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
			Console.Write("In Class4Patch.Prefix");
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
		public void Method5(object xxxyyy)
#pragma warning restore IDE0060
		{
			Console.WriteLine("In Class5.Method5");
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
			Console.Write("In Class5Patch.Prefix");
			prefixed = true;
		}

		public static void Postfix(
#pragma warning disable IDE0060
			[HarmonyArgument("xxxyyy")] object bar
#pragma warning restore IDE0060
		)
		{
			Console.Write("In Class5Patch.Prefix");
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

		public List<object> Method6()
		{
			Console.WriteLine("In Class6.Method6");
			return new List<object>() { someFloat, someString, someStruct };
		}
	}

	public class Class6Patch
	{
		public static void Prefix(ref float ___someFloat, ref string ___someString, ref Class6Struct ___someStruct)
		{
			Console.Write("In Class6Patch.Prefix");
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
		public static object state2;

		public TestStruct Method7(string test)
		{
			Console.WriteLine("Method7: " + test);
			state1 = test;
			return new TestStruct() { a = 333, b = 666 };
		}
	}

	public static class Class7Patch
	{
		public static void Postfix(ref TestStruct __result)
		{
			Console.WriteLine("Class8Patch Postfix");
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

		public static TestStruct Method8(string test)
		{
			Console.WriteLine("Method8: " + test);
			mainRun = true;
			return new TestStruct() { a = 1, b = 2 };
		}
	}

	public class Class8Patch
	{
		public static void Postfix(ref TestStruct __result)
		{
			Console.WriteLine("Class8Patch Postfix");
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
		public void Method(string foo)
		{
			Console.WriteLine("foo=" + foo);
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
			Console.WriteLine("prefix");
			prefixed = true;
		}

		[HarmonyPostfix]
		public static void Patch2()
		{
			Console.WriteLine("postfix");
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
			Console.WriteLine("Method10");
			return true;
		}
	}

	public class Class10Patch
	{
		public static bool originalResult = false;
		public static bool postfixed = false;

		public static void Postfix(bool __result)
		{
			Console.WriteLine("Method10 Patch result: " + __result);
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
		public static DynamicMethod Prefix(MethodBase method)
		{
			var dynamicMethod = new DynamicMethod(method.Name + "_Class11Patch_Prefix",
				typeof(bool),
				new[] { typeof(string).MakeByRefType(), typeof(int) });

			_ = dynamicMethod.DefineParameter(1, ParameterAttributes.None, "__result");
			_ = dynamicMethod.DefineParameter(2, ParameterAttributes.None, "dummy");

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

			return dynamicMethod;
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