namespace HarmonyLibTests.Patching
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
			Assert.NotNull(originalClass);
			var originalMethod = originalClass.GetMethod("Method");
			Assert.NotNull(originalMethod);

			var instance = new Harmony("test");
			Assert.NotNull(instance);

			var patcher = new PatchProcessor(instance, originalMethod);
			Assert.NotNull(patcher);
			patcher.Patch();

			var result = ClassExceptionFilter.Method(null);
			Assert.AreEqual(100, result);
		}
		*/
	}
}
