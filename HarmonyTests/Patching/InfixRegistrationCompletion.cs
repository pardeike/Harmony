using HarmonyLib;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace HarmonyLibTests.Patching
{
	[TestFixture, NonParallelizable]
	public class InfixRegistrationCompletion : TestLogger
	{
		static readonly List<string> trace = [];
		static readonly List<MethodBase> prepared = [];
		Harmony harmony;
		[SetUp] public void SetUp() { harmony = new Harmony("infix.completion.registration." + Guid.NewGuid()); trace.Clear(); prepared.Clear(); }
		[TearDown] public void TearDown() => harmony.UnpatchAll(harmony.Id);
		static MethodInfo Method(string name) => AccessTools.DeclaredMethod(typeof(InfixRegistrationCompletion), name);
		static HarmonyMethod Fix(string name, InfixOuterBody? body = null) => new(Method(name)) { innerMethod = new InnerMethod(Method(nameof(Call))), infixOuterBody = body };
		[MethodImpl(MethodImplOptions.NoInlining)] static int Call(int value) { trace.Add("call"); return value * 2; }
		[MethodImpl(MethodImplOptions.NoInlining)] static int Outer(int value) => Call(value);
		[MethodImpl(MethodImplOptions.NoInlining)] static IEnumerable<int> Iterator(int value) { yield return Call(value); yield return Call(value + 1); }
		static void First(ref int value) { trace.Add("first"); value++; }
		static void Second(ref int value) { trace.Add("second"); value += 10; }
		static void After(ref int __result) { trace.Add("after"); __result += 3; }
		static void FinalA() => trace.Add("finalA");
		static Exception FinalB(Exception __exception) { trace.Add("finalB"); return __exception; }
		static void Ordinary() => trace.Add("factory");
		static void Missing() { }

		[Test]
		public void Pending_inner_roles_accumulate_in_order_and_remove_by_role()
		{
			var outer = Method(nameof(Outer));
			var processor = harmony.CreateProcessor(outer).AddInnerPrefix(Fix(nameof(First))).AddInnerPrefix(Fix(nameof(Second)))
				.AddInnerPostfix(Fix(nameof(After))).AddInnerFinalizer(Fix(nameof(FinalA))).AddInnerFinalizer(Fix(nameof(FinalB)));
			processor.Patch();
			Assert.AreEqual(27, Outer(1));
			Assert.AreEqual(new[] { "first", "second", "call", "after", "finalA", "finalB" }, trace);
			var patches = Harmony.GetPatchInfo(outer);
			Assert.AreEqual(2, patches.InnerPrefixes.Count);
			Assert.AreEqual(2, patches.InnerFinalizers.Count);
			Assert.AreEqual(new[] { 0, 1 }, patches.InnerPrefixes.Select(patch => patch.index));
			processor.Unpatch(HarmonyPatchType.InnerFinalizer, harmony.Id);
			Assert.IsEmpty(Harmony.GetPatchInfo(outer).InnerFinalizers);
			trace.Clear();
			Assert.AreEqual(27, Outer(1));
			Assert.AreEqual(new[] { "first", "second", "call", "after" }, trace);
		}

		[HarmonyInfix(typeof(InfixRegistrationCompletion), nameof(Call)), HarmonyPrefix]
		static void AttributedFirst(ref int value) => First(ref value);
		[HarmonyInfix(typeof(InfixRegistrationCompletion), nameof(Call)), HarmonyPostfix]
		static void AttributedAfter(ref int __result) => After(ref __result);
		[HarmonyInfix(typeof(InfixRegistrationCompletion), nameof(Call)), HarmonyFinalizer]
		static void AttributedFinalizer() => FinalA();

		[Test]
		public void MethodInfo_overloads_accumulate_and_nulls_preserve_pending_patches()
		{
			var processor = harmony.CreateProcessor(Method(nameof(Outer)))
				.AddInnerPrefix(Method(nameof(AttributedFirst))).AddInnerPrefix(Method(nameof(AttributedFirst)))
				.AddInnerPostfix(Method(nameof(AttributedAfter))).AddInnerPostfix(Method(nameof(AttributedAfter)))
				.AddInnerFinalizer(Method(nameof(AttributedFinalizer))).AddInnerFinalizer(Method(nameof(AttributedFinalizer)));
			processor.AddInnerPrefix((HarmonyMethod)null).AddInnerPostfix((HarmonyMethod)null).AddInnerFinalizer((HarmonyMethod)null);
			Assert.Throws<ArgumentNullException>(() => processor.AddInnerPrefix((MethodInfo)null));
			Assert.Throws<ArgumentNullException>(() => processor.AddInnerPostfix((MethodInfo)null));
			Assert.Throws<ArgumentNullException>(() => processor.AddInnerFinalizer((MethodInfo)null));
			processor.Patch();
			Assert.AreEqual(12, Outer(1));
			Assert.AreEqual(new[] { "first", "first", "call", "after", "after", "finalA", "finalA" }, trace);
		}

		[Test]
		public void Reusing_a_processor_reinstalls_its_pending_registrations()
		{
			var processor = harmony.CreateProcessor(Method(nameof(Outer))).AddInnerPrefix(Fix(nameof(First)));
			processor.Patch();
			processor.Patch();
			Assert.AreEqual(6, Outer(1));
			Assert.AreEqual(2, Harmony.GetPatchInfo(Method(nameof(Outer))).InnerPrefixes.Count);
		}

		[Test]
		public void Invalid_member_of_a_batch_leaves_existing_state_and_wrapper_unchanged()
		{
			var outer = Method(nameof(Outer));
			harmony.CreateProcessor(outer).AddInnerPrefix(Fix(nameof(First))).Patch();
			var before = HarmonySharedState.GetPatchInfo(outer).Serialize();
			var missing = new HarmonyMethod(Method(nameof(Second))) { innerMethod = new InnerMethod(Method(nameof(Missing))) };
			Assert.Catch<Exception>(() => harmony.CreateProcessor(outer).AddInnerPrefix(Fix(nameof(Second))).AddInnerPrefix(missing).Patch());
			Assert.AreEqual(before, HarmonySharedState.GetPatchInfo(outer).Serialize());
			Assert.AreEqual(4, Outer(1));
		}

		[Test]
		public void Ordinary_pending_prefixes_still_replace_each_other()
		{
			harmony.CreateProcessor(Method(nameof(Outer))).AddPrefix(Method(nameof(First))).AddPrefix(Method(nameof(Second))).Patch();
			Assert.AreEqual(22, Outer(1));
			Assert.AreEqual(new[] { "second", "call" }, trace);
		}

		[HarmonyPatch]
		class RepeatedOrdinaryTargets
		{
			static IEnumerable<MethodBase> TargetMethods() => [Method(nameof(Outer)), Method(nameof(Outer))];
			static void Prefix(ref int value) => First(ref value);
		}

		[HarmonyPatch]
		class RepeatedDeclaredTargets
		{
			static IEnumerable<MethodBase> TargetMethods() => [Method(nameof(Outer)), Method(nameof(Outer))];
			[HarmonyPrefix, HarmonyInfix(typeof(InfixRegistrationCompletion), nameof(Call))]
			static void Before(ref int value) => First(ref value);
		}

		[TestCase(typeof(RepeatedOrdinaryTargets)), TestCase(typeof(RepeatedDeclaredTargets))]
		public void Repeated_explicit_class_targets_keep_their_existing_multiplicity(Type patchType)
		{
			harmony.CreateClassProcessor(patchType).Patch();
			Assert.AreEqual(6, Outer(1));
			Assert.AreEqual(new[] { "first", "first", "call" }, trace);
		}

		[TestCase(false)]
		[TestCase(true)]
		public void Auto_processor_unpatch_uses_its_successfully_installed_body(bool byMethod)
		{
			var factory = Method(nameof(Iterator));
			var body = AccessTools.StateMachineMoveNext(factory);
			Assert.IsNotNull(body);
			var patch = Fix(nameof(First), InfixOuterBody.Auto);
			var processor = harmony.CreateProcessor(factory).AddInnerPrefix(patch);
			processor.Patch();
			Assert.AreEqual(new[] { 4, 6 }, Iterator(1));
			Assert.AreEqual(1, Harmony.GetPatchInfo(body).InnerPrefixes.Count);
			Assert.AreEqual(0, Harmony.GetPatchInfo(factory)?.Owners.Count ?? 0);
			patch.infixOuterBody = InfixOuterBody.Declared;
			Assert.Throws<ArgumentException>(() => processor.Patch());
			if (byMethod) processor.Unpatch(Method(nameof(First)));
			else processor.Unpatch(HarmonyPatchType.InnerPrefix, harmony.Id);
			Assert.AreEqual(new[] { 2, 4 }, Iterator(1));
			Assert.IsEmpty(Harmony.GetPatchInfo(body).InnerPrefixes);
		}

		[Test]
		public void Mixed_manual_targets_fail_before_installing_either_method()
		{
			var factory = Method(nameof(Iterator));
			Assert.Throws<ArgumentException>(() => harmony.CreateProcessor(factory).AddPrefix(Method(nameof(Ordinary)))
				.AddInnerPrefix(Fix(nameof(First), InfixOuterBody.Auto)).Patch());
			Assert.AreEqual(0, Harmony.GetPatchInfo(factory)?.Owners.Count ?? 0);
			Assert.AreEqual(0, Harmony.GetPatchInfo(AccessTools.StateMachineMoveNext(factory))?.Owners.Count ?? 0);
			Assert.AreEqual(new[] { 2, 4 }, Iterator(1));
		}

		[HarmonyPatch(typeof(InfixRegistrationCompletion), nameof(Iterator))]
		class MixedClass
		{
			[HarmonyPrepare] static bool Prepare(MethodBase original) { if (original is not null) prepared.Add(original); return true; }
			[HarmonyPrefix] static void Factory() => Ordinary();
			[HarmonyPrefix, HarmonyInfix(typeof(InfixRegistrationCompletion), nameof(Call), OuterBody = InfixOuterBody.Auto)]
			static void Body(ref int value) => First(ref value);
		}

		[Test]
		public void Class_roles_group_by_actual_method_and_prepare_and_unpatch_both_jobs()
		{
			var processor = harmony.CreateClassProcessor(typeof(MixedClass));
			Assert.AreEqual(2, processor.Patch().Count);
			CollectionAssert.AreEquivalent(new[] { Method(nameof(Iterator)), AccessTools.StateMachineMoveNext(Method(nameof(Iterator))) }, prepared);
			Assert.AreEqual(new[] { 4, 6 }, Iterator(1));
			Assert.AreEqual(new[] { "factory", "first", "call", "first", "call" }, trace);
			processor.Unpatch();
			trace.Clear();
			Assert.AreEqual(new[] { 2, 4 }, Iterator(1));
			Assert.AreEqual(new[] { "call", "call" }, trace);
		}

		class AliasedClass
		{
			[HarmonyTargetMethods] static IEnumerable<MethodBase> Targets() => [Method(nameof(Iterator)), AccessTools.StateMachineMoveNext(Method(nameof(Iterator)))];
			[HarmonyPrefix, HarmonyInfix(typeof(InfixRegistrationCompletion), nameof(Call), OuterBody = InfixOuterBody.Auto)]
			static void Body(ref int value) => First(ref value);
		}

		[Test]
		public void Factory_and_body_aliases_add_each_class_declaration_once()
		{
			var processor = harmony.CreateClassProcessor(typeof(AliasedClass));
			Assert.AreEqual(1, processor.Patch().Count);
			Assert.AreEqual(1, Harmony.GetPatchInfo(AccessTools.StateMachineMoveNext(Method(nameof(Iterator)))).InnerPrefixes.Count);
			Assert.AreEqual(new[] { 4, 6 }, Iterator(1));
			processor.Unpatch();
			Assert.AreEqual(new[] { 2, 4 }, Iterator(1));
		}

		[HarmonyPatch(typeof(InfixRegistrationCompletion), nameof(Iterator))]
		class SkipBody
		{
			[HarmonyPrepare] static bool Prepare(MethodBase original) { if (original is null) return true; prepared.Add(original); return false; }
			[HarmonyPrefix, HarmonyInfix(typeof(InfixRegistrationCompletion), "NoSuchOperation", OuterBody = InfixOuterBody.Auto)]
			static void Body() { }
		}

		[Test]
		public void Per_body_prepare_can_skip_an_unresolvable_inner_selector()
		{
			Assert.DoesNotThrow(() => harmony.CreateClassProcessor(typeof(SkipBody)).Patch());
			Assert.AreEqual(new[] { AccessTools.StateMachineMoveNext(Method(nameof(Iterator))) }, prepared);
			Assert.AreEqual(0, Harmony.GetPatchInfo(prepared.Single())?.Owners.Count ?? 0);
		}
	}
}
