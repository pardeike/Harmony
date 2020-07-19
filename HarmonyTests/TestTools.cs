using HarmonyLib;
using NUnit.Framework;
using NUnit.Framework.Constraints;
using NUnit.Framework.Internal;
using System;
using System.IO;
using System.Linq;

namespace HarmonyLibTests
{
	public static class TestTools
	{
		// Change this from TestContext.Out to TestContext.Error for immediate output to stderr to help diagnose crashes.
		// Note: Must be a property rather than a field, since the specific TestContext streams can change between tests.
		private static TextWriter LogWriter => TestContext.Out;

		public static void Log(object obj, int indentLevel = 1, bool writeLine = true)
		{
			var text = $"{new string('\t', indentLevel)}{obj?.ToString().Replace("\n", "\n" + new string('\t', indentLevel + 1)) ?? "null"}";
			if (writeLine)
				LogWriter.WriteLine(text);
			else
				LogWriter.Write(text);
		}

		// Workaround for [Explicit] attribute not working in Visual Studio: https://github.com/nunit/nunit3-vs-adapter/issues/658
		public static void AssertIgnoreIfVSTest()
		{
			if (System.Diagnostics.Process.GetCurrentProcess().ProcessName is "testhost")
				Assert.Ignore();
		}

		// Guarantees that assertion failures throw AssertionException, regardless of whether in Assert.Multiple mode.
		public static void AssertImmediate(TestDelegate testDelegate)
		{
			var currentContext = TestExecutionContext.CurrentContext;
			var multipleAssertLevelProp = AccessTools.Property(currentContext.GetType(), "MultipleAssertLevel");
			var origLevel = multipleAssertLevelProp.GetValue(currentContext, null);
			multipleAssertLevelProp.SetValue(currentContext, 0, null);
			try
			{
				testDelegate();
			}
			finally
			{
				multipleAssertLevelProp.SetValue(currentContext, origLevel, null);
			}
		}

		// AssertThat overloads below are a workaround for the inability to capture and expose ConstraintResult
		// (which contains IsSuccess and ActualValue) when using Assert in Assert.Multiple mode.
		// Especially useful when using Assert.That with a Throws constraint and you need to capture any caught exception.
		// Also includes a workaround for Throws constraints reporting failed assertions within the test delegate as an unexpected
		// AssertionException rather than just reporting the assertion failure message itself.

		public static ConstraintResult AssertThat<TActual>(TActual actual, IResolveConstraint expression, string message = null, params object[] args)
		{
			var capture = new CaptureResultConstraint(expression);
			Assert.That(actual, capture, message, args);
			return capture.capturedResult;
		}

		public static ConstraintResult AssertThat<TActual>(TActual actual, IResolveConstraint expression, Func<string> getExceptionMessage)
		{
			var capture = new CaptureResultConstraint(expression);
			Assert.That(actual, capture, getExceptionMessage);
			return capture.capturedResult;
		}

		public static ConstraintResult AssertThat<TActual>(ActualValueDelegate<TActual> del, IResolveConstraint expr, string message = null, params object[] args)
		{
			var capture = new CaptureResultConstraint(expr);
			Assert.That(del, capture, message, args);
			return capture.capturedResult;
		}

		public static ConstraintResult AssertThat<TActual>(ActualValueDelegate<TActual> del, IResolveConstraint expr, Func<string> getExceptionMessage)
		{
			var capture = new CaptureResultConstraint(expr);
			Assert.That(del, capture, getExceptionMessage);
			return capture.capturedResult;
		}

		public static ConstraintResult AssertThat(TestDelegate code, IResolveConstraint constraint, string message = null, params object[] args)
		{
			var capture = new CaptureResultConstraint(constraint);
			Assert.That(code, capture, message, args);
			return capture.capturedResult;
		}

		public static ConstraintResult AssertThat(TestDelegate code, IResolveConstraint constraint, Func<string> getExceptionMessage)
		{
			var capture = new CaptureResultConstraint(constraint);
			Assert.That(code, capture, getExceptionMessage);
			return capture.capturedResult;
		}

		private class CaptureResultConstraint : IConstraint
		{
			private readonly IResolveConstraint parent;
			private IConstraint resolvedParent;
			public ConstraintResult capturedResult;

			public string DisplayName => throw new NotImplementedException();

			public string Description => throw new NotImplementedException();

#pragma warning disable CA1819 // Properties should not return arrays
			public object[] Arguments => throw new NotImplementedException();
#pragma warning restore CA1819 // Properties should not return arrays

			public ConstraintBuilder Builder { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

			public CaptureResultConstraint(IResolveConstraint parent)
			{
				this.parent = parent;
			}

			private ConstraintResult CaptureResult(ConstraintResult result)
			{
				capturedResult = result;
				// If failure result is due to an AssertionException, report that assertion failure directly,
				// and return a dummy "success" constraint to avoid the redundant unexpected AssertionException report.
				if (result.IsSuccess is false && result.ActualValue is AssertionException ex)
				{
					Assert.Fail(ex.Message);
					capturedResult = new ConstraintResult(resolvedParent, null, isSuccess: false); // result returned by above AssertThat
					result = new ConstraintResult(resolvedParent, null, isSuccess: true); // result returned to Assert.That
				}
				return result;
			}

			public ConstraintResult ApplyTo<TActual>(TActual actual)
			{
				return CaptureResult(resolvedParent.ApplyTo(actual));
			}

			public ConstraintResult ApplyTo<TActual>(ActualValueDelegate<TActual> del)
			{
				return CaptureResult(resolvedParent.ApplyTo(del));
			}

			public ConstraintResult ApplyTo<TActual>(ref TActual actual)
			{
				return CaptureResult(resolvedParent.ApplyTo(ref actual));
			}

			public IConstraint Resolve()
			{
				resolvedParent = parent.Resolve();
				return this;
			}
		}

		// Returns the exception Type of a Throws constraint.
		public static Type ThrowsConstraintExceptionType(IConstraint resolvedConstraint)
		{
			switch (resolvedConstraint)
			{
				case ThrowsNothingConstraint _:
					return null;
				case ThrowsExceptionConstraint _:
					return typeof(Exception);
				case ThrowsConstraint _ when resolvedConstraint.Arguments[0] is TypeConstraint typeConstraint:
					return (Type)typeConstraint.Arguments[0];
				default:
					throw new ArgumentException("unrecognized Throws constraint");
			}
		}
	}

	public class TestLogger
	{
		[SetUp]
		public void BaseSetUp()
		{
			var args = TestContext.CurrentContext.Test.Arguments.Select(a => a.ToString()).ToArray().Join();
			if (args.Length > 0) args = $"({args})";
			TestTools.Log($"### {TestContext.CurrentContext.Test.MethodName}({args})", indentLevel: 0);
		}

		[TearDown]
		public void BaseTearDown()
		{
			TestTools.Log($"--- {TestContext.CurrentContext.Test.MethodName}", indentLevel: 0);
		}
	}
}