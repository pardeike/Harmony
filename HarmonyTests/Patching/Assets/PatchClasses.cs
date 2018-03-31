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
}