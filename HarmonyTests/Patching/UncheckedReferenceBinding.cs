using HarmonyLib;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;

namespace HarmonyLibTests.Patching
{
	[TestFixture, NonParallelizable]
	public class UncheckedReferenceBinding : TestLogger
	{
		Harmony harmony;
		static int observed;
		static int originalCalls;
		static int factoryCalls;
		static SpecificGene originalGene = new();
		static OtherGene replacementGene = new();

		[SetUp]
		public void SetUp()
		{
			harmony = new Harmony("test." + nameof(UncheckedReferenceBinding));
			observed = originalCalls = factoryCalls = 0;
		}

		[TearDown]
		public void TearDown() => harmony.UnpatchAll(harmony.Id);

		static MethodInfo Method(string name) => AccessTools.Method(typeof(UncheckedReferenceBinding), name);
		static HarmonyMethod Unchecked(string name) => new(Method(name)) { uncheckedReferenceBinding = true };

		[TestCase(false, false), TestCase(false, true), TestCase(true, false), TestCase(true, true)]
		public void Shared_callback_uses_matching_by_value_arguments(bool attribute, bool boxed)
		{
			var callback = attribute ? new HarmonyMethod(Method(nameof(AnnotatedShared))) : Unchecked(nameof(Shared));
			var first = Method(boxed ? nameof(BoxFirst) : nameof(ReferenceFirst));
			var second = Method(boxed ? nameof(BoxSecond) : nameof(ReferenceSecond));
			harmony.Patch(first, prefix: callback);
			harmony.Patch(second, prefix: callback);
			if (boxed) { BoxFirst(new(), 123); BoxSecond(456, new()); }
			else { ReferenceFirst(new(), new()); ReferenceSecond(new(), new()); }
			Assert.AreEqual(46, observed);
			Assert.AreEqual(2, originalCalls);
			Assert.IsTrue(Harmony.GetPatchInfo(first).Prefixes.Single().uncheckedReferenceBinding);
			Assert.IsTrue(Harmony.GetPatchInfo(second).Prefixes.Single().uncheckedReferenceBinding);
		}

		[Test]
		public void Explicit_false_overrides_the_attribute()
		{
			var callback = new HarmonyMethod(Method(nameof(AnnotatedShared))) { uncheckedReferenceBinding = false };
			Assert.Catch<ArgumentException>(() => harmony.Patch(Method(nameof(ReferenceFirst)), prefix: callback));
		}

		[Test]
		public void Patch_class_discovery_imports_the_method_option()
		{
			_ = harmony.CreateClassProcessor(typeof(DiscoveredPatch)).Patch();
			ReferenceFirst(new(), new());
			Assert.AreEqual(17, observed);
			Assert.IsTrue(Harmony.GetPatchInfo(Method(nameof(ReferenceFirst))).Prefixes.Single().uncheckedReferenceBinding);
		}

		[HarmonyPatch(typeof(UncheckedReferenceBinding), nameof(ReferenceFirst))]
		static class DiscoveredPatch
		{
			[HarmonyPrefix, HarmonyUncheckedReferenceBinding]
			static void Prefix(A first, B second, MethodBase __originalMethod) => Shared(first, second, __originalMethod);
		}

		[TestCase(false), TestCase(true)]
		public void Transpiler_registrations_reject_the_option(bool attribute)
		{
			var original = Method(nameof(ReferenceFirst));
			var transpiler = attribute ? new HarmonyMethod(Method(nameof(AnnotatedTranspiler))) : Unchecked(nameof(Transpiler));
			var error = Assert.Catch<ArgumentException>(() => harmony.Patch(original, transpiler: transpiler));
			StringAssert.Contains("transpiler", error.Message);
			var patches = Harmony.GetPatchInfo(original);
			Assert.IsTrue(patches is null || patches.Transpilers.Count == 0);
			harmony.Patch(original, transpiler: new HarmonyMethod(Method(nameof(AnnotatedTranspiler))) { uncheckedReferenceBinding = false });
			Assert.IsFalse(Harmony.GetPatchInfo(original).Transpilers.Single().uncheckedReferenceBinding);
		}

		[Test]
		public void Transpiler_records_never_version_the_state()
		{
			var method = Method(nameof(Transpiler));
			var info = new PatchInfo
			{
				transpilers = [new Patch(0, "legacy", Priority.Normal, [], [], false, method.MetadataToken, method.Module.ModuleVersionId.ToString(), uncheckedReferenceBinding: true)]
			};
			Assert.IsFalse(info.HasUncheckedReferenceBindings);
			var bytes = info.Serialize();
			Assert.AreNotEqual((byte)'H', bytes[0]);
			Assert.IsFalse(PatchInfoSerialization.Deserialize(bytes).HasUncheckedReferenceBindings);
		}

		[TestCase(false), TestCase(true)]
		public void A_checked_registration_of_the_same_callback_still_fails(bool factory)
		{
			var original = Method(nameof(ReferenceFirst));
			var callback = factory ? nameof(Factory) : nameof(Shared);
			harmony.Patch(original, prefix: Unchecked(callback));
			var before = HarmonySharedState.GetPatchInfo(original);
			Assert.Catch<ArgumentException>(() => harmony.Patch(original, prefix: new HarmonyMethod(Method(callback))));
			var after = HarmonySharedState.GetPatchInfo(original);
			Assert.AreEqual(before.VersionCount, after.VersionCount);
			Assert.AreEqual(1, after.prefixes.Length);
			ReferenceFirst(new(), new());
			Assert.AreEqual(17, observed);
			Assert.AreEqual(1, originalCalls);
			if (factory) Assert.AreEqual(3, factoryCalls);
		}

		[TestCase(nameof(Shared)), TestCase(nameof(Factory)), TestCase(nameof(DynamicFactory))]
		public void Override_survives_rebuilds_and_factory_resolution(string callback)
		{
			var original = Method(nameof(ReferenceFirst));
			var registration = Unchecked(callback);
			harmony.Patch(original, prefix: registration);
			registration.uncheckedReferenceBinding = false;
			harmony.Patch(original, postfix: new HarmonyMethod(Method(nameof(Empty))));
			ReferenceFirst(new(), new());
			Assert.AreEqual(17, observed);
			Assert.IsTrue(HarmonySharedState.GetPatchInfo(original).prefixes.Single().uncheckedReferenceBinding);
			harmony.Unpatch(original, Method(nameof(Empty)));
			ReferenceFirst(new(), new());
			Assert.AreEqual(34, observed);
			Assert.AreEqual(2, originalCalls);
			if (callback != nameof(Shared)) Assert.AreEqual(3, factoryCalls);
		}

		[Test]
		public void Sibling_ref_passthrough_can_return_a_different_reference()
		{
			harmony.Patch(Method(nameof(ReadGene)), postfix: Unchecked(nameof(SiblingResult)));
			Gene value = ReadGene();
			Assert.AreSame(replacementGene, value);
			Assert.AreEqual(typeof(OtherGene), value.GetType());
		}

		[Test]
		public void Null_only_unrelated_argument_is_available_when_explicitly_unchecked()
		{
			harmony.Patch(Method(nameof(NullOriginal)), prefix: Unchecked(nameof(NullReader)));
			Assert.IsNull(NullOriginal(null));
			Assert.AreEqual(1, observed);
		}

		[TestCase(HarmonyPatchType.Postfix), TestCase(HarmonyPatchType.Finalizer)]
		public void Every_ordinary_callback_role_carries_its_registration_option(HarmonyPatchType role)
		{
			var patch = Unchecked(role == HarmonyPatchType.Postfix ? nameof(Shared) : nameof(NullFinalizer));
			harmony.Patch(Method(nameof(ReferenceFirst)), postfix: role == HarmonyPatchType.Postfix ? patch : null,
				 finalizer: role == HarmonyPatchType.Finalizer ? patch : null);
			ReferenceFirst(new(), new());
			Assert.AreEqual(role == HarmonyPatchType.Postfix ? 17 : 1, observed);
			Assert.AreEqual(1, originalCalls);
		}

		[TestCase(nameof(RefMetadata), "value, not an address")]
		[TestCase(nameof(RefArgs), "value, not an address")]
		[TestCase(nameof(ValueArgument), "no boxing or unboxing")]
		[TestCase(nameof(NullableArgument), "no boxing or unboxing")]
		[TestCase(nameof(MissingArgument), "Parameter \"missing\" not found")]
		[TestCase(nameof(MissingResult), "void operation has no result")]
		[TestCase(nameof(UnboxedRunFlag), "no boxing or unboxing")]
		public void Structural_requirements_remain_checked(string callback, string diagnostic)
		{
			var error = Assert.Catch(() => harmony.Patch(Method(nameof(ReferenceFirst)), prefix: Unchecked(callback)));
			StringAssert.Contains(diagnostic, error.ToString());
		}

		[TestCase(nameof(ReadGeneValue), nameof(UnrelatedResult), "existing result contract")]
		[TestCase(nameof(ReadGene), nameof(WrongResultRef), "Wrong type of __resultRef")]
		public void Existing_result_contracts_remain_checked(string original, string callback, string diagnostic)
		{
			var error = Assert.Catch(() => harmony.Patch(Method(original), postfix: Unchecked(callback)));
			StringAssert.Contains(diagnostic, error.ToString());
		}

		[Test]
		public void Existing_infix_storage_contract_remains_checked()
		{
			var callback = Unchecked(nameof(Shared));
			callback.innerMethod = new InnerMethod(Method(nameof(ReferenceFirst)));
			var error = Assert.Catch(() => harmony.CreateProcessor(Method(nameof(Outer))).AddInnerPrefix(callback).Patch());
			StringAssert.Contains("Storage type", error.ToString());
		}

		[Test]
		public void Metadata_roundtrip_requires_version_five_and_preserves_the_flag()
		{
#if NET5_0_OR_GREATER && !NET9_0_OR_GREATER
            var previous = PatchInfoSerialization.useBinaryFormatter;
            const string formatterSwitch = "System.Runtime.Serialization.EnableUnsafeBinaryFormatterSerialization";
            _ = AppContext.TryGetSwitch(formatterSwitch, out var previousSwitch);
            try
            {
                AppContext.SetSwitch(formatterSwitch, true);
                PatchInfoSerialization.useBinaryFormatter = false;
                Check();
                PatchInfoSerialization.useBinaryFormatter = true;
                Check();
            }
            finally
            {
                PatchInfoSerialization.useBinaryFormatter = previous;
                AppContext.SetSwitch(formatterSwitch, previousSwitch);
            }
#else
			Check();
#endif
			static void Check()
			{
				var info = new PatchInfo();
				info.AddPrefixes("unchecked", Unchecked(nameof(Shared)));
				info.AddPostfixes("checked", new HarmonyMethod(Method(nameof(Empty))));
				var bytes = info.Serialize();
				Assert.AreEqual(5, bytes[14]);
				var restored = PatchInfoSerialization.Deserialize(bytes);
				Assert.IsTrue(restored.prefixes.Single().uncheckedReferenceBinding);
				Assert.IsFalse(restored.postfixes.Single().uncheckedReferenceBinding);
				Assert.AreEqual(bytes, restored.Serialize());
				var downgraded = (byte[])bytes.Clone();
				downgraded[14] = 4;
				var error = Assert.Throws<SerializationException>(() => PatchInfoSerialization.Deserialize(downgraded));
				StringAssert.Contains("version 5", error.Message);
				var stripped = bytes.Skip(16).ToArray();
				Assert.Throws<SerializationException>(() => PatchInfoSerialization.Deserialize(stripped));
				restored.RemovePrefix("unchecked");
				Assert.IsFalse(PatchInfoSerialization.Deserialize(restored.Serialize()).HasUncheckedReferenceBindings);
				Assert.AreNotEqual((byte)'H', restored.Serialize()[0]);
			}
		}

		static IEnumerable<TestCaseData> GenericBindings()
		{
			var parameter = typeof(Node<>).GetGenericArguments()[0];
			yield return new(parameter, typeof(Uri), true);
			yield return new(typeof(Node<>), typeof(Uri), true);
			yield return new(typeof(List<>).MakeGenericType(parameter), typeof(Uri), true);
			yield return new(parameter.MakeByRefType(), typeof(Uri).MakeByRefType(), true);
			yield return new(typeof(Leaf), typeof(Node<Leaf>), true);
			yield return new(typeof(Leaf).MakeByRefType(), typeof(Node<Leaf>).MakeByRefType(), true);
			yield return new(typeof(Leaf[]), typeof(Node<Leaf>[]), true);
			yield return new(typeof(RightLeaf), typeof(Right<Left<RightLeaf>>), true);
			yield return new(typeof(IEnumerable<IEnumerable<string>>), typeof(IEnumerable<IEnumerable<object>>), true);
			yield return new(typeof(List<string>), typeof(List<object>), false);
			yield return new(typeof(Node<Leaf>), typeof(Node<OtherLeaf>), false);
			yield return new(typeof(IList<string>), typeof(IList<object>), true);
		}

		[TestCaseSource(nameof(GenericBindings))]
		public void Generic_policy_is_preserved_and_reference_mismatches_can_be_overridden(Type source, Type destination, bool accepted)
		{
			Assert.AreEqual(accepted, MethodCreatorTools.BindingIncompatibility(source, destination) is null);
			Assert.IsNull(MethodCreatorTools.BindingIncompatibility(source, destination, uncheckedReferenceBinding: true));
		}

		class A { public int value = 17; }
		class B { public int value = 29; }
		class Other { }
		class Gene { }
		class SpecificGene : Gene { }
		class OtherGene : Gene { }
		class Node<T> where T : Node<T> { }
		class Leaf : Node<Leaf> { }
		class OtherLeaf : Node<OtherLeaf> { }
		class Left<T> where T : Right<Left<T>> { }
		class Right<T> { }
		class RightLeaf : Right<Left<RightLeaf>> { }

		[MethodImpl(MethodImplOptions.NoInlining)]
		static void ReferenceFirst(A first, Other second) => originalCalls++;
		[MethodImpl(MethodImplOptions.NoInlining)]
		static void ReferenceSecond(Other first, B second) => originalCalls++;
		[MethodImpl(MethodImplOptions.NoInlining)]
		static void BoxFirst(A first, int second) => originalCalls++;
		[MethodImpl(MethodImplOptions.NoInlining)]
		static void BoxSecond(int first, B second) => originalCalls++;
		[MethodImpl(MethodImplOptions.NoInlining)]
		static ref SpecificGene ReadGene() => ref originalGene;
		[MethodImpl(MethodImplOptions.NoInlining)]
		static SpecificGene ReadGeneValue() => originalGene;
		[MethodImpl(MethodImplOptions.NoInlining)]
		static string NullOriginal(string value) => value;
		[MethodImpl(MethodImplOptions.NoInlining)]
		static void Outer() => ReferenceFirst(new(), new());

		static void Shared(A first, B second, MethodBase __originalMethod)
		{
			if (__originalMethod.Name.EndsWith("First")) observed += first.value;
			else observed += second.value;
		}
		[HarmonyUncheckedReferenceBinding]
		static void AnnotatedShared(A first, B second, MethodBase __originalMethod) => Shared(first, second, __originalMethod);
		static MethodInfo Factory(MethodBase original) { factoryCalls++; return Method(nameof(Shared)); }
		static DynamicMethod DynamicFactory(MethodBase original)
		{
			factoryCalls++;
			var callback = new DynamicMethod("unchecked_shared", typeof(void), [typeof(A), typeof(B), typeof(MethodBase)], typeof(UncheckedReferenceBinding).Module, true);
			callback.DefineParameter(1, ParameterAttributes.None, "first");
			callback.DefineParameter(2, ParameterAttributes.None, "second");
			callback.DefineParameter(3, ParameterAttributes.None, "__originalMethod");
			var il = callback.GetILGenerator();
			il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldarg_1); il.Emit(OpCodes.Ldarg_2);
			il.Emit(OpCodes.Call, Method(nameof(Shared))); il.Emit(OpCodes.Ret);
			return callback;
		}
		static void Empty() { }
		static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) => instructions;
		[HarmonyUncheckedReferenceBinding]
		static IEnumerable<CodeInstruction> AnnotatedTranspiler(IEnumerable<CodeInstruction> instructions) => instructions;
		static ref OtherGene SiblingResult(ref OtherGene result) => ref replacementGene;
		static void NullReader(Uri value) { Assert.IsNull(value); observed++; }
		static Uri NullFinalizer(Uri __exception) { Assert.IsNull(__exception); observed++; return null; }
		static void RefMetadata(ref MethodBase __originalMethod) { }
		static void RefArgs(ref object[] __args) { }
		static void ValueArgument(Guid second) { }
		static void NullableArgument(int? second) { }
		static void MissingArgument(object missing) { }
		static void MissingResult(object __result) { }
		static void UnboxedRunFlag(object __runOriginal) { }
		static void UnrelatedResult(ref OtherGene __result) { }
		static void WrongResultRef(ref RefResult<Gene> __resultRef) { }
	}
}
