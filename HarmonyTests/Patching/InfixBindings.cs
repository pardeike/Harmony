using HarmonyLib;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using static HarmonyLib.Code;

namespace HarmonyLibTests.Patching
{
	[TestFixture, NonParallelizable]
	public class InfixBindings : TestLogger
	{
		static readonly List<object[]> arrays = [];
		static readonly List<int> values = [];
		Harmony harmony;

		[SetUp]
		public void SetUp()
		{
			harmony = new Harmony("test.infix.bindings." + Guid.NewGuid());
			arrays.Clear();
			values.Clear();
		}

		[TearDown]
		public void TearDown() => harmony.UnpatchAll(harmony.Id);

		static MethodInfo Method(string name) => AccessTools.Method(typeof(InfixBindings), name);
		void Apply(string outer, string inner, string patch, bool postfix = false, int priority = Priority.Normal)
			=> Apply(Method(outer), Method(inner), patch, postfix, priority);

		void Apply(MethodInfo outer, MethodInfo inner, string patch, bool postfix = false, int priority = Priority.Normal)
		{
			var processor = harmony.CreateProcessor(outer);
			var fix = new HarmonyMethod(Method(patch)) { innerMethod = new InnerMethod(inner), priority = priority };
			_ = (postfix ? processor.AddInnerPostfix(fix) : processor.AddInnerPrefix(fix)).Patch();
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		static int ValueCall(int value) => value * 10;
		[MethodImpl(MethodImplOptions.NoInlining)]
		static int ValueOuter(int value) => ValueCall(value) + value;
		[MethodImpl(MethodImplOptions.NoInlining)]
		static int PairCall(int first, int second) => first * 10 + second;
		[MethodImpl(MethodImplOptions.NoInlining)]
		static int PairOuter(int first, int second) => PairCall(first, second);
		[MethodImpl(MethodImplOptions.NoInlining)]
		static int RefCall(ref int value) { value += 10; return value; }
		[MethodImpl(MethodImplOptions.NoInlining)]
		static int RefOuter(ref int value) => RefCall(ref value);
		[MethodImpl(MethodImplOptions.NoInlining)]
		static int OutCall(out int value) { value = 40; return value; }
		[MethodImpl(MethodImplOptions.NoInlining)]
		static int OutOuter(out int value) => OutCall(out value);
		[MethodImpl(MethodImplOptions.NoInlining)]
		static int ExactCall(int __result) => __result * 2;
		[MethodImpl(MethodImplOptions.NoInlining)]
		static int ExactOuter(int __state) => ExactCall(__state + 1) + __state * 100;
		[MethodImpl(MethodImplOptions.NoInlining)]
		static int LoopOuter(int value)
		{
			var result = 0;
			for (var i = 0; i < 2; i++) result += ValueCall(value + i);
			return result;
		}

		static void ExactPatch([HarmonyArgument("__result", ArgumentMode.Original)] ref int inner,
			[HarmonyOuter, HarmonyArgument("__state", ArgumentMode.Original)] ref int outer)
		{ inner += 10; outer += 100; }
		static void BoxValue(ref object value) => value = (int)value + 10;
		static void ReadArray(object[] __args) { arrays.Add(__args); values.Add((int)__args[0]); }
		static void IncrementArray(object[] __args) { ReadArray(__args); __args[0] = (int)__args[0] + 1; }
		static void WriteValue(ref int value) => value += 10;
		static bool Skip(ref int __result) { __result = 7; return false; }
		static void NeverArray(object[] __args) => Assert.Fail("This guarded prefix must be skipped");
		static void BothArrays(object[] __args, [HarmonyOuter, HarmonyArgument("__args")] object[] outer)
		{
			__args[0] = (int)__args[0] + 1;
			outer[0] = (int)outer[0] + 100;
		}
		static void BothArraysReversed([HarmonyOuter, HarmonyArgument("__args")] object[] outer, object[] __args) => BothArrays(__args, outer);
		static void ArrayAndTyped(object[] __args, ref int value) { }
		static void ArrayAndTypedReversed(ref int value, object[] __args) { }
		static void ArrayAndObservation(object[] __args, int value) { values.Add(value); __args[0] = value + 1; }
		static void BoxAndTyped(ref object first, [HarmonyArgument("first")] ref int again) { }
		static void DistinctBoxAndTyped(ref object first, ref int second) { first = 3; second = 4; }
		static void DirectAlias(ref int value, [HarmonyArgument("value")] ref int again) { value = 3; again += 4; }
		static void OuterArray([HarmonyOuter, HarmonyArgument("__args")] object[] outer) => ReadArray(outer);
		static void OuterResult([HarmonyOuter] ref int __result) { }
		static void OuterRun([HarmonyOuter] bool __runOriginal) { }
		static void RefRun(ref bool __runOriginal) { }
		static void WrongRun(int __runOriginal) { }
		static void InnerException(Exception __exception) { }
		static void InnerLocal(ref int __var_counter) { }
		static void RefArray(ref object[] __args) { }
		static void MissingExact([HarmonyOuter, HarmonyArgument("missing", ArgumentMode.Original)] int argument) { }
		static void StaticRefReceiver(ref object __instance) { }
		static void StaticValueReceiver(int __instance) { }
		static void StaticNullReceiver(object __instance) => Assert.IsNull(__instance);
		[MethodImpl(MethodImplOptions.NoInlining)]
		static int RefNoArgumentsOuter() { var value = 2; return RefCall(ref value); }
		static void BothArraysWithEmptyOuter(object[] __args, [HarmonyOuter, HarmonyArgument("__args")] object[] outer)
		{ Assert.IsEmpty(outer); __args[0] = 5; }
		[MethodImpl(MethodImplOptions.NoInlining)]
		static int ExactSpecialCall(int __args, int __0, int ___field, int __var_0, int __var_name, int __originalMember)
			=> __args + __0 + ___field + __var_0 + __var_name + __originalMember;
		[MethodImpl(MethodImplOptions.NoInlining)]
		static int ExactSpecialOuter(int __args, int __0, int ___field, int __var_0, int __var_name, int __originalMember)
			=> ExactSpecialCall(__args + 10, __0 + 10, ___field + 10, __var_0 + 10, __var_name + 10, __originalMember + 10);
		static void ExactSpecialNames(
			[HarmonyArgument("__args", ArgumentMode.Original)] int a,
			[HarmonyArgument("__0", ArgumentMode.Original)] int b,
			[HarmonyArgument("___field", ArgumentMode.Original)] int c,
			[HarmonyArgument("__var_0", ArgumentMode.Original)] int d,
			[HarmonyArgument("__var_name", ArgumentMode.Original)] int e,
			[HarmonyArgument("__originalMember", ArgumentMode.Original)] int f,
			[HarmonyOuter, HarmonyArgument("__args", ArgumentMode.Original)] int oa,
			[HarmonyOuter, HarmonyArgument("__0", ArgumentMode.Original)] int ob,
			[HarmonyOuter, HarmonyArgument("___field", ArgumentMode.Original)] int oc,
			[HarmonyOuter, HarmonyArgument("__var_0", ArgumentMode.Original)] int od,
			[HarmonyOuter, HarmonyArgument("__var_name", ArgumentMode.Original)] int oe,
			[HarmonyOuter, HarmonyArgument("__originalMember", ArgumentMode.Original)] int of)
		{
			Assert.AreEqual(new[] { 11, 12, 13, 14, 15, 16 }, new[] { a, b, c, d, e, f });
			Assert.AreEqual(new[] { 1, 2, 3, 4, 5, 6 }, new[] { oa, ob, oc, od, oe, of });
		}

		[Test]
		public void Every_special_name_category_can_be_a_real_argument_in_both_scopes()
		{
			Apply(nameof(ExactSpecialOuter), nameof(ExactSpecialCall), nameof(ExactSpecialNames));
			Assert.AreEqual(81, ExactSpecialOuter(1, 2, 3, 4, 5, 6));
		}

		[Test]
		public void Dual_arrays_are_safe_with_inner_refs_when_outer_has_no_arguments()
		{
			Apply(nameof(RefNoArgumentsOuter), nameof(RefCall), nameof(BothArraysWithEmptyOuter));
			Assert.AreEqual(15, RefNoArgumentsOuter());
		}

		[Test]
		public void Postfix_can_be_the_first_array_receiver_after_prefix_skip()
		{
			Apply(nameof(ValueOuter), nameof(ValueCall), nameof(Skip), priority: Priority.First);
			Apply(nameof(ValueOuter), nameof(ValueCall), nameof(NeverArray));
			Apply(nameof(ValueOuter), nameof(ValueCall), nameof(ReadArray), true);
			Assert.AreEqual(9, ValueOuter(2));
			Assert.AreEqual(new[] { 2 }, values);
			Assert.AreEqual(1, arrays.Count);
		}
		static string WrongPassthrough(string prior) => prior;
		static int IntPassthrough(int prior) => prior + 10;
		[MethodImpl(MethodImplOptions.NoInlining)]
		static T GenericResult<T>(T value) => value;
		[MethodImpl(MethodImplOptions.NoInlining)]
		static int GenericResultOuter() => GenericResult(3) + GenericResult("ab").Length;
		static int PassthroughNamedState(int __state) => __state + 1;
		static int PassthroughNamedOuterState([HarmonyOuter] int __state) => __state + 1;
		static void StringState(ref string __state) { Assert.IsNull(__state); __state = "written"; }
		static void OuterStringState(out string __state) => __state = "outer";
		static object[] PassthroughNamedOuterArray([HarmonyOuter] object[] __args) => __args;
		[MethodImpl(MethodImplOptions.NoInlining)]
		static bool BoolCall() => true;
		[MethodImpl(MethodImplOptions.NoInlining)]
		static bool BoolOuter() => BoolCall();
		static bool DualRoleState(bool __state) { values.Add(__state ? 1 : 0); return true; }

		[Test]
		public void First_passthrough_parameter_does_not_declare_inner_state()
		{
			Apply(nameof(ValueOuter), nameof(ValueCall), nameof(StringState), true);
			Apply(nameof(ValueOuter), nameof(ValueCall), nameof(PassthroughNamedState), true);
			Assert.AreEqual(12, ValueOuter(1));
		}

		[Test]
		public void First_passthrough_parameter_ignores_outer_scope_annotations()
		{
			harmony.Patch(Method(nameof(ValueOuter)), prefix: new HarmonyMethod(Method(nameof(OuterStringState))));
			Apply(nameof(ValueOuter), nameof(ValueCall), nameof(PassthroughNamedOuterState), true);
			Assert.AreEqual(12, ValueOuter(1));
			var patch = new Patch(new HarmonyMethod(Method(nameof(PassthroughNamedOuterArray)))
			{ innerMethod = new InnerMethod(Method(nameof(GenericResult)).MakeGenericMethod(typeof(object[]))) }, 0, harmony.Id);
			var config = new MethodCreatorConfig(Method(nameof(ValueOuter)), null, [], [], [], [], [], [new Infix(patch)], false);
			_ = new MethodCreator(config);
			Assert.IsFalse(config.AnyInfixHasOuter(InjectionType.ArgsArray));
		}

		[Test]
		public void Reusing_a_method_as_prefix_and_passthrough_retains_real_prefix_state_demand()
		{
			Apply(nameof(BoolOuter), nameof(BoolCall), nameof(DualRoleState));
			Apply(nameof(BoolOuter), nameof(BoolCall), nameof(DualRoleState), true);
			Assert.IsTrue(BoolOuter());
			Assert.AreEqual(new[] { 0, 1 }, values);
		}

		[Test]
		public void Incompatible_passthrough_fails_without_changing_installed_state()
		{
			Apply(nameof(ValueOuter), nameof(ValueCall), nameof(IncrementArray));
			var before = HarmonySharedState.GetPatchInfo(Method(nameof(ValueOuter))).Serialize();
			var error = Assert.Throws<HarmonyException>(() => Apply(nameof(ValueOuter), nameof(ValueCall), nameof(WrongPassthrough), true));
			StringAssert.Contains("passthrough", error.ToString());
			StringAssert.Contains(nameof(WrongPassthrough), error.ToString());
			Assert.AreEqual(21, ValueOuter(1));
			Assert.AreEqual(before, HarmonySharedState.GetPatchInfo(Method(nameof(ValueOuter))).Serialize());
		}

		[Test]
		public void Later_incompatible_family_result_leaves_the_previous_detour_and_bytes_unchanged()
		{
			var outer = Method(nameof(GenericResultOuter));
			var family = Method(nameof(GenericResult));
			Apply(outer, family.MakeGenericMethod(typeof(int)), nameof(IntPassthrough), true);
			var before = HarmonySharedState.GetPatchInfo(outer).Serialize();
			Assert.AreEqual(15, GenericResultOuter());
			Assert.Throws<HarmonyException>(() => Apply(outer, family, nameof(IntPassthrough), true));
			Assert.AreEqual(15, GenericResultOuter());
			Assert.AreEqual(before, HarmonySharedState.GetPatchInfo(outer).Serialize());
		}

		[Test]
		public void Exact_names_bind_in_the_requested_scope()
		{
			Apply(nameof(ExactOuter), nameof(ExactCall), nameof(ExactPatch));
			Assert.AreEqual(10226, ExactOuter(2));
		}

		[Test]
		public void Boxed_captured_value_writes_back_without_changing_ordinary_boxing()
		{
			harmony.Patch(Method(nameof(ValueCall)), prefix: new HarmonyMethod(Method(nameof(BoxValue))));
			Assert.AreEqual(10, ValueCall(1), "Ordinary by-value boxed argument keeps its established behavior");
			harmony.UnpatchAll(harmony.Id);
			Apply(nameof(ValueOuter), nameof(ValueCall), nameof(BoxValue));
			Assert.AreEqual(111, ValueOuter(1), "Infix writes its captured slot, while the containing argument remains 1");
		}

		[Test]
		public void Array_refresh_survives_a_skipped_array_prefix()
		{
			Apply(nameof(ValueOuter), nameof(ValueCall), nameof(IncrementArray), priority: Priority.First);
			Apply(nameof(ValueOuter), nameof(ValueCall), nameof(WriteValue), priority: Priority.High);
			Apply(nameof(ValueOuter), nameof(ValueCall), nameof(Skip));
			Apply(nameof(ValueOuter), nameof(ValueCall), nameof(NeverArray), priority: Priority.Low);
			Apply(nameof(ValueOuter), nameof(ValueCall), nameof(ReadArray), true);
			Assert.AreEqual(8, ValueOuter(1));
			Assert.AreEqual(new[] { 1, 12 }, values);
			Assert.AreSame(arrays[0], arrays[1]);
		}

		[Test]
		public void Arrays_refresh_after_ref_calls()
		{
			Apply(nameof(RefOuter), nameof(RefCall), nameof(IncrementArray));
			Apply(nameof(RefOuter), nameof(RefCall), nameof(ReadArray), true);
			var value = 1;
			Assert.AreEqual(12, RefOuter(ref value));
			Assert.AreEqual(12, value);
			Assert.AreEqual(new[] { 1, 12 }, values);
			Assert.AreSame(arrays[0], arrays[1]);
		}

		[TestCase(false)]
		[TestCase(true)]
		public void Out_storage_initializes_once_before_prefixes(bool skip)
		{
			Apply(nameof(OutOuter), nameof(OutCall), nameof(IncrementArray), priority: Priority.First);
			if (skip) Apply(nameof(OutOuter), nameof(OutCall), nameof(Skip));
			Apply(nameof(OutOuter), nameof(OutCall), nameof(ReadArray), true);
			Assert.AreEqual(skip ? 7 : 40, OutOuter(out var value));
			Assert.AreEqual(skip ? 1 : 40, value);
			Assert.AreEqual(new[] { 0, skip ? 1 : 40 }, values);
		}

		[Test]
		public void Array_lifetime_is_one_site_execution()
		{
			Apply(nameof(LoopOuter), nameof(ValueCall), nameof(IncrementArray));
			Apply(nameof(LoopOuter), nameof(ValueCall), nameof(ReadArray), true);
			Assert.AreEqual(50, LoopOuter(1));
			Assert.AreSame(arrays[0], arrays[1]);
			Assert.AreSame(arrays[2], arrays[3]);
			Assert.AreNotSame(arrays[0], arrays[2]);
		}

		[TestCase(nameof(BothArrays))]
		[TestCase(nameof(BothArraysReversed))]
		public void Disjoint_dual_arrays_do_not_depend_on_parameter_order(string patch)
		{
			Apply(nameof(ValueOuter), nameof(ValueCall), patch);
			Assert.AreEqual(121, ValueOuter(1));
		}

		[TestCase(nameof(BothArrays))]
		[TestCase(nameof(BothArraysReversed))]
		public void Potentially_overlapping_dual_arrays_fail_before_installation(string patch)
		{
			var before = HarmonySharedState.GetPatchInfo(Method(nameof(RefOuter)))?.Serialize();
			Assert.Throws<HarmonyException>(() => Apply(nameof(RefOuter), nameof(RefCall), patch));
			var value = 1;
			Assert.AreEqual(11, RefOuter(ref value));
			Assert.IsFalse(Harmony.GetPatchInfo(Method(nameof(RefOuter)))?.Owners.Contains(harmony.Id) ?? false);
			Assert.AreEqual(before, HarmonySharedState.GetPatchInfo(Method(nameof(RefOuter)))?.Serialize(), "Records and version must be unchanged after failure");
		}

		[TestCase(nameof(ArrayAndTyped))]
		[TestCase(nameof(ArrayAndTypedReversed))]
		public void Array_and_overlapping_typed_writer_are_rejected(string patch)
			=> Assert.Throws<HarmonyException>(() => Apply(nameof(ValueOuter), nameof(ValueCall), patch));

		[Test]
		public void Array_and_by_value_observation_are_allowed()
		{
			Apply(nameof(ValueOuter), nameof(ValueCall), nameof(ArrayAndObservation));
			Assert.AreEqual(21, ValueOuter(1));
			Assert.AreEqual(new[] { 1 }, values);
		}

		[Test]
		public void Boxed_copyback_rejects_competing_writer_but_allows_distinct_slots()
		{
			Assert.Throws<HarmonyException>(() => Apply(nameof(PairOuter), nameof(PairCall), nameof(BoxAndTyped)));
			Apply(nameof(PairOuter), nameof(PairCall), nameof(DistinctBoxAndTyped));
			Assert.AreEqual(34, PairOuter(1, 2));
		}

		[Test]
		public void Direct_refs_can_share_actual_storage()
		{
			Apply(nameof(ValueOuter), nameof(ValueCall), nameof(DirectAlias));
			Assert.AreEqual(71, ValueOuter(1));
		}

		[TestCase(nameof(OuterResult))]
		[TestCase(nameof(OuterRun))]
		[TestCase(nameof(RefRun))]
		[TestCase(nameof(WrongRun))]
		[TestCase(nameof(InnerException))]
		[TestCase(nameof(InnerLocal))]
		[TestCase(nameof(RefArray))]
		[TestCase(nameof(MissingExact))]
		[TestCase(nameof(StaticRefReceiver))]
		[TestCase(nameof(StaticValueReceiver))]
		public void Invalid_scopes_or_storage_fail_before_installation(string patch)
			=> Assert.Throws<HarmonyException>(() => Apply(nameof(ValueOuter), nameof(ValueCall), patch));

		[Test]
		public void Static_receiver_observation_gets_null()
		{
			Apply(nameof(ValueOuter), nameof(ValueCall), nameof(StaticNullReceiver));
			Assert.AreEqual(11, ValueOuter(1));
		}

		[Test]
		public void Outer_attribute_is_invalid_on_an_ordinary_patch()
			=> Assert.Throws<ArgumentException>(() => harmony.Patch(Method(nameof(ValueOuter)), prefix: new HarmonyMethod(Method(nameof(OuterArray)))));

		[TestCase(false, false)]
		[TestCase(true, true)]
		public void Refresh_plan_only_refills_when_an_intervening_writer_exists(bool write, bool refresh)
		{
			var prefixes = new List<MethodInfo> { Method(nameof(ReadArray)) };
			if (write) prefixes.Add(Method(nameof(WriteValue)));
			var postfixes = new List<MethodInfo> { Method(nameof(ReadArray)) };
			var config = new MethodCreatorConfig(Method(nameof(ValueOuter)), null, prefixes, postfixes, [], [], [], [], false);
			var creator = new MethodCreator(config);
			var inner = TestTools.CreateBindingContext(Method(nameof(ValueCall)), typeof(InfixBindings), null,
				[new InjectionStorage(config.DeclareLocal(typeof(int)))], new VariableState());
			var outer = new PatchBindingContext(Method(nameof(ValueOuter)), new VariableState());
			_ = creator.SetupInfixBindings(inner, outer, prefixes, postfixes, []);
			Assert.AreEqual(refresh, inner.refreshArgumentArray);
			Assert.IsFalse(outer.variables.TryGetValue(InjectionType.ArgsArray, out _));
		}

		public interface IReadValue { int Read(); }
		public class Receiver : IReadValue
		{
			public int value;
			[MethodImpl(MethodImplOptions.NoInlining)]
			public int Read() => value;
		}
		[MethodImpl(MethodImplOptions.NoInlining)]
		static int ReceiverOuter(Receiver receiver) => receiver.Read();
		static IEnumerable<CodeInstruction> ConstrainedReceiver(IEnumerable<CodeInstruction> _)
			=> [Ldarga[0], Constrained[typeof(Receiver)], Callvirt[AccessTools.Method(typeof(IReadValue), nameof(IReadValue.Read))], Ret];
		static void ReceiverObservation(Receiver __instance, int ___value) { values.Add(__instance.value); values.Add(___value); }
		static void ReceiverReplacement(ref Receiver __instance) => __instance = new Receiver { value = 30 };
		[HarmonyDelegate(typeof(Receiver), nameof(Receiver.Read))]
		delegate int ReceiverReader();
		static void ReceiverDelegate(ReceiverReader read) => values.Add(read());

		[Test]
		public void Constrained_reference_receiver_loads_the_value_and_preserves_its_pointer()
		{
			harmony.Patch(Method(nameof(ReceiverOuter)), transpiler: new HarmonyMethod(Method(nameof(ConstrainedReceiver))));
			var target = AccessTools.Method(typeof(IReadValue), nameof(IReadValue.Read));
			Apply(Method(nameof(ReceiverOuter)), target, nameof(ReceiverObservation), priority: Priority.High);
			Apply(Method(nameof(ReceiverOuter)), target, nameof(ReceiverDelegate), priority: Priority.High);
			Apply(Method(nameof(ReceiverOuter)), target, nameof(ReceiverReplacement));
			Assert.AreEqual(30, ReceiverOuter(new Receiver { value = 7 }));
			Assert.AreEqual(new[] { 7, 7, 7 }, values);
		}

		public struct Counter
		{
			public int value;
			[MethodImpl(MethodImplOptions.NoInlining)]
			public int Add(int amount) { value += amount; return value; }
		}
		[MethodImpl(MethodImplOptions.NoInlining)]
		static int CounterOuter(Counter counter, int amount)
		{
			var result = counter.Add(amount);
			return result + counter.value * 100;
		}
		static void CounterArray([HarmonyOuter, HarmonyArgument("__args")] object[] outer)
		{
			arrays.Add(outer);
			values.Add(((Counter)outer[0]).value);
		}
		static void CounterDualArrays(object[] __args, [HarmonyOuter, HarmonyArgument("__args")] object[] outer)
		{
			__args[0] = 2;
			outer[0] = new Counter { value = 100 };
		}

		[Test]
		public void Struct_receiver_changes_refresh_the_outer_array()
		{
			var target = AccessTools.Method(typeof(Counter), nameof(Counter.Add));
			Apply(Method(nameof(CounterOuter)), target, nameof(CounterArray));
			Apply(Method(nameof(CounterOuter)), target, nameof(CounterArray), true);
			Assert.AreEqual(1010, CounterOuter(new Counter { value = 7 }, 3));
			Assert.AreEqual(new[] { 7, 10 }, values);
			Assert.AreSame(arrays[0], arrays[1]);
		}

		[Test]
		public void Managed_pointer_receiver_does_not_disallow_disjoint_dual_arrays()
		{
			Apply(Method(nameof(CounterOuter)), AccessTools.Method(typeof(Counter), nameof(Counter.Add)), nameof(CounterDualArrays));
			Assert.AreEqual(10302, CounterOuter(new Counter { value = 7 }, 3));
		}

		static void NamedCounter([HarmonyOuter] ref int __var_counter) => values.Add(__var_counter++);
		[Test]
		public void Named_outer_local_survives_sites_but_resets_per_invocation()
		{
			Apply(nameof(LoopOuter), nameof(ValueCall), nameof(NamedCounter));
			Assert.AreEqual(30, LoopOuter(1));
			Assert.AreEqual(30, LoopOuter(1));
			Assert.AreEqual(new[] { 0, 1, 0, 1 }, values);
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		static IntPtr NativeIntegerCall(ref IntPtr value, UIntPtr unsigned)
		{
			value = new IntPtr(value.ToInt64() + (long)unsigned.ToUInt64());
			return value;
		}
		[MethodImpl(MethodImplOptions.NoInlining)]
		static IntPtr NativeIntegerOuter(IntPtr value, UIntPtr unsigned) => NativeIntegerCall(ref value, unsigned);
		static void NativeIntegerPrefix(ref IntPtr value, UIntPtr unsigned)
		{
			value = new IntPtr(value.ToInt64() + 1);
			values.Add((int)unsigned.ToUInt32());
		}
		static IntPtr NativeIntegerPostfix(IntPtr result) => new(result.ToInt64() + 10);

		[Test]
		public void Native_integer_arguments_refs_and_results_are_not_function_pointers()
		{
			Apply(nameof(NativeIntegerOuter), nameof(NativeIntegerCall), nameof(NativeIntegerPrefix));
			Apply(nameof(NativeIntegerOuter), nameof(NativeIntegerCall), nameof(NativeIntegerPostfix), true);
			Assert.AreEqual(new IntPtr(18), NativeIntegerOuter(new IntPtr(4), new UIntPtr(3)));
			Assert.AreEqual(new[] { 3 }, values);
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		static unsafe int PointerCall(int* value) => *value;
		[MethodImpl(MethodImplOptions.NoInlining)]
		static unsafe int PointerOuter(int* value) => PointerCall(value);
		static unsafe void PointerObservation(int* value) => values.Add(*value);

		[Test]
		public unsafe void Typed_pointer_is_supported_but_an_argument_array_is_not()
		{
			Assert.Throws<HarmonyException>(() => Apply(nameof(PointerOuter), nameof(PointerCall), nameof(ReadArray)));
			Apply(nameof(PointerOuter), nameof(PointerCall), nameof(PointerObservation));
			var value = 7;
			Assert.AreEqual(7, PointerOuter(&value));
			Assert.AreEqual(new[] { 7 }, values);
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		static unsafe int RefPointerCall(ref int* value) => *value;
		[MethodImpl(MethodImplOptions.NoInlining)]
		static unsafe int RefPointerOuter(int* value) => RefPointerCall(ref value);

		[Test]
		public unsafe void Managed_pointer_to_native_pointer_loads_the_native_value()
		{
			Apply(nameof(RefPointerOuter), nameof(RefPointerCall), nameof(PointerObservation));
			var value = 7;
			Assert.AreEqual(7, RefPointerOuter(&value));
			Assert.AreEqual(new[] { 7 }, values);
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		static unsafe int* PointerResultCall(int* value, int iteration) => value;
		[MethodImpl(MethodImplOptions.NoInlining)]
		static unsafe int PointerResultOuter(int* value)
		{
			var result = 0;
			for (var i = 0; i < 2; i++) result = result * 10 + (PointerResultCall(value, i) == null ? 0 : 1);
			return result;
		}
		static unsafe bool PointerState(int* value, int iteration, ref int* __state)
		{
			values.Add(__state == null ? 0 : 1);
			__state = value;
			return iteration == 0;
		}
		static unsafe void PointerResult(int* value, int iteration, int* __state, int* __result)
		{
			Assert.IsTrue(__state == value);
			Assert.IsTrue(__result == (iteration == 0 ? value : null));
		}

		[Test]
		public unsafe void Pointer_result_and_state_reset_before_a_skipped_loop_iteration()
		{
			Apply(nameof(PointerResultOuter), nameof(PointerResultCall), nameof(PointerState));
			Apply(nameof(PointerResultOuter), nameof(PointerResultCall), nameof(PointerResult), true);
			var value = 7;
			Assert.AreEqual(10, PointerResultOuter(&value));
			Assert.AreEqual(new[] { 0, 0 }, values);
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		static unsafe ref int* RefPointerResultCall(ref int* value) => ref value;
		[MethodImpl(MethodImplOptions.NoInlining)]
		static unsafe int RefPointerResultOuter(int* value) => *RefPointerResultCall(ref value);
		static unsafe void RefPointerResult(ref int* __result) => __result++;

		[Test]
		public unsafe void Ref_pointer_return_supports_a_typed_postfix_without_a_default_reference()
		{
			Apply(nameof(RefPointerResultOuter), nameof(RefPointerResultCall), nameof(RefPointerResult), true);
			int* pair = stackalloc int[] { 7, 11 };
			Assert.AreEqual(11, RefPointerResultOuter(pair));
		}

		public class Container
		{
			public int seed;
			[MethodImpl(MethodImplOptions.NoInlining)]
			public int Run(int value) => ValueCall(value) + value + seed;
		}
		static void OuterArrayAndField([HarmonyOuter, HarmonyArgument("__args")] object[] outer, [HarmonyOuter] ref int ___seed)
		{
			outer[0] = 5;
			___seed = 10;
		}

		[Test]
		public void Outer_field_and_by_value_argument_slots_are_provably_distinct()
		{
			Apply(AccessTools.Method(typeof(Container), nameof(Container.Run)), Method(nameof(ValueCall)), nameof(OuterArrayAndField));
			Assert.AreEqual(25, new Container().Run(1));
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		static int AliasedOutCall(out int first, ref int second) { first = 10; second += 1; return first; }
		[MethodImpl(MethodImplOptions.NoInlining)]
		static int AliasedOutOuter()
		{
			var value = 7;
			var result = AliasedOutCall(out value, ref value);
			return result * 100 + value;
		}
		static void ObservePairArray(object[] __args) { values.Add((int)__args[0]); values.Add((int)__args[1]); }
		static void WritePairArray(object[] __args) { ObservePairArray(__args); __args[0] = 2; __args[1] = 3; }

		[TestCase(false)]
		[TestCase(true)]
		public void Aliased_outputs_initialize_once_and_restore_in_argument_order(bool skip)
		{
			Apply(nameof(AliasedOutOuter), nameof(AliasedOutCall), nameof(WritePairArray), priority: Priority.First);
			if (skip) Apply(nameof(AliasedOutOuter), nameof(AliasedOutCall), nameof(Skip));
			Apply(nameof(AliasedOutOuter), nameof(AliasedOutCall), nameof(ObservePairArray), true);
			Assert.AreEqual(skip ? 703 : 1111, AliasedOutOuter());
			Assert.AreEqual(new[] { 0, 0, skip ? 3 : 11, skip ? 3 : 11 }, values);
		}

		[Test]
		public void Unrequested_arrays_have_no_storage_or_allocation_instructions()
		{
			var patch = Method(nameof(WriteValue));
			var config = new MethodCreatorConfig(Method(nameof(ValueOuter)), null, [patch], [], [], [], [], [], false);
			var creator = new MethodCreator(config);
			var inner = TestTools.CreateBindingContext(Method(nameof(ValueCall)), typeof(InfixBindings), null,
				[new InjectionStorage(config.DeclareLocal(typeof(int)))], new VariableState());
			var outer = new PatchBindingContext(Method(nameof(ValueOuter)), new VariableState());
			Assert.IsEmpty(creator.SetupInfixBindings(inner, outer, [patch], [], []));
			Assert.IsFalse(inner.variables.TryGetValue(InjectionType.ArgsArray, out _));
			Assert.IsFalse(outer.variables.TryGetValue(InjectionType.ArgsArray, out _));
			Assert.IsFalse(creator.EmitPatchCall(patch, inner, false, outer).Any(code => code.opcode == OpCodes.Newarr));
		}

#if NET5_0_OR_GREATER || NETFRAMEWORK
		[MethodImpl(MethodImplOptions.NoInlining)]
		static unsafe void ManagedFunctionPointer(delegate*<int> __state) { }
		[MethodImpl(MethodImplOptions.NoInlining)]
		static unsafe void ManagedFunctionPointerReference(ref delegate*<int> __state) { }
		[MethodImpl(MethodImplOptions.NoInlining)]
		static unsafe void UnmanagedFunctionPointer(delegate* unmanaged[Cdecl]<int> __state) { }
		[MethodImpl(MethodImplOptions.NoInlining)]
		static unsafe void UnmanagedFunctionPointerReference(ref delegate* unmanaged[Cdecl]<int> __state) { }
		[MethodImpl(MethodImplOptions.NoInlining)]
		static unsafe delegate*<int> ManagedFunctionPointerReturn() => null;
		[MethodImpl(MethodImplOptions.NoInlining)]
		static unsafe delegate* unmanaged[Cdecl]<int> UnmanagedFunctionPointerReturn() => null;
		static void FunctionPointerNoop() { }
		static MethodInfo functionPointerTarget;
		static IEnumerable<CodeInstruction> FunctionPointerBody(IEnumerable<CodeInstruction> _, ILGenerator generator)
		{
			foreach (var parameter in functionPointerTarget.GetParameters())
				if (parameter.ParameterType.IsByRef)
					// Supply native-sized storage without importing the function-pointer type before Infix can reject the call.
					yield return Ldloca[generator.DeclareLocal(typeof(IntPtr))];
				else
				{
					yield return Ldc_I4_0;
					yield return Conv_U;
				}
			yield return Call[functionPointerTarget];
			if (functionPointerTarget.ReturnType != typeof(void)) yield return Pop;
			yield return Ldc_I4_7;
			yield return Ret;
		}

		[TestCase(nameof(ManagedFunctionPointer))]
		[TestCase(nameof(ManagedFunctionPointerReference))]
		[TestCase(nameof(UnmanagedFunctionPointer))]
		[TestCase(nameof(UnmanagedFunctionPointerReference))]
		[TestCase(nameof(ManagedFunctionPointerReturn))]
		[TestCase(nameof(UnmanagedFunctionPointerReturn))]
		public void Function_pointer_calls_are_rejected_without_changing_the_installed_patch(string target)
		{
			var outer = Method(nameof(ValueOuter));
			harmony.CreateProcessor(outer).AddPrefix(Method(nameof(WriteValue))).Patch();
			Assert.AreEqual(121, ValueOuter(1));
			var before = HarmonySharedState.GetPatchInfo(outer).Serialize();
			functionPointerTarget = Method(target);
			var error = Assert.Throws<HarmonyException>(() => harmony.CreateProcessor(outer)
				.AddTranspiler(Method(nameof(FunctionPointerBody)))
				.AddInnerPrefix(new HarmonyMethod(Method(nameof(FunctionPointerNoop))) { innerMethod = new InnerMethod(functionPointerTarget) }).Patch());
			Assert.IsInstanceOf<ArgumentException>(error.InnerException);
			StringAssert.Contains("function-pointer signatures", error.ToString());
			StringAssert.Contains("selected inner call", error.ToString());
			StringAssert.Contains(target, error.ToString());
			Assert.AreEqual(121, ValueOuter(1));
			Assert.AreEqual(before, HarmonySharedState.GetPatchInfo(outer).Serialize());
		}

		[TestCase(nameof(ManagedFunctionPointer))]
		[TestCase(nameof(ManagedFunctionPointerReference))]
		[TestCase(nameof(UnmanagedFunctionPointer))]
		[TestCase(nameof(UnmanagedFunctionPointerReference))]
		[TestCase(nameof(ManagedFunctionPointerReturn))]
		[TestCase(nameof(UnmanagedFunctionPointerReturn))]
		public void Function_pointer_patch_signatures_are_rejected_without_changing_the_installed_patch(string patch)
		{
			Apply(nameof(ValueOuter), nameof(ValueCall), nameof(IncrementArray));
			Assert.AreEqual(21, ValueOuter(1));
			var before = HarmonySharedState.GetPatchInfo(Method(nameof(ValueOuter))).Serialize();
			var error = Assert.Throws<HarmonyException>(() => Apply(nameof(ValueOuter), nameof(ValueCall), patch, Method(patch).ReturnType != typeof(void)));
			Assert.IsInstanceOf<ArgumentException>(error.InnerException);
			StringAssert.Contains("function-pointer signatures", error.ToString());
			StringAssert.Contains("patch", error.ToString());
			StringAssert.Contains(patch, error.ToString());
			Assert.AreEqual(21, ValueOuter(1));
			Assert.AreEqual(before, HarmonySharedState.GetPatchInfo(Method(nameof(ValueOuter))).Serialize());
		}

#endif

#if NET5_0_OR_GREATER
		[MethodImpl(MethodImplOptions.NoInlining)]
		static int SpanCall(Span<int> value) => value[0];
		[MethodImpl(MethodImplOptions.NoInlining)]
		static int SpanOuter(int[] value) => SpanCall(value);
		static void SpanWrite(Span<int> value) => value[0] += 3;
		static void BoxUnrepresentable(object value) { }

		[Test]
		public void Byref_like_arguments_allow_typed_use_and_reject_boxed_views()
		{
			Assert.Throws<HarmonyException>(() => Apply(nameof(SpanOuter), nameof(SpanCall), nameof(ReadArray)));
			Assert.Throws<HarmonyException>(() => Apply(nameof(SpanOuter), nameof(SpanCall), nameof(BoxUnrepresentable)));
			Apply(nameof(SpanOuter), nameof(SpanCall), nameof(SpanWrite));
			Assert.AreEqual(10, SpanOuter([7]));
		}
#endif
	}
}
