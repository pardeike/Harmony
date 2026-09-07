using HarmonyLib;
using NUnit.Framework;
using System;
using System.Runtime.CompilerServices;

namespace HarmonyLibTests.Patching
{
	[TestFixture, NonParallelizable]
	public class InfixFinalizerResults : TestLogger
	{
		class Payload { public int Value = 7; }
		static int observedResult;
		static Exception observedException;
		[MethodImpl(MethodImplOptions.NoInlining)] static int Read(Payload payload) => 5 + payload.Value;
		[MethodImpl(MethodImplOptions.NoInlining)]
		static Exception Recover(Exception __exception, ref int __result)
		{
			observedResult = __result;
			observedException = __exception;
			__result += 10;
			return null;
		}

		[TestCase(false), TestCase(true)]
		public void Finalizer_reads_the_default_result_after_a_fault(bool invokeWrapper)
		{
			var harmony = new Harmony("test.infix.finalizer.result." + Guid.NewGuid());
			var original = AccessTools.DeclaredMethod(typeof(InfixFinalizerResults), nameof(Read));
			var finalizer = new HarmonyMethod(AccessTools.DeclaredMethod(typeof(InfixFinalizerResults), nameof(Recover)))
			{
				innerTarget = new InnerTarget(AccessTools.Field(typeof(Payload), nameof(Payload.Value)), InnerTargetKind.FieldRead)
			};
			try
			{
				var wrapper = harmony.CreateProcessor(original).AddInnerFinalizer(finalizer).Patch();
				observedResult = -1;
				observedException = null;
				var result = invokeWrapper ? (int)wrapper.Invoke(null, [null]) : Read(null);
				Assert.IsInstanceOf<NullReferenceException>(observedException);
				Assert.AreEqual(0, observedResult, "The operation did not return a value to replace the initialized result.");
				Assert.AreEqual(15, result);
			}
			finally { harmony.UnpatchAll(harmony.Id); }
		}
	}
}
