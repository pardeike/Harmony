namespace Patching_Postfix
{
	using HarmonyLib;
	using System.Collections.Generic;

	public class ResultExample
	{
		// <result>
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
			static void Postfix(ref string __result)
			{
				if (__result == "foo")
					__result = "bar";
			}
		}
		// </result>

		public static string name;
	}

	public class PassThroughExample
	{
		// <passthrough>
		public class OriginalCode
		{
			public string GetName()
			{
				return "David";
			}

			public IEnumerable<int> GetNumbers()
			{
				yield return 1;
				yield return 2;
				yield return 3;
			}
		}

		[HarmonyPatch(typeof(OriginalCode), "GetName")]
		class Patch1
		{
			static string Postfix(string name)
			{
				return "Hello " + name;
			}
		}

		[HarmonyPatch(typeof(OriginalCode), "GetNumbers")]
		class Patch2
		{
			static IEnumerable<int> Postfix(IEnumerable<int> values)
			{
				yield return 0;
				foreach (var value in values)
					if (value > 1)
						yield return value * 10;
				yield return 99;
			}
		}

		// will make GetNumbers() return [0, 20, 30, 99] instead of [1, 2, 3]
		// </passthrough>
	}

	public class ArgsExample
	{
		// <args>
		public class OriginalCode
		{
			public void Test(int counter)
			{
				// ...
			}
		}

		[HarmonyPatch(typeof(OriginalCode), "Test")]
		class Patch
		{
			static void Prefix(int counter)
			{
				FileLog.Log("counter = " + counter);
			}
		}
		// </args>
	}
}
