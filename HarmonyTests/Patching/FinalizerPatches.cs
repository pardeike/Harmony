using HarmonyLib;
using HarmonyLibTests.Assets;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace HarmonyLibTests
{
	[TestFixture]
	public class FinalizerPatches
	{
		readonly bool debug = false;
		Dictionary<string, object> info;

		[Test]
		public void Test_NoThrowingVoidMethod_EmptyFinalizer()
		{
			AssertNoThrownException();
			AssertGotNoResult();
		}

		[Test]
		public void Test_NoThrowingVoidMethod_EmptyFinalizerWithExceptionArg()
		{
			AssertNoThrownException();
			AssertNullExceptionInput();
			AssertGotNoResult();
		}

		[Test]
		public void Test_NoThrowingVoidMethod_FinalizerReturningNull()
		{
			AssertNoThrownException();
			AssertGotNoResult();
		}

		[Test]
		public void Test_NoThrowingVoidMethod_FinalizerReturningException()
		{
			AssertThrownException<ReplacedException>();
			AssertNullExceptionInput();
			AssertGotNoResult();
		}

		//

		[Test]
		public void Test_ThrowingVoidMethod_EmptyFinalizer()
		{
			AssertThrownException<OriginalException>();
			AssertGotNoResult();
		}

		[Test]
		public void Test_ThrowingVoidMethod_EmptyFinalizerWithExceptionArg()
		{
			AssertThrownException<OriginalException>();
			AssertExceptionInput<OriginalException>();
			AssertGotNoResult();
		}

		[Test]
		public void Test_ThrowingVoidMethod_FinalizerReturningNull()
		{
			AssertNoThrownException();
			AssertGotNoResult();
		}

		[Test]
		public void Test_ThrowingVoidMethod_FinalizerReturningException()
		{
			AssertThrownException<ReplacedException>();
			AssertExceptionInput<OriginalException>();
			AssertGotNoResult();
		}

		//

		[Test]
		public void Test_NoThrowingStringReturningMethod_EmptyFinalizer()
		{
			AssertNoThrownException();
			AssertGotResult("OriginalResult");
		}

		[Test]
		public void Test_NoThrowingStringReturningMethod_EmptyFinalizerWithExceptionArg()
		{
			AssertNoThrownException();
			AssertNullExceptionInput();
			AssertGotResult("OriginalResult");
		}

		[Test]
		public void Test_NoThrowingStringReturningMethod_FinalizerReturningNull()
		{
			AssertNoThrownException();
			AssertGotResult("OriginalResult");
		}

		[Test]
		public void Test_NoThrowingStringReturningMethod_FinalizerReturningException()
		{
			AssertThrownException<ReplacedException>();
			AssertNullExceptionInput();
			AssertGotNoResult();
		}

		[Test]
		public void Test_NoThrowingStringReturningMethod_FinalizerReturningNullAndChangingResult()
		{
			AssertNoThrownException();
			AssertGotResult("ReplacementResult");
		}

		[Test]
		public void Test_NoThrowingStringReturningMethod_FinalizerReturningExceptionAndChangingResult()
		{
			AssertThrownException<ReplacedException>();
			AssertGotNoResult();
		}

		//

		[Test]
		public void Test_ThrowingStringReturningMethod_EmptyFinalizer()
		{
			if (Type.GetType("Mono.Runtime") != null)
				Assert.Ignore("Mono runtime cannot handle invalid IL in dead code. Test ignored.");

			AssertThrownException<OriginalException>();
			AssertGotNoResult();
		}

		[Test]
		public void Test_ThrowingStringReturningMethod_EmptyFinalizerWithExceptionArg()
		{
			if (Type.GetType("Mono.Runtime") != null)
				Assert.Ignore("Mono runtime cannot handle invalid IL in dead code. Test ignored.");

			AssertThrownException<OriginalException>();
			AssertExceptionInput<OriginalException>();
			AssertGotNoResult();
		}

		[Test]
		public void Test_ThrowingStringReturningMethod_FinalizerReturningNull()
		{
			if (Type.GetType("Mono.Runtime") != null)
				Assert.Ignore("Mono runtime cannot handle invalid IL in dead code. Test ignored.");

			AssertNoThrownException();
			AssertGotNullResult();
		}

		[Test]
		public void Test_ThrowingStringReturningMethod_FinalizerReturningException()
		{
			if (Type.GetType("Mono.Runtime") != null)
				Assert.Ignore("Mono runtime cannot handle invalid IL in dead code. Test ignored.");

			AssertThrownException<ReplacedException>();
			AssertExceptionInput<OriginalException>();
			AssertGotNoResult();
		}

		[Test]
		public void Test_ThrowingStringReturningMethod_FinalizerReturningNullAndChangingResult()
		{
			if (Type.GetType("Mono.Runtime") != null)
				Assert.Ignore("Mono runtime cannot handle invalid IL in dead code. Test ignored.");

			AssertNoThrownException();
			AssertGotResult("ReplacementResult");
		}

		[Test]
		public void Test_ThrowingStringReturningMethod_FinalizerReturningExceptionAndChangingResult()
		{
			if (Type.GetType("Mono.Runtime") != null)
				Assert.Ignore("Mono runtime cannot handle invalid IL in dead code. Test ignored.");

			AssertThrownException<ReplacedException>();
			AssertGotNoResult();
		}

		//

		[SetUp]
		public void SetUp()
		{
			var testMethod = TestContext.CurrentContext.Test.Name;
			var parts = testMethod.Split('_');
			var originalType = AccessTools.TypeByName("HarmonyLibTests.Assets." + parts[1]);
			var patchType = AccessTools.TypeByName("HarmonyLibTests.Assets." + parts[2]);

			Assert.IsNotNull(originalType);
			var originalMethod = originalType.GetMethod("Method");
			Assert.IsNotNull(originalMethod);

			var finalizer = patchType.GetMethod("Finalizer");
			Assert.IsNotNull(finalizer);

			if (debug)
			{
				FileLog.Log("### Original: " + parts[1]);
				FileLog.Log("### Patching: " + parts[2]);
			}
			var instance = new Harmony("test");
			Assert.IsNotNull(instance);
			instance.UnpatchAll();
			var patcher = instance.CreateProcessor(originalMethod);
			Assert.IsNotNull(patcher);
			_ = patcher.AddFinalizer(finalizer);
			if (debug) Harmony.DEBUG = true;
			_ = patcher.Patch();
			Harmony.DEBUG = false;

			var trv = Traverse.Create(patchType);
			_ = trv.Field("finalized").SetValue(false);
			_ = trv.Field("exception").SetValue(new NullReferenceException("replace-me"));

			var obj = Activator.CreateInstance(originalType);
			var m_method = AccessTools.Method(originalType, "Method");
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
			Assert.IsTrue((bool)info["finalized"], "Finalizer not called");
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
			Assert.IsNull(info["result"], "Result should be null");
		}

		private void AssertGotNoResult()
		{
			Assert.NotNull(info, "info should not be null");
			Assert.False(info.ContainsKey("result"), "Should not return result");
		}

		private void AssertNoThrownException()
		{
			Assert.NotNull(info, "info should not be null");
			Assert.IsNull(info["outerexception"], "Should not throw an exception");
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
			Assert.IsNull(info["exception"], "Finalizer should get null exception input");
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