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
	public class InfixExecution : TestLogger
	{
		static readonly List<string> trace = [];
		static int skip;
		Harmony harmony;

		[SetUp]
		public void SetUp()
		{
			harmony = new Harmony("test.infix.execution." + Guid.NewGuid());
			trace.Clear();
			skip = 0;
		}

		[TearDown]
		public void TearDown() => harmony.UnpatchAll(harmony.Id);

		static MethodInfo Method(string name) => AccessTools.Method(typeof(InfixExecution), name);
		HarmonyMethod Fix(string name, MethodInfo target, int priority = Priority.Normal, params int[] positions)
			=> new(Method(name)) { innerMethod = new InnerMethod(target, positions), priority = priority };

		[MethodImpl(MethodImplOptions.NoInlining)]
		static int Call(int value)
		{
			trace.Add("call:" + value);
			return value * 2;
		}
		[MethodImpl(MethodImplOptions.NoInlining)]
		static int Adapter(int value) => Call(value);
		[MethodImpl(MethodImplOptions.NoInlining)]
		static int Outer(int value)
		{
			var result = Call(value);
			trace.Add("outer:" + value);
			return result;
		}
		[MethodImpl(MethodImplOptions.NoInlining)]
		static int ThreeCalls(int value) => Call(value) + Call(value + 1) + Call(value + 2);

		static bool PrefixA(ref int value, ref int __state, ref int __result)
		{
			trace.Add("A:" + value);
			value += 1;
			__state = 10;
			__result = 30;
			return skip != 1;
		}
		static bool PrefixB(ref int value, ref int __state, ref int __result)
		{
			trace.Add("B:" + value);
			value += 2;
			__state += 20;
			__result += 40;
			return skip != 2;
		}
		static void Observe(int value, bool __runOriginal) => trace.Add("observe:" + value + ":" + __runOriginal);
		static void PostfixA(int value, int __state, bool __runOriginal, ref int __result)
		{
			trace.Add("C:" + value + ":" + __state + ":" + __runOriginal);
			__result += 3;
		}
		static void PostfixB(int value, int __state, ref int __result)
		{
			trace.Add("D:" + value + ":" + __state);
			__result *= 2;
		}
		static int PassA(int result) { trace.Add("passA:" + result); return result + 5; }
		static int PassB(int result) { trace.Add("passB:" + result); return result * 3; }

		static IEnumerable<TestCaseData> Schedules()
		{
			foreach (var priorityA in new[] { Priority.High, Priority.Normal, Priority.Low })
				foreach (var priorityB in new[] { Priority.High, Priority.Normal, Priority.Low })
					foreach (var reverse in new[] { false, true })
						foreach (var skipAt in new[] { 0, 1, 2 })
							yield return new TestCaseData(priorityA, priorityB, reverse, skipAt);
		}

		[TestCaseSource(nameof(Schedules))]
		public void Scheduling_matches_ordinary_Harmony(int priorityA, int priorityB, bool reverse, int skipAt)
		{
			skip = skipAt;
			var prefixNames = reverse ? new[] { nameof(PrefixB), nameof(PrefixA) } : new[] { nameof(PrefixA), nameof(PrefixB) };
			var postfixNames = reverse ? new[] { nameof(PostfixB), nameof(PostfixA) } : new[] { nameof(PostfixA), nameof(PostfixB) };
			int PriorityFor(string name) => name.EndsWith("A", StringComparison.Ordinal) ? priorityA : priorityB;
			foreach (var name in prefixNames)
				harmony.Patch(Method(nameof(Adapter)), prefix: new HarmonyMethod(Method(name)) { priority = PriorityFor(name) });
			harmony.Patch(Method(nameof(Adapter)), prefix: new HarmonyMethod(Method(nameof(Observe))) { priority = Priority.Last });
			MethodInfo ordinaryReplacement = null;
			foreach (var name in postfixNames.Concat([nameof(PassA), nameof(PassB)]))
				ordinaryReplacement = harmony.Patch(Method(nameof(Adapter)), postfix: new HarmonyMethod(Method(name)) { priority = PriorityFor(name) });
			// Compare the generated schedules through the same entrypoint; native detour lifecycle has separate coverage.
			var expected = ordinaryReplacement.Invoke(null, [1]);
			var expectedTrace = trace.ToArray();
			Assert.That(expectedTrace.Count(item => item.StartsWith("pass", StringComparison.Ordinal)), Is.EqualTo(2),
				"The ordinary control must execute both passthrough postfixes");
			harmony.UnpatchAll(harmony.Id);
			trace.Clear();
			foreach (var name in prefixNames)
				harmony.CreateProcessor(Method(nameof(Adapter))).AddInnerPrefix(Fix(name, Method(nameof(Call)), PriorityFor(name))).Patch();
			harmony.CreateProcessor(Method(nameof(Adapter))).AddInnerPrefix(Fix(nameof(Observe), Method(nameof(Call)), Priority.Last)).Patch();
			MethodInfo innerReplacement = null;
			foreach (var name in postfixNames.Concat([nameof(PassA), nameof(PassB)]))
				innerReplacement = harmony.CreateProcessor(Method(nameof(Adapter))).AddInnerPostfix(Fix(name, Method(nameof(Call)), PriorityFor(name))).Patch();
			Assert.That(innerReplacement.Invoke(null, [1]), Is.EqualTo(expected));
			Assert.That(trace, Is.EqualTo(expectedTrace));
		}

		[Test]
		public void Captured_value_writes_do_not_reassign_outer_argument()
		{
			harmony.CreateProcessor(Method(nameof(Outer))).AddInnerPrefix(Fix(nameof(PrefixA), Method(nameof(Call))))
				.AddInnerPostfix(Fix(nameof(PostfixA), Method(nameof(Call)))).Patch();
			Assert.That(Outer(1), Is.EqualTo(7));
			Assert.That(trace, Is.EqualTo(new[] { "A:1", "call:2", "C:2:10:True", "outer:1" }));
		}

		static void Mark(int value) => trace.Add("patch:" + value);
		static IEnumerable<TestCaseData> Positions()
		{
			yield return new TestCaseData(new int[0], new[] { 1, 2, 3 });
			for (var position = -3; position <= 3; position++)
				if (position != 0) yield return new TestCaseData(new[] { position }, new[] { position > 0 ? position : 4 + position });
			yield return new TestCaseData(new[] { 1, 1, -3, 3, -1 }, new[] { 1, 3 });
		}
		[TestCaseSource(nameof(Positions))]
		public void Positions_select_physical_calls_once_per_record(int[] positions, int[] values)
		{
			harmony.CreateProcessor(Method(nameof(ThreeCalls))).AddInnerPrefix(Fix(nameof(Mark), Method(nameof(Call)), Priority.Normal, positions)).Patch();
			Assert.That(ThreeCalls(1), Is.EqualTo(12));
			Assert.That(trace.Where(item => item.StartsWith("patch:", StringComparison.Ordinal)), Is.EqualTo(values.Select(value => "patch:" + value)));
		}

		[Test]
		public void Generated_position_model_covers_counts_duplicates_and_integer_extremes()
		{
			for (var count = 0; count <= 5; count++)
				foreach (var positions in Enumerable.Range(-7, 15).Concat([int.MinValue, int.MaxValue]).Select(position => new[] { position })
						.Concat(new[] { new int[0], new[] { 1, 1, -1 }, new[] { 2, -2 } }))
				{
					var valid = count > 0 && positions.All(position => position != 0 && Math.Abs((long)position) <= count);
					if (!valid) Assert.Throws<ArgumentException>(() => Infix.ResolvePositions(count, positions));
					else
					{
						var expected = Enumerable.Range(0, count).Where(index => positions.Length == 0
							|| positions.Contains(index + 1) || positions.Contains(index - count));
						Assert.That(Infix.ResolvePositions(count, positions).OrderBy(index => index), Is.EqualTo(expected));
					}
				}
		}

		[TestCase(4), TestCase(-4), TestCase(int.MinValue), TestCase(int.MaxValue)]
		public void Invalid_later_registration_leaves_installed_patch_unchanged(int position)
		{
			var outer = Method(nameof(ThreeCalls));
			harmony.CreateProcessor(outer).AddInnerPrefix(Fix(nameof(Mark), Method(nameof(Call)), Priority.Normal, 1)).Patch();
			var before = PatchInfoSerialization.Serialize(HarmonySharedState.GetPatchInfo(outer));
			Assert.Throws<HarmonyException>(() => harmony.CreateProcessor(outer)
				.AddInnerPostfix(Fix(nameof(Mark), Method(nameof(Call)), Priority.Normal, position)).Patch());
			Assert.That(PatchInfoSerialization.Serialize(HarmonySharedState.GetPatchInfo(outer)), Is.EqualTo(before));
			_ = ThreeCalls(1);
			Assert.That(trace.Count(item => item.StartsWith("patch:", StringComparison.Ordinal)), Is.EqualTo(1));
		}

		static IEnumerable<CodeInstruction> DuplicateCall(IEnumerable<CodeInstruction> source)
		{
			foreach (var instruction in source)
			{
				yield return instruction;
				if (instruction.Calls(Method(nameof(Call))))
				{
					yield return new CodeInstruction(OpCodes.Ldc_I4, 10);
					yield return instruction;
					yield return new CodeInstruction(OpCodes.Add);
				}
			}
		}
		[Test]
		public void Repeated_instruction_object_is_two_sites_after_transpilers()
		{
			harmony.CreateProcessor(Method(nameof(Adapter))).AddTranspiler(Method(nameof(DuplicateCall)))
				.AddInnerPrefix(Fix(nameof(Mark), Method(nameof(Call)), Priority.Normal, -1)).Patch();
			Assert.That(Adapter(1), Is.EqualTo(22));
			Assert.That(trace, Is.EqualTo(new[] { "call:1", "patch:10", "call:10" }));
		}
		[Test]
		public void Dependency_sort_keeps_reused_patch_method_records_separate()
		{
			var shared = Method(nameof(Mark));
			var a = new Patch(shared, 0, "a", Priority.Normal, [], ["c"], false);
			var b = new Patch(shared, 1, "b", Priority.Normal, ["c"], [], false);
			var c = new Patch(Method(nameof(Observe)), 2, "c", Priority.Normal, [], [], false);
			var sorted = new PatchSorter([a, b, c], false, true).Sort();
			Assert.That(sorted.Select(patch => patch.owner), Is.EqualTo(new[] { "b", "c", "a" }));
			Assert.That(new HashSet<Patch>([a, b]).Count, Is.EqualTo(1), "Public Patch equality must retain its existing method identity");
		}
	}
}
