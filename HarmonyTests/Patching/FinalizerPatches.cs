using HarmonyLib;
using HarmonyLibTests.Assets;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace HarmonyLibTests.Patching
{
	[TestFixture]
	public class FinalizerPatches : TestLogger
	{
		static Dictionary<string, object> info;

		[Test]
		public void Test_NoThrowingVoidMethod_EmptyFinalizer()
		{
			Patch();
			AssertNoThrownException();
			AssertGotNoResult();
		}

		[Test]
		public void Test_NoThrowingVoidMethod_EmptyFinalizerWithExceptionArg()
		{
			Patch();
			AssertNoThrownException();
			AssertNullExceptionInput();
			AssertGotNoResult();
		}

		[Test]
		public void Test_NoThrowingVoidMethod_FinalizerReturningNull()
		{
			Patch();
			AssertNoThrownException();
			AssertGotNoResult();
		}

		[Test]
		public void Test_NoThrowingVoidMethod_FinalizerReturningException()
		{
			Patch();
			AssertThrownException<ReplacedException>();
			AssertNullExceptionInput();
			AssertGotNoResult();
		}

		//

		[Test]
		public void Test_ThrowingVoidMethod_EmptyFinalizer()
		{
			Patch();
			AssertThrownException<OriginalException>();
			AssertGotNoResult();
		}

		[Test]
		public void Test_ThrowingVoidMethod_EmptyFinalizerWithExceptionArg()
		{
			Patch();
			AssertThrownException<OriginalException>();
			AssertExceptionInput<OriginalException>();
			AssertGotNoResult();
		}

		[Test]
		public void Test_ThrowingVoidMethod_FinalizerReturningNull()
		{
			Patch();
			AssertNoThrownException();
			AssertGotNoResult();
		}

		[Test]
		public void Test_ThrowingVoidMethod_FinalizerReturningException()
		{
			Patch();
			AssertThrownException<ReplacedException>();
			AssertExceptionInput<OriginalException>();
			AssertGotNoResult();
		}

		//

		[Test]
		public void Test_NoThrowingStringReturningMethod_EmptyFinalizer()
		{
			Patch();
			AssertNoThrownException();
			AssertGotResult("OriginalResult");
		}

		[Test]
		public void Test_NoThrowingStringReturningMethod_EmptyFinalizerWithExceptionArg()
		{
			Patch();
			AssertNoThrownException();
			AssertNullExceptionInput();
			AssertGotResult("OriginalResult");
		}

		[Test]
		public void Test_NoThrowingStringReturningMethod_FinalizerReturningNull()
		{
			Patch();
			AssertNoThrownException();
			AssertGotResult("OriginalResult");
		}

		[Test]
		public void Test_NoThrowingStringReturningMethod_FinalizerReturningException()
		{
			Patch();
			AssertThrownException<ReplacedException>();
			AssertNullExceptionInput();
			AssertGotNoResult();
		}

		[Test]
		public void Test_NoThrowingStringReturningMethod_FinalizerReturningNullAndChangingResult()
		{
			Patch();
			AssertNoThrownException();
			AssertGotResult("ReplacementResult");
		}

		[Test]
		public void Test_NoThrowingStringReturningMethod_FinalizerReturningExceptionAndChangingResult()
		{
			Patch();
			AssertThrownException<ReplacedException>();
			AssertGotNoResult();
		}

		//

		[Test]
		public void Test_ThrowingStringReturningMethod_EmptyFinalizer()
		{
			SkipIfMono();
			Patch();
			AssertThrownException<OriginalException>();
			AssertGotNoResult();
		}

		[Test]
		public void Test_ThrowingStringReturningMethod_EmptyFinalizerWithExceptionArg()
		{
			SkipIfMono();
			Patch();
			AssertThrownException<OriginalException>();
			AssertExceptionInput<OriginalException>();
			AssertGotNoResult();
		}

		[Test]
		public void Test_ThrowingStringReturningMethod_FinalizerReturningNull()
		{
			SkipIfMono();
			Patch();
			AssertNoThrownException();
			AssertGotNullResult();
		}

		[Test]
		public void Test_ThrowingStringReturningMethod_FinalizerReturningException()
		{
			SkipIfMono();
			Patch();
			AssertThrownException<ReplacedException>();
			AssertExceptionInput<OriginalException>();
			AssertGotNoResult();
		}

		[Test]
		public void Test_ThrowingStringReturningMethod_FinalizerReturningNullAndChangingResult()
		{
			SkipIfMono();
			Patch();
			AssertNoThrownException();
			AssertGotResult("ReplacementResult");
		}

		[Test]
		public void Test_ThrowingStringReturningMethod_FinalizerReturningExceptionAndChangingResult()
		{
			SkipIfMono();
			Patch();
			AssertThrownException<ReplacedException>();
			AssertGotNoResult();
		}

		//

		public static void SkipIfMono()
		{
			if (AccessTools.IsMonoRuntime)
				Assert.Ignore("Mono runtime cannot handle invalid IL in dead code. Test ignored.");
		}

		public static void Patch()
		{
			var testMethod = TestContext.CurrentContext.Test.Name;
			var parts = testMethod.Split('_');
			var originalType = AccessTools.TypeByName("HarmonyLibTests.Assets." + parts[1]);
			var patchType = AccessTools.TypeByName("HarmonyLibTests.Assets." + parts[2]);

			Assert.NotNull(originalType, nameof(originalType));
			var originalMethod = originalType.GetMethod("Method");
			Assert.NotNull(originalMethod, nameof(originalMethod));

			Assert.NotNull(patchType, nameof(patchType));
			var finalizer = patchType.GetMethod("Finalizer");
			Assert.NotNull(finalizer, nameof(finalizer));

			var instance = new Harmony("finalizer-test");
			instance.UnpatchAll("finalizer-test");
			var patcher = instance.CreateProcessor(originalMethod);
			Assert.NotNull(patcher, nameof(patcher));
			_ = patcher.AddFinalizer(finalizer);
			_ = patcher.Patch();

			var trv = Traverse.Create(patchType);
			_ = trv.Field("finalized").SetValue(false);
			_ = trv.Field("exception").SetValue(new NullReferenceException("replace-me"));

			var obj = Activator.CreateInstance(originalType);
			var m_method = AccessTools.Method(originalType, "Method");
			Assert.NotNull(m_method, nameof(m_method));
			info = new Dictionary<string, object>();
			try
			{
				if (m_method.ReturnType == typeof(void))
					_ = m_method.Invoke(obj, null);
				else
					info["result"] = m_method.Invoke(obj, new object[0]);
				info["outerexception"] = null;
			}
			catch (TargetInvocationException e)
			{
				info["outerexception"] = e.InnerException;
			}
			trv.Fields().ForEach(name => info[name] = trv.Field(name).GetValue());
			Assert.True((bool)info["finalized"], "Finalizer not called");
		}

		private void AssertGotResult(string str)
		{
			Assert.NotNull(str, "str should not be null");
			Assert.NotNull(info, "info should not be null");
			Assert.True(info.ContainsKey("result"), "Should return result");
			Assert.AreEqual(str, info["result"]);
		}

		private void AssertGotNullResult()
		{
			Assert.NotNull(info, "info should not be null");
			Assert.True(info.ContainsKey("result"), "Should return result");
			Assert.Null(info["result"], "Result should be null");
		}

		private void AssertGotNoResult()
		{
			Assert.NotNull(info, "info should not be null");
			Assert.False(info.ContainsKey("result"), "Should not return result");
		}

		private void AssertNoThrownException()
		{
			Assert.NotNull(info, "info should not be null");
			Assert.Null(info["outerexception"], "Should not throw an exception");
		}

		private void AssertThrownException<E>()
		{
			Assert.NotNull(info, "info should not be null");
			Assert.NotNull(info["outerexception"], "Should throw an exception");
			Assert.IsInstanceOf(typeof(E), info["outerexception"]);
		}

		private void AssertNullExceptionInput()
		{
			Assert.NotNull(info, "info should not be null");
			Assert.True(info.ContainsKey("exception"), "Finalizer should have an exception field");
			Assert.Null(info["exception"], "Finalizer should get null exception input");
		}

		private void AssertExceptionInput<E>()
		{
			Assert.NotNull(info, "info should not be null");
			Assert.True(info.ContainsKey("exception"), "Finalizer should have an exception field");
			Assert.NotNull(info["exception"], "Finalizer should get an exception input");
			Assert.IsInstanceOf(typeof(E), info["exception"]);
		}
	}
}
