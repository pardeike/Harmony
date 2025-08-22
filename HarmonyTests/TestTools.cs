using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
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
		// Note: This must be a property rather than a field, since the specific TestContext streams can change between tests.
		static TextWriter LogWriterOut => TestContext.Out;
		static TextWriter LogWriterError => TestContext.Error;

		public static void WriteLine(string text, bool isError) => (isError ? LogWriterError : LogWriterOut).WriteLine(text);

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

		public static ConstraintResult AssertThat(TestDelegate code, IResolveConstraint constraint, string message = null, params object[] args)
		{
			var capture = new CaptureResultConstraint(constraint);
			Assert.That(code, capture, message, args);
			return capture.capturedResult;
		}

		class CaptureResultConstraint(IResolveConstraint parent) : IConstraint
		{
			readonly IResolveConstraint parent = parent;
			IConstraint resolvedParent;
			public ConstraintResult capturedResult;

			public string DisplayName => throw new NotImplementedException();

			public string Description => throw new NotImplementedException();

			public object[] Arguments => throw new NotImplementedException();

			public ConstraintBuilder Builder { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

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

			public ConstraintResult ApplyTo<TActual>(TActual actual) => CaptureResult(resolvedParent.ApplyTo(actual));

			public ConstraintResult ApplyTo<TActual>(ActualValueDelegate<TActual> del) => CaptureResult(resolvedParent.ApplyTo(del));

			public ConstraintResult ApplyTo<TActual>(ref TActual actual) => CaptureResult(resolvedParent.ApplyTo(ref actual));

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

		public static string GetAssemblyTempDirectory()
		{
			var path = Path.Combine(Path.GetTempPath(), "HarmonyTests_" + Guid.NewGuid().ToString("N"));
			_ = Directory.CreateDirectory(path);
			return path;
		}

		// Run an action in a test isolation context.

		public static void RunInIsolationContext(Action<ITestIsolationContext> action) =>
#if NETCOREAPP
			TestAssemblyLoadContext.RunInIsolationContext(action);
#else
			TestDomainProxy.RunInIsolationContext(action);
#endif

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

			protected override Assembly Load(AssemblyName name) =>
				// Defer loading of assembly's dependencies to parent (AssemblyLoadContext.Default) assembly load context.
				null;

			public void AssemblyLoad(string name)
			{
				// First check if the assembly is in a temp directory used by our tests
				foreach (var dir in Directory.GetDirectories(Path.GetTempPath(), "HarmonyTests_*"))
				{
					var possiblePath = Path.Combine(dir, name + ".dll");
					if (File.Exists(possiblePath))
					{
						_ = LoadFromAssemblyPath(possiblePath);
						return;
					}
				}

				// Fallback to base directory
				_ = LoadFromAssemblyPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, name + ".dll"));
			}

			// There's no separate AppDomain, so this is just an alias for callback(arg).
			public void ParentCallback<T>(Action<T> callback, T arg) => callback(arg);
		}
#else
		// For .NET Framework and its multiple AppDomain support, need a MarshalByRefObject, so that for an instance created
		// via appDomain.CreateInstanceAndUnwrap, all calls to that instance's methods are executed in that appDomain.

		class TestDomainProxy(AppDomain parentDomain) : MarshalByRefObject, ITestIsolationContext
		{
			readonly AppDomain parentDomain = parentDomain;

			// Run an action in "isolation" (separate AppDomain that's unloaded afterwards)
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
					[AppDomain.CurrentDomain], default, default
#if NET35
					, default // .NET Framework requires obsolete Evidence parameter overload
#endif
					);
				proxy.Run(action);
				AppDomain.Unload(testDomain);
			}

			// Rules for proxy instance methods:
			// Ensure that all loaded Types of the dummy assemblies are never leaked out of the test domain, so:
			// 1) never return loaded Types (or instances of those Types); and
			// 2) always catch exceptions that may contain loaded Types (or instances of those Types) directly.
			// As long as there is no such leakage, AppDomain.Unload will fully unload the domain and all its assemblies.

			void Run(Action<ITestIsolationContext> action) => action(this);

			// Note: Console usage won't work within a non-main domain - that has to be delegated to the main domain via a callback.
			public void ParentCallback<T>(Action<T> action, T arg) => parentDomain.DoCallBack(new ActionTCallback<T>(action, arg).Call);

			// Delegates used for DoCallback must be serializable.
			[Serializable]
			class ActionTCallback<T>(Action<T> action, T arg)
			{
				readonly Action<T> action = action;
				readonly T arg = arg;

				public void Call() => action(arg);
			}

			public void AssemblyLoad(string assemblyName)
			{
				// First check if the assembly is in a temp directory used by our tests
				foreach (var dir in Directory.GetDirectories(Path.GetTempPath(), "HarmonyTests_*"))
				{
					var possiblePath = Path.Combine(dir, assemblyName + ".dll");
					if (File.Exists(possiblePath))
					{
						_ = Assembly.LoadFile(possiblePath);
						return;
					}
				}

				// Fallback to regular load
				_ = Assembly.Load(assemblyName);
			}
		}
#endif
	}

	public class TestLogger
	{
		class ExplicitException(string message) : ResultStateException(message)
		{
			public override ResultState ResultState => ResultState.Explicit;
		}

		[SetUp]
		public void BaseSetUp() => SkipExplicitTestIfVSTest();

		// Workaround for [Explicit] attribute sometimes not working in the NUnit3 VS Test Adapter, which applies to both Visual Studio and
		// vstest.console (bug: https://github.com/nunit/nunit3-vs-adapter/issues/658). It does apparently work with `dotnet test` as long
		// as the test dll isn't specified (which delegates to vstest.console).
		// So always skip [Explicit] tests when NUnit3 VS Test Adapter is used - the [Explicit] attribute needs to be commented out to run the test.
		static void SkipExplicitTestIfVSTest()
		{
			var test = TestExecutionContext.CurrentContext.CurrentTest;
			if (test.Method?.IsDefined<ExplicitAttribute>(true) ?? test.TypeInfo?.IsDefined<ExplicitAttribute>(true) ?? false)
			{
				// Due to the way the NUnit3 VS Test Adapter creates separate AppDomains for tests and the difficulty with getting process
				// command line arguments in a cross-platform way, there's no direct way to determine whether the adapter is being used.
				// Indirect ways to determine whether the adapter is used in various ways:
				// 1) process name starts with "testhost" (e.g. testhost.x86)
				// 2) process name starts with "vstest" (e.g. vstest.console)
				using var process = Process.GetCurrentProcess();
				if (process.ProcessName.StartsWith("testhost") || process.ProcessName.StartsWith("vstest"))
					throw GetExplicitException(test);
				// 3) process modules include a *VisualStudio* dll
				// This case is needed when run under mono, since process name is just "mono(.exe)" then.
				if (process.Modules.Cast<ProcessModule>().Any(module => module.ModuleName.StartsWith("Microsoft.VisualStudio")))
					throw GetExplicitException(test);
			}
		}

		static ExplicitException GetExplicitException(Test test)
		{
			// This is the least fragile way to get the explicit reason message.
			var explicitAttribute = test.GetCustomAttributes<ExplicitAttribute>(true).First();
			explicitAttribute.ApplyToTest(test);
			return new ExplicitException((string)test.Properties.Get(PropertyNames.SkipReason) ?? "");
		}
	}
}
