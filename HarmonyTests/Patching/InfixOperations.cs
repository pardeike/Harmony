using HarmonyLib;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

namespace HarmonyLibTests.Patching
{
	[TestFixture, NonParallelizable]
	public class InfixOperations : TestLogger
	{
		static readonly List<object> observed = [];
		static readonly List<object[]> arrays = [];
		static readonly List<string> trace = [];
		static bool skip;
		static int receiverEvaluations, valueEvaluations, repair;
		static Store replacement;
		static object[] literals;
		Harmony harmony;

		[SetUp]
		public void SetUp()
		{
			harmony = new Harmony("test.infix.operations." + Guid.NewGuid());
			observed.Clear(); arrays.Clear(); trace.Clear();
			skip = false; receiverEvaluations = valueEvaluations = repair = 0;
			replacement = new Store { Value = 9 };
			Store.StaticValue = Store.StaticVolatileValue = 40;
		}
		[TearDown]
		public void TearDown() => harmony.UnpatchAll(harmony.Id);
		static MethodInfo Method(string name) => AccessTools.DeclaredMethod(typeof(InfixOperations), name);
		static HarmonyMethod Fix(string name, InnerTarget target) => new(Method(name)) { innerTarget = target };
		void Apply(string outer, InnerTarget target, string before = null, string after = null)
		{
			var processor = harmony.CreateProcessor(Method(outer));
			if (before is not null) processor.AddInnerPrefix(Fix(before, target));
			if (after is not null) processor.AddInnerPostfix(Fix(after, target));
			processor.Patch();
		}

		public class Store
		{
			public int Value = 40;
			public volatile int VolatileValue = 40;
			public static int StaticValue;
			public static volatile int StaticVolatileValue;
			public int GetterCalls, SetterCalls;
			public int this[int index]
			{
				[MethodImpl(MethodImplOptions.NoInlining)]
				get { GetterCalls++; return Value + index; }
				[MethodImpl(MethodImplOptions.NoInlining)]
				set { SetterCalls++; Value = value - index; }
			}
		}
		[MethodImpl(MethodImplOptions.NoInlining)] static Store Receiver(Store value) { receiverEvaluations++; return value; }
		[MethodImpl(MethodImplOptions.NoInlining)] static int Value(int value) { valueEvaluations++; return value; }
		[MethodImpl(MethodImplOptions.NoInlining)] static int ReadInstance(Store store) => Receiver(store).Value;
		[MethodImpl(MethodImplOptions.NoInlining)] static int ReadVolatileInstance(Store store) => Receiver(store).VolatileValue;
		[MethodImpl(MethodImplOptions.NoInlining)] static int ReadStatic() => Store.StaticValue;
		[MethodImpl(MethodImplOptions.NoInlining)] static int ReadVolatileStatic() => Store.StaticVolatileValue;
		[MethodImpl(MethodImplOptions.NoInlining)] static void WriteInstance(Store store, int value) => Receiver(store).Value = Value(value);
		[MethodImpl(MethodImplOptions.NoInlining)] static void WriteVolatileInstance(Store store, int value) => Receiver(store).VolatileValue = Value(value);
		[MethodImpl(MethodImplOptions.NoInlining)] static void WriteStatic(int value) => Store.StaticValue = Value(value);
		[MethodImpl(MethodImplOptions.NoInlining)] static void WriteVolatileStatic(int value) => Store.StaticVolatileValue = Value(value);
		static bool BeforeRead(Store __instance, ref int __result)
		{
			trace.Add("before"); observed.Add(__instance);
			if (skip) __result = 7;
			return !skip;
		}
		static int AfterRead(int result) { trace.Add("after"); return result + 10; }
		static bool BeforeWrite(Store __instance, ref int __0)
		{
			trace.Add("before"); observed.Add(__instance); __0 += 3;
			return !skip;
		}
		static void AfterWrite(int value) { trace.Add("after"); observed.Add(value); }
		static void WriteByName(ref int value) => value += 3;

		[Test]
		public void Field_operations_preserve_evaluation_order_skip_and_volatile_prefixes([Values] bool isStatic, [Values] bool isVolatile, [Values] bool write, [Values] bool skipped)
		{
			skip = skipped;
			var fieldName = isStatic ? isVolatile ? nameof(Store.StaticVolatileValue) : nameof(Store.StaticValue) : isVolatile ? nameof(Store.VolatileValue) : nameof(Store.Value);
			var outer = (write ? "Write" : "Read") + (isVolatile ? "Volatile" : "") + (isStatic ? "Static" : "Instance");
			var field = AccessTools.Field(typeof(Store), fieldName);
			var store = new Store();
			if (isVolatile) Assert.IsTrue(PatchProcessor.GetOriginalInstructions(Method(outer)).Any(code => code.opcode == OpCodes.Volatile));
			Apply(outer, new InnerTarget(field, write ? InnerTargetKind.FieldWrite : InnerTargetKind.FieldRead), write ? nameof(BeforeWrite) : nameof(BeforeRead), write ? nameof(AfterWrite) : nameof(AfterRead));
			object[] arguments = isStatic ? write ? [10] : [] : write ? [store, 10] : [store];
			var result = Method(outer).Invoke(null, arguments);
			Assert.AreEqual(write ? skipped ? 40 : 13 : 40, field.GetValue(isStatic ? null : store));
			if (!write) Assert.AreEqual(skipped ? 17 : 50, result);
			Assert.AreEqual(isStatic ? 0 : 1, receiverEvaluations);
			Assert.AreEqual(write ? 1 : 0, valueEvaluations);
			Assert.AreSame(isStatic ? null : store, observed[0]);
			if (write) Assert.AreEqual(13, observed[1]);
			Assert.AreEqual(new[] { "before", "after" }, trace);
		}

		[Test]
		public void Field_write_value_name_binds_the_same_captured_operand_as_numeric_injection()
		{
			Apply(nameof(WriteInstance), new InnerTarget(AccessTools.Field(typeof(Store), nameof(Store.Value)), InnerTargetKind.FieldWrite), nameof(WriteByName));
			var store = new Store(); WriteInstance(store, 10);
			Assert.AreEqual(13, store.Value);
			Assert.AreEqual(1, valueEvaluations);
		}
		static IEnumerable<CodeInstruction> UnalignedField(IEnumerable<CodeInstruction> instructions)
		{
			foreach (var instruction in instructions)
			{
				if (instruction.opcode == OpCodes.Ldfld)
				{
					yield return new CodeInstruction(OpCodes.Unaligned, (byte)1).MoveLabelsFrom(instruction).MoveBlocksFrom(instruction);
					yield return new CodeInstruction(OpCodes.Volatile);
				}
				yield return instruction;
			}
		}
		[Test]
		public void Field_read_keeps_combined_unaligned_and_volatile_prefixes_on_the_operation()
		{
			harmony.CreateProcessor(Method(nameof(ReadInstance))).AddTranspiler(Method(nameof(UnalignedField)))
				.AddInnerPostfix(Fix(nameof(AfterRead), new InnerTarget(AccessTools.Field(typeof(Store), nameof(Store.Value)), InnerTargetKind.FieldRead))).Patch();
			Assert.AreEqual(50, ReadInstance(new Store()));
			Assert.AreEqual(1, receiverEvaluations);
			var instructions = PatchProcessor.GetCurrentInstructions(Method(nameof(ReadInstance)));
			var load = instructions.FindIndex(instruction => instruction.opcode == OpCodes.Ldfld);
			Assert.AreEqual(OpCodes.Unaligned, instructions[load - 2].opcode);
			Assert.AreEqual((byte)1, instructions[load - 2].operand);
			Assert.AreEqual(OpCodes.Volatile, instructions[load - 1].opcode);
		}
		static bool RepairRead(ref Store __instance, ref int __result)
		{
			if (repair == 1) __instance = replacement;
			if (repair == 2) __result = 7;
			return repair != 2;
		}
		static bool RepairWrite(ref Store __instance) { if (repair == 1) __instance = replacement; return repair != 2; }
		[Test]
		public void Field_receiver_can_be_replaced_or_skipped_before_its_null_check([Values] bool write, [Values(0, 1, 2)] int action)
		{
			repair = action;
			Apply(write ? nameof(WriteInstance) : nameof(ReadInstance), new InnerTarget(AccessTools.Field(typeof(Store), nameof(Store.Value)), write ? InnerTargetKind.FieldWrite : InnerTargetKind.FieldRead), write ? nameof(RepairWrite) : nameof(RepairRead));
			if (action == 0)
			{
				if (write) Assert.Throws<NullReferenceException>(() => WriteInstance(null, 12));
				else Assert.Throws<NullReferenceException>(() => ReadInstance(null));
			}
			else if (write) { WriteInstance(null, 12); Assert.AreEqual(action == 1 ? 12 : 9, replacement.Value); }
			else Assert.AreEqual(action == 1 ? 9 : 7, ReadInstance(null));
			Assert.AreEqual(1, receiverEvaluations);
			Assert.AreEqual(write ? 1 : 0, valueEvaluations);
		}

		static void ArrayBefore(object[] __args) { arrays.Add(__args); if (__args.Length != 0) __args[0] = (int)__args[0] + 3; }
		static void ArrayAfter(object[] __args) => arrays.Add(__args);
		[TestCase(false), TestCase(true)]
		public void Field_arrays_exclude_the_receiver_and_write_back_only_the_store_value(bool write)
		{
			Apply(write ? nameof(WriteInstance) : nameof(ReadInstance), new InnerTarget(AccessTools.Field(typeof(Store), nameof(Store.Value)), write ? InnerTargetKind.FieldWrite : InnerTargetKind.FieldRead), nameof(ArrayBefore), nameof(ArrayAfter));
			var store = new Store();
			if (write) WriteInstance(store, 10); else Assert.AreEqual(40, ReadInstance(store));
			Assert.AreEqual(write ? 13 : 40, store.Value);
			Assert.AreEqual(write ? new object[] { 13 } : new object[0], arrays[0]);
			Assert.AreSame(arrays[0], arrays[1]);
		}
		static void FieldMember(FieldInfo __originalMember) => observed.Add(__originalMember);
		static void MethodMetadata(MethodBase __originalMethod) { }
		static void MemberMetadata(MemberInfo __originalMember) { }
		[Test]
		public void Field_metadata_is_the_real_field_and_method_metadata_rejection_keeps_the_existing_patch()
		{
			var field = AccessTools.Field(typeof(Store), nameof(Store.Value));
			var target = new InnerTarget(field, InnerTargetKind.FieldRead);
			Apply(nameof(ReadInstance), target, nameof(FieldMember));
			var before = HarmonySharedState.GetPatchInfo(Method(nameof(ReadInstance))).Serialize();
			Assert.Throws<HarmonyException>(() => Apply(nameof(ReadInstance), target, nameof(MethodMetadata)));
			Assert.AreEqual(before, HarmonySharedState.GetPatchInfo(Method(nameof(ReadInstance))).Serialize());
			Assert.AreEqual(40, ReadInstance(new Store()));
			Assert.AreEqual(new object[] { field }, observed);
		}
		public struct StructStore { public int Value; }
		[MethodImpl(MethodImplOptions.NoInlining)] static int ReadStruct(StructStore store) => store.Value;
		[MethodImpl(MethodImplOptions.NoInlining)] static void WriteStruct(ref StructStore store, int value) => store.Value = value;
		[TestCase(false), TestCase(true)]
		public void Struct_instance_fields_are_rejected_before_installation(bool write)
		{
			var outer = Method(write ? nameof(WriteStruct) : nameof(ReadStruct));
			Assert.Throws<ArgumentException>(() => new InnerTarget(AccessTools.Field(typeof(StructStore), nameof(StructStore.Value)),
				write ? InnerTargetKind.FieldWrite : InnerTargetKind.FieldRead));
			Assert.IsNull(Harmony.GetPatchInfo(outer));
			var store = new StructStore { Value = 4 }; WriteStruct(ref store, 7); Assert.AreEqual(7, ReadStruct(store));
		}
		public class GenericStore<T> { public T Value; }
		[MethodImpl(MethodImplOptions.NoInlining)] static int GenericFields(GenericStore<int> first, GenericStore<string> second) => first.Value + second.Value.Length;
		[TestCase(false), TestCase(true)]
		public void Generic_field_targets_keep_exact_constructions_or_select_the_explicit_family(bool family)
		{
			var field = AccessTools.Field(family ? typeof(GenericStore<>) : typeof(GenericStore<int>), nameof(GenericStore<int>.Value));
			Apply(nameof(GenericFields), new InnerTarget(field, InnerTargetKind.FieldRead), nameof(FieldMember));
			Assert.AreEqual(7, GenericFields(new GenericStore<int> { Value = 4 }, new GenericStore<string> { Value = "abc" }));
			Assert.AreEqual(family ? new[] { typeof(GenericStore<int>), typeof(GenericStore<string>) } : new[] { typeof(GenericStore<int>) }, observed.Cast<FieldInfo>().Select(member => member.DeclaringType));
		}

		[MethodImpl(MethodImplOptions.NoInlining)] static int GetIndex(Store store, int index) => store[index];
		[MethodImpl(MethodImplOptions.NoInlining)] static void SetIndex(Store store, int index, int value) => store[index] = value;
		static void IndexBefore(ref int index) => index++;
		static void IndexSetter(ref int index, ref int value) { index++; value += 10; }
		static void AccessorMetadata(MethodInfo __originalMember, MethodBase __originalMethod) { Assert.AreEqual(__originalMember, __originalMethod); observed.Add(__originalMember); }
		[Test]
		public void Indexer_selectors_have_the_same_behavior_as_their_actual_accessor_methods([Values] bool write, [Values] bool propertySelector)
		{
			var property = typeof(Store).GetProperty("Item");
			var method = write ? property.GetSetMethod() : property.GetGetMethod();
			var target = propertySelector ? new InnerTarget(property, write ? InnerTargetKind.Setter : InnerTargetKind.Getter) : new InnerTarget(method);
			Apply(write ? nameof(SetIndex) : nameof(GetIndex), target, write ? nameof(IndexSetter) : nameof(IndexBefore), nameof(AccessorMetadata));
			var store = new Store();
			if (write) { SetIndex(store, 2, 8); Assert.AreEqual(15, store.Value); }
			else Assert.AreEqual(43, GetIndex(store, 2));
			Assert.AreEqual(write ? 0 : 1, store.GetterCalls);
			Assert.AreEqual(write ? 1 : 0, store.SetterCalls);
			Assert.AreEqual(new object[] { method }, observed);
		}

		public class Constructed
		{
			public static int Calls;
			public int Value;
			[MethodImpl(MethodImplOptions.NoInlining)] public Constructed(int value) { Calls++; Value = value; }
		}
		static Constructed constructorReplacement;
		[MethodImpl(MethodImplOptions.NoInlining)] static Constructed Construct(int value) => new(Value(value));
		static bool ConstructorBefore(object __instance, ref int value, ref Constructed __result)
		{
			Assert.IsNull(__instance); value += 3;
			if (skip) __result = constructorReplacement;
			return !skip;
		}
		static void ConstructorAfter(object __instance, Constructed __result, ConstructorInfo __originalMember)
		{
			Assert.IsNull(__instance); Assert.AreEqual(typeof(Constructed).GetConstructor([typeof(int)]), __originalMember); __result.Value += 10;
		}
		[TestCase(false), TestCase(true)]
		public void Construction_captures_arguments_once_supports_skip_and_exposes_the_result_not_a_receiver(bool skipped)
		{
			skip = skipped; constructorReplacement = new Constructed(20); Constructed.Calls = 0;
			Apply(nameof(Construct), new InnerTarget(typeof(Constructed).GetConstructor([typeof(int)])), nameof(ConstructorBefore), nameof(ConstructorAfter));
			var result = Construct(4);
			Assert.AreEqual(skipped ? 30 : 17, result.Value);
			Assert.AreEqual(skipped ? 0 : 1, Constructed.Calls);
			Assert.AreEqual(1, valueEvaluations);
		}
		public struct ConstructedStruct
		{
			public int Value;
			[MethodImpl(MethodImplOptions.NoInlining)] public ConstructedStruct(int value) => Value = value;
		}
		[MethodImpl(MethodImplOptions.NoInlining)] static int ConstructStruct(int value) => new ConstructedStruct(value).Value;
		static void StructConstructorAfter(ref ConstructedStruct __result) => __result.Value += 10;
		[Test]
		public void Value_type_construction_can_transform_its_result()
		{
			Apply(nameof(ConstructStruct), new InnerTarget(typeof(ConstructedStruct).GetConstructor([typeof(int)])), after: nameof(StructConstructorAfter));
			Assert.AreEqual(15, ConstructStruct(5));
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		static string BuildText(string value, int depth)
		{
			var builder = new StringBuilder();
			if (depth > 0) builder.Append(BuildText(value + depth, depth - 1));
			return builder.Append("anchor").ToString();
		}
		[HarmonyPatch(typeof(InfixOperations), nameof(BuildText))]
		class TextPatches
		{
			[HarmonyPostfix, HarmonyInfix(typeof(StringBuilder), InnerTargetKind.Constructor)]
			static void Capture(StringBuilder __result, [HarmonyOuter] ref StringBuilder __var_builder)
			{
				Assert.IsNull(__var_builder); __var_builder = __result;
			}
			[HarmonyPostfix, HarmonyInfix("anchor")]
			static string Anchor(string result, [HarmonyOuter] StringBuilder __var_builder, [HarmonyOuter] string value)
			{
				Assert.AreEqual("anchor", result); __var_builder.Append(value); return "!";
			}
		}
		static string ExpectedText(string value, int depth) => (depth > 0 ? ExpectedText(value + depth, depth - 1) : "") + value + "!";
		void PatchText(bool attributes)
		{
			if (attributes) harmony.CreateClassProcessor(typeof(TextPatches)).Patch();
			else
			{
				harmony.CreateProcessor(Method(nameof(BuildText)))
					.AddInnerPostfix(new HarmonyMethod(AccessTools.Method(typeof(TextPatches), "Capture")) { innerTarget = new InnerTarget(typeof(StringBuilder).GetConstructor(Type.EmptyTypes)) }).Patch();
				harmony.CreateProcessor(Method(nameof(BuildText)))
					.AddInnerPostfix(new HarmonyMethod(AccessTools.Method(typeof(TextPatches), "Anchor")) { innerTarget = InnerTarget.Constant("anchor") }).Patch();
			}
		}
		[Test]
		public void Constructor_capture_and_literal_anchor_share_outer_locals_but_recursive_invocations_do_not([Values] bool attributes, [Values(0, 2)] int depth)
		{
			PatchText(attributes);
			for (var i = 0; i < 2; i++) Assert.AreEqual(ExpectedText("value" + i, depth), BuildText("value" + i, depth));
		}
		[Test]
		public void Constructor_capture_and_literal_anchor_are_isolated_between_concurrent_invocations()
		{
			PatchText(false);
			var threads = new Thread[4]; var results = new string[threads.Length]; var errors = new Exception[threads.Length];
			using var start = new ManualResetEvent(false);
			for (var i = 0; i < threads.Length; i++)
			{
				var index = i;
				threads[i] = new Thread(() => { start.WaitOne(); try { results[index] = BuildText("worker" + index, 2); } catch (Exception error) { errors[index] = error; } }) { IsBackground = true };
				threads[i].Start();
			}
			start.Set();
			for (var i = 0; i < threads.Length; i++)
			{
				Assert.IsTrue(threads[i].Join(TimeSpan.FromSeconds(10)), "Worker did not finish");
				Assert.IsNull(errors[i]); Assert.AreEqual(ExpectedText("worker" + i, 2), results[i]);
			}
		}

		[MethodImpl(MethodImplOptions.NoInlining)] static string IteratorValue(string value) => value;
		static IEnumerable<string> IteratorText()
		{
			for (var i = 0; i < 3; i++)
			{
				var before = IteratorValue("iterator-before");
				var builder = new StringBuilder();
				yield return builder.Append(before).Append("iterator-after").ToString();
			}
		}
		[HarmonyPatch(typeof(InfixOperations)), HarmonyPatch(nameof(IteratorText), MethodType.Enumerator)]
		class IteratorPatches
		{
			[HarmonyPostfix, HarmonyInfix("iterator-before")]
			static string ReadBefore(string result, [HarmonyOuter] StringBuilder __var_builder)
			{
				Assert.IsNull(__var_builder); trace.Add("before"); return "start";
			}
			[HarmonyPostfix, HarmonyInfix(typeof(StringBuilder), InnerTargetKind.Constructor)]
			static void Capture(StringBuilder __result, [HarmonyOuter] ref StringBuilder __var_builder)
			{
				Assert.IsNull(__var_builder); trace.Add("capture"); __var_builder = __result;
			}
			[HarmonyPostfix, HarmonyInfix("iterator-after")]
			static string ReadAfter(string result, [HarmonyOuter] StringBuilder __var_builder)
			{
				Assert.AreEqual("start", __var_builder.ToString()); trace.Add("after"); __var_builder.Append("middle"); return "end";
			}
		}
		[Test]
		public void Named_outer_locals_reset_for_each_MoveNext_invocation_not_for_the_iterator_lifetime()
		{
			harmony.CreateClassProcessor(typeof(IteratorPatches)).Patch();
			using var iterator = IteratorText().GetEnumerator();
			for (var i = 0; i < 3; i++)
			{
				Assert.IsTrue(iterator.MoveNext()); Assert.AreEqual("startmiddleend", iterator.Current);
			}
			Assert.IsFalse(iterator.MoveNext());
			Assert.AreEqual(Enumerable.Range(0, 3).SelectMany(_ => new[] { "before", "capture", "after" }), trace);
		}

		[MethodImpl(MethodImplOptions.NoInlining)] static void LiteralSequence() { }
		static CodeInstruction Literal(object value) => value switch
		{
			string _ => new(OpCodes.Ldstr, value),
			int _ => new(OpCodes.Ldc_I4, value),
			long _ => new(OpCodes.Ldc_I8, value),
			float _ => new(OpCodes.Ldc_R4, value),
			double _ => new(OpCodes.Ldc_R8, value),
			_ => throw new ArgumentException("Unsupported test literal")
		};
		static IEnumerable<CodeInstruction> LiteralBody(IEnumerable<CodeInstruction> _)
		{
			foreach (var value in literals) { yield return Literal(value); yield return new CodeInstruction(OpCodes.Pop); }
			yield return new CodeInstruction(OpCodes.Ret);
		}
		static void ObserveLiteral(object __result) => observed.Add(__result);
		[TestCase("text"), TestCase(42), TestCase(42L), TestCase(1.25f), TestCase(1.25d)]
		public void Literal_targets_preserve_their_value_and_clr_category(object literal)
		{
			literals = [literal];
			harmony.CreateProcessor(Method(nameof(LiteralSequence))).AddTranspiler(Method(nameof(LiteralBody)))
				.AddInnerPostfix(Fix(nameof(ObserveLiteral), InnerTarget.Constant(literal))).Patch();
			LiteralSequence(); Assert.AreEqual(new[] { literal }, observed); Assert.AreEqual(literal.GetType(), observed[0].GetType());
		}
		[Test]
		public void Floating_literal_targets_distinguish_signed_zero_and_nan_payloads([Values] bool wide, [Values(0, 1, 2, 3)] int selected)
		{
			literals = wide
				? new ulong[] { 0, 0x8000000000000000, 0x7ff8000000000001, 0x7ff8000000000002 }.Select(bits => (object)BitConverter.ToDouble(BitConverter.GetBytes(bits), 0)).ToArray()
				: new uint[] { 0, 0x80000000, 0x7fc00001, 0x7fc00002 }.Select(bits => (object)BitConverter.ToSingle(BitConverter.GetBytes(bits), 0)).ToArray();
			harmony.CreateProcessor(Method(nameof(LiteralSequence))).AddTranspiler(Method(nameof(LiteralBody)))
				.AddInnerPostfix(Fix(nameof(ObserveLiteral), InnerTarget.Constant(literals[selected]))).Patch();
			LiteralSequence(); Assert.AreEqual(1, observed.Count, "Only the bit-exact selected instruction may receive the postfix");
			Assert.AreEqual(literals[selected].GetType(), observed[0].GetType());
		}
		[MethodImpl(MethodImplOptions.NoInlining)] static int ThreeTwos() => 0;
		static IEnumerable<CodeInstruction> EquivalentTwos(IEnumerable<CodeInstruction> _)
		{
			yield return new CodeInstruction(OpCodes.Ldc_I4_2);
			yield return new CodeInstruction(OpCodes.Ldc_I4_S, (sbyte)2);
			yield return new CodeInstruction(OpCodes.Add);
			yield return new CodeInstruction(OpCodes.Ldc_I4, 2);
			yield return new CodeInstruction(OpCodes.Add);
			yield return new CodeInstruction(OpCodes.Ret);
		}
		static int AddTen(int result) { observed.Add(result); return result + 10; }
		[TestCase(false), TestCase(true)]
		public void Literal_positions_normalize_opcode_forms_and_deduplicate_selected_occurrences(bool all)
		{
			int[] positions = all ? [] : [1, -1, 1, -3];
			harmony.CreateProcessor(Method(nameof(ThreeTwos))).AddTranspiler(Method(nameof(EquivalentTwos)))
				.AddInnerPostfix(Fix(nameof(AddTen), InnerTarget.Constant(2, positions))).Patch();
			Assert.AreEqual(all ? 36 : 26, ThreeTwos()); Assert.AreEqual(all ? 3 : 2, observed.Count);
		}
		[TestCase(nameof(MethodMetadata)), TestCase(nameof(MemberMetadata))]
		public void Literal_operations_reject_member_metadata_before_installation(string patch)
		{
			literals = [42];
			var before = HarmonySharedState.GetPatchInfo(Method(nameof(LiteralSequence)))?.Serialize();
			Assert.Throws<HarmonyException>(() => harmony.CreateProcessor(Method(nameof(LiteralSequence))).AddTranspiler(Method(nameof(LiteralBody)))
				.AddInnerPrefix(Fix(patch, InnerTarget.Constant(42))).Patch());
			Assert.AreEqual(before, HarmonySharedState.GetPatchInfo(Method(nameof(LiteralSequence)))?.Serialize());
			LiteralSequence(); Assert.IsEmpty(observed);
		}
	}
}
