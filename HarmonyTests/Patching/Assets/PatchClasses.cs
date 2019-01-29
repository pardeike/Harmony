using Harmony;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace HarmonyTests.Assets
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

		public static IEnumerable<CodeInstruction> Transpiler(ILGenerator il, MethodBase original, IEnumerable<CodeInstruction> instructions)
		{
			var localVar = il.DeclareLocal(typeof(int));
			yield return new CodeInstruction(OpCodes.Ldc_I4, 123);
			yield return new CodeInstruction(OpCodes.Stloc, localVar);

			foreach (var instruction in instructions)
				yield return instruction;
		}

		public static void _reset()
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

	public class Class3
	{
		public string log = "-";
		public string GetLog => log;

		public void TestMethod(string s)
		{
			log = s;
			try
			{
				log = log + ",test";
				var z = 0;
				var n = 1 / z;
				if (n == 0)
					log = log + ",zero";
				else
					log = log + ",!zero";
				goto ending;
			}
			catch (Exception ex)
			{
				log = log + ",ex:" + ex.GetType().Name;
			}
			finally
			{
				log = log + ",finally";
			}
			log = log + ",end";
			return;
			ending:
			log = log + ",fail";
		}
	}

	public class Class4
	{
		public void Method4(object sender)
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

		public static bool Prefix(Class4 __instance, object sender)
		{
			Console.Write("In Class4Patch.Prefix");
			prefixed = true;
			senderValue = sender;
			return true;
		}

		public static void _reset()
		{
			prefixed = false;
			senderValue = null;
			originalExecuted = false;
		}
	}

	public class Class5
	{
		public void Method5(object xxxyyy)
		{
			Console.WriteLine("In Class5.Method5");
		}
	}

	public class Class5Patch
	{
		public static bool prefixed = false;
		public static bool postfixed = false;

		[HarmonyArgument("xxxyyy", "bar")]
		public static void Prefix(object bar)
		{
			Console.Write("In Class5Patch.Prefix");
			prefixed = true;
		}

		public static void Postfix(
			[HarmonyArgument("xxxyyy")] object bar
		)
		{
			Console.Write("In Class5Patch.Prefix");
			postfixed = true;
		}

		public static void _reset()
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

		public Tuple<float, string, Class6Struct> Method6()
		{
			Console.WriteLine("In Class6.Method6");
			return new Tuple<float, string, Class6Struct>(someFloat, someString, someStruct);
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
	
	public struct TestStruct {
		public long a;
		public long b;
	}

	public class Class7
	{
		public bool mainRun = false;

		public TestStruct Method7(string test)
		{
			mainRun = true;
			return new TestStruct() { a = 1, b = 2 };
		}
	}

	public class Class7Patch
	{
		public static void Postfix(ref TestStruct __result)
		{
			__result = new TestStruct() { a = 10, b = 20 };
		}
	}

	public class Class8
	{
		public static bool mainRun = false;

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

		public static void Postfix(int __state)
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
}