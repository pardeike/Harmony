using HarmonyLib;
using HarmonyLibTests.Assets;
using NUnit.Framework;
using System;

namespace HarmonyLibTests
{
	[TestFixture]
	public class TargetMethod
	{
		[Test]
		public void Test_TargetMethod_Returns_Null()
		{
			var patchClass = typeof(Class15Patch);
			Assert.IsNotNull(patchClass);

			var harmonyInstance = new Harmony("test");
			Assert.IsNotNull(harmonyInstance);

			var processor = harmonyInstance.ProcessorForAnnotatedClass(patchClass);
			Assert.IsNotNull(processor);

			Exception exception = null;
			try
			{
				Assert.IsNotNull(processor.Patch());
			}
			catch (Exception ex)
			{
				exception = ex;
			}
			Assert.IsNotNull(exception);
			Assert.IsTrue(exception.Message.Contains("returned an unexpected result: null"));
		}

		[Test]
		public void Test_TargetMethod_Returns_Wrong_Type()
		{
			var patchClass = typeof(Class16Patch);
			Assert.IsNotNull(patchClass);

			var harmonyInstance = new Harmony("test");
			Assert.IsNotNull(harmonyInstance);

			var processor = harmonyInstance.ProcessorForAnnotatedClass(patchClass);
			Assert.IsNotNull(processor);

			Exception exception = null;
			try
			{
				Assert.IsNotNull(processor.Patch());
			}
			catch (Exception ex)
			{
				exception = ex;
			}
			Assert.IsNotNull(exception);
			Assert.IsTrue(exception.Message.Contains("has wrong return type"));
		}

		[Test]
		public void Test_TargetMethods_Returns_Null()
		{
			var patchClass = typeof(Class17Patch);
			Assert.IsNotNull(patchClass);

			var harmonyInstance = new Harmony("test");
			Assert.IsNotNull(harmonyInstance);

			var processor = harmonyInstance.ProcessorForAnnotatedClass(patchClass);
			Assert.IsNotNull(processor);

			Exception exception = null;
			try
			{
				Assert.IsNotNull(processor.Patch());
			}
			catch (Exception ex)
			{
				exception = ex;
			}
			Assert.IsNotNull(exception);
			Assert.IsTrue(exception.Message.Contains("returned an unexpected result: some element was null"));
		}
	}
}