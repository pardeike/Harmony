namespace HarmonyLibTests.Assets
{
	public class TraverseFields
	{
		public static string[] testStrings = ["test01", "test02", "test03", "test04"];
		public static string[] fieldNames = ["publicField", "privateField", "protectedField", "internalField"];
	}

	public class TraverseFields_AccessModifiers(string[] s)
	{
		public string publicField = s[0];
		readonly string privateField = s[1];
		protected string protectedField = s[2];
		internal string internalField = s[3];

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

		public override string ToString() => "TraverseFields_AccessModifiers";
	}
}
