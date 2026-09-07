#if NETCOREAPP3_0_OR_GREATER
using HarmonyLib;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace HarmonyLibTests.Patching
{
	[TestFixture, NonParallelizable]
	public class InfixAsyncIteratorExecution : TestLogger
	{
		static readonly List<int> callbacks = [];
		Harmony harmony;

		[SetUp]
		public void SetUp()
		{
			harmony = new Harmony("test.infix.asynciterator." + Guid.NewGuid());
			callbacks.Clear();
		}

		[TearDown] public void TearDown() => harmony.UnpatchAll(harmony.Id);
		static MethodInfo Method(string name) => AccessTools.DeclaredMethod(typeof(InfixAsyncIteratorExecution), name);

		[MethodImpl(MethodImplOptions.NoInlining)]
		static int Call(int value) => value;

		static async IAsyncEnumerable<int> Sequence(int value, Task resume)
		{
			yield return Call(value);
			await resume;
			yield return Call(value);
			yield return value;
		}

		static void IncrementCaptured([HarmonyOuter, HarmonyArgument("value", ArgumentMode.Captured)] ref int value)
		{
			callbacks.Add(value);
			value += 10;
		}

		[Test]
		public async Task Auto_async_iterator_executes_captures_across_suspension_and_unpatches_its_body()
		{
			var factory = Method(nameof(Sequence));
			var body = AccessTools.StateMachineMoveNext(factory);
			var patch = new HarmonyMethod(Method(nameof(IncrementCaptured)))
			{
				innerMethod = new InnerMethod(Method(nameof(Call))),
				infixOuterBody = InfixOuterBody.Auto
			};
			var processor = harmony.CreateProcessor(factory).AddInnerPrefix(patch);
			processor.Patch();

			Assert.That(body.ReturnType, Is.EqualTo(typeof(void)), "The execution body is MoveNext, not MoveNextAsync");
			Assert.That(Harmony.GetPatchInfo(body).InnerPrefixes.Count, Is.EqualTo(1));
			Assert.That(Harmony.GetPatchInfo(factory)?.Owners.Count ?? 0, Is.Zero);
			Assert.That(await ReadSequence(), Is.EqualTo(new[] { 2, 12, 22 }));
			Assert.That(callbacks, Is.EqualTo(new[] { 2, 12 }), "Both selected calls must execute the installed callback");

			processor.Unpatch(HarmonyPatchType.InnerPrefix, harmony.Id);
			Assert.That(Harmony.GetPatchInfo(body).InnerPrefixes, Is.Empty);
			callbacks.Clear();
			Assert.That(await ReadSequence(), Is.EqualTo(new[] { 2, 2, 2 }));
			Assert.That(callbacks, Is.Empty);
		}

		static async Task<int[]> ReadSequence()
		{
			var suspension = new TaskCompletionSource<bool>();
			await using var enumerator = Sequence(2, suspension.Task).GetAsyncEnumerator();
			Assert.That(await enumerator.MoveNextAsync(), Is.True);
			var first = enumerator.Current;
			var pending = enumerator.MoveNextAsync();
			Assert.That(pending.IsCompleted, Is.False, "The iterator must suspend at the await after its first yield");
			suspension.SetResult(true);
			Assert.That(await pending, Is.True);
			var second = enumerator.Current;
			Assert.That(await enumerator.MoveNextAsync(), Is.True);
			var third = enumerator.Current;
			Assert.That(await enumerator.MoveNextAsync(), Is.False);
			return [first, second, third];
		}
	}
}
#endif
