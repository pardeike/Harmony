using HarmonyLib;
using NUnit.Framework;
using System.Linq;

namespace HarmonyLibTests
{
	public static class TestTools
	{
		public static void Log(string str)
		{
			TestContext.WriteLine($"    {str}");
		}

		// Workaround for [Explicit] attribute not working in Visual Studio: https://github.com/nunit/nunit3-vs-adapter/issues/658
		public static void AssertIgnoreIfVSTest()
		{
			if (System.Diagnostics.Process.GetCurrentProcess().ProcessName is "testhost")
				Assert.Ignore();
		}

	}

	public class TestLogger
	{
		[SetUp]
		public void BaseSetUp()
		{
			var args = TestContext.CurrentContext.Test.Arguments.Select(a => a.ToString()).ToArray().Join();
			if (args.Length > 0) args = $"({args})";
			TestContext.WriteLine($"### {TestContext.CurrentContext.Test.MethodName}({args})");
		}

		[TearDown]
		public void BaseTearDown()
		{
			TestContext.WriteLine($"--- {TestContext.CurrentContext.Test.MethodName} ");
		}
	}
}