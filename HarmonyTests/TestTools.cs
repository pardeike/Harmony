using HarmonyLib;
using NUnit.Framework;
using System.Linq;

namespace HarmonyLibTests
{
	public static class TestTools
	{
		public static void Log(string str)
		{
			TestContext.Progress.WriteLine($"    {str}");
		}
	}

	public class TestLogger
	{
		[SetUp]
		public void BaseSetUp()
		{
			var args = TestContext.CurrentContext.Test.Arguments.Select(a => a.ToString()).ToArray().Join();
			if (args.Length > 0) args = $"({args})";
			TestContext.Progress.WriteLine($"### {TestContext.CurrentContext.Test.MethodName}({args})");
		}

		[TearDown]
		public void BaseTearDown()
		{
			TestContext.Progress.WriteLine($"--- {TestContext.CurrentContext.Test.MethodName} ");
		}
	}
}