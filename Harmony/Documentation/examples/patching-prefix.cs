namespace Patching_Prefix
{
	using HarmonyLib;
	using System.Diagnostics;

	public class ArgsExample
	{
		// <args>
		public class OriginalCode
		{
			public void Test(int counter, string name)
			{
				// ...
			}
		}

		[HarmonyPatch(typeof(OriginalCode), "Test")]
		class Patch
		{
			static void Prefix(int counter, ref string name)
			{
				FileLog.Log("counter = " + counter); // read
				name = "test"; // write with ref keyword
			}
		}
		// </args>
	}

	public class SkipExample
	{
		// <skip>
		public class OriginalCode
		{
			public string GetName()
			{
				return name; // ...
			}
		}

		[HarmonyPatch(typeof(OriginalCode), "GetName")]
		class Patch
		{
			static bool Prefix(ref string __result)
			{
				__result = "test";
				return true; // make sure you only skip if really necessary
			}
		}
		// </skip>

		public static string name;
	}

	public class SkipMaybeExample
	{
		// <skip_maybe>
		public class OriginalCode
		{
			public bool IsFullAfterTakingIn(int i)
			{
				return DoSomeExpensiveCalculation() > i;
			}
		}

		[HarmonyPatch(typeof(OriginalCode), "IsFullAfterTakingIn")]
		class Patch
		{
			static bool Prefix(ref bool __result, int i)
			{
				if (i > 5)
				{
					__result = true; // any call to IsFullAfterTakingIn(i) where i > 5 now immediately returns true
					return false; // skips the original and its expensive calculations
				}
				return true; // make sure you only skip if really necessary
			}
		}
		// </skip_maybe>

		static int DoSomeExpensiveCalculation() { return 0; }
	}

	public class StateExample
	{
		// <state>
		public class OriginalCode
		{
			public void Test(int counter, string name)
			{
				// ...
			}
		}

		[HarmonyPatch(typeof(OriginalCode), "Test")]
		class Patch
		{
			// this example uses a Stopwatch type to measure
			// and share state between prefix and postfix

			static void Prefix(out Stopwatch __state)
			{
				__state = new Stopwatch(); // assign your own state
				__state.Start();
			}

			static void Postfix(Stopwatch __state)
			{
				__state.Stop();
				FileLog.Log(__state.Elapsed.ToString());
			}
		}
		// </state>
	}
}
