using HarmonyLib;
using NUnit.Framework;
using System;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace HarmonyLibTests.Patching
{
	[TestFixture, NonParallelizable]
	public class BoxedArgumentCopyBack : TestLogger
	{
		Harmony harmony;
		static object observed;
		static bool replaceBox;
		static bool nullBox;

		[SetUp]
		public void SetUp()
		{
			harmony = new Harmony("test.boxed-argument-copy-back." + Guid.NewGuid());
			observed = null;
			replaceBox = false;
			nullBox = false;
		}

		[TearDown]
		public void TearDown() => harmony.UnpatchAll(harmony.Id);

		static MethodInfo Method(string name) => AccessTools.DeclaredMethod(typeof(BoxedArgumentCopyBack), name);
		void Add(MethodInfo original, string name, HarmonyPatchType role, int priority = Priority.Normal)
		{
			var patch = new HarmonyMethod(Method(name)) { priority = priority };
			if (role == HarmonyPatchType.Prefix) harmony.Patch(original, prefix: patch);
			else if (role == HarmonyPatchType.Postfix) harmony.Patch(original, postfix: patch);
			else harmony.Patch(original, finalizer: patch);
		}

		[TestCase(HarmonyPatchType.Prefix, false)]
		[TestCase(HarmonyPatchType.Postfix, false)]
		[TestCase(HarmonyPatchType.Finalizer, false)]
		[TestCase(HarmonyPatchType.Prefix, true)]
		[TestCase(HarmonyPatchType.Postfix, true)]
		[TestCase(HarmonyPatchType.Finalizer, true)]
		public void Boxed_writes_reach_the_selected_argument_in_every_role(HarmonyPatchType role, bool instance)
		{
			var original = instance ? AccessTools.DeclaredMethod(typeof(InstanceTarget), nameof(InstanceTarget.Run)) : Method(nameof(ValueTarget));
			Add(original, nameof(WriteBox), role, Priority.High);
			Add(original, nameof(Observe), role, Priority.Low);
			var result = instance ? new InstanceTarget().Run(2, 1) : ValueTarget(2, 1);
			Assert.AreEqual(role == HarmonyPatchType.Prefix ? 217 : 201, result);
			Assert.AreEqual(17, observed);
		}

		[TestCase(false, false, false)]
		[TestCase(false, false, true)]
		[TestCase(false, true, false)]
		[TestCase(false, true, true)]
		[TestCase(true, false, false)]
		[TestCase(true, false, true)]
		[TestCase(true, true, false)]
		[TestCase(true, true, true)]
		public void Boxed_copy_back_follows_array_restore_independent_of_parameter_order(bool originalRef, bool writeBox, bool boxFirst)
		{
			replaceBox = writeBox;
			Add(Method(originalRef ? nameof(RefTarget) : nameof(ValueTarget)), boxFirst ? nameof(BoxThenArray) : nameof(ArrayThenBox), HarmonyPatchType.Prefix);
			var value = 1;
			var result = originalRef ? RefTarget(2, ref value) : ValueTarget(2, value);
			Assert.AreEqual(writeBox ? 217 : 201, result);
			if (originalRef) Assert.AreEqual(writeBox ? 17 : 1, value);
		}

		[TestCase(false, false)]
		[TestCase(false, true)]
		[TestCase(true, false)]
		[TestCase(true, true)]
		public void An_unchanged_boxed_alias_copies_back_after_a_typed_write(bool originalRef, bool boxFirst)
		{
			Add(Method(originalRef ? nameof(RefTarget) : nameof(ValueTarget)), boxFirst ? nameof(BoxBeforeTyped) : nameof(TypedBeforeBox), HarmonyPatchType.Prefix);
			var value = 1;
			var result = originalRef ? RefTarget(2, ref value) : ValueTarget(2, value);
			Assert.AreEqual(201, result);
			if (originalRef) Assert.AreEqual(1, value);
		}

		[TestCase(false, false, false)]
		[TestCase(false, false, true)]
		[TestCase(false, true, false)]
		[TestCase(false, true, true)]
		[TestCase(true, false, false)]
		[TestCase(true, false, true)]
		[TestCase(true, true, false)]
		[TestCase(true, true, true)]
		public void Boxed_aliases_copy_back_in_callback_parameter_order(bool originalRef, bool writeBoth, bool reverse)
		{
			replaceBox = writeBoth;
			Add(Method(originalRef ? nameof(RefTarget) : nameof(ValueTarget)), reverse ? nameof(SecondBoxBeforeFirst) : nameof(FirstBoxBeforeSecond), HarmonyPatchType.Prefix);
			var value = 1;
			var expected = reverse ? 17 : writeBoth ? 23 : 1;
			var result = originalRef ? RefTarget(2, ref value) : ValueTarget(2, value);
			Assert.AreEqual(200 + expected, result);
			if (originalRef) Assert.AreEqual(expected, value);
		}

		[TestCase(false, false)]
		[TestCase(false, true)]
		[TestCase(true, false)]
		[TestCase(true, true)]
		public void Invalid_box_copy_back_throws_after_array_restore(bool originalRef, bool useNull)
		{
			nullBox = useNull;
			var original = Method(originalRef ? nameof(RefTarget) : nameof(ValueTarget));
			Add(original, nameof(InvalidBox), HarmonyPatchType.Prefix);
			Add(original, nameof(Observe), HarmonyPatchType.Finalizer);
			var value = 1;
			TestDelegate invoke = () => { if (originalRef) RefTarget(2, ref value); else ValueTarget(2, value); };
			Assert.Throws(useNull ? typeof(NullReferenceException) : typeof(InvalidCastException), invoke);
			Assert.AreEqual(99, observed);
			if (originalRef) Assert.AreEqual(99, value);
		}

		[TestCase(nameof(ThrowTyped), 17)]
		[TestCase(nameof(ThrowBox), 1)]
		[TestCase(nameof(ThrowArray), 1)]
		public void Throwing_callbacks_keep_existing_delayed_copy_back_boundaries(string patch, int expected)
		{
			var original = Method(nameof(ValueTarget));
			Add(original, patch, HarmonyPatchType.Prefix);
			Add(original, nameof(Observe), HarmonyPatchType.Finalizer);
			Assert.Throws<InvalidOperationException>(() => ValueTarget(2, 1));
			Assert.AreEqual(expected, observed);
		}

		[Test]
		public void Pass_through_postfix_preserves_its_result_while_copying_back_a_box()
		{
			var original = Method(nameof(ValueTarget));
			Add(original, nameof(PassThrough), HarmonyPatchType.Postfix);
			Add(original, nameof(Observe), HarmonyPatchType.Finalizer);
			Assert.AreEqual(202, ValueTarget(2, 1));
			Assert.AreEqual(17, observed);
		}

		[Test]
		public void Indexed_alias_uses_the_selected_argument_storage()
		{
			Add(Method(nameof(ValueTarget)), nameof(WriteIndexed), HarmonyPatchType.Prefix);
			Assert.AreEqual(217, ValueTarget(2, 1));
		}

		[Test]
		public void Each_box_is_copied_back_to_its_own_argument()
		{
			Add(Method(nameof(ValueTarget)), nameof(WriteBoth), HarmonyPatchType.Prefix);
			Assert.AreEqual(317, ValueTarget(2, 1));
		}

		[Test]
		public void Closed_generic_value_argument_is_copied_back()
		{
			Add(Method(nameof(GenericTarget)).MakeGenericMethod(typeof(int)), nameof(WriteBox), HarmonyPatchType.Prefix);
			Assert.AreEqual(17, GenericTarget(1));
		}

		[Test]
		public void Struct_argument_is_unboxed_into_its_value_slot()
		{
			Add(Method(nameof(StructTarget)), nameof(WriteStruct), HarmonyPatchType.Prefix);
			Assert.AreEqual(17, StructTarget(new Sample { value = 1 }).value);
		}

		[TestCase(false)]
		[TestCase(true)]
		public void Nullable_argument_accepts_an_underlying_box_or_null(bool useNull)
		{
			nullBox = useNull;
			Add(Method(nameof(NullableTarget)), nameof(WriteNullable), HarmonyPatchType.Prefix);
			Assert.AreEqual(useNull ? (int?)null : 17, NullableTarget(1));
		}

		[Test]
		public void Initially_null_nullable_argument_can_keep_its_untouched_box()
		{
			Add(Method(nameof(NullableTarget)), nameof(KeepNull), HarmonyPatchType.Prefix);
			Assert.IsNull(NullableTarget(null));
		}

		[Test]
		public void Interface_boxed_binding_copies_back_to_the_argument()
		{
			Add(Method(nameof(ValueTarget)), nameof(WriteComparable), HarmonyPatchType.Prefix);
			Assert.AreEqual(217, ValueTarget(2, 1));
		}

		[Test]
		public void Enum_argument_keeps_its_exact_box_type()
		{
			Add(Method(nameof(EnumTarget)), nameof(WriteEnum), HarmonyPatchType.Prefix);
			Assert.AreEqual(Choice.Other, EnumTarget(Choice.One));
		}

		static void WriteBox(ref object value) => value = 17;
		static void TypedBeforeBox(ref int value, [HarmonyArgument("value")] ref object snapshot) => value = 17;
		static void BoxBeforeTyped([HarmonyArgument("value")] ref object snapshot, ref int value) => value = 17;
		static void FirstBoxBeforeSecond([HarmonyArgument("value")] ref object first, [HarmonyArgument("value")] ref object second)
		{
			first = 17;
			if (replaceBox) second = 23;
		}
		static void SecondBoxBeforeFirst([HarmonyArgument("value")] ref object second, [HarmonyArgument("value")] ref object first)
			=> FirstBoxBeforeSecond(ref first, ref second);
		static void WriteBoth(ref object before, ref object value) { before = 3; value = 17; }
		static void WriteIndexed([HarmonyArgument(1)] ref object renamed) => renamed = 17;
		static void Observe(object[] __args) => observed = __args[1];
		static void ArrayThenBox(object[] __args, ref object value)
		{
			__args[1] = 99;
			if (replaceBox) value = 17;
		}
		static void BoxThenArray(ref object value, object[] __args) => ArrayThenBox(__args, ref value);
		static void InvalidBox(object[] __args, ref object value) { __args[1] = 99; value = nullBox ? null : "incompatible"; }
		static void ThrowTyped(ref int value) { value = 17; throw new InvalidOperationException("patch failure"); }
		static void ThrowBox(ref object value) { value = 17; throw new InvalidOperationException("patch failure"); }
		static void ThrowArray(object[] __args) { __args[1] = 17; throw new InvalidOperationException("patch failure"); }
		static int PassThrough(int result, ref object value) { value = 17; return result + 1; }
		static void WriteStruct(ref object value) => value = new Sample { value = 17 };
		static void WriteNullable(ref object value) => value = nullBox ? null : (object)17;
		static void KeepNull(ref object value) => Assert.IsNull(value);
		static void WriteComparable(ref IComparable value) => value = 17;
		static void WriteEnum(ref object value) => value = Choice.Other;

		public struct Sample { public int value; }
		public enum Choice { One, Other }
		public class InstanceTarget
		{
			[MethodImpl(MethodImplOptions.NoInlining)]
			public int Run(int before, int value) => before * 100 + value;
		}
		[MethodImpl(MethodImplOptions.NoInlining)]
		static int ValueTarget(int before, int value) => before * 100 + value;
		[MethodImpl(MethodImplOptions.NoInlining)]
		static int RefTarget(int before, ref int value) => before * 100 + value;
		[MethodImpl(MethodImplOptions.NoInlining)]
		static Sample StructTarget(Sample value) => value;
		[MethodImpl(MethodImplOptions.NoInlining)]
		static int? NullableTarget(int? value) => value;
		[MethodImpl(MethodImplOptions.NoInlining)]
		static Choice EnumTarget(Choice value) => value;
		[MethodImpl(MethodImplOptions.NoInlining)]
		static T GenericTarget<T>(T value) => value;
	}
}
