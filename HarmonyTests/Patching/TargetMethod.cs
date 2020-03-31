using HarmonyLib;
using HarmonyLibTests.Assets;
using NUnit.Framework;
using System;

namespace HarmonyLibTests
{
	[TestFixture]
	public class TargetMethod
	{
		[Test, NonParallelizable]
		public void Test_TargetMethod_Returns_Null()
		{
			var patchClass = typeof(Class15Patch);
			Assert.NotNull(patchClass);

			var harmonyInstance = new Harmony("test");
			Assert.NotNull(harmonyInstance);

			var processor = harmonyInstance.CreateClassProcessor(patchClass);
			Assert.NotNull(processor);

			Exception exception = null;
			try
			{
				Assert.NotNull(processor.Patch());
			}
			catch (Exception ex)
			{
				exception = ex;
			}
			Assert.NotNull(exception);
			Assert.NotNull(exception.InnerException);
			Assert.True(exception.InnerException.Message.Contains("returned an unexpected result: null"));
		}

		[Test, NonParallelizable]
		public void Test_TargetMethod_Returns_Wrong_Type()
		{
			var patchClass = typeof(Class16Patch);
			Assert.NotNull(patchClass);

			var harmonyInstance = new Harmony("test");
			Assert.NotNull(harmonyInstance);

			var processor = harmonyInstance.CreateClassProcessor(patchClass);
			Assert.NotNull(processor);

			Exception exception = null;
			try
			{
				Assert.NotNull(processor.Patch());
			}
			catch (Exception ex)
			{
				exception = ex;
			}
			Assert.NotNull(exception);
			Assert.NotNull(exception.InnerException);
			Assert.True(exception.InnerException.Message.Contains("has wrong return type"));
		}

		[Test, NonParallelizable]
		public void Test_TargetMethods_Returns_Null()
		{
			var patchClass = typeof(Class17Patch);
			Assert.NotNull(patchClass);

			var harmonyInstance = new Harmony("test");
			Assert.NotNull(harmonyInstance);

			var processor = harmonyInstance.CreateClassProcessor(patchClass);
			Assert.NotNull(processor);

			Exception exception = null;
			try
			{
				Assert.NotNull(processor.Patch());
			}
			catch (Exception ex)
			{
				exception = ex;
			}
			Assert.NotNull(exception);
			Assert.NotNull(exception.InnerException);
			Assert.True(exception.InnerException.Message.Contains("returned an unexpected result: some element was null"));
		}
	}
}