using HarmonyLib;
using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
#if NET45_OR_GREATER || NETCOREAPP
using System.Threading.Tasks;
#endif

namespace HarmonyLibTests.Patching
{
	[TestFixture, NonParallelizable]
	public class InfixPersistentState : TestLogger
	{
		Harmony harmony;
		static readonly List<int[]> observations = [];
		static readonly List<WeakReference> retained = [];
		static bool rejectDisposeRebuild;
		static MethodInfo Method(string name) => AccessTools.DeclaredMethod(typeof(InfixPersistentState), name);
		[SetUp] public void SetUp() { harmony = new Harmony("test.infix.persistent." + Guid.NewGuid()); observations.Clear(); retained.Clear(); rejectDisposeRebuild = false; }
		[TearDown] public void TearDown() => harmony.UnpatchAll(harmony.Id);
		[MethodImpl(MethodImplOptions.NoInlining)] static int Call(int value) => value;
		static HarmonyMethod Fix(string name) => new(Method(name))
		{
			innerMethod = new InnerMethod(Method(nameof(Call))),
			infixOuterBody = InfixOuterBody.Auto
		};
		static void Count(ref int value, [HarmonyOuter, HarmonyArgument("count", ArgumentMode.Persistent)] ref int count,
			[HarmonyOuter] ref int __var_step)
		{
			value = ++count;
			lock (observations) observations.Add([count, ++__var_step]);
		}
		static void Keep([HarmonyOuter, HarmonyArgument("object", ArgumentMode.Persistent)] ref object value)
		{
			if (value is not null) return;
			value = new object();
			retained.Add(new WeakReference(value));
		}
		static void WrongType([HarmonyOuter, HarmonyArgument("count", ArgumentMode.Persistent)] ref string value) { }
		static void WrongLifetime([HarmonyOuter] ref int __var_count) { }
		static void WrongScope([HarmonyArgument("count", ArgumentMode.Persistent)] ref int value) { }
		static void StringCount(ref int value, [HarmonyOuter, HarmonyArgument("count", ArgumentMode.Persistent)] ref string count)
			=> value = (count += "x").Length;
		static void KeepReceiver([HarmonyOuter] object __instance, [HarmonyOuter, HarmonyArgument("receiver", ArgumentMode.Persistent)] ref object receiver)
			=> receiver = __instance;
		static IEnumerable<CodeInstruction> DisposeTranspiler(IEnumerable<CodeInstruction> instructions)
			=> rejectDisposeRebuild ? throw new InvalidOperationException("Dispose rebuild rejected") : instructions;
		static Exception Finalizer(Exception __exception, [HarmonyOuter, HarmonyArgument("count", ArgumentMode.Persistent)] int count)
		{
			Assert.That(count, Is.GreaterThan(0));
			return __exception;
		}
		[HarmonyInline]
		static void InlineCount(ref int value, [HarmonyOuter, HarmonyArgument("count", ArgumentMode.Persistent)] ref int count) => value = ++count;
		static int Ordinary() => Call(10) * 100 + Call(20);
		static IEnumerable<int> Iterator(int count)
		{
			try { for (var i = 0; i < count; i++) yield return Call(10); }
			finally { }
		}

		[Test]
		public void Synchronous_methods_keep_normal_invocation_lifetimes()
		{
			harmony.CreateProcessor(Method(nameof(Ordinary))).AddInnerPrefix(Fix(nameof(Count))).Patch();
			Assert.That(Ordinary(), Is.EqualTo(102));
			Assert.That(Ordinary(), Is.EqualTo(102));
			Assert.That(observations, Is.EqualTo(new[] { new[] { 1, 1 }, new[] { 2, 2 }, new[] { 1, 1 }, new[] { 2, 2 } }));
		}
		[Test]
		public void Documented_persistent_counter_starts_fresh_for_each_enumeration()
		{
			harmony.CreateClassProcessor(typeof(Patching_Infix.SequencePersistentPatch)).Patch();
			var source = Patching_Infix.Sequence.Count(3);
			Assert.That(source.ToArray(), Is.EqualTo(new[] { 1, 2, 3 }));
			Assert.That(source.ToArray(), Is.EqualTo(new[] { 1, 2, 3 }));
		}

		[Test]
		public void Iterator_state_survives_yields_and_separates_interleaved_and_repeated_enumerations()
		{
			harmony.CreateProcessor(Method(nameof(Iterator))).AddInnerPrefix(Fix(nameof(Count))).Patch();
			var source = Iterator(3);
			using var first = source.GetEnumerator();
			using var second = source.GetEnumerator();
			Assert.That(first.MoveNext(), Is.True);
			Assert.That(second.MoveNext(), Is.True);
			Assert.That(first.Current, Is.EqualTo(1));
			Assert.That(second.Current, Is.EqualTo(1));
			Assert.That(first.MoveNext(), Is.True);
			Assert.That(first.Current, Is.EqualTo(2));
			Assert.That(second.MoveNext(), Is.True);
			Assert.That(second.Current, Is.EqualTo(2));
			Assert.That(source.ToArray(), Is.EqualTo(new[] { 1, 2, 3 }));
			Assert.That(observations.All(item => item[1] == 1), Is.True, "Ordinary outer named locals must still reset at each MoveNext");
		}

		[TestCase(nameof(WrongType))]
		[TestCase(nameof(WrongLifetime))]
		[TestCase(nameof(WrongScope))]
		public void Invalid_state_registration_preserves_the_previous_patch(string patch)
		{
			harmony.CreateProcessor(Method(nameof(Iterator))).AddInnerPrefix(Fix(nameof(Count))).Patch();
			var body = AccessTools.StateMachineMoveNext(Method(nameof(Iterator)));
			var before = HarmonySharedState.GetPatchInfo(body).Serialize();
			Assert.Throws<ArgumentException>(() => harmony.CreateProcessor(body).AddInnerPrefix(Fix(patch)).Patch());
			Assert.That(HarmonySharedState.GetPatchInfo(body).Serialize(), Is.EqualTo(before));
			Assert.That(Iterator(2).ToArray(), Is.EqualTo(new[] { 1, 2 }));
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		static void Collect() { GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); }
		[MethodImpl(MethodImplOptions.NoInlining)]
		static T OnFinishedThread<T>(Func<T> action)
		{
			// Mono can conservatively keep stale object/byref values in the caller's stack.
			// End the allocating thread before collection so these probes measure heap roots.
			T result = default;
			Exception failure = null;
			var thread = new Thread(() =>
			{
				try { result = action(); }
				catch (Exception error) { failure = error; }
			});
			thread.Start();
			Assert.That(thread.Join(TimeSpan.FromSeconds(15)), Is.True, "The allocation thread did not finish");
			if (failure is not null) throw failure;
			return result;
		}
		[MethodImpl(MethodImplOptions.NoInlining)]
		static WeakReference AbandonIterator()
		{
			var iterator = Iterator(2).GetEnumerator();
			Assert.That(iterator.MoveNext(), Is.True);
			return new WeakReference(iterator);
		}
		[Test]
		public void Abandoned_iterator_is_collectible_even_when_its_slot_references_itself()
		{
			if (typeof(object).Assembly.GetType("System.Runtime.CompilerServices.ConditionalWeakTable`2") is null)
				Assert.Ignore("CLR 2.0 lacks dependent handles; abandoned cyclic iterator state requires explicit disposal on that runtime");
			harmony.CreateProcessor(Method(nameof(Iterator))).AddInnerPrefix(Fix(nameof(KeepReceiver))).Patch();
			var weak = OnFinishedThread(AbandonIterator);
			Collect();
			Assert.That(weak.IsAlive, Is.False);
		}
		[Test]
		public void Old_wrapper_entering_after_unpatch_does_not_retain_new_state()
		{
			var execution = OnFinishedThread(() =>
			{
				var processor = harmony.CreateProcessor(Method(nameof(Iterator)))
					.AddInnerPrefix(Fix(nameof(Keep))).AddInnerPrefix(Fix(nameof(Count)));
				var wrapper = processor.Patch();
				var enumerator = Iterator(2).GetEnumerator();
				processor.Unpatch(HarmonyPatchType.InnerPrefix, harmony.Id);
				Assert.That(wrapper.Invoke(null, [enumerator]), Is.True);
				Assert.That(enumerator.Current, Is.EqualTo(1));
				Assert.That(wrapper.Invoke(null, [enumerator]), Is.True);
				Assert.That(enumerator.Current, Is.EqualTo(1), "A saved wrapper must not create a new suspended lifetime after unpatching");
				return Tuple.Create(enumerator, wrapper);
			});
			using var iterator = execution.Item1;
			Assert.That(retained.Count, Is.EqualTo(2));
			Collect();
			Assert.That(retained.All(item => !item.IsAlive), Is.True);
			GC.KeepAlive(execution);
		}
		[Test]
		public void Rejected_disposal_cleanup_preserves_the_installed_body()
		{
			var processor = harmony.CreateProcessor(Method(nameof(Iterator))).AddInnerPrefix(Fix(nameof(Count)));
			processor.Patch();
			using var iterator = Iterator(2).GetEnumerator();
			var dispose = iterator.GetType().GetInterfaceMap(typeof(IDisposable)).TargetMethods.Single();
			harmony.CreateProcessor(dispose).AddTranspiler(Method(nameof(DisposeTranspiler))).Patch();
			var body = AccessTools.StateMachineMoveNext(Method(nameof(Iterator)));
			var bytes = HarmonySharedState.GetPatchInfo(body).Serialize();
			rejectDisposeRebuild = true;
			try
			{
				var error = Assert.Throws<TargetInvocationException>(() => processor.Unpatch(HarmonyPatchType.InnerPrefix, harmony.Id));
				Assert.That(error.InnerException, Is.TypeOf<InvalidOperationException>());
				Assert.That(HarmonySharedState.GetPatchInfo(body).Serialize(), Is.EqualTo(bytes));
				Assert.That(Iterator(2).ToArray(), Is.EqualTo(new[] { 1, 2 }));
			}
			finally { rejectDisposeRebuild = false; }
		}
		[Test]
		public void Completed_iterator_releases_state_even_when_the_enumerator_is_retained()
		{
			harmony.CreateProcessor(Method(nameof(Iterator))).AddInnerPrefix(Fix(nameof(Keep))).Patch();
			using var iterator = Iterator(2).GetEnumerator();
			while (iterator.MoveNext()) { }
			Assert.That(retained.Count, Is.EqualTo(1));
			Collect();
			Assert.That(retained[0].IsAlive, Is.False);
			GC.KeepAlive(iterator);
		}

		[TestCase(false)]
		[TestCase(true)]
		public void Disposal_or_unpatch_releases_state_without_retaining_a_cleanup_patch(bool unpatch)
		{
			var processor = harmony.CreateProcessor(Method(nameof(Iterator))).AddInnerPrefix(Fix(nameof(Keep)));
			processor.Patch();
			using var iterator = Iterator(2).GetEnumerator();
			Assert.That(iterator.MoveNext(), Is.True);
			var dispose = iterator.GetType().GetInterfaceMap(typeof(IDisposable)).TargetMethods.Single();
			if (unpatch) processor.Unpatch(HarmonyPatchType.InnerPrefix, harmony.Id);
			else iterator.Dispose();
			Collect();
			Assert.That(retained.Single().IsAlive, Is.False);
			processor.Unpatch(HarmonyPatchType.InnerPrefix, harmony.Id);
			Assert.That(Harmony.GetPatchInfo(dispose)?.Finalizers.Count ?? 0, Is.Zero);
			GC.KeepAlive(iterator);
		}

		[Test]
		public void Persistence_roundtrips_with_a_required_version_and_downgrades_after_removal()
		{
			var state = new PatchInfo();
			state.AddInnerPrefixes("persistent", Fix(nameof(Count)));
			var bytes = state.Serialize();
			Assert.That(bytes[14], Is.EqualTo(4));
			Assert.That(PatchInfoSerialization.Deserialize(bytes).Serialize(), Is.EqualTo(bytes));
			bytes[14] = 3;
			Assert.Throws<System.Runtime.Serialization.SerializationException>(() => PatchInfoSerialization.Deserialize(bytes));
			state.RemoveInnerPrefix("persistent");
			Assert.That(state.GetRequiredInfixVersion(), Is.LessThan(4));
			var parameter = Method(nameof(Count)).GetParameters()[1];
			var attribute = (HarmonyArgument)parameter.GetCustomAttributes(typeof(HarmonyArgument), true).Single();
			Assert.That(attribute.OriginalName, Is.Not.EqualTo("count"));
			Assert.That(attribute.Index, Is.EqualTo(int.MinValue));
		}

#if NET45_OR_GREATER || NETCOREAPP
		static async Task<int> Nested(int depth)
		{
			var first = Call(10);
			await Task.Yield();
			if (depth > 0) Assert.That(await Nested(depth - 1), Is.EqualTo(102));
			return first * 100 + Call(20);
		}
		[Test]
		public async Task Nested_async_executions_and_finalizer_helpers_keep_separate_state()
		{
			harmony.CreateProcessor(Method(nameof(Nested))).AddInnerPrefix(Fix(nameof(Count))).AddInnerFinalizer(Fix(nameof(Finalizer))).Patch();
			Assert.That(await Nested(3), Is.EqualTo(102));
		}

		[Test]
		public async Task Optional_inlining_uses_the_same_persistent_storage()
		{
			harmony.CreateProcessor(Method(nameof(Async))).AddInnerPrefix(Fix(nameof(InlineCount))).Patch();
			var resume = new TaskCompletionSource<bool>();
			var task = Async(resume.Task, true);
			Assert.That(task.IsCompleted, Is.False);
			resume.SetResult(true);
			Assert.That(await task, Is.EqualTo(new[] { 1, 2, 3 }));
		}

		[TestCase(false)]
		[TestCase(true)]
		public async Task Fault_and_cancellation_release_saved_objects(bool cancel)
		{
			harmony.CreateProcessor(Method(nameof(Async))).AddInnerPrefix(Fix(nameof(Keep))).Patch();
			var resume = new TaskCompletionSource<bool>();
			var task = Async(resume.Task, false);
			Assert.That(task.IsCompleted, Is.False);
			if (cancel) resume.SetCanceled();
			else resume.SetException(new InvalidOperationException("expected"));
			try { await task; Assert.Fail("Expected the original task failure"); }
			catch (OperationCanceledException) when (cancel) { }
			catch (InvalidOperationException) when (!cancel) { }
			Collect();
			Assert.That(retained.Single().IsAlive, Is.False);
			GC.KeepAlive(task);
		}

		readonly struct NotificationAwaiter : INotifyCompletion
		{
			public NotificationAwaiter GetAwaiter() => this;
			public bool IsCompleted => false;
			public int GetResult() => 7;
			public void OnCompleted(Action next) => ThreadPool.QueueUserWorkItem(_ => next());
		}
		static async Task<int> CustomAwaiter()
		{
			var first = Call(10);
			var value = await new NotificationAwaiter();
			return first * 100 + Call(value);
		}
		[Test]
		public async Task AwaitOnCompleted_supports_custom_notification_only_awaiters()
		{
			harmony.CreateProcessor(Method(nameof(CustomAwaiter))).AddInnerPrefix(Fix(nameof(Count))).Patch();
			Assert.That(await CustomAwaiter(), Is.EqualTo(102));
		}

		static async void AsyncVoid(Task resume, TaskCompletionSource<int> completed)
		{
			var first = Call(10);
			await resume;
			completed.SetResult(first * 100 + Call(20));
		}
		[Test]
		public async Task Async_void_uses_the_public_builder_protocol_too()
		{
			harmony.CreateProcessor(Method(nameof(AsyncVoid))).AddInnerPrefix(Fix(nameof(Count))).Patch();
			var resume = new TaskCompletionSource<bool>();
			var completed = new TaskCompletionSource<int>();
			AsyncVoid(resume.Task, completed);
			Assert.That(completed.Task.IsCompleted, Is.False);
			resume.SetResult(true);
			Assert.That(await completed.Task, Is.EqualTo(102));
		}

		static async Task<int[]> Async(Task resume, bool anotherAwait)
		{
			var first = Call(10);
			await resume.ConfigureAwait(false);
			var second = Call(20);
			if (anotherAwait) await Task.Yield();
			return [first, second, Call(30)];
		}
		[TestCase(false)]
		[TestCase(true)]
		public async Task Async_state_survives_real_suspensions_and_concurrent_calls(bool anotherAwait)
		{
			harmony.CreateProcessor(Method(nameof(Async))).AddInnerPrefix(Fix(nameof(Count))).Patch();
			var resume = new TaskCompletionSource<bool>();
			var first = Async(resume.Task, anotherAwait);
			var second = Async(resume.Task, anotherAwait);
			Assert.That(first.IsCompleted || second.IsCompleted, Is.False, "Both executions must actually suspend");
			resume.SetResult(true);
			Assert.That(await first, Is.EqualTo(new[] { 1, 2, 3 }));
			Assert.That(await second, Is.EqualTo(new[] { 1, 2, 3 }));
		}
		[Test]
		public async Task Already_completed_awaits_have_the_same_state_semantics()
		{
			harmony.CreateProcessor(Method(nameof(Async))).AddInnerPrefix(Fix(nameof(Count))).Patch();
			var first = Async(Task.FromResult(true), false);
			Assert.That(first.IsCompleted, Is.True);
			Assert.That(await first, Is.EqualTo(new[] { 1, 2, 3 }));
			Assert.That(await Async(Task.FromResult(true), false), Is.EqualTo(new[] { 1, 2, 3 }));
		}
		[Test]
		public async Task Unpatching_a_suspended_method_preserves_its_original_async_protocol()
		{
			var processor = harmony.CreateProcessor(Method(nameof(Async))).AddInnerPrefix(Fix(nameof(Count)));
			processor.Patch();
			var resume = new TaskCompletionSource<bool>();
			var task = Async(resume.Task, true);
			Assert.That(task.IsCompleted, Is.False);
			processor.Unpatch(HarmonyPatchType.InnerPrefix, harmony.Id);
			resume.SetResult(true);
			Assert.That(await task, Is.EqualTo(new[] { 1, 20, 30 }));
		}
		[Test]
		public async Task Changing_a_slot_type_while_suspended_initializes_new_typed_storage()
		{
			var processor = harmony.CreateProcessor(Method(nameof(Async))).AddInnerPrefix(Fix(nameof(Count))).AddInnerPrefix(Fix(nameof(Keep)));
			processor.Patch();
			var resume = new TaskCompletionSource<bool>();
			var task = Async(resume.Task, false);
			Assert.That(task.IsCompleted, Is.False);
			processor.Unpatch(Method(nameof(Count)));
			harmony.CreateProcessor(Method(nameof(Async))).AddInnerPrefix(Fix(nameof(StringCount))).Patch();
			resume.SetResult(true);
			Assert.That(await task, Is.EqualTo(new[] { 1, 1, 2 }));
		}
		static void ObserveBuilder() { }
		[Test]
		public void Intercepting_builder_bookkeeping_with_persistent_state_is_rejected_before_publication()
		{
			var body = AccessTools.StateMachineMoveNext(Method(nameof(Async)));
			harmony.CreateProcessor(body).AddInnerPrefix(Fix(nameof(Count))).Patch();
			var bytes = HarmonySharedState.GetPatchInfo(body).Serialize();
			var registration = PatchProcessor.GetOriginalInstructions(body).Select(code => code.operand).OfType<MethodInfo>()
				.First(method => method.Name == "AwaitUnsafeOnCompleted");
			var patch = new HarmonyMethod(Method(nameof(ObserveBuilder))) { innerMethod = new InnerMethod(registration) };
			var error = Assert.Throws<HarmonyException>(() => harmony.CreateProcessor(body).AddInnerFinalizer(patch).Patch());
			Assert.That(error.ToString(), Does.Contain("bookkeeping"));
			Assert.That(HarmonySharedState.GetPatchInfo(body).Serialize(), Is.EqualTo(bytes));
		}
#endif

#if NETCOREAPP3_0_OR_GREATER
		static async ValueTask<int> ValueAsync(Task resume)
		{
			var first = Call(10);
			await new ValueTask(resume).ConfigureAwait(false);
			return first * 100 + Call(20);
		}
		[Test]
		public async Task ValueTask_preserves_its_result_and_state_across_suspension()
		{
			harmony.CreateProcessor(Method(nameof(ValueAsync))).AddInnerPrefix(Fix(nameof(Count))).Patch();
			var resume = new TaskCompletionSource<bool>();
			var task = ValueAsync(resume.Task);
			Assert.That(task.IsCompleted, Is.False);
			resume.SetResult(true);
			Assert.That(await task, Is.EqualTo(102));
			Assert.That(await ValueAsync(Task.CompletedTask), Is.EqualTo(102));
		}

#if NET6_0_OR_GREATER
		[AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
		static async ValueTask<int> PooledAsync(Task resume)
		{
			var first = Call(10);
			await resume.ConfigureAwait(false);
			return first * 100 + Call(20);
		}
		[Test]
		public async Task Pooled_builders_do_not_reuse_persistent_state_between_executions()
		{
			harmony.CreateProcessor(Method(nameof(PooledAsync))).AddInnerPrefix(Fix(nameof(Count))).Patch();
			for (var i = 0; i < 3; i++)
			{
				var resume = new TaskCompletionSource<bool>();
				var task = PooledAsync(resume.Task);
				Assert.That(task.IsCompleted, Is.False);
				resume.SetResult(true);
				Assert.That(await task, Is.EqualTo(102));
			}
		}
#endif

		sealed class DisposalGate : INotifyCompletion
		{
			Action continuation;
			public DisposalGate GetAwaiter() => this;
			public bool IsCompleted => false;
			public void GetResult() { }
			public void OnCompleted(Action next) => continuation = next;
			public bool Resume() { var next = continuation; continuation = null; next(); return true; }
		}
		static async IAsyncEnumerable<int> AsyncIterator(Task resume, DisposalGate disposal = null)
		{
			try
			{
				yield return Call(10);
				await resume.ConfigureAwait(false);
				yield return Call(20);
			}
			finally
			{
				if (disposal is null) await Task.Yield();
				else await disposal;
				Call(30);
			}
		}
		[Test]
		public async Task Async_iterators_keep_state_during_awaited_disposal_and_reset_on_reuse()
		{
			harmony.CreateProcessor(Method(nameof(AsyncIterator))).AddInnerPrefix(Fix(nameof(Count))).Patch();
			var resume = new TaskCompletionSource<bool>();
			var source = AsyncIterator(resume.Task);
			var first = source.GetAsyncEnumerator();
			Assert.That(await first.MoveNextAsync(), Is.True);
			Assert.That(first.Current, Is.EqualTo(1));
			var next = first.MoveNextAsync();
			Assert.That(next.IsCompleted, Is.False);
			resume.SetResult(true);
			Assert.That(await next, Is.True);
			Assert.That(first.Current, Is.EqualTo(2));
			await first.DisposeAsync();
			Assert.That(observations.Select(item => item[0]), Is.EqualTo(new[] { 1, 2, 3 }));
			observations.Clear();
			await using var second = source.GetAsyncEnumerator();
			Assert.That(await second.MoveNextAsync(), Is.True);
			Assert.That(second.Current, Is.EqualTo(1));
			Assert.That(await second.MoveNextAsync(), Is.True);
			Assert.That(second.Current, Is.EqualTo(2));
			Assert.That(await second.MoveNextAsync(), Is.False);
			Assert.That(observations.Select(item => item[0]), Is.EqualTo(new[] { 1, 2, 3 }));
		}
		[Test]
		public void Awaited_disposal_releases_async_iterator_state()
		{
			harmony.CreateProcessor(Method(nameof(AsyncIterator))).AddInnerPrefix(Fix(nameof(Keep))).Patch();
			var iterator = OnFinishedThread(() =>
			{
				var gate = new DisposalGate();
				var enumerator = AsyncIterator(Task.CompletedTask, gate).GetAsyncEnumerator();
				Assert.That(enumerator.MoveNextAsync().GetAwaiter().GetResult(), Is.True);
				var disposal = enumerator.DisposeAsync();
				Assert.That(disposal.IsCompleted, Is.False);
				// Completing DisposeAsync can run its consumer inside MoveNext. Wait for the whole
				// resumption to return, including Harmony's finally, before testing released references.
				Assert.That(gate.Resume(), Is.True);
				disposal.GetAwaiter().GetResult();
				return enumerator;
			});
			Collect();
			Assert.That(retained.Single().IsAlive, Is.False);
			GC.KeepAlive(iterator);
		}
#endif
	}
}
