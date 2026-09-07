using HarmonyLib;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace HarmonyLibTests.Patching
{
	[TestFixture, NonParallelizable]
	public class InfixFinalizers : TestLogger
	{
		static readonly List<string> trace = [];
		static int failure;
		static int decision;
#if NET5_0_OR_GREATER
		static int backing;
#endif
		static readonly Exception operationError = new InvalidOperationException("operation");
		static readonly Exception replacementError = new ArgumentException("replacement");
		Harmony harmony;
		[SetUp]
		public void SetUp()
		{
			harmony = new Harmony("test.infix.finalizers." + Guid.NewGuid());
			trace.Clear();
			failure = 0;
			decision = 1;
#if NET5_0_OR_GREATER
			backing = 7;
#endif
		}
		[TearDown] public void TearDown() => harmony.UnpatchAll(harmony.Id);
		static MethodInfo Method(string name) => AccessTools.DeclaredMethod(typeof(InfixFinalizers), name);
		static HarmonyMethod Fix(string name, string inner) => new(Method(name)) { innerMethod = new InnerMethod(Method(inner)) };
		static void Record(string name, int at) { trace.Add(name); if (failure == at) throw new InvalidOperationException(name); }
		[MethodImpl(MethodImplOptions.NoInlining)] static int Operation(int value) { Record("call", 2); return value; }
		[MethodImpl(MethodImplOptions.NoInlining)] static int Ordinary(int value) { Record("call", 2); return value; }
		[MethodImpl(MethodImplOptions.NoInlining)] static int Outer(int value) => 5 + Operation(value);
		static void Before(out int __state) { __state = 42; Record("prefix", 1); }
		static bool Skip(ref int __result) { trace.Add("skip"); if (failure != 8) return true; __result = 23; return false; }
		static void After(int __state, ref int __result) { Record("postfix:" + __state, 3); __result++; }
		static int ReturningA(int result) { Record("returnA:" + result, 4); return result + 2; }
		static int ReturningB(int result) { Record("returnB:" + result, 5); return result + 3; }
		static void Observe(Exception __exception, int __result) => Record("observe:" + __exception?.Message + ":" + __result, 6);
		static Exception Decide(Exception __exception, int __result)
		{
			Record("decide:" + __exception?.Message + ":" + __result, 7);
			return decision == 1 ? null : decision == 2 && __exception != null ? replacementError : __exception;
		}

		static IEnumerable<TestCaseData> PhaseCases()
		{
			for (var phase = 0; phase <= 8; phase++)
				for (var action = 0; action <= 2; action++) yield return new TestCaseData(phase, action);
		}
		[TestCaseSource(nameof(PhaseCases))]
		public void Finalization_matches_ordinary_Harmony_at_every_phase(int phase, int action)
		{
			failure = phase;
			decision = action;
			var ordinary = harmony.CreateProcessor(Method(nameof(Ordinary))).AddPrefix(Method(nameof(Before))).Patch();
			ordinary = harmony.CreateProcessor(Method(nameof(Ordinary))).AddPrefix(Method(nameof(Skip))).Patch();
			foreach (var postfix in new[] { nameof(After), nameof(ReturningA), nameof(ReturningB) })
				ordinary = harmony.CreateProcessor(Method(nameof(Ordinary))).AddPostfix(Method(postfix)).Patch();
			foreach (var finalizer in new[] { nameof(Observe), nameof(Decide) })
				ordinary = harmony.CreateProcessor(Method(nameof(Ordinary))).AddFinalizer(Method(finalizer)).Patch();
			var expected = Invoke(ordinary, 10);
			var expectedTrace = trace.ToArray();
			trace.Clear();
			var processor = harmony.CreateProcessor(Method(nameof(Outer)))
				.AddInnerPrefix(Fix(nameof(Before), nameof(Operation))).AddInnerPrefix(Fix(nameof(Skip), nameof(Operation)))
				.AddInnerPostfix(Fix(nameof(After), nameof(Operation))).AddInnerPostfix(Fix(nameof(ReturningA), nameof(Operation)))
				.AddInnerPostfix(Fix(nameof(ReturningB), nameof(Operation)))
				.AddInnerFinalizer(Fix(nameof(Observe), nameof(Operation))).AddInnerFinalizer(Fix(nameof(Decide), nameof(Operation)));
			var actual = Invoke(processor.Patch(), 10);
			Assert.That(trace, Is.EqualTo(expectedTrace));
			Assert.That(actual.error?.GetType(), Is.EqualTo(expected.error?.GetType()));
			Assert.That(actual.error?.Message, Is.EqualTo(expected.error?.Message));
			if (actual.error == null) Assert.That(actual.value, Is.EqualTo(expected.value + 5));
			if (phase == 0) Assert.That(trace, Is.EqualTo(new[] { "prefix", "skip", "call", "postfix:42", "returnA:11", "returnB:13", "observe::16", "decide::16" }));
			if (phase == 5) Assert.That(trace.Any(item => item == "observe:returnB:13:11"), Is.True, "Returning values commit after the complete phase");
		}
		static (int value, Exception error) Invoke(MethodInfo wrapper, int value)
		{
			try { return ((int)wrapper.Invoke(null, [value]), null); }
			catch (TargetInvocationException error) { return (0, error.InnerException); }
		}

		[MethodImpl(MethodImplOptions.NoInlining)] static int Throwing(int value) => throw operationError;
		static Exception Recover(Exception __exception, ref int __result) { __result = 10; return null; }
		static Exception Preserve(Exception __exception) => __exception;
		static void MutateOuter([HarmonyOuter] ref int value, [HarmonyOuter] ref int destination)
		{
			value = 77;
			destination = 99;
		}
		[MethodImpl(MethodImplOptions.NoInlining)]
		static int MutableOuter(ref int destination, int value)
		{
			try { var pending = destination + Throwing(value); return pending + destination * 100 + value; }
			catch (InvalidOperationException) { return destination * 100 + value; }
		}
		[TestCase(false), TestCase(true)]
		public void Caller_backed_writes_survive_both_return_and_escaping_exception(bool suppress)
		{
			harmony.CreateProcessor(Method(nameof(MutableOuter))).AddInnerPrefix(Fix(nameof(MutateOuter), nameof(Throwing)))
				.AddInnerFinalizer(Fix(suppress ? nameof(Recover) : nameof(Preserve), nameof(Throwing))).Patch();
			var destination = 4;
			Assert.That(MutableOuter(ref destination, 3), Is.EqualTo(suppress ? 9991 : 9977));
			Assert.That(destination, Is.EqualTo(99));
		}
		static void OuterState(out int __state) => __state = 20;
		static Exception StateRecovery(Exception __exception, [HarmonyOuter] ref int __state, [HarmonyOuter] ref int __var_count, ref int __result)
		{
			__result = __state++ + __var_count++;
			return null;
		}
		[MethodImpl(MethodImplOptions.NoInlining)] static int Repeated(int count) { var sum = 0; for (var i = 0; i < count; i++) sum += Throwing(i); return sum; }
		[Test]
		public void Caller_state_and_named_locals_are_shared_between_helpers_and_reset_per_invocation()
		{
			harmony.Patch(Method(nameof(Repeated)), prefix: new HarmonyMethod(Method(nameof(OuterState))));
			harmony.CreateProcessor(Method(nameof(Repeated))).AddInnerFinalizer(Fix(nameof(StateRecovery), nameof(Throwing))).Patch();
			Assert.That(Repeated(3), Is.EqualTo(66));
			Assert.That(Repeated(3), Is.EqualTo(66));
		}

		static Exception ArrayRecovery(Exception __exception, object[] __args, [HarmonyOuter, HarmonyArgument("__args")] object[] outerArgs, ref int __result)
		{
			__result = (int)__args[0] + (int)outerArgs[0];
			outerArgs[0] = 30;
			return null;
		}
		[MethodImpl(MethodImplOptions.NoInlining)] static int ArraysOuter(int value) { var result = Throwing(value + 1); return result + value; }
		[Test]
		public void Finalizer_only_arrays_preserve_logical_value_arguments_and_write_caller_storage()
		{
			var finalizer = new HarmonyMethod(Method(nameof(ArrayRecovery))) { innerMethod = new InnerMethod(Method(nameof(Throwing))) };
			harmony.CreateProcessor(Method(nameof(ArraysOuter))).AddInnerFinalizer(finalizer).Patch();
			Assert.That(ArraysOuter(4), Is.EqualTo(39));
		}
		static Exception ArrayRestart(Exception __exception, object[] __args, ref int __result)
		{
			trace.Add("array:" + __args[0]);
			if (__exception == null) { __args[0] = 99; throw operationError; }
			__result = (int)__args[0];
			return null;
		}
		[Test]
		public void Reentered_finalizer_discards_uncommitted_array_edits()
		{
			harmony.CreateProcessor(Method(nameof(Outer))).AddInnerFinalizer(Fix(nameof(ArrayRestart), nameof(Operation))).Patch();
			Assert.That(Outer(4), Is.EqualTo(9));
			Assert.That(trace, Is.EqualTo(new[] { "call", "array:4", "array:4" }));
		}

		[MethodImpl(MethodImplOptions.NoInlining)] static bool FilterCall() => throw operationError;
		static Exception RecoverFilter(Exception __exception, ref bool __result) { __result = true; return null; }
		[MethodImpl(MethodImplOptions.NoInlining)]
		static int FilterOuter()
		{
			try { throw new ApplicationException("outer"); }
			catch (ApplicationException) when (FilterCall()) { return 21; }
		}
		[Test]
		public void A_filter_can_call_a_helper_with_its_own_exception_handling()
		{
			harmony.CreateProcessor(Method(nameof(FilterOuter))).AddInnerFinalizer(Fix(nameof(RecoverFilter), nameof(FilterCall))).Patch();
			Assert.That(FilterOuter(), Is.EqualTo(21));
		}
		[MethodImpl(MethodImplOptions.NoInlining)] static void VoidCall() => throw operationError;
		static Exception RecoverVoid(Exception __exception) { trace.Add("recover:" + __exception.Message); return null; }
		[MethodImpl(MethodImplOptions.NoInlining)] static int FinallyOuter() { try { return 5; } finally { VoidCall(); trace.Add("finally continued"); } }
		[Test]
		public void Suppression_inside_finally_continues_the_original_finally_body()
		{
			harmony.CreateProcessor(Method(nameof(FinallyOuter))).AddInnerFinalizer(Fix(nameof(RecoverVoid), nameof(VoidCall))).Patch();
			Assert.That(FinallyOuter(), Is.EqualTo(5));
			Assert.That(trace, Is.EqualTo(new[] { "recover:operation", "finally continued" }));
		}

#if NET5_0_OR_GREATER
		[MethodImpl(MethodImplOptions.NoInlining)] static ref int RefOperation() { if (failure != 0) throw operationError; return ref backing; }
		[MethodImpl(MethodImplOptions.NoInlining)] static int RefOuter() => 5 + RefOperation();
		static Exception RecoverRef(Exception __exception, ref int __result) { __result = 33; return null; }
		[TestCase(0), TestCase(1)]
		public void Ref_results_use_live_or_heap_backed_default_storage(int throwAt)
		{
			failure = throwAt;
			harmony.CreateProcessor(Method(nameof(RefOuter))).AddInnerFinalizer(Fix(nameof(RecoverRef), nameof(RefOperation))).Patch();
			Assert.That(RefOuter(), Is.EqualTo(38));
			Assert.That(backing, Is.EqualTo(throwAt == 0 ? 33 : 7));
		}
		[MethodImpl(MethodImplOptions.NoInlining)] static ref int Backing() => ref backing;
		[MethodImpl(MethodImplOptions.NoInlining)] static void PendingReference() => Backing() = Throwing(3);
		[Test]
		public void A_pending_managed_reference_survives_suppression_and_is_used_by_the_outer_store()
		{
			harmony.CreateProcessor(Method(nameof(PendingReference))).AddInnerFinalizer(Fix(nameof(Recover), nameof(Throwing))).Patch();
			PendingReference();
			Assert.That(backing, Is.EqualTo(10));
		}
#endif

		[Test]
		public void Helpers_remain_callable_after_collection_and_are_rebuilt_after_removal()
		{
			for (var i = 0; i < 3; i++)
			{
				harmony.CreateProcessor(Method(nameof(ArraysOuter))).AddInnerFinalizer(Fix(nameof(Recover), nameof(Throwing))).Patch();
				GC.Collect();
				GC.WaitForPendingFinalizers();
				Assert.That(ArraysOuter(4), Is.EqualTo(14));
				harmony.Unpatch(Method(nameof(ArraysOuter)), HarmonyPatchType.InnerFinalizer, harmony.Id);
				Assert.That(Assert.Throws<InvalidOperationException>(() => ArraysOuter(4)), Is.SameAs(operationError));
			}
		}
		static int BadFinalizer(Exception __exception) => 0;
		static void BadException(ref Exception __exception) { }
		static void OuterException([HarmonyOuter] Exception __exception) { }
		[TestCase(nameof(BadFinalizer)), TestCase(nameof(BadException)), TestCase(nameof(OuterException))]
		public void Invalid_finalizers_leave_the_working_wrapper_intact(string name)
		{
			harmony.CreateProcessor(Method(nameof(ArraysOuter))).AddInnerFinalizer(Fix(nameof(Recover), nameof(Throwing))).Patch();
			Assert.Throws(name == nameof(BadFinalizer) ? typeof(ArgumentException) : typeof(HarmonyException),
				() => harmony.CreateProcessor(Method(nameof(ArraysOuter))).AddInnerFinalizer(Fix(name, nameof(Throwing))).Patch());
			Assert.That(ArraysOuter(4), Is.EqualTo(14));
		}
	}
}
