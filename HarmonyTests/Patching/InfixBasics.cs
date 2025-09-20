using HarmonyLib;
using NUnit.Framework;

namespace HarmonyLibTests.Patching
{
	[TestFixture, NonParallelizable]
	public class InfixBasics : TestLogger
	{
		// Test class and methods for infix functionality
		class TestClass
		{
			public static int TestMethod(int input)
			{
				var helper = new HelperClass();
				var result = helper.Add(input, 10); // This call will be infixed
				return result * 2;
			}

			public static string TestMethodWithMultipleCalls(string input)
			{
				var helper = new HelperClass();
				var result1 = helper.Process(input); // First call
				var result2 = helper.Process(result1); // Second call  
				return result2;
			}
		}

		class HelperClass
		{
			public int Add(int a, int b) => a + b;
			public string Process(string input) => $"[{input}]";
		}

		// Basic infix prefix test
		[HarmonyInfixTarget(typeof(HelperClass), nameof(HelperClass.Add))]
		[HarmonyInfixPrefix]
		static bool InfixPrefix_Add(int a, int b, ref int __result)
		{
			if (a < 0)
			{
				__result = -1;
				return false; // Skip original call
			}
			return true; // Continue with original call
		}

		// Basic infix postfix test  
		[HarmonyInfixTarget(typeof(HelperClass), nameof(HelperClass.Add))]
		[HarmonyInfixPostfix]
		static void InfixPostfix_Add(int a, int b, ref int __result)
		{
			__result += 100; // Modify result after the call
		}

		[Test]
		public void Test_BasicInfixPrefixSkip()
		{
			var originalMethod = typeof(TestClass).GetMethod(nameof(TestClass.TestMethod));
			Assert.That(originalMethod, Is.Not.Null);

			var harmony = new Harmony("test-infix-basic");
			
			// Apply infix prefix that should skip when input is negative
			harmony.Patch(originalMethod, 
				innerprefixes: new[] { new HarmonyMethod(typeof(InfixBasics).GetMethod(nameof(InfixPrefix_Add))) });

			try
			{
				// Test normal execution (should call original Add method)
				var result1 = TestClass.TestMethod(5);
				// Normal: Add(5, 10) = 15, then * 2 = 30
				Assert.That(result1, Is.EqualTo(30));

				// Test skip execution (should skip Add and use __result = -1)
				var result2 = TestClass.TestMethod(-3);
				// Skipped: Add not called, __result = -1, then -1 * 2 = -2  
				Assert.That(result2, Is.EqualTo(-2));
			}
			finally
			{
				harmony.UnpatchAll();
			}
		}

		[Test]
		public void Test_BasicInfixPostfix()
		{
			var originalMethod = typeof(TestClass).GetMethod(nameof(TestClass.TestMethod));
			Assert.That(originalMethod, Is.Not.Null);

			var harmony = new Harmony("test-infix-postfix");
			
			// Apply infix postfix that adds 100 to result
			harmony.Patch(originalMethod,
				innerpostfixes: new[] { new HarmonyMethod(typeof(InfixBasics).GetMethod(nameof(InfixPostfix_Add))) });

			try
			{
				var result = TestClass.TestMethod(5);
				// Normal: Add(5, 10) = 15, then +100 = 115, then * 2 = 230
				Assert.That(result, Is.EqualTo(230));
			}
			finally
			{
				harmony.UnpatchAll();
			}
		}
	}
}