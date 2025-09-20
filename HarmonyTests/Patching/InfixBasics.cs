using HarmonyLib;
using NUnit.Framework;
using System.Reflection;

namespace HarmonyLibTests.Patching
{
	[TestFixture, NonParallelizable]
	public class InfixBasics : TestLogger
	{
		// Test class and methods for infix functionality
		public class TestClass
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

		public class HelperClass
		{
			public int Add(int a, int b) => a + b;
			public string Process(string input) => $"[{input}]";
		}

		[Test]
		public void Test_BasicInfixPrefixSkip()
		{
			var originalMethod = typeof(TestClass).GetMethod(nameof(TestClass.TestMethod));
			Assert.That(originalMethod, Is.Not.Null);

			var harmony = new Harmony("test-infix-basic");
			
			// Use PatchClassProcessor to process the attribute-based infix patches
			var processor = new PatchClassProcessor(harmony, typeof(InfixPrefixPatch));
			var replacements = processor.Patch();
			
			Assert.That(replacements, Is.Not.Null.And.Not.Empty, "No replacement methods created");

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
			
			// Use PatchClassProcessor to process the attribute-based infix patches
			var processor = new PatchClassProcessor(harmony, typeof(InfixPostfixPatch));
			var replacements = processor.Patch();
			
			Assert.That(replacements, Is.Not.Null.And.Not.Empty, "No replacement methods created");

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

	// Separate patch classes with proper attributes
	[HarmonyPatch(typeof(InfixBasics.TestClass), nameof(InfixBasics.TestClass.TestMethod))]
	class InfixPrefixPatch
	{
		[HarmonyInfixTarget(typeof(InfixBasics.HelperClass), nameof(InfixBasics.HelperClass.Add))]
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
	}

	[HarmonyPatch(typeof(InfixBasics.TestClass), nameof(InfixBasics.TestClass.TestMethod))]
	class InfixPostfixPatch
	{
		[HarmonyInfixTarget(typeof(InfixBasics.HelperClass), nameof(InfixBasics.HelperClass.Add))]
		[HarmonyInfixPostfix]
		static void InfixPostfix_Add(int a, int b, ref int __result)
		{
			__result += 100; // Modify result after the call
		}
	}
}