using HarmonyLib;
using HarmonyLibTests.Assets;
using NUnit.Framework;

namespace HarmonyLibTests.Patching
{
	// DynamicMethod does not support 'catch .. when' so for now we cannot enable this test

	public class TestExceptionFilterBlock
	{
		[Test]
		[Ignore("Filter exceptions are currently not supported in DynamicMethods")]
		public void TestExceptionsWithFilter()
		{
			var originalClass = typeof(ClassExceptionFilter);
			Assert.NotNull(originalClass);
			var originalMethod = originalClass.GetMethod("Method1");
			Assert.NotNull(originalMethod);

			var instance = new Harmony("test");
			Assert.NotNull(instance);

			var patcher = new PatchProcessor(instance, originalMethod);
			Assert.NotNull(patcher);
			_ = patcher.Patch();

			ClassExceptionFilter.Method1();
		}

		[Test]
		[Ignore("Filter exceptions are currently not supported in DynamicMethods")]
		public void TestPlainMethodExceptions()
		{
			var originalClass = typeof(ClassExceptionFilter);
			Assert.NotNull(originalClass);
			var originalMethod = originalClass.GetMethod("Method2");
			Assert.NotNull(originalMethod);

			var instance = new Harmony("test");
			Assert.NotNull(instance);

			var patcher = new PatchProcessor(instance, originalMethod);
			Assert.NotNull(patcher);
			_ = patcher.Patch();

			var result = ClassExceptionFilter.Method2(null);
			Assert.AreEqual(100, result);
		}
	}
}
