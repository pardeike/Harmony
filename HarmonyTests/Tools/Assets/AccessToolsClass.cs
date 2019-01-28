namespace HarmonyTests.Assets
{
	public class AccessToolsClass
	{
		class Inner
		{
		}

		string field;
		readonly string field2;

		int _property;
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

		public AccessToolsClass()
		{
			field = "hello";
			field2 = "dummy";
		}

		public string Method()
		{
			return field;
		}

		public string Method2()
		{
			return field2;
		}

		public void SetField(string val)
		{
			field = val;
		}
	}

	public class AccessToolsSubClass : AccessToolsClass
	{
	}
}