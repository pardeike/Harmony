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
	public class InfixControlFlow : TestLogger
	{
		static readonly List<string> trace = [];
		static int failure;
		static int backing;
		static int replacement;
		Harmony harmony;
		[SetUp]
		public void SetUp()
		{
			harmony = new Harmony("test.infix.control." + Guid.NewGuid());
			trace.Clear();
			failure = 0;
			backing = 7;
			replacement = 19;
		}
		[TearDown]
		public void TearDown() => harmony.UnpatchAll(harmony.Id);
		static MethodInfo Method(string name) => AccessTools.Method(typeof(InfixControlFlow), name);
		static HarmonyMethod Fix(string name, MethodInfo target, params int[] positions)
			=> new(Method(name)) { innerMethod = new InnerMethod(target, positions) };
		void Patch(string outer, string inner, string prefix = null, string postfix = null)
		{
			var processor = harmony.CreateProcessor(Method(outer));
			if (prefix is not null) processor.AddInnerPrefix(Fix(prefix, Method(inner)));
			if (postfix is not null) processor.AddInnerPostfix(Fix(postfix, Method(inner)));
			processor.Patch();
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		static int Call(int value)
		{
			trace.Add("call:" + value);
			if (failure == 2) throw new InvalidOperationException("call");
			return value;
		}
		static void Before() { trace.Add("before"); if (failure == 1) throw new InvalidOperationException("prefix"); }
		static void After() { trace.Add("after"); if (failure == 3) throw new InvalidOperationException("postfix"); }
		static void LastAfter() => trace.Add("last");
		static void CalleeBefore(ref int value) { trace.Add("callee prefix"); value += 10; }
		static void CalleeAfter() => trace.Add("callee postfix");
		static Exception Finalizer(Exception __exception) { trace.Add("finalizer:" + __exception?.Message); return null; }
		[MethodImpl(MethodImplOptions.NoInlining)]
		static int Handled(int value)
		{
			try { return Call(value); }
			catch (InvalidOperationException) { trace.Add("catch"); return -1; }
			finally { trace.Add("finally"); }
		}
		[MethodImpl(MethodImplOptions.NoInlining)]
		static int Unhandled(int value) => Call(value);
		[TestCase(0), TestCase(1), TestCase(2), TestCase(3)]
		public void Exceptions_leave_pipeline_and_reach_original_handlers(int throwAt)
		{
			failure = throwAt;
			Patch(nameof(Handled), nameof(Call), nameof(Before), nameof(After));
			var last = Fix(nameof(LastAfter), Method(nameof(Call)));
			last.priority = Priority.Last;
			harmony.CreateProcessor(Method(nameof(Handled))).AddInnerPostfix(last).Patch();
			Assert.That(Handled(5), Is.EqualTo(throwAt == 0 ? 5 : -1));
			var expected = new List<string> { "before" };
			if (throwAt != 1) expected.Add("call:5");
			if (throwAt == 0 || throwAt == 3) expected.Add("after");
			expected.Add(throwAt == 0 ? "last" : "catch");
			expected.Add("finally");
			Assert.That(trace, Is.EqualTo(expected));
		}
		[TestCase(1), TestCase(2), TestCase(3)]
		public void Ordinary_finalizer_can_suppress_inner_pipeline_exception(int throwAt)
		{
			failure = throwAt;
			harmony.Patch(Method(nameof(Unhandled)), finalizer: new HarmonyMethod(Method(nameof(Finalizer))));
			Patch(nameof(Unhandled), nameof(Call), nameof(Before), nameof(After));
			Assert.DoesNotThrow(() => Unhandled(5));
			Assert.That(trace.Last(), Is.EqualTo("finalizer:" + new[] { "", "prefix", "call", "postfix" }[throwAt]));
		}
		[Test]
		public void Original_call_still_executes_callee_patches_inside_the_infix()
		{
			harmony.Patch(Method(nameof(Call)), prefix: new HarmonyMethod(Method(nameof(CalleeBefore))), postfix: new HarmonyMethod(Method(nameof(CalleeAfter))));
			Patch(nameof(Unhandled), nameof(Call), nameof(Before), nameof(After));
			Assert.That(Unhandled(5), Is.EqualTo(15));
			Assert.That(trace, Is.EqualTo(new[] { "before", "callee prefix", "call:15", "callee postfix", "after" }));
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		static int Loop(int count)
		{
			var result = 0;
			for (var i = 0; i < count; i++) result += Call(i);
			return result;
		}
		static void StateFirst(ref int __state) { trace.Add("stateFirst:" + __state); __state += 1; }
		static void StateSecond(int __state) => trace.Add("stateSecond:" + __state);
		[Test]
		public void Postfix_state_is_shared_within_execution_and_reset_at_each_loop_iteration()
		{
			Patch(nameof(Loop), nameof(Call), postfix: nameof(StateFirst));
			var second = Fix(nameof(StateSecond), Method(nameof(Call)));
			second.priority = Priority.Last;
			harmony.CreateProcessor(Method(nameof(Loop))).AddInnerPostfix(second).Patch();
			Assert.That(Loop(3), Is.EqualTo(3));
			Assert.That(trace, Is.EqualTo(Enumerable.Range(0, 3).SelectMany(i => new[] { "call:" + i, "stateFirst:0", "stateSecond:1" })));
		}
		static void OuterState(out int __state) => __state = 10;
		static void Bridge([HarmonyOuter] ref int __state, [HarmonyOuter] ref int __var_count)
		{
			trace.Add("bridge:" + __state++ + ":" + __var_count++);
		}
		[Test]
		public void Outer_state_and_named_local_persist_across_sites_but_not_invocations()
		{
			harmony.Patch(Method(nameof(Loop)), prefix: new HarmonyMethod(Method(nameof(OuterState))));
			Patch(nameof(Loop), nameof(Call), nameof(Bridge));
			_ = Loop(2);
			_ = Loop(2);
			Assert.That(trace.Where(item => item.StartsWith("bridge:", StringComparison.Ordinal)),
				Is.EqualTo(new[] { "bridge:10:0", "bridge:11:1", "bridge:10:0", "bridge:11:1" }));
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		static ref int RefCall() => ref backing;
		[MethodImpl(MethodImplOptions.NoInlining)]
		static int ReadRef() => RefCall();
		static ref int ReplacementRef() => ref replacement;
		static void ReplaceRef(ref RefResult<int> __resultRef) => __resultRef = ReplacementRef;
		static bool SkipRef(ref int __result) { __result = 23; return false; }
		[Test]
		public void Skipped_ref_return_uses_private_default_storage()
		{
			Patch(nameof(ReadRef), nameof(RefCall), nameof(SkipRef));
			Assert.That(ReadRef(), Is.EqualTo(23));
			Assert.That(backing, Is.EqualTo(7));
		}
		[Test]
		public void Postfix_only_ref_replacement_does_not_allocate_a_dummy_array()
		{
			var patch = new Patch(Fix(nameof(ReplaceRef), Method(nameof(RefCall))), 0, harmony.Id);
			var config = new MethodCreatorConfig(Method(nameof(ReadRef)), null, [], [], [], [], [], [new Infix(patch)], false);
			var (method, instructions) = new MethodCreator(config).CreateReplacement();
			Assert.That(instructions.Values.Count(instruction => instruction.opcode == OpCodes.Newarr), Is.Zero);
			Assert.That(method.Invoke(null, []), Is.EqualTo(19));
		}

		class Receiver
		{
			internal int value;
			[MethodImpl(MethodImplOptions.NoInlining)]
			internal virtual int Get() => value;
		}
		[MethodImpl(MethodImplOptions.NoInlining)]
		static int NullCall(Receiver receiver) => receiver.Get();
		static void ReplaceReceiver(ref Receiver __instance) => __instance = new Receiver { value = 31 };
		static bool SkipReceiver(ref int __result) { __result = 37; return false; }
		[TestCase(false), TestCase(true)]
		public void Prefix_can_replace_null_receiver_or_skip_before_callvirt_check(bool skipCall)
		{
			var target = AccessTools.Method(typeof(Receiver), nameof(Receiver.Get));
			harmony.CreateProcessor(Method(nameof(NullCall)))
				.AddInnerPrefix(Fix(skipCall ? nameof(SkipReceiver) : nameof(ReplaceReceiver), target)).Patch();
			Assert.That(NullCall(null), Is.EqualTo(skipCall ? 37 : 31));
		}

		class Container<T>
		{
			[MethodImpl(MethodImplOptions.NoInlining)]
			internal static string Use(T value) => value.ToString();
		}
		[MethodImpl(MethodImplOptions.NoInlining)]
		static string GenericCalls() => Container<int>.Use(1) + Container<string>.Use("two");
		static void GenericMark(object value, MethodBase __originalMethod) => trace.Add("generic:" + value + ":" + __originalMethod.DeclaringType.GetGenericArguments()[0].Name);
		[TestCase(false), TestCase(true)]
		public void Exact_generic_target_stays_exact_and_definition_selects_family(bool family)
		{
			var target = AccessTools.Method(family ? typeof(Container<>) : typeof(Container<int>), nameof(Container<int>.Use));
			harmony.CreateProcessor(Method(nameof(GenericCalls))).AddInnerPrefix(Fix(nameof(GenericMark), target)).Patch();
			Assert.That(GenericCalls(), Is.EqualTo("1two"));
			Assert.That(trace, Is.EqualTo(family ? new[] { "generic:1:Int32", "generic:two:String" } : new[] { "generic:1:Int32" }));
		}
		[Test]
		public void Generic_family_positions_count_all_constructions_together()
		{
			var family = AccessTools.Method(typeof(Container<>), nameof(Container<int>.Use));
			harmony.CreateProcessor(Method(nameof(GenericCalls))).AddInnerPrefix(Fix(nameof(GenericMark), family, -1)).Patch();
			_ = GenericCalls();
			Assert.That(trace, Is.EqualTo(new[] { "generic:two:String" }));
		}
		[Test]
		public void Reused_patch_method_exact_and_family_are_independent_records()
		{
			var outer = Method(nameof(GenericCalls));
			var exact = AccessTools.Method(typeof(Container<int>), nameof(Container<int>.Use));
			var family = AccessTools.Method(typeof(Container<>), nameof(Container<int>.Use));
			harmony.CreateProcessor(outer).AddInnerPrefix(Fix(nameof(GenericMark), exact)).Patch();
			harmony.CreateProcessor(outer).AddInnerPrefix(Fix(nameof(GenericMark), family)).Patch();
			_ = GenericCalls();
			Assert.That(trace, Is.EqualTo(new[] { "generic:1:Int32", "generic:1:Int32", "generic:two:String" }));
			Assert.That(Harmony.GetPatchInfo(outer).InnerPrefixes.Count, Is.EqualTo(2));
		}
	}
}
