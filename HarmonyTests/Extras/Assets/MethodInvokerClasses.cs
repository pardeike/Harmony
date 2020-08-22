namespace HarmonyLibTests.Assets
{
	public class TestMethodInvokerObject
	{
		public int Value;
		public void Method1(int a)
		{
			Value += a;
		}
	}

	public struct TestMethodInvokerStruct
	{
		public int Value;
	}

	public static class MethodInvokerClass
	{
		public static void Method1(int a, ref int b, out int c, out TestMethodInvokerObject d, ref TestMethodInvokerStruct e)
		{
			b += 1;
			c = b * 2;
			d = new TestMethodInvokerObject
			{
				Value = a
			};
			e.Value = a;
		}
	}
}
