using HarmonyLibTests.Assets.Structs;
using System;

namespace HarmonyLibTests.Assets.Methods
{
	public static class ReturningStructs_Patch
	{
		public static void Prefix(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
		}
	}

	public class ReturningStructs
	{
		public const byte MagicNumber = 42;

		public St01 IM01(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return new St01 { b1 = MagicNumber };
		}

		public St02 IM02(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return new St02 { b1 = MagicNumber };
		}

		public St03 IM03(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return new St03 { b1 = MagicNumber };
		}

		public St04 IM04(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return new St04 { b1 = MagicNumber };
		}

		public St05 IM05(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return new St05 { b1 = MagicNumber };
		}

		public St06 IM06(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return new St06 { b1 = MagicNumber };
		}

		public St07 IM07(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return new St07 { b1 = MagicNumber };
		}

		public St08 IM08(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return new St08 { b1 = MagicNumber };
		}

		public St09 IM09(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return new St09 { b1 = MagicNumber };
		}

		public St10 IM10(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return new St10 { b1 = MagicNumber };
		}

		public St11 IM11(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return new St11 { b1 = MagicNumber };
		}

		public St12 IM12(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return new St12 { b1 = MagicNumber };
		}

		public St13 IM13(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return new St13 { b1 = MagicNumber };
		}

		public St14 IM14(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return new St14 { b1 = MagicNumber };
		}

		public St15 IM15(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return new St15 { b1 = MagicNumber };
		}

		public St16 IM16(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return new St16 { b1 = MagicNumber };
		}

		public St17 IM17(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return new St17 { b1 = MagicNumber };
		}

		public St18 IM18(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return new St18 { b1 = MagicNumber };
		}

		public St19 IM19(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return new St19 { b1 = MagicNumber };
		}

		public St20 IM20(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return new St20 { b1 = MagicNumber };
		}

		//

		public static St01 SM01(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return new St01 { b1 = MagicNumber };
		}

		public static St02 SM02(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return new St02 { b1 = MagicNumber };
		}

		public static St03 SM03(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return new St03 { b1 = MagicNumber };
		}

		public static St04 SM04(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return new St04 { b1 = MagicNumber };
		}

		public static St05 SM05(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return new St05 { b1 = MagicNumber };
		}

		public static St06 SM06(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return new St06 { b1 = MagicNumber };
		}

		public static St07 SM07(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return new St07 { b1 = MagicNumber };
		}

		public static St08 SM08(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return new St08 { b1 = MagicNumber };
		}

		public static St09 SM09(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return new St09 { b1 = MagicNumber };
		}

		public static St10 SM10(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return new St10 { b1 = MagicNumber };
		}

		public static St11 SM11(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return new St11 { b1 = MagicNumber };
		}

		public static St12 SM12(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return new St12 { b1 = MagicNumber };
		}

		public static St13 SM13(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return new St13 { b1 = MagicNumber };
		}

		public static St14 SM14(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return new St14 { b1 = MagicNumber };
		}

		public static St15 SM15(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return new St15 { b1 = MagicNumber };
		}

		public static St16 SM16(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return new St16 { b1 = MagicNumber };
		}

		public static St17 SM17(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return new St17 { b1 = MagicNumber };
		}

		public static St18 SM18(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return new St18 { b1 = MagicNumber };
		}

		public static St19 SM19(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return new St19 { b1 = MagicNumber };
		}

		public static St20 SM20(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return new St20 { b1 = MagicNumber };
		}
	}
}