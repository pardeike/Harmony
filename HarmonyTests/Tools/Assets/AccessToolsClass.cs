namespace HarmonyTests.Assets
{
	public class AccessToolsClass
	{
		private class Inner
		{
		}

		private string field;
		private string field2;

		private int _property;
		private int Property
		{
			get => _property;
			set => _property = value;
		}
		private int Property2
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
}