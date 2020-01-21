namespace HarmonyLibTests.Assets
{
	public class AccessToolsClass
	{
		class Inner
		{
		}

		public const string field1Value = "f1";
		public const string field2Value = "f2";
		public const string field3Value = "f3";
		public const string field4Value = "f4";

		private string field1;
		private readonly string field2;
		private static string field3 = field3Value;
		private readonly static string field4 = field4Value;

		int _property;

#pragma warning disable IDE0051
		int Property
		{
			get => _property;
			set => _property = value;
		}
		int Property2
		{
			get => _property;
			set => _property = value;
		}
#pragma warning restore IDE0051

		public AccessToolsClass()
		{
			field1 = field1Value;
			field2 = field2Value;
			field3 = field3Value;
			// Does not work on Net Core 3.x
			// _ = Traverse.Create<AccessToolsClass>().Field("field4").SetValue(field4Value);
		}

		public string Method1()
		{
			return field1;
		}

		public string Method2()
		{
			return field2;
		}

		public void SetField(string val)
		{
			field1 = val;
		}

		public string Method3()
		{
			return field3;
		}

		public string Method4()
		{
			return field4;
		}
	}

	public class AccessToolsSubClass : AccessToolsClass
	{
	}
}