using HarmonyLib;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace HarmonyLibTests.Patching
{
	[TestFixture, NonParallelizable]
	public class InfixArgumentArraySynchronization : TestLogger
	{
		static readonly List<object[]> arrays = [];
		static readonly List<int> values = [];
		Harmony harmony;
		[SetUp]
		public void SetUp() { harmony = new Harmony("test.infix.argument-array-sync." + Guid.NewGuid()); arrays.Clear(); values.Clear(); }
		[TearDown]
		public void TearDown() => harmony.UnpatchAll(harmony.Id);
		static MethodInfo Method(string name) => AccessTools.DeclaredMethod(typeof(InfixArgumentArraySynchronization), name);
		void Add(string name, HarmonyPatchType role, int priority = Priority.Normal)
		{
			var fix = new HarmonyMethod(Method(name)) { innerMethod = new InnerMethod(Method(nameof(Inner))), priority = priority };
			var processor = harmony.CreateProcessor(Method(nameof(Outer)));
			if (role == HarmonyPatchType.Prefix) processor.AddInnerPrefix(fix);
			else if (role == HarmonyPatchType.Postfix) processor.AddInnerPostfix(fix);
			else processor.AddInnerFinalizer(fix);
			processor.Patch();
		}

		[TestCase(false, false)]
		[TestCase(false, true)]
		[TestCase(true, false)]
		[TestCase(true, true)]
		public void Array_receivers_preserve_typed_writes_in_both_scopes(bool outer, bool helper)
		{
			Add(outer ? nameof(ReadOuter) : nameof(Read), HarmonyPatchType.Prefix, Priority.First);
			Add(outer ? nameof(WriteOuter) : nameof(Write), HarmonyPatchType.Prefix, Priority.High);
			Add(outer ? nameof(ReadOuter) : nameof(Read), HarmonyPatchType.Prefix, Priority.Low);
			Add(outer ? nameof(ReadOuter) : nameof(Read), HarmonyPatchType.Postfix);
			if (helper) Add(nameof(Finalizer), HarmonyPatchType.Finalizer);
			var value = 1;
			Assert.AreEqual(111, Outer(ref value));
			Assert.AreEqual(111, value);
			Assert.AreEqual(new[] { 1, 11, 111 }, values);
			Assert.AreSame(arrays[0], arrays[1]);
			Assert.AreSame(arrays[1], arrays[2]);
		}

		[TestCase(false)]
		[TestCase(true)]
		public void First_array_receiver_after_a_typed_write_sees_current_storage(bool outer)
		{
			Add(outer ? nameof(WriteOuter) : nameof(Write), HarmonyPatchType.Prefix, Priority.High);
			Add(outer ? nameof(ReadOuter) : nameof(Read), HarmonyPatchType.Prefix, Priority.Low);
			var value = 1;
			Assert.AreEqual(111, Outer(ref value));
			Assert.AreEqual(new[] { 11 }, values);
		}

		[TestCase(false)]
		[TestCase(true)]
		public void First_finalizer_array_receiver_sees_original_ref_changes(bool outer)
		{
			Add(outer ? nameof(ReadOuter) : nameof(Read), HarmonyPatchType.Finalizer);
			var value = 1;
			Assert.AreEqual(101, Outer(ref value));
			Assert.AreEqual(101, value);
			Assert.AreEqual(new[] { 101 }, values);
		}

		static void Read(object[] __args) { arrays.Add(__args); values.Add((int)__args[0]); }
		static void ReadOuter([HarmonyOuter, HarmonyArgument("__args")] object[] outer) => Read(outer);
		static void Write(ref int value) => value += 10;
		static void WriteOuter([HarmonyOuter] ref int value) => value += 10;
		static void Finalizer(Exception __exception) { }
		[MethodImpl(MethodImplOptions.NoInlining)]
		static int Inner(ref int value) { value += 100; return value; }
		[MethodImpl(MethodImplOptions.NoInlining)]
		static int Outer(ref int value) => Inner(ref value);
	}
}
