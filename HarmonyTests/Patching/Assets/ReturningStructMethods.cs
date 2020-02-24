using HarmonyLib;
using HarmonyLibTests.Assets.Structs;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace HarmonyLibTests.Assets.Methods
{
	[HarmonyPatch]
	public static class ReturningStructs_Patch
	{
		[HarmonyDebug]
		static void Prefix(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
		}

		static IEnumerable<MethodBase> TargetMethods()
		{
			var cls = typeof(ReturningStructs);
			foreach (var useStatic in new bool[] { false, true })
			{
				for (var n = 1; n <= 20; n++)
				{
					var name = $"{(useStatic ? "S" : "I")}M{n.ToString("D2")}";
					yield return AccessTools.DeclaredMethod(cls, name);
				}
			}
		}
	}

	public class ReturningStructs
	{
		public St01 IM01(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return default;
		}

		public St02 IM02(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return default;
		}

		public St03 IM03(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return default;
		}

		public St04 IM04(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return default;
		}

		public St05 IM05(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return default;
		}

		public St06 IM06(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return default;
		}

		public St07 IM07(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return default;
		}

		public St08 IM08(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return default;
		}

		public St09 IM09(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return default;
		}

		public St10 IM10(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return default;
		}

		public St11 IM11(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return default;
		}

		public St12 IM12(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return default;
		}

		public St13 IM13(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return default;
		}

		public St14 IM14(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return default;
		}

		public St15 IM15(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return default;
		}

		public St16 IM16(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return default;
		}

		public St17 IM17(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return default;
		}

		public St18 IM18(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return default;
		}

		public St19 IM19(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return default;
		}

		public St20 IM20(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return default;
		}

		//

		public static St01 SM01(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return default;
		}

		public static St02 SM02(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return default;
		}

		public static St03 SM03(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return default;
		}

		public static St04 SM04(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return default;
		}

		public static St05 SM05(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return default;
		}

		public static St06 SM06(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return default;
		}

		public static St07 SM07(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return default;
		}

		public static St08 SM08(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return default;
		}

		public static St09 SM09(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return default;
		}

		public static St10 SM10(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return default;
		}

		public static St11 SM11(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return default;
		}

		public static St12 SM12(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return default;
		}

		public static St13 SM13(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return default;
		}

		public static St14 SM14(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return default;
		}

		public static St15 SM15(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return default;
		}

		public static St16 SM16(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return default;
		}

		public static St17 SM17(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return default;
		}

		public static St18 SM18(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return default;
		}

		public static St19 SM19(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return default;
		}

		public static St20 SM20(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return default;
		}
	}
}