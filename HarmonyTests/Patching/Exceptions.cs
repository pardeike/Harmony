using HarmonyLib;
using HarmonyLibTests.Assets;
using NUnit.Framework;
using System;

namespace HarmonyLibTests.Patching
{
	[TestFixture, NonParallelizable]
	public class TestExceptionFilterBlock
	{
		Harmony harmony;
		[SetUp]
		public void SetUp() => harmony = new Harmony("test.exception.filters." + Guid.NewGuid());
		[TearDown]
		public void TearDown() => harmony.UnpatchAll(harmony.Id);
		static void Prefix() { }

		[Test]
		public void TestExceptionsWithFilter()
		{
			var originalClass = typeof(ClassExceptionFilter);
			Assert.NotNull(originalClass);
			var originalMethod = originalClass.GetMethod("Method1");
			Assert.NotNull(originalMethod);

			var instance = harmony;
			Assert.NotNull(instance);

			var patcher = new PatchProcessor(instance, originalMethod);
			patcher.AddPrefix(AccessTools.DeclaredMethod(typeof(TestExceptionFilterBlock), nameof(Prefix)));
			Assert.NotNull(patcher);
			_ = patcher.Patch();

			ClassExceptionFilter.Method1();
		}

		[Test]
		public void TestPlainMethodExceptions()
		{
			var originalClass = typeof(ClassExceptionFilter);
			Assert.NotNull(originalClass);
			var originalMethod = originalClass.GetMethod("Method2");
			Assert.NotNull(originalMethod);

			var instance = harmony;
			Assert.NotNull(instance);

			var patcher = new PatchProcessor(instance, originalMethod);
			patcher.AddPrefix(AccessTools.DeclaredMethod(typeof(TestExceptionFilterBlock), nameof(Prefix)));
			Assert.NotNull(patcher);
			_ = patcher.Patch();

			var result = ClassExceptionFilter.Method2(null);
			Assert.AreEqual(100, result);
			Assert.AreEqual(101, ClassExceptionFilter.Method2(new Exception("test")));
			Assert.AreEqual(110, ClassExceptionFilter.Method2(new ArithmeticException("arithmetic")));
			Assert.Throws<InvalidOperationException>(() => ClassExceptionFilter.Method2(new InvalidOperationException("unmatched")));
		}
	}
}
