namespace HarmonyLibTests.Assets
{
	public class TraverseFields
	{
		public static string[] testStrings = new string[] { "test01", "test02", "test03", "test04" };
		public static string[] fieldNames = new string[] { "publicField", "privateField", "protectedField", "internalField" };
	}

	public class TraverseFields_AccessModifiers
	{
		public string publicField;
		readonly string privateField;
		protected string protectedField;
		internal string internalField;

		public TraverseFields_AccessModifiers(string[] s)
		{
			publicField = s[0];
			privateField = s[1];
			protectedField = s[2];
			internalField = s[3];
		}

		public string GetTestField(int n)
		{
			switch (n)
			{
				case 0:
					return publicField;
				case 1:
					return privateField;
				case 2:
					return protectedField;
				case 3:
					return internalField;
				default:
					return null;
			}
		}

		public override string ToString()
		{
			return "TraverseFields_AccessModifiers";
		}
	}
}
