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
			AssertThrownException<OriginalException>();
			AssertGotNoResult();
		}

		[Test]
		public void Test_ThrowingStringReturningMethod_EmptyFinalizerWithExceptionArg()
		{
			AssertThrownException<OriginalException>();
			AssertExceptionInput<OriginalException>();
			AssertGotNoResult();
		}

		[Test]
		public void Test_ThrowingStringReturningMethod_FinalizerReturningNull()
		{
			AssertNoThrownException();
			AssertGotResult(null);
		}

		[Test]
		public void Test_ThrowingStringReturningMethod_FinalizerReturningException()
		{
			AssertThrownException<ReplacedException>();
			AssertExceptionInput<OriginalException>();
			AssertGotNoResult();
		}

		[Test]
		public void Test_ThrowingStringReturningMethod_FinalizerReturningNullAndChangingResult()
		{
			AssertNoThrownException();
			AssertGotResult("ReplacementResult");
		}

		[Test]
		public void Test_ThrowingStringReturningMethod_FinalizerReturningExceptionAndChangingResult()
		{
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

			Harmony.SELF_PATCHING = false;
			if (Harmony.DEBUG)
			{
				FileLog.Reset();
				FileLog.Log("### Original: " + parts[1]);
				FileLog.Log("### Patching: " + parts[2]);
			}
			var instance = new Harmony("test");
			Assert.IsNotNull(instance);
			instance.Unpatch(originalMethod, HarmonyPatchType.All);
			var patcher = instance.CreateProcessor(originalMethod);
			Assert.IsNotNull(patcher);
			_ = patcher.AddFinalizer(finalizer);
			_ = patcher.Patch();

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
					info["result"] = m_method.Invoke(obj, null);
				info["outerexception"] = null;
			}
			catch (TargetInvocationException e)
			{
				info["outerexception"] = e.InnerException;
			}
			trv.Fields().ForEach(name => info[name] = trv.Field(name).GetValue());

			instance.UnpatchAll();

			Assert.IsTrue((bool)info["finalized"], "Finalizer not called");
		}

		private void AssertGotResult(string str)
		{
			Assert.True(info.ContainsKey("result"), "Should return result");
			Assert.AreEqual(str, info["result"]);
		}

		private void AssertGotNoResult()
		{
			Assert.False(info.ContainsKey("result"), "Should not return result");
		}

		private void AssertNoThrownException()
		{
			Assert.IsNull(info["outerexception"], "Should not throw an exception");
		}

		private void AssertThrownException<E>()
		{
			Assert.NotNull(info["outerexception"], "Should throw an exception");
			Assert.IsInstanceOf(typeof(E), info["outerexception"]);
		}

		private void AssertNullExceptionInput()
		{
			Assert.True(info.ContainsKey("exception"), "Finalizer should have an exception field");
			Assert.IsNull(info["exception"], "Finalizer should get null exception input");
		}

		private void AssertExceptionInput<E>()
		{
			Assert.True(info.ContainsKey("exception"), "Finalizer should have an exception field");
			Assert.NotNull(info["exception"], "Finalizer should get an exception input");
			Assert.IsInstanceOf(typeof(E), info["exception"]);
		}
	}
}