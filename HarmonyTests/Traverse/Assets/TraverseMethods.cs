using System.Linq;

namespace HarmonyTests.Assets
{
	public class TraverseMethods_Instance
	{
		public bool Method1_called;

		private void Method1()
		{
			Method1_called = true;
		}

		private string Method2(string arg1)
		{
			return arg1 + arg1;
		}
	}

	public static class TraverseMethods_Static
	{
		static int StaticMethod(int a, int b)
		{
			return a * b;
		}
	}

	public static class TraverseMethods_VarArgs
	{
		static int Test1(int a, int b)
		{
			return a + b;
		}

		static int Test2(int a, int b, int c)
		{
			return a + b + c;
		}
		static int Test3(int multiplier, params int[] n)
		{
			return n.Aggregate(0, (acc, x) => acc + x) * multiplier;
		}
	}

	public static class TraverseMethods_Parameter
	{
		static string WithRefParameter(ref string refParameter)
		{
			refParameter = "hello";
			return "ok";
		}

		static string WithOutParameter(out string refParameter)
		{
			refParameter = "hello";
			return "ok";
		}

		static T WithGenericParameter<T>(T refParameter)
		{
			return refParameter;
		}
	}
}