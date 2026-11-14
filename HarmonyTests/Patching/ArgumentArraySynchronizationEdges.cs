using HarmonyLib;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace HarmonyLibTests.Patching
{
	[TestFixture, NonParallelizable]
	public class ArgumentArraySynchronizationEdges : TestLogger
	{
		static readonly List<object[]> arrays = [];
		static readonly List<object> values = [];
		Harmony harmony;

		[SetUp]
		public void SetUp()
		{
			harmony = new Harmony("test.argument-array-sync.edges." + Guid.NewGuid());
			arrays.Clear();
			values.Clear();
		}

		[TearDown]
		public void TearDown() => harmony.UnpatchAll(harmony.Id);

		static HarmonyMethod Patch(string name, int priority = Priority.Normal) => new(AccessTools.DeclaredMethod(typeof(ArgumentArraySynchronizationEdges), name)) { priority = priority };
		void Add(string target, string patch, HarmonyPatchType role, int priority = Priority.Normal)
		{
			var original = AccessTools.DeclaredMethod(typeof(ArgumentArraySynchronizationEdges), target);
			var method = Patch(patch, priority);
			if (role == HarmonyPatchType.Prefix) harmony.Patch(original, prefix: method);
			else if (role == HarmonyPatchType.Postfix) harmony.Patch(original, postfix: method);
			else harmony.Patch(original, finalizer: method);
		}

		[Test]
		public void Alternating_array_and_named_writes_share_one_current_array()
		{
			Add(nameof(ValueTarget), nameof(EditArray), HarmonyPatchType.Prefix, Priority.First);
			Add(nameof(ValueTarget), nameof(WriteTyped), HarmonyPatchType.Prefix, Priority.High);
			Add(nameof(ValueTarget), nameof(ReadArray), HarmonyPatchType.Prefix, Priority.Low);
			Assert.AreEqual(17, ValueTarget(1));
			Assert.AreEqual(new object[] { 1, 17 }, values);
			Assert.AreSame(arrays[0], arrays[1]);
		}

		[Test]
		public void Boxed_write_to_an_original_ref_argument_reaches_later_array()
		{
			Add(nameof(RefValueTarget), nameof(EditArray), HarmonyPatchType.Prefix, Priority.First);
			Add(nameof(RefValueTarget), nameof(WriteBoxed), HarmonyPatchType.Prefix, Priority.High);
			Add(nameof(RefValueTarget), nameof(ReadArray), HarmonyPatchType.Prefix, Priority.Low);
			var value = 1;
			Assert.AreEqual(17, RefValueTarget(ref value));
			Assert.AreEqual(17, value);
			Assert.AreEqual(new object[] { 1, 17 }, values);
		}

		[Test]
		public void Consecutive_array_writes_are_committed_between_callbacks()
		{
			Add(nameof(ValueTarget), nameof(EditArray), HarmonyPatchType.Prefix, Priority.First);
			Add(nameof(ValueTarget), nameof(ReadArray), HarmonyPatchType.Prefix, Priority.Low);
			Assert.AreEqual(7, ValueTarget(1));
			Assert.AreEqual(new object[] { 1, 7 }, values);
			Assert.AreSame(arrays[0], arrays[1]);
		}

		[TestCase(nameof(ReadAliasedArray))]
		[TestCase(nameof(ReadBroadArray))]
		public void Aliased_and_broader_array_injections_observe_named_writes(string observer)
		{
			Add(nameof(ValueTarget), nameof(WriteTyped), HarmonyPatchType.Prefix, Priority.High);
			Add(nameof(ValueTarget), observer, HarmonyPatchType.Prefix, Priority.Low);
			Assert.AreEqual(11, ValueTarget(1));
			Assert.AreEqual(new object[] { 11 }, values);
		}

		[TestCase(HarmonyPatchType.Prefix)]
		[TestCase(HarmonyPatchType.Postfix)]
		public void Finalizer_observes_named_writes_from_a_throwing_patch(HarmonyPatchType role)
		{
			Add(nameof(RefTarget), nameof(WriteThenThrow), role);
			Add(nameof(RefTarget), nameof(ReadArray), HarmonyPatchType.Finalizer);
			var value = 1;
			Assert.Throws<InvalidOperationException>(() => RefTarget(ref value));
			Assert.AreEqual(new object[] { 17 }, values);
			Assert.AreEqual(17, value);
		}

		[Test]
		public void Empty_arrays_share_identity_across_all_roles()
		{
			Add(nameof(EmptyTarget), nameof(ReadEmptyArray), HarmonyPatchType.Prefix);
			Add(nameof(EmptyTarget), nameof(ReadEmptyArray), HarmonyPatchType.Postfix);
			Add(nameof(EmptyTarget), nameof(ReadEmptyArray), HarmonyPatchType.Finalizer);
			Assert.AreEqual(42, EmptyTarget());
			Assert.AreEqual(3, arrays.Count);
			Assert.IsEmpty(arrays[0]);
			Assert.AreSame(arrays[0], arrays[1]);
			Assert.AreSame(arrays[1], arrays[2]);
		}

		[Test]
		public void Throwing_finalizer_discards_uncommitted_array_edits_on_retry()
		{
			Add(nameof(ValueTarget), nameof(EditThenThrowFinalizer), HarmonyPatchType.Finalizer);
			Assert.Throws<InvalidOperationException>(() => ValueTarget(1));
			Assert.AreEqual(new object[] { 1, 1 }, values);
			Assert.AreSame(arrays[0], arrays[1]);
		}

		static void ReadArray(object[] __args) { arrays.Add(__args); values.Add(__args[0]); }
		static void EditArray(object[] __args) { ReadArray(__args); __args[0] = 7; }
		static void WriteTyped(ref int value) => value += 10;
		static void WriteBoxed(ref object value) => value = (int)value + 10;
		static void ReadAliasedArray([HarmonyArgument("__args")] object[] renamed) => ReadArray(renamed);
		static void ReadBroadArray(object __args) => ReadArray((object[])__args);
		static void ReadEmptyArray(object[] __args) => arrays.Add(__args);
		static void WriteThenThrow(ref int value) { value = 17; throw new InvalidOperationException("patch failure"); }
		static void EditThenThrowFinalizer(Exception __exception, object[] __args)
		{
			ReadArray(__args);
			if (__exception == null) { __args[0] = 99; throw new InvalidOperationException("finalizer failure"); }
		}
		[MethodImpl(MethodImplOptions.NoInlining)]
		static int ValueTarget(int value) => value;
		[MethodImpl(MethodImplOptions.NoInlining)]
		static void RefTarget(ref int value) => value += 100;
		[MethodImpl(MethodImplOptions.NoInlining)]
		static int RefValueTarget(ref int value) => value;
		[MethodImpl(MethodImplOptions.NoInlining)]
		static int EmptyTarget() => 42;
	}
}
