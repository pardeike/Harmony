using HarmonyLib;
using NUnit.Framework;
using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
#if NET5_0_OR_GREATER
using System.Text.Json;
#endif

namespace HarmonyLibTests.Patching
{
	[TestFixture, NonParallelizable]
	public class InfixCompletionMetadata : TestLogger
	{
		static MethodInfo Method(string name) => AccessTools.DeclaredMethod(typeof(InfixCompletionMetadata), name);
		static int Call(int value) => value;
		static void Noop() { }
		static Exception Finalizer(Exception __exception) => __exception;
		static bool InvalidFinalizer() => true;
		static void Capture([HarmonyArgument("value", ArgumentMode.Captured)] int value) { }
		static void Exact([HarmonyArgument("value", ArgumentMode.Original)] int value) { }
		static int Passthrough([HarmonyArgument("value", ArgumentMode.Captured)] int result) => result;
		static int PassthroughAndCapture(int result, [HarmonyArgument("value", ArgumentMode.Captured)] int value) => result;
		static Exception CapturingFinalizer([HarmonyArgument("value", ArgumentMode.Captured)] int value) => null;
		static HarmonyMethod Fix(string name) => new(Method(name)) { innerMethod = new InnerMethod(Method(nameof(Call))) };

		[HarmonyInfix(typeof(InfixCompletionMetadata), nameof(Call), OuterBody = InfixOuterBody.Auto), HarmonyPrefix]
		static void Automatic() { }
		[HarmonyInfix(typeof(InfixCompletionMetadata), nameof(Call)), HarmonyFinalizer]
		static Exception AttributedFinalizer(Exception __exception) => __exception;
		[HarmonyInfix(typeof(InfixCompletionMetadata), nameof(Call)), HarmonyFinalizer, HarmonyPrefix]
		static void ConflictingFinalizer() { }
		[HarmonyInline] static void Inline() { }

		[Test]
		public void Finalizer_role_is_distinct_and_ordinary_registration_rejects_it()
		{
			var method = Method(nameof(AttributedFinalizer));
			Assert.AreEqual(HarmonyPatchType.InnerFinalizer, AttributePatch.Create(method).type);
			var state = new PatchInfo();
			state.AddInnerFinalizers("owner", new HarmonyMethod(method));
			Assert.AreEqual(method, state.innerfinalizers.Single().PatchMethod);
			Assert.Throws<ArgumentException>(() => state.AddFinalizers("owner", new HarmonyMethod(method)));
			Assert.Throws<ArgumentException>(() => state.AddInnerFinalizers("owner", Fix(nameof(InvalidFinalizer))));
			Assert.Throws<ArgumentException>(() => AttributePatch.Create(Method(nameof(ConflictingFinalizer))));
			state.RemovePatch(method);
			Assert.IsFalse(state.HasInfixes);
		}

		[Test]
		public void Automatic_declaration_masks_both_older_selector_shapes_but_resolves_in_current_reader()
		{
			var declaration = (HarmonyInfix)Method(nameof(Automatic)).GetCustomAttributes(typeof(HarmonyInfix), true).Single();
			Assert.IsNull(declaration.innerName);
			Assert.IsNull(declaration.innerMemberName);
			Assert.AreEqual((InnerTargetKind)int.MinValue, declaration.innerTargetKind);
			Assert.AreEqual((MethodType)int.MinValue, declaration.info.methodType);
			var method = new HarmonyMethod(Method(nameof(Automatic)));
			Assert.AreEqual(InfixOuterBody.Auto, method.infixOuterBody);
			Assert.IsNull(method.methodType);
			var state = new PatchInfo();
			state.AddInnerPrefixes("owner", method);
			Assert.AreEqual(Method(nameof(Call)), state.innerprefixes.Single().innerMethod.Method);
			Assert.AreEqual(1, state.Serialize()[14], "Body routing is finished before persistence and adds no stored binding capability.");
			declaration.OuterBody = InfixOuterBody.Declared;
			Assert.AreEqual(nameof(Call), declaration.innerName);
			Assert.AreEqual(InnerTargetKind.Method, declaration.innerTargetKind);
			Assert.Throws<ArgumentOutOfRangeException>(() => declaration.OuterBody = (InfixOuterBody)123);
		}

		[Test]
		public void Captured_mode_uses_a_legacy_rejection_marker_and_preserves_the_requested_name()
		{
			var argument = (HarmonyArgument)Method(nameof(Capture)).GetParameters()[0].GetCustomAttributes(typeof(HarmonyArgument), true).Single();
			Assert.AreEqual(ArgumentMode.Captured, argument.Mode);
			Assert.AreEqual("value", argument.NewName);
			Assert.AreNotEqual("value", argument.OriginalName);
			Assert.AreEqual(int.MinValue, argument.Index);
			Assert.Throws<ArgumentOutOfRangeException>(() => new HarmonyArgument("value", (ArgumentMode)123));
		}

		[TestCase(nameof(Noop), false, 1)]
		[TestCase(nameof(Exact), false, 1)]
		[TestCase(nameof(Capture), false, 3)]
		[TestCase(nameof(Passthrough), true, 1)]
		[TestCase(nameof(PassthroughAndCapture), true, 3)]
		public void Capability_version_counts_actual_captured_bindings_only(string callback, bool postfix, int expected)
		{
			var state = new PatchInfo();
			if (postfix) state.AddInnerPostfixes("owner", Fix(callback));
			else state.AddInnerPrefixes("owner", Fix(callback));
			var bytes = state.Serialize();
			Assert.AreEqual(expected, bytes[14]);
			Assert.AreEqual(expected, PatchInfoSerialization.Deserialize(bytes).Serialize()[14]);
			if (expected == 3)
			{
				bytes[14] = 2;
				Assert.Throws<SerializationException>(() => PatchInfoSerialization.Deserialize(bytes));
			}
		}

		[Test]
		public void Finalizer_state_roundtrips_and_removal_downgrades_the_envelope()
		{
			var state = new PatchInfo();
			state.AddInnerPrefixes("prefix", Fix(nameof(Noop)));
			state.AddInnerFinalizers("finalizer", Fix(nameof(Finalizer)), Fix(nameof(CapturingFinalizer)));
			var bytes = state.Serialize();
			Assert.AreEqual(3, bytes[14]);
			var restored = PatchInfoSerialization.Deserialize(bytes);
			Assert.AreEqual(new[] { Method(nameof(Finalizer)), Method(nameof(CapturingFinalizer)) }, restored.innerfinalizers.Select(patch => patch.PatchMethod));
			Assert.AreEqual(3, restored.Serialize()[14]);
			bytes[14] = 2;
			Assert.Throws<SerializationException>(() => PatchInfoSerialization.Deserialize(bytes));
			restored.RemoveInnerFinalizer("finalizer");
			Assert.AreEqual(1, restored.Serialize()[14]);
			Assert.IsEmpty(PatchInfoSerialization.Deserialize(restored.Serialize()).innerfinalizers);
		}

		[Test]
		public void Existing_inspection_constructor_supplies_an_empty_finalizer_list()
		{
			Assert.IsEmpty(new Patches([], [], [], [], [], []).InnerFinalizers);
			var patch = new Patch(Fix(nameof(Finalizer)), 0, "finalizer-owner");
			var all = new Patches([], [], [], [], [], [], [patch]);
			Assert.AreEqual(new[] { "finalizer-owner" }, all.Owners);
		}

		[Test]
		public void Inline_attribute_is_an_optional_method_hint()
		{
			var attribute = (HarmonyInline)Method(nameof(Inline)).GetCustomAttributes(typeof(HarmonyInline), true).Single();
			Assert.IsTrue(attribute.Enabled);
			Assert.IsFalse(new HarmonyInline(false).Enabled);
			Assert.IsNull(typeof(HarmonyMethod).GetField("inline"));
		}

#if NET5_0_OR_GREATER
		[Test]
		public void Ordinary_json_retains_its_exact_shape_and_captured_state_includes_the_empty_third_role()
		{
			Assert.AreEqual("{\"prefixes\":[],\"postfixes\":[],\"transpilers\":[],\"finalizers\":[],\"innerprefixes\":[],\"innerpostfixes\":[],\"VersionCount\":0}",
				JsonSerializer.Serialize(new PatchInfo()));
			var state = new PatchInfo();
			state.AddInnerPrefixes("owner", Fix(nameof(Capture)));
			Assert.That(JsonSerializer.Serialize(state), Does.Contain("\"innerfinalizers\":[]"));
			state.RemoveInnerPrefix("owner");
			Assert.That(JsonSerializer.Serialize(state), Does.Not.Contain("innerfinalizers"));
		}

		[TestCase("missing")]
		[TestCase("null")]
		[TestCase("unknown-role")]
		public void Version_three_json_rejects_missing_or_invalid_role_data(string damage)
		{
			var state = new PatchInfo();
			state.AddInnerPrefixes("owner", Fix(nameof(Capture)));
			var json = JsonSerializer.Serialize(state);
			json = damage switch
			{
				"missing" => json.Replace("\"innerfinalizers\":[],", ""),
				"null" => json.Replace("\"innerfinalizers\":[]", "\"innerfinalizers\":null"),
				_ => json.Replace("\"innerfinalizers\":[]", "\"innerfinalizers\":[],\"innertranspilers\":[]")
			};
			var bytes = Encoding.ASCII.GetBytes("HARMONY-INFIX\0").Concat(new byte[] { 3, 1 }).Concat(Encoding.UTF8.GetBytes(json)).ToArray();
			Assert.Throws<SerializationException>(() => PatchInfoSerialization.Deserialize(bytes));
		}
#endif
	}
}
