using HarmonyLib;
using NUnit.Framework;
using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace HarmonyLibTests.Extras
{
	[TestFixture, NonParallelizable]
	class RetrieveOriginalMethod : TestLogger
	{
		private static void CheckStackTraceFor(MethodBase expectedMethod)
		{
			Assert.NotNull(expectedMethod);

			var st = new StackTrace(1, false);
			var frame = st.GetFrame(0);
			Assert.NotNull(frame);

			var methodFromStackframe = Harmony.GetMethodFromStackframe(frame);
			Assert.NotNull(methodFromStackframe);
			Assert.AreEqual(expectedMethod, methodFromStackframe);

			var replacement = frame.GetMethod() as MethodInfo;
			Assert.NotNull(replacement);
			var original = Harmony.GetOriginalMethod(replacement);
			Assert.NotNull(original);
			Assert.AreEqual(expectedMethod, original);
		}

		[Test]
		public void TestRegularMethod()
		{
			var harmony = new Harmony("test-original-method");
			var originalMethod = SymbolExtensions.GetMethodInfo(() => PatchTarget());
			var dummyPrefix = SymbolExtensions.GetMethodInfo(() => DummyPrefix());
			_ = harmony.Patch(originalMethod, new HarmonyMethod(dummyPrefix));
			PatchTarget();
		}

		[Test]
		public void TestConstructor()
		{
			var harmony = new Harmony("test-original-method-1");
			var originalMethod = AccessTools.Constructor(typeof(NestedClass), [typeof(int)]);
			var dummyPrefix = SymbolExtensions.GetMethodInfo(() => DummyPrefix());
			_ = harmony.Patch(originalMethod, new HarmonyMethod(dummyPrefix));
			var inst = new NestedClass(5);
			_ = inst.index;
		}

		internal static void PatchTarget()
		{
			try
			{
				CheckStackTraceFor(AccessTools.Method(typeof(RetrieveOriginalMethod), nameof(PatchTarget))); // call this from within PatchTarget
				throw new Exception();
			}
			catch (Exception e)
			{
				_ = e;
			}
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		internal static void DummyPrefix()
		{
		}

		class NestedClass
		{
			public NestedClass(int i)
			{
				try
				{
					CheckStackTraceFor(AccessTools.Constructor(typeof(NestedClass), [typeof(int)]));
					throw new Exception();
				}
				catch (Exception e)
				{
					_ = e;
				}
				index = i;
			}

			public int index;
		}
	}
}
