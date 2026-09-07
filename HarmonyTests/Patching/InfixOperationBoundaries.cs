using HarmonyLib;
using NUnit.Framework;
using System;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace HarmonyLibTests.Patching
{
	[TestFixture, NonParallelizable]
	public class InfixOperationBoundaries : TestLogger
	{
		static readonly Exception failure = new InvalidOperationException("inner operation patch");
		static Exception caught;
		static int finallyCalls, constructions;
		static readonly Payload payload = new();
		Harmony harmony;

		class Payload
		{
			public int Value = 7;
			public Payload() => constructions++;
		}

		[SetUp]
		public void SetUp()
		{
			harmony = new Harmony("test.infix.operation.boundaries." + Guid.NewGuid());
			caught = null; finallyCalls = constructions = 0;
		}
		[TearDown] public void TearDown() => harmony.UnpatchAll(harmony.Id);
		static MethodInfo Method(string name) => AccessTools.DeclaredMethod(typeof(InfixOperationBoundaries), name);
		static InnerTarget Target(string operation) => operation switch
		{
			"Field" => new InnerTarget(AccessTools.Field(typeof(Payload), nameof(Payload.Value)), InnerTargetKind.FieldRead),
			"Constructor" => new InnerTarget(typeof(Payload).GetConstructor(Type.EmptyTypes)),
			_ => InnerTarget.Constant("anchor")
		};
		static void Throw() => throw failure;
		[MethodImpl(MethodImplOptions.NoInlining)] static string Format(int value) => value.ToString();
		static Exception Suppress(Exception __exception) { caught = __exception; finallyCalls++; return null; }
		class Counter(int value)
		{
			public int Value = value;
		}
		[MethodImpl(MethodImplOptions.NoInlining)] static int Rebuilt(int value) => new Counter(value).Value + 42;
		static Counter IncreaseConstructed(Counter previous) => new(previous.Value + 1);
		static int IncreaseValue(int previous) => previous + 1;

		[Test]
		public void Repeated_rebuild_collection_and_removal_keep_only_current_operation_patches()
		{
			var original = Method(nameof(Rebuilt));
			for (var cycle = 0; cycle < 8; cycle++)
			{
				var targets = new[] { new InnerTarget(typeof(Counter).GetConstructor([typeof(int)])),
					new InnerTarget(AccessTools.Field(typeof(Counter), nameof(Counter.Value)), InnerTargetKind.FieldRead), InnerTarget.Constant(42) };
				for (var index = 0; index < targets.Length; index++)
				{
					harmony.CreateProcessor(original).AddInnerPostfix(new HarmonyMethod(Method(index == 0 ? nameof(IncreaseConstructed) : nameof(IncreaseValue)))
					{ innerTarget = targets[index] }).Patch();
					Assert.AreEqual(cycle + 43 + index, Rebuilt(cycle));
				}
				GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
				for (var value = 0; value < 100; value++) Assert.AreEqual(value + 45, Rebuilt(value));
				harmony.Unpatch(original, HarmonyPatchType.All, harmony.Id);
				Assert.AreEqual(cycle + 42, Rebuilt(cycle));
				Assert.IsEmpty(Harmony.GetPatchInfo(original).InnerPostfixes);
			}
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		static string FieldHandled()
		{
			try { return Format(payload.Value); }
			catch (InvalidOperationException error) { caught = error; return "caught"; }
			finally { finallyCalls++; }
		}
		[MethodImpl(MethodImplOptions.NoInlining)]
		static string ConstructorHandled()
		{
			try { return new Payload().Value.ToString(); }
			catch (InvalidOperationException error) { caught = error; return "caught"; }
			finally { finallyCalls++; }
		}
		[MethodImpl(MethodImplOptions.NoInlining)]
		static string LiteralHandled()
		{
			try { return "anchor"; }
			catch (InvalidOperationException error) { caught = error; return "caught"; }
			finally { finallyCalls++; }
		}
		[MethodImpl(MethodImplOptions.NoInlining)] static string FieldUnhandled() => Format(payload.Value);
		[MethodImpl(MethodImplOptions.NoInlining)] static string ConstructorUnhandled() => new Payload().Value.ToString();
		[MethodImpl(MethodImplOptions.NoInlining)] static string LiteralUnhandled() => "anchor";

		[Test]
		public void Exceptions_from_each_operation_patch_reach_existing_handlers_or_outer_finalizers(
			[Values("Field", "Constructor", "Literal")] string operation, [Values] bool postfix, [Values] bool handled)
		{
			var original = Method(operation + (handled ? "Handled" : "Unhandled"));
			var processor = harmony.CreateProcessor(original);
			var patch = new HarmonyMethod(Method(nameof(Throw))) { innerTarget = Target(operation) };
			if (postfix) processor.AddInnerPostfix(patch); else processor.AddInnerPrefix(patch);
			if (!handled) processor.AddFinalizer(Method(nameof(Suppress)));
			processor.Patch();
			Assert.AreEqual(handled ? "caught" : null, original.Invoke(null, null));
			Assert.AreSame(failure, caught);
			Assert.AreEqual(1, finallyCalls);
			Assert.AreEqual(operation == "Constructor" && postfix ? 1 : 0, constructions);
		}
	}
}
