using HarmonyLib;
using HarmonyLibTests.Assets;
using NUnit.Framework;

namespace HarmonyLibTests
{
	public class TestExceptionFilterBlock
	{
		// Filter exceptions are currently not supported in DynamicMethods
		// Example:
		// catch (Exception e) when (e.Message == "test") { }

		/*
		[Test]
		public void TestPlainMethodExceptions()
		{
			var originalClass = typeof(ClassExceptionFilter);
			Assert.IsNotNull(originalClass);
			var originalMethod = originalClass.GetMethod("Method");
			Assert.IsNotNull(originalMethod);

			var instance = new Harmony("test");
			Assert.IsNotNull(instance);

			var patcher = new PatchProcessor(instance, originalMethod);
			Assert.IsNotNull(patcher);
			patcher.Patch();

			var result = ClassExceptionFilter.Method(null);
			Assert.AreEqual(100, result);
		}
		*/
	}
}