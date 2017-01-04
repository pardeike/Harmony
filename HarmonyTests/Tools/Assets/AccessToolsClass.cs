namespace HarmonyTests.Assets
{
	public class AccessToolsClass
	{
		private class inner
		{
		}

		private string field;
		private string field2;

		private int _property;
		private int property
		{
			get { return _property; }
			set { _property = value; }
		}
		private int property2
		{
			get { return _property; }
			set { _property = value; }
		}

		public AccessToolsClass()
		{
			field = "hello";
			field2 = "dummy";
		}

		public string method()
		{
			return field;
		}

		public string method2()
		{
			return field2;
		}

		public void setfield(string val)
		{
			field = val;
		}
	}
}