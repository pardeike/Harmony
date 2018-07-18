using Harmony;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace HarmonyTests.Assets
{
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

	// fails (Issue #77)
	public struct Class7ReturnType { public long a, b; }

	// works
	// public struct Class7ReturnType { public byte a, b; }
	// public class Class7ReturnType { public long a, b; }

	public class Class7
	{
		public Class7ReturnType Method7()
		{
			var result = new Class7ReturnType() { a = 1, b = 2 };
			Console.Write("In Class7Patch.Method7 " + result.a + " " + result.b);
			return result;
		}
	}

	public class Class7Patch
	{
		public static void Prefix()
		{
			Console.Write("In Class7Patch.Prefix");
		}
	}
}