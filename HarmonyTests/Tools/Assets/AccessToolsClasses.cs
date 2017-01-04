namespace HarmonyTests.Assets
{
	public class AccessToolsClass
	{
		private class inner
		{
		}

		private string field;

		private int _property;
		private int property
		{
			get { return _property; }
			set { _property = value; }
		}

		public AccessToolsClass()
		{
			field = "hello";
		}

		public string method()
		{
			return field;
		}

		public void setfield(string val)
		{
			field = val;
		}
	}
}