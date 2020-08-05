using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
#if NETCOREAPP
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
#endif
using HarmonyLib;
using NUnit.Framework;
using NUnit.Framework.Constraints;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;

namespace HarmonyLibTests
{
	public interface ITestIsolationContext
	{
		void AssemblyLoad(string name);

		void ParentCallback<T>(Action<T> callback, T arg);
	}

	public static class TestTools
	{
		// Change this from TestContext.Out to TestContext.Error for immediate output to stderr to help diagnose crashes.
		// Note: Must be a property rather than a field, since the specific TestContext streams can change between tests.
		static TextWriter LogWriter => TestContext.Out;

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

		class CaptureResultConstraint : IConstraint
		{
			readonly IResolveConstraint parent;
			IConstraint resolvedParent;
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

			ConstraintResult CaptureResult(ConstraintResult result)
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

		// Run an action in a test isolation context.
		public static void RunInIsolationContext(Action<ITestIsolationContext> action)
		{
#if NETCOREAPP
			TestAssemblyLoadContext.RunInIsolationContext(action);
#else
			TestDomainProxy.RunInIsolationContext(action);
#endif
		}

#if NETCOREAPP
		// .NET Core does not support multiple AppDomains, but it does support unloading assemblies via AssemblyLoadContext.
		// Based off sample code in https://docs.microsoft.com/en-us/dotnet/standard/assembly/unloadability
		class TestAssemblyLoadContext : AssemblyLoadContext, ITestIsolationContext
		{
			// Run an action in "isolation" (collectible AssemblyLoadContext that's unloaded afterwards).
			public static void RunInIsolationContext(Action<ITestIsolationContext> action)
			{
				var alcWeakRef = RunInAssemblyLoadContext(action);
				// Ensure test assembly load context is unloaded before ending this test.
				for (var i = 0; alcWeakRef.IsAlive && i < 10; i++)
				{
					GC.Collect();
					GC.WaitForPendingFinalizers();
				}
			}

			// These must be a separate non-inlined method so that the TestAssemblyLoadContext it creates can be Unload()-ed and GC-ed
			// (which is required for the unloading to finish).
			[MethodImpl(MethodImplOptions.NoInlining)]
			static WeakReference RunInAssemblyLoadContext(Action<ITestIsolationContext> action)
			{
				var alc = new TestAssemblyLoadContext();
				var alcWeakRef = new WeakReference(alc, trackResurrection: true);
				action(alc);
				alc.Unload();
				return alcWeakRef;
			}

			public TestAssemblyLoadContext() : base(isCollectible: true) { }

			protected override Assembly Load(AssemblyName name)
			{
				// Defer loading of assembly's dependencies to parent (AssemblyLoadContext.Default) assembly load context.
				return null;
			}

			public void AssemblyLoad(string name)
			{
				LoadFromAssemblyPath(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, name + ".dll"));
			}

			// There's no separate AppDomain, so this is just an alias for callback(arg).
			public void ParentCallback<T>(Action<T> callback, T arg)
			{
				callback(arg);
			}
		}
#else
		// For .NET Framework and its multiple AppDomain support, need a MarshalByRefObject, so that for an instance created
		// via appDomain.CreateInstanceAndUnwrap, all calls to that instance's methods are executed in that appDomain.

		class TestDomainProxy : MarshalByRefObject, ITestIsolationContext
		{
			readonly AppDomain parentDomain;

			// Run an action in "isolation" (seperate AppDomain that's unloaded afterwards).
			// This a static method and thus is run in the AppDomain of the caller (the main AppDomain).
			public static void RunInIsolationContext(Action<ITestIsolationContext> action)
			{
				var testDomain = AppDomain.CreateDomain("TestDomain", AppDomain.CurrentDomain.Evidence, new AppDomainSetup
				{
					ApplicationBase = AppDomain.CurrentDomain.SetupInformation.ApplicationBase,
				});
				// There's no simpler way to call a non-parameterless constructor than this monstrosity.
				var proxy = (TestDomainProxy)testDomain.CreateInstanceAndUnwrap(
					typeof(TestDomainProxy).Assembly.FullName, typeof(TestDomainProxy).FullName, default, default, default,
					new object[] { AppDomain.CurrentDomain }, default, default
#if NET35
					, default // .NET Framework requires obsolete Evidence parameter overload
#endif
					);
				proxy.Run(action);
				AppDomain.Unload(testDomain);
			}

			public TestDomainProxy(AppDomain parentDomain)
			{
				this.parentDomain = parentDomain;
			}

			// Rules for proxy instance methods:
			// Ensure that all loaded Types of the dummy assemblies are never leaked out of the test domain, so:
			// 1) never return loaded Types (or instances of those Types); and
			// 2) always catch exceptions that may contain loaded Types (or instances of those Types) directly.
			// As long as there is no such leakage, AppDomain.Unload will fully unload the domain and all its assemblies.

			void Run(Action<ITestIsolationContext> action)
			{
				action(this);
			}

			// Note: Console usage won't work within a non-main domain - that has to be delegated to the main domain via a callback.
			public void ParentCallback<T>(Action<T> action, T arg)
			{
				parentDomain.DoCallBack(new ActionTCallback<T>(action, arg).Call);
			}

			// Delegates used for DoCallback must be serializable.
			[Serializable]
			class ActionTCallback<T>
			{
				readonly Action<T> action;
				readonly T arg;

				public ActionTCallback(Action<T> action, T arg)
				{
					this.action = action;
					this.arg = arg;
				}

				public void Call()
				{
					action(arg);
				}
			}

			public void AssemblyLoad(string assemblyName)
			{
				Assembly.Load(assemblyName);
			}
		}
#endif
	}

	public class TestLogger
	{
		static readonly PropertyInfo listenerProperty = AccessTools.Property(typeof(TestExecutionContext), "Listener");

		// .NET Framework 3.5 doesn't have Lazy, so using the singleton lazy instantiation workaround.
		class StartDateTimeSingleton
		{
			public static DateTime value = DateTime.Now;
		}

		class RecordingListener : ITestListener
		{
			public readonly ITestListener origListener;
			readonly TextWriter stringWriter;
			public readonly StringBuilder stringBuilder;

			public RecordingListener(ITestListener origListener)
			{
				this.origListener = origListener;
				stringBuilder = new StringBuilder();
				stringWriter = TextWriter.Synchronized(new StringWriter(stringBuilder));
			}

			public void SendMessage(TestMessage message)
			{
				origListener.SendMessage(message);
			}

			public void TestFinished(ITestResult result)
			{
				origListener.TestFinished(result);
			}

			public void TestOutput(TestOutput output)
			{
				origListener.TestOutput(output);
				stringWriter.Write(output.Text);
			}

			public void TestStarted(ITest test)
			{
				origListener.TestStarted(test);
			}
		}

		[SetUp]
		public void BaseSetUp()
		{
			var context = TestExecutionContext.CurrentContext;
			TestTools.Log($"### {context.CurrentResult.FullName}", indentLevel: 0);

			// Azure Pipelines doesn't upload stdout/stderr for non-failed tests, we need to write out test stdout/stderr to files and attach them.
			if (Environment.GetEnvironmentVariable("AGENT_TEMPDIRECTORY") != null)
			{
				// Console.Out is already redirected to TestExecutionContext.CurrentContext.CurrentResult.OutWriter,
				// from which the standard output can be extracted.
				// Console.Error/TestContext.Error/TestContext.Progress are all redirected to EventListenerTextWriter,
				// which calls TestExecutionContext.Listener.TestOutput, so we temporarily redirect TestExecutionContext.Listener
				// to a custom ITestListener than passes through to the original Listener, while recording the output to a StringWriter.
				var errorListener = (ITestListener)listenerProperty.GetValue(context, null);
				listenerProperty.SetValue(context, new RecordingListener(errorListener), null);
			}
		}

		[TearDown]
		public void BaseTearDown()
		{
			var context = TestExecutionContext.CurrentContext;
			var recordingListener = listenerProperty.GetValue(context, null) as RecordingListener;
			if (recordingListener != null)
				listenerProperty.SetValue(context, recordingListener.origListener, null);

			var result = context.CurrentResult;
			TestTools.Log($"--- {result.FullName} => {result.ResultState}", indentLevel: 0);

			// Azure Pipelines doesn't upload stdout/stderr for non-failed tests, we need to write out test stdout/stderr to files and attach them.
			var resultStatus = result.ResultState.Status;
			if (resultStatus != TestStatus.Failed && resultStatus != TestStatus.Skipped &&
				Environment.GetEnvironmentVariable("AGENT_TEMPDIRECTORY") is string agentTempDirName)
			{
				// Dirname based off the to-be-generated trx filename (not necessary but useful for manually finding files).
				var attachmentDirName = PathCombine(agentTempDirName,
					$"{Environment.UserName}_{Environment.MachineName}_{StartDateTimeSingleton.value:yyyy-MM-dd_HH_mm_ss}.trxinput",
					Path.GetRandomFileName());
				_ = Directory.CreateDirectory(attachmentDirName);

				var stdoutFileName = PathCombine(attachmentDirName, "Standard_Console_Output.log");
				File.WriteAllText(stdoutFileName, result.Output);
				TestContext.AddTestAttachment(stdoutFileName);

				var errorOutput = recordingListener.stringBuilder.ToString();
				if (errorOutput.Length > 0)
				{
					var stderrFileName = PathCombine(attachmentDirName, "Standard_Error_Output.log");
					File.WriteAllText(stderrFileName, errorOutput);
					TestContext.AddTestAttachment(stderrFileName);
				}
			}
		}

		static string PathCombine(params string[] paths)
		{
#if NET35
			return paths.Aggregate("", Path.Combine);
#else
			return Path.Combine(paths);
#endif
		}
	}
}