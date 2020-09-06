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

	//

	public class ReturningStructs_I01
	{
		public St01 IM01(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return new St01();
		}
	}

	public class ReturningStructs_I02
	{
		public St02 IM02(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return new St02();
		}
	}

	public class ReturningStructs_I03
	{
		public St03 IM03(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return new St03();
		}
	}

	public class ReturningStructs_I04
	{
		public St04 IM04(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return new St04();
		}
	}

	public class ReturningStructs_I05
	{
		public St05 IM05(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return new St05();
		}
	}

	public class ReturningStructs_I06
	{
		public St06 IM06(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return new St06();
		}
	}

	public class ReturningStructs_I07
	{
		public St07 IM07(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return new St07();
		}
	}

	public class ReturningStructs_I08
	{
		public St08 IM08(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return new St08();
		}
	}

	public class ReturningStructs_I09
	{
		public St09 IM09(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return new St09();
		}
	}

	public class ReturningStructs_I10
	{
		public St10 IM10(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return new St10();
		}
	}

	public class ReturningStructs_I11
	{
		public St11 IM11(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return new St11();
		}
	}

	public class ReturningStructs_I12
	{
		public St12 IM12(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return new St12();
		}
	}

	public class ReturningStructs_I13
	{
		public St13 IM13(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return new St13();
		}
	}

	public class ReturningStructs_I14
	{
		public St14 IM14(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return new St14();
		}
	}

	public class ReturningStructs_I15
	{
		public St15 IM15(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return new St15();
		}
	}

	public class ReturningStructs_I16
	{
		public St16 IM16(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return new St16();
		}
	}

	public class ReturningStructs_I17
	{
		public St17 IM17(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return new St17();
		}
	}

	public class ReturningStructs_I18
	{
		public St18 IM18(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return new St18();
		}
	}

	public class ReturningStructs_I19
	{
		public St19 IM19(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return new St19();
		}
	}

	public class ReturningStructs_I20
	{
		public St20 IM20(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return new St20();
		}
	}

	public class ReturningStructs_S01
	{
		public static St01 SM01(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return new St01();
		}
	}

	public class ReturningStructs_S02
	{
		public static St02 SM02(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return new St02();
		}
	}

	public class ReturningStructs_S03
	{
		public static St03 SM03(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return new St03();
		}
	}

	public class ReturningStructs_S04
	{
		public static St04 SM04(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return new St04();
		}
	}

	public class ReturningStructs_S05
	{
		public static St05 SM05(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return new St05();
		}
	}

	public class ReturningStructs_S06
	{
		public static St06 SM06(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return new St06();
		}
	}

	public class ReturningStructs_S07
	{
		public static St07 SM07(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return new St07();
		}
	}

	public class ReturningStructs_S08
	{
		public static St08 SM08(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return new St08();
		}
	}

	public class ReturningStructs_S09
	{
		public static St09 SM09(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return new St09();
		}
	}

	public class ReturningStructs_S10
	{
		public static St10 SM10(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return new St10();
		}
	}

	public class ReturningStructs_S11
	{
		public static St11 SM11(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return new St11();
		}
	}

	public class ReturningStructs_S12
	{
		public static St12 SM12(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return new St12();
		}
	}

	public class ReturningStructs_S13
	{
		public static St13 SM13(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return new St13();
		}
	}

	public class ReturningStructs_S14
	{
		public static St14 SM14(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return new St14();
		}
	}

	public class ReturningStructs_S15
	{
		public static St15 SM15(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return new St15();
		}
	}

	public class ReturningStructs_S16
	{
		public static St16 SM16(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return new St16();
		}
	}

	public class ReturningStructs_S17
	{
		public static St17 SM17(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return new St17();
		}
	}

	public class ReturningStructs_S18
	{
		public static St18 SM18(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return new St18();
		}
	}

	public class ReturningStructs_S19
	{
		public static St19 SM19(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return new St19();
		}
	}

	public class ReturningStructs_S20
	{
		public static St20 SM20(string s)
		{
			if (s != "test") throw new ArgumentException("First argument corrupt");
			return new St20();
		}
	}
}
