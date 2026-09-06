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
	public class InfixBoundaries : TestLogger
	{
		static readonly List<string> trace = [];
		static ExceptionBlockType handler;
		Harmony harmony;
		[SetUp]
		public void SetUp() { harmony = new Harmony("test.infix.boundaries." + Guid.NewGuid()); trace.Clear(); }
		[TearDown]
		public void TearDown() => harmony.UnpatchAll(harmony.Id);
		static MethodInfo Method(string name) => AccessTools.Method(typeof(InfixBoundaries), name);
		static HarmonyMethod Fix(string name, string target) => new(Method(name)) { innerMethod = new InnerMethod(Method(target)) };
		[MethodImpl(MethodImplOptions.NoInlining)]
		static void Ping() => trace.Add("call");
		static void Before() => trace.Add("before");
		static void After() => trace.Add("after");
		static void Empty() { }
		static void ThrowInTry() => throw new InvalidOperationException();
		[MethodImpl(MethodImplOptions.NoInlining)]
		static void Boundary(bool fail) { if (fail) throw new InvalidOperationException(); }

		static IEnumerable<CodeInstruction> HandlerBody(IEnumerable<CodeInstruction> _, ILGenerator generator)
		{
			var failed = generator.DefineLabel();
			var done = generator.DefineLabel();
			yield return new CodeInstruction(OpCodes.Call, Method(nameof(Ping))).WithBlocks(new ExceptionBlock(ExceptionBlockType.BeginExceptionBlock));
			yield return new CodeInstruction(OpCodes.Ldarg_0);
			yield return new CodeInstruction(OpCodes.Brtrue, failed);
			yield return new CodeInstruction(OpCodes.Leave, done);
			yield return new CodeInstruction(OpCodes.Newobj, AccessTools.Constructor(typeof(InvalidOperationException))).WithLabels(failed);
			yield return new CodeInstruction(OpCodes.Throw);
			if (handler == ExceptionBlockType.BeginCatchBlock)
				yield return new CodeInstruction(OpCodes.Pop).WithBlocks(new ExceptionBlock(handler, typeof(InvalidOperationException)));
			var call = new CodeInstruction(OpCodes.Call, Method(nameof(Ping)));
			if (handler != ExceptionBlockType.BeginCatchBlock) call.blocks.Add(new ExceptionBlock(handler));
			call.blocks.Add(new ExceptionBlock(ExceptionBlockType.EndExceptionBlock));
			yield return call;
			yield return new CodeInstruction(OpCodes.Ret).WithLabels(done);
		}
		[TestCase(ExceptionBlockType.BeginFinallyBlock, false)]
		[TestCase(ExceptionBlockType.BeginFinallyBlock, true)]
		[TestCase(ExceptionBlockType.BeginFaultBlock, false)]
		[TestCase(ExceptionBlockType.BeginFaultBlock, true)]
		[TestCase(ExceptionBlockType.BeginCatchBlock, false)]
		[TestCase(ExceptionBlockType.BeginCatchBlock, true)]
		public void Zero_argument_void_call_keeps_opening_and_closing_handler_boundaries(ExceptionBlockType block, bool fail)
		{
			handler = block;
			harmony.CreateProcessor(Method(nameof(Boundary))).AddTranspiler(Method(nameof(HandlerBody)))
				.AddInnerPrefix(Fix(nameof(Before), nameof(Ping))).AddInnerPostfix(Fix(nameof(After), nameof(Ping))).Patch();
			if (fail && block != ExceptionBlockType.BeginCatchBlock) Assert.Throws<InvalidOperationException>(() => Boundary(fail));
			else Assert.DoesNotThrow(() => Boundary(fail));
			var calls = fail || block == ExceptionBlockType.BeginFinallyBlock ? 2 : 1;
			Assert.That(trace, Is.EqualTo(Enumerable.Range(0, calls).SelectMany(_ => new[] { "before", "call", "after" })));
		}

		static IEnumerable<CodeInstruction> CatchEntryLoop(IEnumerable<CodeInstruction> _, ILGenerator generator)
		{
			var count = generator.DeclareLocal(typeof(int));
			var entry = generator.DefineLabel();
			var repeat = generator.DefineLabel();
			var done = generator.DefineLabel();
			yield return new CodeInstruction(OpCodes.Call, Method(nameof(ThrowInTry))).WithBlocks(new ExceptionBlock(ExceptionBlockType.BeginExceptionBlock));
			yield return new CodeInstruction(OpCodes.Call, Method(nameof(Ping))).WithLabels(entry).WithBlocks(new ExceptionBlock(ExceptionBlockType.BeginCatchBlock, typeof(InvalidOperationException)));
			yield return new CodeInstruction(OpCodes.Pop);
			yield return new CodeInstruction(OpCodes.Ldloc, count);
			yield return new CodeInstruction(OpCodes.Ldc_I4_1);
			yield return new CodeInstruction(OpCodes.Add);
			yield return new CodeInstruction(OpCodes.Dup);
			yield return new CodeInstruction(OpCodes.Stloc, count);
			yield return new CodeInstruction(OpCodes.Ldc_I4_2);
			yield return new CodeInstruction(OpCodes.Blt, repeat);
			yield return new CodeInstruction(OpCodes.Leave, done);
			yield return new CodeInstruction(OpCodes.Ldnull).WithLabels(repeat);
			yield return new CodeInstruction(OpCodes.Br, entry).WithBlocks(new ExceptionBlock(ExceptionBlockType.EndExceptionBlock));
			yield return new CodeInstruction(OpCodes.Ret).WithLabels(done);
		}
		[TestCase(false)]
		[TestCase(true)]
		public void Branch_to_handler_entry_targets_its_call_not_the_generated_try_exit(bool infix)
		{
			var processor = harmony.CreateProcessor(Method(nameof(Boundary))).AddTranspiler(Method(nameof(CatchEntryLoop)));
			if (infix) processor.AddInnerPrefix(Fix(nameof(Before), nameof(Ping))).AddInnerPostfix(Fix(nameof(After), nameof(Ping)));
			var replacement = processor.Patch();
			Assert.IsTrue(replacement.GetMethodBody().InitLocals);
			Boundary(false);
			Assert.That(trace, Is.EqualTo(Enumerable.Range(0, 2).SelectMany(_ => infix ? new[] { "before", "call", "after" } : new[] { "call" })));
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		static bool Filter(Exception exception) { trace.Add("filter"); return exception is InvalidOperationException; }
		[MethodImpl(MethodImplOptions.NoInlining)]
		static int Filtered()
		{
			try { throw new InvalidOperationException(); }
			catch (Exception exception) when (Filter(exception)) { trace.Add("catch"); return 7; }
			finally { trace.Add("finally"); }
		}
		[Test]
		public void Ordinary_patch_retains_nested_filter_and_finally_regions()
		{
			Assert.That(Filtered(), Is.EqualTo(7));
			Assert.That(trace, Is.EqualTo(new[] { "filter", "catch", "finally" }));
			trace.Clear();
			harmony.CreateProcessor(Method(nameof(Filtered))).AddPrefix(Method(nameof(Empty))).Patch();
			Assert.That(Filtered(), Is.EqualTo(7));
			Assert.That(trace, Is.EqualTo(new[] { "filter", "catch", "finally" }));
		}
		[Test]
		public void Call_in_filter_retains_nested_exception_regions()
		{
			harmony.CreateProcessor(Method(nameof(Filtered))).AddInnerPrefix(Fix(nameof(Before), nameof(Filter)))
				.AddInnerPostfix(Fix(nameof(After), nameof(Filter))).Patch();
			Assert.That(Filtered(), Is.EqualTo(7));
			Assert.That(trace, Is.EqualTo(new[] { "before", "filter", "after", "catch", "finally" }));
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		static int MultipleCatches(bool throwFromCatch)
		{
			try { throw new ArgumentException(); }
			catch (ArgumentException)
			{
				trace.Add("first");
				if (throwFromCatch) throw new InvalidOperationException();
				return 1;
			}
			catch (InvalidOperationException) { trace.Add("second"); return 2; }
			finally { trace.Add("finally"); }
		}
		[TestCase(false, false)]
		[TestCase(true, false)]
		[TestCase(false, true)]
		[TestCase(true, true)]
		public void Ordinary_multiple_catches_do_not_handle_exceptions_from_each_other(bool throwFromCatch, bool debug)
		{
			harmony.CreateProcessor(Method(nameof(MultipleCatches))).AddPrefix(new HarmonyMethod(Method(nameof(Empty))) { debug = debug }).Patch();
			if (throwFromCatch) Assert.Throws<InvalidOperationException>(() => MultipleCatches(true));
			else Assert.That(MultipleCatches(false), Is.EqualTo(1));
			Assert.That(trace, Is.EqualTo(new[] { "first", "finally" }));
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		static bool ChooseFilter(Exception exception, bool accept, bool throwInFilter)
		{
			trace.Add("filter");
			if (throwInFilter) throw new ApplicationException();
			return accept;
		}
		[MethodImpl(MethodImplOptions.NoInlining)]
		static int FilterChoice(bool accept, bool throwInFilter)
		{
			try { throw new InvalidOperationException(); }
			catch (Exception exception) when (ChooseFilter(exception, accept, throwInFilter)) { trace.Add("selected"); return 1; }
			catch (Exception) { trace.Add("fallback"); return 2; }
			finally { trace.Add("finally"); }
		}
		[MethodImpl(MethodImplOptions.NoInlining)]
		static int UnpatchedFilterChoice(bool accept, bool throwInFilter)
		{
			try { throw new InvalidOperationException(); }
			catch (Exception exception) when (ChooseFilter(exception, accept, throwInFilter)) { trace.Add("selected"); return 1; }
			catch (Exception) { trace.Add("fallback"); return 2; }
			finally { trace.Add("finally"); }
		}
		[TestCase(false, false, false)]
		[TestCase(true, false, false)]
		[TestCase(true, true, false)]
		[TestCase(false, false, true)]
		[TestCase(true, false, true)]
		[TestCase(true, true, true)]
		public void Filters_preserve_the_unpatched_runtime_outcome(bool accept, bool throwInFilter, bool infix)
		{
			// Keep this control untouched: an unpatch can itself leave a regenerated method installed.
			int? baseline = null;
			ApplicationException baselineException = null;
			try { baseline = UnpatchedFilterChoice(accept, throwInFilter); }
			catch (ApplicationException exception) when (AccessTools.IsMonoRuntime && throwInFilter)
			{
				baselineException = exception;
				TestContext.Progress.WriteLine("The unpatched Mono filter propagates its exception; require the same patched outcome.");
			}
			if (baselineException is null) Assert.That(baseline, Is.EqualTo(accept && !throwInFilter ? 1 : 2));
			var expected = trace.ToList();
			trace.Clear();
			var processor = harmony.CreateProcessor(Method(nameof(FilterChoice))).AddPrefix(Method(nameof(Empty)));
			if (infix) processor.AddInnerPrefix(Fix(nameof(Before), nameof(ChooseFilter))).AddInnerPostfix(Fix(nameof(After), nameof(ChooseFilter)));
			processor.Patch();
			if (baselineException is null) Assert.That(FilterChoice(accept, throwInFilter), Is.EqualTo(baseline));
			else Assert.Throws<ApplicationException>(() => FilterChoice(accept, throwInFilter));
			if (infix)
			{
				expected.Insert(0, "before");
				if (!throwInFilter) expected.Insert(expected.IndexOf("filter") + 1, "after");
			}
			Assert.That(trace, Is.EqualTo(expected));
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		static int IntCall(int value) => value;
		[MethodImpl(MethodImplOptions.NoInlining)]
		static int TailCaller(int value) => IntCall(value);
		static IEnumerable<CodeInstruction> Tail(IEnumerable<CodeInstruction> source)
		{
			foreach (var instruction in source)
			{
				if (instruction.Calls(Method(nameof(IntCall)))) yield return new CodeInstruction(OpCodes.Tailcall);
				yield return instruction;
			}
		}
		[Test]
		public void Selected_tail_call_fails_before_installation()
		{
			var original = Method(nameof(TailCaller));
			var before = HarmonySharedState.GetPatchInfo(original);
			Assert.Throws<HarmonyException>(() => harmony.CreateProcessor(original).AddTranspiler(Method(nameof(Tail)))
				.AddInnerPrefix(Fix(nameof(Before), nameof(IntCall))).Patch());
			Assert.That(HarmonySharedState.GetPatchInfo(original)?.VersionCount, Is.EqualTo(before?.VersionCount));
			Assert.That(TailCaller(5), Is.EqualTo(5));
			Assert.That(trace, Is.Empty);
		}
	}
}
