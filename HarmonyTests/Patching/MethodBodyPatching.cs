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
	public class MethodBodyPatching : TestLogger
	{
		[MethodImpl(MethodImplOptions.NoInlining)]
		static int Target() => 1;
		[MethodImpl(MethodImplOptions.NoInlining)]
		static int TargetSnapshot() => 1;
		[MethodImpl(MethodImplOptions.NoInlining)]
		static int DefaultTarget() => 5;
		[MethodImpl(MethodImplOptions.NoInlining)]
		static int LateTarget() => 5;
		[MethodImpl(MethodImplOptions.NoInlining)]
		static int ClaimTarget() => 1;
		static int Source() => 23;
		static void RejectedPostfix(ref int __result) => __result += 100;
		[MethodImpl(MethodImplOptions.NoInlining)]
		static int ExportTarget() => 1;
		[MethodImpl(MethodImplOptions.NoInlining)]
		static int OwnedReverse() => 1;
		[MethodImpl(MethodImplOptions.NoInlining)]
		static int Reverse() => -1;
		[MethodImpl(MethodImplOptions.NoInlining)]
		static int ReverseSource() => 23;
		[MethodImpl(MethodImplOptions.NoInlining)]
		static int ReverseStandin() => -1;
		static void Postfix(ref int __result) => __result++;
		static MethodInfo dynamicExportCallback;
		static MethodInfo DynamicExportFactory(MethodBase original) => dynamicExportCallback;
		[MethodImpl(MethodImplOptions.NoInlining)]
		static int DynamicExportTarget(int value) => value;
		static MethodInfo Method(string name) => AccessTools.DeclaredMethod(typeof(MethodBodyPatching), name);
		static T DefinitionTarget<T>(T value) => typeof(T) == typeof(void) ? default : value;
		static class DefinitionClass<T>
		{
			internal static T Target(T value) => typeof(T) == typeof(void) ? default : value;
			internal static U Method<U>(T ignored, U value) => typeof(T) == typeof(U) ? value : value;
			internal static Type[] Tokens() => [typeof(DefinitionClass<T>), typeof(DefinitionClass<>), typeof(List<T>), typeof(List<>)];
		}
#if NETCOREAPP
		interface CovariantDefinition<out T> { T Target() => default; }
#endif
		class OwnMembers<T>
		{
			internal T Value;
			internal OwnMembers(T value) => Value = value;
			static T StaticSibling(T value) => value;
			internal T InstanceSibling() => Value;
			internal static object[] Target<U>(T value, U other)
			{
				var own = new OwnMembers<T>(StaticSibling(value));
				return [Identity(own.InstanceSibling()), other, typeof(T), typeof(U)];
			}
			internal static object[] Structured<U>(T value, U other)
			{
				var own = new OwnMembers<T>(StaticSibling(value));
				return [Identity(own.InstanceSibling()), other, typeof(T), typeof(U)];
			}
		}
#pragma warning disable CS0693 // Legal metadata: the regression needs distinct owners with the same parameter name.
		class Shadowed<T>
		{
			internal T Value;
			internal object Read<T>(T ignored) => Value;
			internal object Structured<T>(T ignored) => Identity(Value);
			internal T ReadValue() => Value;
		}
#pragma warning restore CS0693
		static object Identity(object value) => value;
		static Exception KeepException(Exception __exception) => __exception;

		[TestCase(false), TestCase(true)]
		public void Generic_snapshots_keep_shadowed_type_and_method_parameters_separate(bool structured)
		{
			if (!MethodBodyGenerator.HasDynamicIL) Assert.Ignore("Body export requires a modern Harmony target.");
			var original = AccessTools.DeclaredMethod(typeof(Shadowed<>), structured ? "Structured" : "Read");
			SetProvider(method => method == original, method => method.GetMethodBody(), (_, _, _) => { });
			var harmony = new Harmony("test.method.body.generic.shadowed." + structured);
			try
			{
				var processor = harmony.CreateProcessor(original).AddPostfix(new HarmonyMethod(Method(nameof(ObserveDefinition))));
				if (structured) processor.AddInnerFinalizer(new HarmonyMethod(Method(nameof(KeepException)))
				{ innerMethod = new InnerMethod(Method(nameof(Identity))) });
				Assert.That(processor.Patch(), Is.SameAs(original));
				var replacement = Snapshot();
				var closed = replacement.DeclaringType.MakeGenericType(typeof(string)).GetMethod(replacement.Name, AccessTools.allDeclared).MakeGenericMethod(typeof(int));
				Assert.That(closed.GetParameters()[0].ParameterType, Is.EqualTo(typeof(Shadowed<string>)));
				Assert.That(closed.GetParameters()[1].ParameterType, Is.EqualTo(typeof(int)));
				Assert.That(closed.Invoke(null, [new Shadowed<string> { Value = "kept" }, 7]), Is.EqualTo("kept"));
			}
			finally { harmony.UnpatchAll(harmony.Id); ClearProvider(); }
		}

		[TestCase(false), TestCase(true)]
		public void Generic_snapshots_execute_own_type_methods_and_constructors(bool structured)
		{
			if (!MethodBodyGenerator.HasDynamicIL) Assert.Ignore("Body export requires a modern Harmony target.");
			var original = AccessTools.DeclaredMethod(typeof(OwnMembers<>), structured ? "Structured" : "Target");
			SetProvider(method => method == original, method => method.GetMethodBody(), (_, _, _) => { });
			var harmony = new Harmony("test.method.body.generic.own.members." + structured);
			try
			{
				var processor = harmony.CreateProcessor(original).AddPostfix(new HarmonyMethod(Method(nameof(ObserveDefinition))));
				if (structured) processor.AddInnerFinalizer(new HarmonyMethod(Method(nameof(KeepException)))
				{ innerMethod = new InnerMethod(Method(nameof(Identity))) });
				Assert.That(processor.Patch(), Is.SameAs(original));
				var replacement = Snapshot();
				var closed = replacement.DeclaringType.MakeGenericType(typeof(string)).GetMethod(replacement.Name, AccessTools.allDeclared).MakeGenericMethod(typeof(int));
				Assert.That(closed.Invoke(null, ["kept", 7]), Is.EqualTo(new object[] { "kept", 7, typeof(string), typeof(int) }));
			}
			finally { harmony.UnpatchAll(harmony.Id); ClearProvider(); }
		}

		static class CloneOperandDefinition<T> { internal static Type Target() => typeof(CloneOperandDefinition<T>); }
		static IEnumerable<CodeInstruction> ReplaceTypeToken(IEnumerable<CodeInstruction> instructions)
			=> instructions.Select(code => code.opcode == OpCodes.Ldtoken ? code.Clone(typeof(CloneOperandDefinition<>)) : code);

		[Test]
		public void Replacing_a_type_token_operand_does_not_keep_its_old_instantiation()
		{
			if (!MethodBodyGenerator.HasDynamicIL) Assert.Ignore("Body export requires a modern Harmony target.");
			var original = AccessTools.DeclaredMethod(typeof(CloneOperandDefinition<>), "Target");
			SetProvider(method => method == original, method => method.GetMethodBody(), (_, _, _) => { });
			var harmony = new Harmony("test.method.body.generic.clone.operand");
			try
			{
				Assert.That(harmony.Patch(original, transpiler: new HarmonyMethod(Method(nameof(ReplaceTypeToken)))), Is.SameAs(original));
				var replacement = Snapshot();
				var closed = replacement.DeclaringType.MakeGenericType(typeof(int)).GetMethod(replacement.Name, AccessTools.allDeclared);
				Assert.That(closed.Invoke(null, null), Is.EqualTo(typeof(CloneOperandDefinition<>)));
			}
			finally { harmony.UnpatchAll(harmony.Id); ClearProvider(); }
		}

		static int DeclinedDefinition<T>() => 1;
		[Test]
		public void A_late_provider_does_not_take_over_a_generic_definition_pinned_to_the_default_detour()
		{
			var original = Method(nameof(DeclinedDefinition));
			Assert.That(HarmonyLib.MethodBodyPatching.Prepare(original), Is.True, "Without a provider the default detour is pinned.");
			SetProvider(_ => true, _ => throw new Exception("unexpected read"), (_, _, _) => throw new Exception("unexpected install"));
			try
			{
				Assert.That(HarmonyLib.MethodBodyPatching.Prepare(original), Is.False);
				Assert.That(HarmonyLib.MethodBodyPatching.IsProviderTarget(original), Is.False);
				Assert.That(HarmonyLib.MethodBodyPatching.IsDefinitionWide(original), Is.False);
			}
			finally { ClearProvider(); }
		}

		static int PinnedFamily<T>() => 1;
		static IEnumerable<CodeInstruction> FailingTranspiler(IEnumerable<CodeInstruction> instructions) => throw new InvalidOperationException("transpiler failed");
		[Test]
		public void A_failed_first_patch_does_not_pin_an_installer()
		{
			if (!MethodBodyGenerator.HasDynamicIL) Assert.Ignore("Body export requires a modern Harmony target.");
			var definition = Method(nameof(PinnedFamily));
			var closed = definition.MakeGenericMethod(typeof(int));
			var installs = 0;
			var selections = 0;
			SetProvider(method => { selections++; return method == definition; }, method => method.GetMethodBody(), (_, _, _) => installs++);
			var harmony = new Harmony("test.method.body.failed.pin");
			try
			{
				Assert.Catch(() => harmony.Patch(closed, transpiler: new HarmonyMethod(Method(nameof(FailingTranspiler)))));
				Assert.That(Harmony.GetPatchInfo(closed), Is.Null);
				Assert.That(HarmonyLib.MethodBodyPatching.IsProviderTarget(closed), Is.False);
				Assert.That(HarmonyLib.MethodBodyPatching.IsProviderTarget(definition), Is.False);
				Assert.That(HarmonyLib.MethodBodyPatching.Instantiations(definition), Is.Empty);
				var previousSelections = selections;
				Assert.DoesNotThrow(() => harmony.Patch(definition, postfix: new HarmonyMethod(Method(nameof(Postfix)))));
				Assert.That(HarmonyLib.MethodBodyPatching.IsProviderTarget(definition), Is.True);
				Assert.That(installs, Is.EqualTo(1));
				Assert.That(selections, Is.GreaterThan(previousSelections), "A retry must reconsider eligibility.");
			}
			finally { harmony.UnpatchAll(harmony.Id); ClearProvider(); }
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		static int SourceOnlyTarget() => 1;
		[MethodImpl(MethodImplOptions.NoInlining)]
		static int InstallerOnlyTarget() => 1;
		static int SourceOnlyDefinition<T>() => 1;

		[Test]
		public void A_body_source_without_an_installer_keeps_the_default_detour()
		{
			var original = Method(nameof(SourceOnlyTarget));
			var definition = Method(nameof(SourceOnlyDefinition));
			HarmonyLib.MethodBodyPatching.SetEligibility(method => method == original || method == definition);
			HarmonyLib.MethodBodyPatching.SetBodySource(_ => Method(nameof(Source)).GetMethodBody());
			var harmony = new Harmony("test.method.body.source.only");
			try
			{
				harmony.Patch(original, postfix: new HarmonyMethod(Method(nameof(Postfix))));
				Assert.That(SourceOnlyTarget(), Is.EqualTo(24), "The default detour installs a body read from the registered source.");
				Assert.That(HarmonyLib.MethodBodyPatching.IsProviderTarget(original), Is.True);
				Assert.That(HarmonyLib.MethodBodyPatching.Prepare(definition), Is.True);
				Assert.That(HarmonyLib.MethodBodyPatching.IsDefinitionWide(definition), Is.False, "Definition-wide installation needs an installer.");
			}
			finally { harmony.UnpatchAll(harmony.Id); ClearProvider(); harmony.CreateProcessor(original).Patch(); }
		}

		[Test]
		public void An_installer_without_a_body_source_reads_by_reflection()
		{
			var original = Method(nameof(InstallerOnlyTarget));
			var installed = new List<MethodInfo>();
			HarmonyLib.MethodBodyPatching.SetEligibility(method => method == original);
			HarmonyLib.MethodBodyPatching.SetInstaller((method, replacement, _) =>
			{
				PatchTools.DetourMethodDefault(method, replacement);
				installed.Add(replacement);
			});
			var harmony = new Harmony("test.method.body.installer.only");
			try
			{
				var replacement = harmony.Patch(original, postfix: new HarmonyMethod(Method(nameof(Postfix))));
				Assert.That(InstallerOnlyTarget(), Is.EqualTo(2));
				Assert.That(installed.Single(), Is.SameAs(replacement));
			}
			finally { harmony.UnpatchAll(harmony.Id); ClearProvider(); }
		}

		[Test]
		public void Each_part_registers_once_and_null_keeps_the_default()
		{
			try
			{
				Assert.DoesNotThrow(() => HarmonyLib.MethodBodyPatching.SetBodySource(null));
				HarmonyLib.MethodBodyPatching.SetBodySource(method => method.GetMethodBody());
				HarmonyLib.MethodBodyPatching.SetEligibility(_ => false);
				Assert.Throws<InvalidOperationException>(() => HarmonyLib.MethodBodyPatching.SetEligibility(_ => false));
				Assert.Throws<InvalidOperationException>(() => HarmonyLib.MethodBodyPatching.SetBodySource(method => method.GetMethodBody()));
				Assert.DoesNotThrow(() => HarmonyLib.MethodBodyPatching.SetInstaller(null));
				Assert.DoesNotThrow(() => HarmonyLib.MethodBodyPatching.SetInstaller((_, _, _) => { }));
				Assert.Throws<InvalidOperationException>(() => HarmonyLib.MethodBodyPatching.SetInstaller((_, _, _) => { }));
			}
			finally { ClearProvider(); }
		}

		[TestCase(2, false), TestCase(3, false), TestCase(4, false), TestCase(4, true)]
		public void Shared_protocol_reuses_compatible_prefixes_without_mutating_the_record(int length, bool incompatible)
		{
			_ = HarmonyLib.MethodBodyPatching.ProtocolLock;
			const string key = "Harmony.MethodBodyPatching.v1";
			var previous = AppDomain.CurrentDomain.GetData(key);
			var record = new object[length];
			record[1] = incompatible ? new object() : new Dictionary<MethodBase, bool>();
			var extra = new object();
			if (length > 3) record[3] = extra;
			AppDomain.CurrentDomain.SetData(key, record);
			try
			{
				if (incompatible)
					Assert.Throws<NotSupportedException>(() => HarmonyLib.MethodBodyPatching.Shared());
				else
				{
					Assert.That(HarmonyLib.MethodBodyPatching.Shared(), Is.SameAs(record));
					Assert.That(record.Length, Is.EqualTo(length));
					if (length > 3) Assert.That(record[3], Is.SameAs(extra));
				}
			}
			finally { AppDomain.CurrentDomain.SetData(key, previous); }
		}

		static int definitionCalls;
		static void ObserveDefinition() => definitionCalls++;
		static int FixedCallbackTarget<T>() => 1;
		static Type fixedCallbackType;
		static void FixedCallback<T>() => fixedCallbackType = typeof(T);
		static MethodInfo FixedCallbackFactory(MethodBase _) => Method(nameof(FixedCallback)).MakeGenericMethod(typeof(string));

		[Test]
		public void Fixed_generic_callback_instantiations_use_factories_to_survive_rebuilds()
		{
			if (!MethodBodyGenerator.HasDynamicIL) Assert.Ignore("Body export requires a modern Harmony target.");
			var original = Method(nameof(FixedCallbackTarget));
			var installs = 0;
			SetProvider(method => method == original, method => method.GetMethodBody(), (_, _, _) => installs++);
			var harmony = new Harmony("test.method.body.generic.fixed");
			try
			{
				Assert.Throws<NotSupportedException>(() => harmony.Patch(original, postfix: new HarmonyMethod(Method(nameof(FixedCallback)).MakeGenericMethod(typeof(string)))));
				Assert.That(installs, Is.Zero);
				Assert.That(harmony.Patch(original, postfix: new HarmonyMethod(Method(nameof(FixedCallbackFactory)))), Is.SameAs(original));
				var first = Snapshot();
				fixedCallbackType = null;
				Assert.That(first.MakeGenericMethod(typeof(int)).Invoke(null, null), Is.EqualTo(1));
				Assert.That(fixedCallbackType, Is.EqualTo(typeof(string)));
				Assert.That(harmony.CreateProcessor(original).Patch(), Is.SameAs(original));
				var rebuilt = Snapshot();
				fixedCallbackType = null;
				rebuilt.MakeGenericMethod(typeof(int)).Invoke(null, null);
				Assert.That(fixedCallbackType, Is.EqualTo(typeof(string)));
			}
			finally { harmony.UnpatchAll(harmony.Id); ClearProvider(); }
		}
		static IEnumerable<CodeInstruction> CloneDefinitionInstructions(IEnumerable<CodeInstruction> instructions) => instructions.Select(code => new CodeInstruction(code));
		static int ForeignParameterDefinition<T>() => 1;
		static IEnumerable<CodeInstruction> ForeignParameterInstructions(IEnumerable<CodeInstruction> instructions)
		{
			yield return new CodeInstruction(OpCodes.Ldtoken, typeof(List<>).GetGenericArguments()[0]);
			yield return new CodeInstruction(OpCodes.Pop);
			foreach (var instruction in instructions) yield return instruction;
		}
		static int ForeignDefinitionTarget<T>() => 1;
		static IEnumerable<CodeInstruction> ForeignDefinitionInstructions(IEnumerable<CodeInstruction> instructions)
		{
			yield return new CodeInstruction(OpCodes.Ldnull);
			yield return new CodeInstruction(OpCodes.Castclass, typeof(List<>));
			yield return new CodeInstruction(OpCodes.Pop);
			foreach (var instruction in instructions) yield return instruction;
		}

		[Test]
		public void Generic_type_tokens_preserve_definition_and_instantiation_after_instruction_copying()
		{
			if (!MethodBodyGenerator.HasDynamicIL) Assert.Ignore("Body export requires a modern Harmony target.");
			var original = AccessTools.DeclaredMethod(typeof(DefinitionClass<>), "Tokens");
			var imported = new List<object>();
			SetProvider(method => method == original, method => method.GetMethodBody(),
				(_, _, write) => write(value => { imported.Add(value); return value is byte[]? 0x11000001 : 0x1b000001; }));
			var harmony = new Harmony("test.method.body.generic.tokens");
			try
			{
				Assert.That(harmony.Patch(original, transpiler: new HarmonyMethod(Method(nameof(CloneDefinitionInstructions)))), Is.SameAs(original));
				var replacement = Snapshot();
				foreach (var argument in new[] { typeof(int), typeof(string) })
				{
					var closed = replacement.DeclaringType.MakeGenericType(argument).GetMethod(replacement.Name, AccessTools.allDeclared);
					Assert.That(closed.Invoke(null, null), Is.EqualTo(new[] { typeof(DefinitionClass<>).MakeGenericType(argument), typeof(DefinitionClass<>), typeof(List<>).MakeGenericType(argument), typeof(List<>) }));
				}
				Assert.That(imported.OfType<KeyValuePair<Type, bool>>().Any(pair => pair.Key == typeof(DefinitionClass<>) && pair.Value), Is.True);
				Assert.That(imported.OfType<Type>(), Does.Contain(typeof(DefinitionClass<>)));
			}
			finally { harmony.UnpatchAll(harmony.Id); ClearProvider(); }
		}

		[Test]
		public void Generic_operands_cannot_silently_rebind_a_foreign_parameter()
		{
			if (!MethodBodyGenerator.HasDynamicIL) Assert.Ignore("Body export requires a modern Harmony target.");
			var original = Method(nameof(ForeignParameterDefinition));
			var installs = 0;
			SetProvider(method => method == original, method => method.GetMethodBody(), (_, _, _) => installs++);
			var harmony = new Harmony("test.method.body.generic.foreign");
			try
			{
				var error = Assert.Catch(() => harmony.Patch(original, transpiler: new HarmonyMethod(Method(nameof(ForeignParameterInstructions)))));
				Assert.That(error.GetBaseException(), Is.TypeOf<ArgumentException>());
				Assert.That(installs, Is.Zero);
				Assert.That(Harmony.GetPatchInfo(original), Is.Null);
			}
			finally { ClearProvider(); }
		}

		[Test]
		public void Foreign_open_type_operands_do_not_acquire_the_targets_generic_arguments()
		{
			if (!MethodBodyGenerator.HasDynamicIL) Assert.Ignore("Body export requires a modern Harmony target.");
			var original = Method(nameof(ForeignDefinitionTarget));
			var installs = 0;
			SetProvider(method => method == original, method => method.GetMethodBody(), (_, _, _) => installs++);
			try
			{
				var error = Assert.Catch(() => new Harmony("test.method.body.generic.foreign.type").Patch(original,
					transpiler: new HarmonyMethod(Method(nameof(ForeignDefinitionInstructions)))));
				Assert.That(error.GetBaseException(), Is.TypeOf<NotSupportedException>());
				Assert.That(installs, Is.Zero);
			}
			finally { ClearProvider(); }
		}

		[Test]
		public void A_provider_cannot_accept_a_partially_open_generic_target()
		{
			var target = AccessTools.DeclaredMethod(typeof(DefinitionClass<string>), "Method");
			var reads = 0;
			SetProvider(method => method == target, method => { reads++; return method.GetMethodBody(); }, (_, _, _) => { });
			try
			{
				Assert.Throws<NotSupportedException>(() => new Harmony("test.method.body.generic.partial").Patch(target));
				Assert.That(reads, Is.Zero);
			}
			finally { ClearProvider(); }
		}

#if NETCOREAPP
		[Test]
		public void Covariant_declaring_types_use_invariant_snapshot_holders()
		{
			if (!MethodBodyGenerator.HasDynamicIL) Assert.Ignore("Body export requires a modern Harmony target.");
			var original = AccessTools.DeclaredMethod(typeof(CovariantDefinition<>), "Target");
			SetProvider(method => method == original, method => method.GetMethodBody(), (_, _, _) => { });
			var harmony = new Harmony("test.method.body.generic.variance");
			try
			{
				Assert.That(harmony.Patch(original), Is.SameAs(original));
				var replacement = Snapshot();
				var closed = replacement.DeclaringType.MakeGenericType(typeof(int)).GetMethod(replacement.Name, AccessTools.allDeclared);
				Assert.That(closed.Invoke(null, [null]), Is.EqualTo(0));
			}
			finally { harmony.UnpatchAll(harmony.Id); ClearProvider(); }
		}
#endif
		static void ResetDefinition<T>(ref T __result) => __result = default;
		static void ReferenceOnlyDefinition<T>(ref T __result) where T : class => __result = default;
		static T CallbackDefinition<T>(T value) => value;
		static T ConstrainedDefinition<T>(T value) where T : struct, IComparable<T> => value.CompareTo(value) == 0 ? value : default;
		[MethodImpl(MethodImplOptions.NoInlining)] static int DefinitionFirst<T>() => 1;
		[MethodImpl(MethodImplOptions.NoInlining)] static int ClosedFirst<T>() => 1;
		// Each test patches its own target. A target's patch record survives unpatching and would mark it as foreign to a later provider.
		static T Family<T>(T value) => value;
		static T FamilyCompose<T>(T value) => value;
		static T FamilyReject<T>(T value) => value;
		static void FamilyIntPostfix(ref int __result) => __result += 100;
		static void FamilyStringPostfix(string value, ref string __result) => __result = value + "!";
		static void FamilyDefinitionPostfix<T>(ref T __result) => definitionCalls++;
		static bool FamilyIntPrefix(int value, ref int __result) { __result = value * 2; return false; }
		static string FamilyStringPassthrough(string result) => result + "?";
		static int familyFinalized;
		static Exception FamilyIntFinalizer(Exception __exception) { familyFinalized++; return __exception; }
		class Holder<T>
		{
			internal T Value;
			internal Holder(T value) => Value = value;
			internal T Read(T fallback) => Value;
		}
		static void HolderIntPostfix(Holder<int> __instance, int fallback, ref int __result, int ___Value) => __result = __instance.Value + fallback + ___Value + 100;
		static void HolderStringPostfix(Holder<string> __instance, ref string __result, MethodBase __originalMethod)
			=> __result = __instance.Value + __originalMethod.DeclaringType.GetGenericArguments()[0].Name;
		static T BodyView<T>(T value) => value;

		[Test]
		public void Constructed_views_read_the_selected_definitions_baseline()
		{
			if (!MethodBodyGenerator.HasDynamicIL) Assert.Ignore("Body export requires a modern Harmony target.");
			var definition = Method(nameof(BodyView));
			var closed = definition.MakeGenericMethod(typeof(int));
			var reads = new List<MethodBase>();
			SetProvider(method => method == definition, method => { reads.Add(method); return method.GetMethodBody(); }, (_, _, _) => { });
			var harmony = new Harmony("test.method.body.generic.body.view");
			try
			{
				harmony.Patch(definition);
				PatchProcessor.GetOriginalInstructions(closed);
				Assert.That(reads, Does.Contain(closed));
			}
			finally { harmony.UnpatchAll(harmony.Id); ClearProvider(); }
		}

		[Test]
		public void Closed_instantiations_install_through_their_definition()
		{
			if (!MethodBodyGenerator.HasDynamicIL) Assert.Ignore("Body export requires a modern Harmony target.");
			var definition = Method(nameof(Family));
			var closedInt = definition.MakeGenericMethod(typeof(int));
			var closedString = definition.MakeGenericMethod(typeof(string));
			var installs = new List<(MethodBase method, MethodInfo replacement)>();
			SetProvider(method => method == definition, method => method.GetMethodBody(),
				(method, replacement, write) => { installs.Add((method, replacement)); write(FakeToken); });
			var harmony = new Harmony("test.method.body.family");
			try
			{
				Assert.That(harmony.Patch(closedInt, postfix: new HarmonyMethod(Method(nameof(FamilyIntPostfix)))), Is.SameAs(closedInt));
				Assert.That(harmony.Patch(closedString, postfix: new HarmonyMethod(Method(nameof(FamilyStringPostfix)))), Is.SameAs(closedString));
				Assert.That(installs, Has.Count.EqualTo(2));
				Assert.That(installs.All(install => ReferenceEquals(install.method, definition) && install.replacement is null), Is.True,
					"An instantiation installs its definition, which has no callable copy.");
				Assert.That(Harmony.GetPatchInfo(closedInt).Postfixes, Has.Count.EqualTo(1));
				Assert.That(Harmony.GetPatchInfo(definition), Is.Null);
				var snapshot = Snapshot();
				Assert.That(snapshot.MakeGenericMethod(typeof(int)).Invoke(null, [42]), Is.EqualTo(142));
				Assert.That(snapshot.MakeGenericMethod(typeof(string)).Invoke(null, ["value"]), Is.EqualTo("value!"));
				Assert.That(snapshot.MakeGenericMethod(typeof(long)).Invoke(null, [7L]), Is.EqualTo(7L));
				harmony.Unpatch(closedInt, HarmonyPatchType.All, harmony.Id);
				snapshot = Snapshot();
				Assert.That(snapshot.MakeGenericMethod(typeof(int)).Invoke(null, [42]), Is.EqualTo(42));
				Assert.That(snapshot.MakeGenericMethod(typeof(string)).Invoke(null, ["value"]), Is.EqualTo("value!"));
			}
			finally { harmony.UnpatchAll(harmony.Id); ClearProvider(); }
		}

		[Test]
		public void Definition_and_instantiation_patches_compose_in_one_body()
		{
			if (!MethodBodyGenerator.HasDynamicIL) Assert.Ignore("Body export requires a modern Harmony target.");
			var definition = Method(nameof(FamilyCompose));
			var closedInt = definition.MakeGenericMethod(typeof(int));
			var closedString = definition.MakeGenericMethod(typeof(string));
			SetProvider(method => method == definition, method => method.GetMethodBody(), (_, _, write) => write(FakeToken));
			var harmony = new Harmony("test.method.body.family.compose");
			try
			{
				harmony.Patch(definition, postfix: new HarmonyMethod(Method(nameof(FamilyDefinitionPostfix))));
				harmony.Patch(closedInt, prefix: new HarmonyMethod(Method(nameof(FamilyIntPrefix))),
					postfix: new HarmonyMethod(Method(nameof(FamilyIntPostfix))), finalizer: new HarmonyMethod(Method(nameof(FamilyIntFinalizer))));
				harmony.Patch(closedString, postfix: new HarmonyMethod(Method(nameof(FamilyStringPassthrough))));
				var snapshot = Snapshot();
				definitionCalls = 0;
				familyFinalized = 0;
				Assert.That(snapshot.MakeGenericMethod(typeof(int)).Invoke(null, [21]), Is.EqualTo(142), "prefix result, skipped original, postfix");
				Assert.That(definitionCalls, Is.EqualTo(1));
				Assert.That(familyFinalized, Is.EqualTo(1));
				Assert.That(snapshot.MakeGenericMethod(typeof(string)).Invoke(null, ["value"]), Is.EqualTo("value?"));
				Assert.That(snapshot.MakeGenericMethod(typeof(long)).Invoke(null, [7L]), Is.EqualTo(7L));
				Assert.That(definitionCalls, Is.EqualTo(3));
				Assert.That(familyFinalized, Is.EqualTo(1));
				Assert.That(Harmony.GetPatchInfo(definition).Postfixes, Has.Count.EqualTo(1));
				Assert.That(Harmony.GetPatchInfo(closedInt).Prefixes, Has.Count.EqualTo(1));
			}
			finally { harmony.UnpatchAll(harmony.Id); ClearProvider(); }
		}

		[Test]
		public void Instantiation_callbacks_bridge_closed_instance_argument_result_and_field_types()
		{
			if (!MethodBodyGenerator.HasDynamicIL) Assert.Ignore("Body export requires a modern Harmony target.");
			var definition = AccessTools.DeclaredMethod(typeof(Holder<>), nameof(Holder<int>.Read));
			var closedInt = AccessTools.DeclaredMethod(typeof(Holder<int>), nameof(Holder<int>.Read));
			var closedString = AccessTools.DeclaredMethod(typeof(Holder<string>), nameof(Holder<string>.Read));
			SetProvider(method => method == definition, method => method.GetMethodBody(), (_, _, write) => write(FakeToken));
			var harmony = new Harmony("test.method.body.family.bridge");
			try
			{
				harmony.Patch(closedInt, postfix: new HarmonyMethod(Method(nameof(HolderIntPostfix))));
				harmony.Patch(closedString, postfix: new HarmonyMethod(Method(nameof(HolderStringPostfix))));
				var snapshot = Snapshot();
				MethodInfo Closed(Type argument) => snapshot.DeclaringType.MakeGenericType(argument).GetMethod(snapshot.Name, AccessTools.allDeclared);
				Assert.That(Closed(typeof(int)).Invoke(null, [new Holder<int>(5), 7]), Is.EqualTo(5 + 7 + 5 + 100));
				Assert.That(Closed(typeof(string)).Invoke(null, [new Holder<string>("v"), "f"]), Is.EqualTo("vString"));
				Assert.That(Closed(typeof(long)).Invoke(null, [new Holder<long>(3L), 1L]), Is.EqualTo(3L));
			}
			finally { harmony.UnpatchAll(harmony.Id); ClearProvider(); }
		}

		static IEnumerable<CodeInstruction> AddHundredBeforeReturn(IEnumerable<CodeInstruction> instructions)
		{
			foreach (var code in instructions)
			{
				if (code.opcode == OpCodes.Ret) { yield return new CodeInstruction(OpCodes.Ldc_I4, 100); yield return new CodeInstruction(OpCodes.Add); }
				yield return code;
			}
		}
		static void FamilyMarkPostfix(ref string __result) => __result += "!";
		static int ClosedInner(int value) => value;
		[MethodImpl(MethodImplOptions.NoInlining)] static int ClosedOuter<T>(int value) => ClosedInner(value) + 1;

		[Test]
		public void An_instantiation_with_a_transpiler_owns_a_closed_body()
		{
			if (!MethodBodyGenerator.HasDynamicIL) Assert.Ignore("Body export requires a modern Harmony target.");
			var definition = Method(nameof(FamilyReject));
			var closedInt = definition.MakeGenericMethod(typeof(int));
			var closedString = definition.MakeGenericMethod(typeof(string));
			SetProvider(method => method == definition, method => method.GetMethodBody(), (_, _, write) => write(FakeToken));
			var harmony = new Harmony("test.method.body.family.transpiler");
			try
			{
				harmony.Patch(definition, postfix: new HarmonyMethod(Method(nameof(FamilyDefinitionPostfix))));
				harmony.Patch(closedInt, postfix: new HarmonyMethod(Method(nameof(FamilyIntPostfix))), transpiler: new HarmonyMethod(Method(nameof(AddHundredBeforeReturn))));
				harmony.Patch(closedString, postfix: new HarmonyMethod(Method(nameof(FamilyMarkPostfix))));
				Assert.That(Harmony.GetPatchInfo(closedInt).Transpilers, Has.Count.EqualTo(1));
				Assert.That(Harmony.GetPatchInfo(definition).Transpilers, Is.Empty);
				definitionCalls = 0;
				Assert.That(Snapshot().MakeGenericMethod(typeof(int)).Invoke(null, [1]), Is.EqualTo(201), "The closed body carries the transpiler, the definition's postfix and the instantiation's postfix.");
				Assert.That(definitionCalls, Is.EqualTo(1));
				Assert.That(Snapshot().MakeGenericMethod(typeof(string)).Invoke(null, ["a"]), Is.EqualTo("a!"), "A sibling keeps the guarded family body.");
				Assert.That(definitionCalls, Is.EqualTo(2));
				var value = new object();
				Assert.That(Snapshot().MakeGenericMethod(typeof(object)).Invoke(null, [value]), Is.SameAs(value));
				Assert.That(definitionCalls, Is.EqualTo(3));
				Assert.That(PatchProcessor.GetCurrentInstructions(closedInt).Any(code => code.opcode == OpCodes.Ldc_I4 && code.operand is 100), Is.True, "The body view of the instantiation includes its transpilers.");
				Assert.That(PatchProcessor.GetCurrentInstructions(closedString).Any(code => code.opcode == OpCodes.Ldc_I4), Is.False);
				harmony.Unpatch(closedInt, HarmonyPatchType.Transpiler, harmony.Id);
				definitionCalls = 0;
				Assert.That(Snapshot().MakeGenericMethod(typeof(int)).Invoke(null, [1]), Is.EqualTo(101), "Without transpilers the instantiation returns to the guarded family body.");
				Assert.That(definitionCalls, Is.EqualTo(1));
			}
			finally { harmony.UnpatchAll(harmony.Id); ClearProvider(); }
		}

		[Test]
		public void An_instantiation_with_an_Infix_patch_owns_a_closed_body()
		{
			if (!MethodBodyGenerator.HasDynamicIL) Assert.Ignore("Body export requires a modern Harmony target.");
			var definition = Method(nameof(ClosedOuter));
			var closedInt = definition.MakeGenericMethod(typeof(int));
			SetProvider(method => method == definition, method => method.GetMethodBody(), (_, _, write) => write(FakeToken));
			var harmony = new Harmony("test.method.body.family.infix");
			try
			{
				harmony.CreateProcessor(closedInt).AddInnerPostfix(new HarmonyMethod(Method(nameof(Postfix))) { innerMethod = new InnerMethod(Method(nameof(ClosedInner))) }).Patch();
				Assert.That(Harmony.GetPatchInfo(closedInt).InnerPostfixes, Has.Count.EqualTo(1));
				Assert.That(Snapshot().MakeGenericMethod(typeof(int)).Invoke(null, [5]), Is.EqualTo(7));
				Assert.That(Snapshot().MakeGenericMethod(typeof(string)).Invoke(null, [5]), Is.EqualTo(6));
			}
			finally { harmony.UnpatchAll(harmony.Id); ClearProvider(); }
		}

		[TestCase(false)]
		[TestCase(true)]
		public void Definition_and_closed_targets_share_one_installer(bool closedFirst)
		{
			if (!MethodBodyGenerator.HasDynamicIL) Assert.Ignore("Body export requires a modern Harmony target.");
			var definition = Method(closedFirst ? nameof(ClosedFirst) : nameof(DefinitionFirst));
			var closed = definition.MakeGenericMethod(typeof(int));
			var postfix = new HarmonyMethod(Method(nameof(Postfix)));
			var harmony = new Harmony("test.method.body.generic.conflict." + closedFirst);
			var installs = 0;
			try
			{
				if (closedFirst) harmony.Patch(closed, postfix: postfix);
				SetProvider(method => method == definition, method => method.GetMethodBody(), (_, _, write) => { installs++; write(FakeToken); });
				if (closedFirst)
				{
					// The instantiation already uses the default detour. The definition cannot take it over.
					Assert.Throws<NotSupportedException>(() => harmony.Patch(definition, postfix: postfix));
					Assert.That(installs, Is.Zero);
					Assert.That(closed.Invoke(null, null), Is.EqualTo(2));
				}
				else
				{
					harmony.Patch(definition, postfix: postfix);
					harmony.Patch(closed, postfix: postfix);
					Assert.That(installs, Is.EqualTo(2), "An instantiation joins its definition's installation.");
					Assert.That(Harmony.GetPatchInfo(closed).Postfixes, Has.Count.EqualTo(1));
					Assert.That(Snapshot().MakeGenericMethod(typeof(int)).Invoke(null, null), Is.EqualTo(3));
					Assert.That(Snapshot().MakeGenericMethod(typeof(string)).Invoke(null, null), Is.EqualTo(2));
				}
			}
			finally { harmony.UnpatchAll(harmony.Id); ClearProvider(); }
		}

		[TestCase(0)]
		[TestCase(1)]
		[TestCase(2)]
		public void Provider_replacements_preserve_open_generic_definitions(int shape)
		{
			if (!MethodBodyGenerator.HasDynamicIL) Assert.Ignore("Body export requires a modern Harmony target.");
			var original = shape == 0 ? Method(nameof(DefinitionTarget))
				: AccessTools.DeclaredMethod(typeof(DefinitionClass<>), shape == 1 ? "Target" : "Method");
			var installs = 0;
			SetProvider(method => method == original, method => method.GetMethodBody(),
				(method, replacement, writeBody) =>
				{
					Assert.That(replacement, Is.Null, "A definition-wide target has no callable copy.");
					var body = writeBody(value => value is byte[]? 0x11000001 : value is Type ? 0x1b000001 : 0x0a000001);
					Assert.That(body.Length, Is.GreaterThan(12));
					installs++;
				});
			var harmony = new Harmony("test.method.body.generic." + shape);
			try
			{
				var returned = harmony.Patch(original, postfix: new HarmonyMethod(Method(nameof(ObserveDefinition))));
				Assert.That(returned, Is.SameAs(original), "The open definition stands in for the missing callable copy.");
				var replacement = Snapshot();
				Assert.That(replacement.ContainsGenericParameters, Is.True);
				foreach (var argument in new[] { typeof(int), typeof(string) })
				{
					var closed = replacement;
					if (closed.DeclaringType.ContainsGenericParameters)
						closed = closed.DeclaringType.MakeGenericType(argument).GetMethod(closed.Name, AccessTools.allDeclared);
					if (closed.IsGenericMethodDefinition) closed = closed.MakeGenericMethod(argument);
					object value = argument == typeof(int) ? 42 : "value";
					definitionCalls = 0;
					Assert.That(closed.Invoke(null, shape == 2 ? [value, value] : [value]), Is.EqualTo(value));
					Assert.That(definitionCalls, Is.EqualTo(1));
				}
				Assert.That(Harmony.GetOriginalMethod(returned), Is.EqualTo(original));
				harmony.UnpatchAll(harmony.Id);
				Assert.That(installs, Is.EqualTo(2));
			}
			finally { harmony.UnpatchAll(harmony.Id); ClearProvider(); }
		}

		[TestCase(false)]
		[TestCase(true)]
		public void Generic_callbacks_bind_to_the_definition_and_preserve_constraints(bool constrained)
		{
			if (!MethodBodyGenerator.HasDynamicIL) Assert.Ignore("Body export requires a modern Harmony target.");
			var original = Method(constrained ? nameof(ConstrainedDefinition) : nameof(CallbackDefinition));
			SetProvider(method => method == original, method => method.GetMethodBody(), (_, _, write) => write(FakeToken));
			var harmony = new Harmony("test.method.body.generic.callback");
			try
			{
				Assert.That(harmony.Patch(original, postfix: new HarmonyMethod(Method(nameof(ResetDefinition)))), Is.SameAs(original));
				var replacement = Snapshot();
				Assert.That(replacement.MakeGenericMethod(typeof(int)).Invoke(null, [42]), Is.EqualTo(0));
				if (constrained)
					Assert.Throws<ArgumentException>(() => replacement.MakeGenericMethod(typeof(string)));
				else
				{
					Assert.That(replacement.MakeGenericMethod(typeof(string)).Invoke(null, ["value"]), Is.Null);
					Assert.Throws<ArgumentException>(() => harmony.Patch(original, postfix: new HarmonyMethod(Method(nameof(ReferenceOnlyDefinition)))));
					Assert.That(Harmony.GetPatchInfo(original).Postfixes, Has.Count.EqualTo(1));
				}
			}
			finally { harmony.UnpatchAll(harmony.Id); ClearProvider(); }
		}

		[TestCase(HarmonyReversePatchType.Original)]
		[TestCase(HarmonyReversePatchType.Snapshot)]
		public void Custom_source_and_installer_keep_rebuild_readers_reverse_and_failure_consistent(HarmonyReversePatchType reverseType)
		{
			var original = Method(reverseType == HarmonyReversePatchType.Snapshot ? nameof(TargetSnapshot) : nameof(Target));
			Func<int> call = reverseType == HarmonyReversePatchType.Snapshot ? TargetSnapshot : Target;
			var source = Method(nameof(Source));
			var installed = new List<MethodInfo>();
			var reads = 0;
			var fail = false;


			bool accepts(MethodBase method) => method == original;
			MethodBody read(MethodBase method) { reads++; return source.GetMethodBody(); }
			void install(MethodBase method, MethodInfo replacement, Func<Func<object, int>, byte[]> writeBody)
			{
				if (fail) throw new InvalidOperationException("provider refused before mutation");
				PatchTools.DetourMethodDefault(method, replacement);
				installed.Add(replacement);
			}
			SetProvider(accepts, read, install);
			var harmony = new Harmony("test.method.body.provider");
			try
			{
				var replacement = harmony.Patch(original, postfix: new HarmonyMethod(Method(nameof(Postfix))));
				Assert.That(reads, Is.EqualTo(1), "Locals and instructions must come from one body snapshot.");
				Assert.That(call(), Is.EqualTo(24));
				Assert.That(installed.Single(), Is.SameAs(replacement));
				Assert.That(Harmony.GetOriginalMethod(replacement), Is.EqualTo(original));
				var expected = PatchProcessor.ReadMethodBody(source).Select(code => code.Key).ToArray();
				var previousReads = reads;
				CollectionAssert.AreEqual(expected, PatchProcessor.ReadMethodBody(original).Select(code => code.Key).ToArray());
				Assert.That(reads, Is.GreaterThan(previousReads));
				Assert.That(PatchProcessor.GetOriginalInstructions(original).Select(code => code.opcode),
					Is.EqualTo(PatchProcessor.GetOriginalInstructions(source).Select(code => code.opcode)));
				harmony.CreateReversePatcher(original, new HarmonyMethod(Method(nameof(Reverse)))).Patch(reverseType);
				Assert.That(Reverse(), Is.EqualTo(23));
				var before = Harmony.GetPatchInfo(original).Postfixes.Count;
				fail = true;
				Assert.Throws<InvalidOperationException>(() => harmony.Patch(original, postfix: new HarmonyMethod(Method(nameof(RejectedPostfix)))));
				Assert.That(Harmony.GetPatchInfo(original).Postfixes.Any(patch => patch.PatchMethod == Method(nameof(RejectedPostfix))), Is.False);
				Assert.That(call(), Is.EqualTo(24));
				Assert.That(Harmony.GetPatchInfo(original).Postfixes.Count, Is.EqualTo(before));
				fail = false;
				harmony.UnpatchAll(harmony.Id);
				Assert.That(call(), Is.EqualTo(23));
				Assert.That(installed, Has.Count.EqualTo(2));
				Assert.That(reads, Is.GreaterThan(3));
				harmony.Patch(Method(nameof(DefaultTarget)), postfix: new HarmonyMethod(Method(nameof(Postfix))));
				Assert.That(DefaultTarget(), Is.EqualTo(6));
				Assert.That(installed, Has.Count.EqualTo(2), "A declined method must keep the existing detour path.");
			}
			finally
			{
				fail = false;
				harmony.UnpatchAll(harmony.Id);
				ClearProvider();
				harmony.CreateProcessor(original).Patch();
			}
		}

		[Test]
		public void A_reverse_patch_stand_in_installs_through_the_provider()
		{
			var original = Method(nameof(ReverseSource));
			var standin = Method(nameof(ReverseStandin));
			var installed = new List<MethodBase>();
			SetProvider(method => method == standin, method => method.GetMethodBody(),
				(method, replacement, _) => { installed.Add(method); PatchTools.DetourMethodDefault(method, replacement); });
			var harmony = new Harmony("test.method.body.reverse.provider");
			try
			{
				harmony.CreateReversePatcher(original, new HarmonyMethod(standin)).Patch();
				Assert.That(installed, Is.EqualTo(new[] { standin }), "The host installs the stand-in it owns.");
				Assert.That(ReverseStandin(), Is.EqualTo(23));
			}
			finally { ClearProvider(); }
		}

		[TearDown]
		public void ResetProvider()
		{
			ClearProvider();
			observed = null;
		}

		[Test]
		public void Teardown_clears_partial_registration_and_the_snapshot()
		{
			HarmonyLib.MethodBodyPatching.SetEligibility(_ => true);
			observed = () => Method(nameof(Target));
			ResetProvider();
			Assert.That(observed, Is.Null);
			Assert.DoesNotThrow(() => HarmonyLib.MethodBodyPatching.SetEligibility(_ => false));
		}

		static void ClearProvider() => HarmonyLib.MethodBodyPatching.ClearProvider();
		// A fake host importer: a signature, a type or an instantiation, or a member token.
		static int FakeToken(object value) => value is byte[]? 0x11000001 : value is Type || value is KeyValuePair<Type, bool> ? 0x1b000001 : 0x0a000001;
		static void SetProvider(Func<MethodBase, bool> accepts, Func<MethodBase, MethodBody> readBody, Action<MethodBase, MethodInfo, Func<Func<object, int>, byte[]>> install)
		{
			HarmonyLib.MethodBodyPatching.SetEligibility(accepts);
			HarmonyLib.MethodBodyPatching.SetBodySource(readBody);
			HarmonyLib.MethodBodyPatching.SetInstaller(install);
			HarmonyLib.MethodBodyPatching.observeBody = (_, snapshot) => observed = snapshot;
		}
		static Func<MethodInfo> observed;
		// A definition-wide target has no callable copy. Tests generate one from the last constructed body to execute it.
		static MethodInfo Snapshot() => observed();

		[Test]
		public void Provider_export_preserves_DynamicMethod_identity_for_host_import()
		{
			if (!MethodBodyGenerator.HasDynamicIL) Assert.Ignore("Body export requires a modern Harmony target.");
			var original = Method(nameof(DynamicExportTarget));
			var callback = new DynamicMethod("exportedPostfix", typeof(void), [typeof(int).MakeByRefType()], typeof(MethodBodyPatching).Module, true);
			callback.DefineParameter(1, ParameterAttributes.None, "__result");
			var il = callback.GetILGenerator();
			il.Emit(OpCodes.Ldarg_0);
			il.Emit(OpCodes.Dup);
			il.Emit(OpCodes.Ldind_I4);
			il.Emit(OpCodes.Ldc_I4_1);
			il.Emit(OpCodes.Add);
			il.Emit(OpCodes.Stind_I4);
			il.Emit(OpCodes.Ret);
			dynamicExportCallback = callback;
			var imported = new List<object>();
			SetProvider(method => method == original, method => method.GetMethodBody(),
				(method, replacement, writeBody) =>
				{
					_ = writeBody(value => { imported.Add(value); return value is byte[]? 0x11000001 : 0x0a000001; });
					PatchTools.DetourMethodDefault(method, replacement);
				});
			var harmony = new Harmony("test.method.body.dynamic.import");
			try
			{
				harmony.Patch(original, postfix: new HarmonyMethod(Method(nameof(DynamicExportFactory))));
				Assert.That(imported.Any(value => ReferenceEquals(value, callback)), Is.True, "The host decides how to import the actual DynamicMethod.");
				Assert.That(DynamicExportTarget(4), Is.EqualTo(5));
			}
			finally { harmony.UnpatchAll(harmony.Id); ClearProvider(); harmony.CreateProcessor(original).Patch(); dynamicExportCallback = null; }
		}

		[Test]
		public void A_body_writer_is_limited_to_one_synchronous_installation_call()
		{
			if (!MethodBodyGenerator.HasDynamicIL) Assert.Ignore("Body export requires a modern Harmony target.");
			var original = Method(nameof(ExportTarget));
			Func<Func<object, int>, byte[]> borrowed = null;
			SetProvider(method => method == original, method => method.GetMethodBody(),
				(method, replacement, writeBody) =>
				{
					borrowed = writeBody;
					var exported = writeBody(value => value is byte[]? 0x11000001 : 0x0a000001);
					Assert.That(exported, Is.Not.Empty);
					Assert.Throws<InvalidOperationException>(() => writeBody(_ => 0));
					PatchTools.DetourMethodDefault(method, replacement);
				});
			var harmony = new Harmony("test.method.body.export");
			try
			{
				harmony.Patch(original, postfix: new HarmonyMethod(Method(nameof(Postfix))));
				Assert.That(ExportTarget(), Is.EqualTo(2));
				Assert.Throws<InvalidOperationException>(() => borrowed(_ => 0));
			}
			finally { harmony.UnpatchAll(harmony.Id); ClearProvider(); harmony.CreateProcessor(original).Patch(); }
		}

		[Test]
		public void A_late_provider_does_not_take_over_existing_default_patches_or_reverse_standins()
		{
			var harmony = new Harmony("test.method.body.late");
			var original = Method(nameof(LateTarget));
			var standin = typeof(Derived).GetMethod(nameof(Base.ReverseSlot));
			var claims = 0;
			harmony.Patch(original, postfix: new HarmonyMethod(Method(nameof(Postfix))));
			harmony.CreateReversePatcher(original, new HarmonyMethod(standin)).Patch();
			try
			{
				SetProvider(method => { claims++; return true; }, _ => throw new InvalidOperationException("wrong source"),
					(_, _, _) => throw new InvalidOperationException("wrong installer"));
				harmony.CreateProcessor(original).Patch();
				Assert.That(LateTarget(), Is.EqualTo(6));
				harmony.CreateReversePatcher(original, new HarmonyMethod(standin)).Patch();
				Assert.That(new Derived().ReverseSlot(), Is.EqualTo(5));
				harmony.Patch(typeof(Base).GetMethod(nameof(Base.ReverseSlot)), postfix: new HarmonyMethod(Method(nameof(Postfix))));
				Assert.That(new Derived().ReverseSlot(), Is.EqualTo(0), "A reflected alias must not switch a reverse stand-in's installer.");
				Assert.That(claims, Is.Zero);
			}
			finally { harmony.UnpatchAll(harmony.Id); ClearProvider(); }
		}

		[Test]
		public void An_already_selected_reverse_standin_receives_a_complete_body_writer()
		{
#if NET35
			Assert.Ignore("Body export is outside the net35 target.");
#endif
			var original = Method(nameof(OwnedReverse));
			var installs = 0;
			SetProvider(method => method == original, method => method.GetMethodBody(),
				(method, replacement, writeBody) =>
				{
					Assert.That(writeBody(value => value is byte[]? 0x11000001 : 0x0a000001), Is.Not.Empty);
					PatchTools.DetourMethodDefault(method, replacement);
					installs++;
				});
			var harmony = new Harmony("test.method.body.reverse.selected");
			try
			{
				harmony.Patch(original, postfix: new HarmonyMethod(Method(nameof(Postfix))));
				Assert.That(OwnedReverse(), Is.EqualTo(2));
				harmony.CreateReversePatcher(Method(nameof(Source)), new HarmonyMethod(original)).Patch();
				Assert.That(OwnedReverse(), Is.EqualTo(23));
				Assert.That(installs, Is.EqualTo(2));
			}
			finally { harmony.UnpatchAll(harmony.Id); ClearProvider(); harmony.CreateProcessor(original).Patch(); }
		}

		[Test]
		public void A_throwing_eligibility_check_leaves_the_target_unclaimed()
		{
			var harmony = new Harmony("test.method.body.claim");
			var original = Method(nameof(ClaimTarget));
			var attempts = 0;
			SetProvider(method =>
			{
				if (method != original) return false;
				if (++attempts == 1) throw new InvalidOperationException("eligibility failed");
				return true;
			}, _ => Method(nameof(Source)).GetMethodBody(), (method, replacement, _) => PatchTools.DetourMethodDefault(method, replacement));
			try
			{
				Assert.Throws<InvalidOperationException>(() => harmony.Patch(original, postfix: new HarmonyMethod(Method(nameof(Postfix)))));
				Assert.That(Harmony.GetPatchInfo(original), Is.Null);
				Assert.That(ClaimTarget(), Is.EqualTo(1));
				harmony.Patch(original, postfix: new HarmonyMethod(Method(nameof(Postfix))));
				Assert.That(ClaimTarget(), Is.EqualTo(24));
				Assert.That(attempts, Is.EqualTo(2));
			}
			finally { harmony.UnpatchAll(harmony.Id); ClearProvider(); harmony.CreateProcessor(original).Patch(); }
		}

		public class Constructed
		{
			internal int Value;
			[MethodImpl(MethodImplOptions.NoInlining)]
			public Constructed() => Value = 1;
		}
		static void ConstructedPostfix(Constructed __instance) => __instance.Value++;

		[Test]
		public void A_provider_can_install_a_constructor()
		{
			if (!MethodBodyGenerator.HasDynamicIL) Assert.Ignore("Body export requires a modern Harmony target.");
			var original = typeof(Constructed).GetConstructor(Type.EmptyTypes);
			var exports = 0;
			SetProvider(method => method == original, method => method.GetMethodBody(),
				(method, replacement, writeBody) =>
				{
					Assert.That(writeBody(value => value is byte[]? 0x11000001 : value is Type ? 0x1b000001 : 0x0a000001), Is.Not.Empty);
					exports++;
					PatchTools.DetourMethodDefault(method, replacement);
				});
			var harmony = new Harmony("test.method.body.constructor");
			try
			{
				harmony.Patch(original, postfix: new HarmonyMethod(Method(nameof(ConstructedPostfix))));
				Assert.That(new Constructed().Value, Is.EqualTo(2));
				Assert.That(exports, Is.EqualTo(1));
				Assert.That(HarmonyLib.MethodBodyPatching.IsProviderTarget(original), Is.True);
			}
			finally { harmony.UnpatchAll(harmony.Id); ClearProvider(); harmony.CreateProcessor(original).Patch(); }
		}

		public class Base
		{
			[MethodImpl(MethodImplOptions.NoInlining)]
			public int Inherited() => 1;
			[MethodImpl(MethodImplOptions.NoInlining)]
			public int ReverseSlot() => -1;
		}
		public class Derived : Base { }

		[Test]
		public void A_body_read_through_a_derived_reflected_type_uses_the_pinned_baseline()
		{
			var original = typeof(Base).GetMethod(nameof(Base.Inherited));
			var reflected = typeof(Derived).GetMethod(nameof(Base.Inherited));
			Assert.That(reflected.ReflectedType, Is.EqualTo(typeof(Derived)));
			var harmony = new Harmony("test.method.body.inherited");
			SetProvider(method => method == original, _ => Method(nameof(Source)).GetMethodBody(),
				(method, replacement, _) => PatchTools.DetourMethodDefault(method, replacement));
			try
			{
				harmony.Patch(original, postfix: new HarmonyMethod(Method(nameof(Postfix))));
				Assert.That(new Derived().Inherited(), Is.EqualTo(24));
				CollectionAssert.AreEqual(Method(nameof(Source)).GetMethodBody().GetILAsByteArray(),
					HarmonyLib.MethodBodyPatching.GetMethodBody(reflected).GetILAsByteArray());
				CollectionAssert.AreEqual(PatchProcessor.ReadMethodBody(original).Select(code => code.Key),
					PatchProcessor.ReadMethodBody(reflected).Select(code => code.Key));
			}
			finally { harmony.UnpatchAll(harmony.Id); ClearProvider(); harmony.CreateProcessor(original).Patch(); }
		}

		[MethodImpl(MethodImplOptions.NoInlining)] static int Mixed<T>() => 1;
		[MethodImpl(MethodImplOptions.NoInlining)] static int States<T>() => 1;
		static T Witness<T>(T value) where T : IDisposable => value;
		[MethodImpl(MethodImplOptions.NoInlining)] static int RebuildSource() => 23;
		[MethodImpl(MethodImplOptions.NoInlining)] static int RebuildStandin() => -1;
		[MethodImpl(MethodImplOptions.NoInlining)] static int RebuildTarget() => 1;
		[MethodImpl(MethodImplOptions.NoInlining)] static int RebuildNever() => 1;
		static void IntStatePrefix(out int __state) => __state = 100;
		static void IntStatePostfix(int __state, ref int __result) => __result += __state;
		static void StringStatePrefix(out string __state) => __state = "1000";
		static void StringStatePostfix(string __state, ref int __result) => __result += int.Parse(__state);

		[Test]
		public void A_definition_wide_export_keeps_local_initialization()
		{
			if (!MethodBodyGenerator.HasDynamicIL) Assert.Ignore("Body export requires a modern Harmony target.");
			var assembly = PatchTools.DefineDynamicAssembly("InitializedLocal_" + Guid.NewGuid().ToString("N"));
			var type = assembly.DefineDynamicModule("initialized").DefineType("Initialized", TypeAttributes.Public);
			var builder = type.DefineMethod("Read", MethodAttributes.Public | MethodAttributes.Static, typeof(int), Type.EmptyTypes);
			builder.DefineGenericParameters("T");
			var il = builder.GetILGenerator();
			il.DeclareLocal(typeof(int));
			il.Emit(OpCodes.Ldloc_0);
			il.Emit(OpCodes.Ret);
			var original = type.CreateType().GetMethod("Read");
			Assert.That(original.GetMethodBody().InitLocals, Is.True);
			ushort header = 0;
			SetProvider(method => method == original, method => method.GetMethodBody(), (_, _, write) => header = BitConverter.ToUInt16(write(FakeToken), 0));
			var harmony = new Harmony("test.method.body.definition.init.locals");
			try
			{
				harmony.Patch(original, postfix: new HarmonyMethod(Method(nameof(Postfix))));
				Assert.That(header & 0x10, Is.Not.Zero, "The exported fat header keeps the init-locals flag the original relies on.");
			}
			finally { harmony.UnpatchAll(harmony.Id); ClearProvider(); }
		}

		[Test]
		public void Instantiation_patches_keep_state_per_registration_target()
		{
			if (!MethodBodyGenerator.HasDynamicIL) Assert.Ignore("Body export requires a modern Harmony target.");
			var definition = Method(nameof(States));
			SetProvider(method => method == definition, method => method.GetMethodBody(), (_, _, write) => write(FakeToken));
			var harmony = new Harmony("test.method.body.family.state");
			try
			{
				harmony.Patch(definition.MakeGenericMethod(typeof(int)), prefix: new HarmonyMethod(Method(nameof(IntStatePrefix))), postfix: new HarmonyMethod(Method(nameof(IntStatePostfix))));
				harmony.Patch(definition.MakeGenericMethod(typeof(string)), prefix: new HarmonyMethod(Method(nameof(StringStatePrefix))), postfix: new HarmonyMethod(Method(nameof(StringStatePostfix))));
				Assert.That(Snapshot().MakeGenericMethod(typeof(int)).Invoke(null, null), Is.EqualTo(101));
				Assert.That(Snapshot().MakeGenericMethod(typeof(string)).Invoke(null, null), Is.EqualTo(1001));
				Assert.That(Snapshot().MakeGenericMethod(typeof(object)).Invoke(null, null), Is.EqualTo(1));
			}
			finally { harmony.UnpatchAll(harmony.Id); ClearProvider(); }
		}

		[Test]
		public void A_closed_sibling_keeps_a_detour_held_family()
		{
			if (AccessTools.IsNetFrameworkRuntime) Assert.Ignore("MonoMod cannot detour a generic method instantiation on .NET Framework.");
			var definition = Method(nameof(Mixed));
			var postfix = new HarmonyMethod(Method(nameof(Postfix)));
			var harmony = new Harmony("test.method.body.family.sibling");
			var installs = 0;
			try
			{
				harmony.Patch(definition.MakeGenericMethod(typeof(int)), postfix: postfix);
				SetProvider(method => method == definition, method => method.GetMethodBody(), (_, _, write) => { installs++; write(FakeToken); });
				harmony.Patch(definition.MakeGenericMethod(typeof(string)), postfix: postfix);
				Assert.That(installs, Is.Zero, "A sibling joins the family's detour instead of pinning the definition.");
				Assert.That(HarmonyLib.MethodBodyPatching.IsProviderTarget(definition.MakeGenericMethod(typeof(string))), Is.False);
				Assert.That(Harmony.GetPatchInfo(definition.MakeGenericMethod(typeof(string))).Postfixes, Has.Count.EqualTo(1));
				Assert.Throws<NotSupportedException>(() => harmony.Patch(definition, postfix: postfix));
				Assert.That(installs, Is.Zero);
				Assert.That(HarmonyLib.MethodBodyPatching.Instantiations(definition), Is.Empty);
				var first = Mixed<int>();
				var sibling = Mixed<string>();
				if ((first != 2 || sibling != 2) && (IntPtr.Size == 4 || Environment.Version.Major == 3 && Environment.Version.Minor == 1 || Environment.Version.Major == 11))
					Assert.Ignore("Shared generic detours are not reliable on x86, .NET Core 3.1, or some .NET 11 runs.");
				Assert.That(first, Is.EqualTo(2));
				Assert.That(sibling, Is.EqualTo(2));
			}
			finally { harmony.UnpatchAll(harmony.Id); ClearProvider(); }
		}

		[Test]
		public void Instantiations_include_the_member_being_installed()
		{
			if (!MethodBodyGenerator.HasDynamicIL) Assert.Ignore("Body export requires a modern Harmony target.");
			var definition = Method(nameof(Witness));
			var closed = definition.MakeGenericMethod(typeof(System.IO.MemoryStream));
			MethodBase[] seen = null;
			SetProvider(method => method == definition, method => method.GetMethodBody(),
				(method, _, write) => { seen = [.. HarmonyLib.MethodBodyPatching.Instantiations(method)]; write(FakeToken); });
			var harmony = new Harmony("test.method.body.family.instantiations");
			try
			{
				harmony.Patch(closed, postfix: new HarmonyMethod(Method(nameof(ObserveDefinition))));
				Assert.That(seen, Is.EqualTo(new[] { closed }), "The installer sees the member before its patch record exists.");
				harmony.UnpatchAll(harmony.Id);
				Assert.That(HarmonyLib.MethodBodyPatching.Instantiations(definition), Is.EqualTo(new[] { closed }), "A member keeps its installer after unpatching.");
			}
			finally { harmony.UnpatchAll(harmony.Id); ClearProvider(); }
		}

		[Test]
		public void Rebuild_reinstalls_reverse_patches_and_patches()
		{
			var standin = Method(nameof(RebuildStandin));
			var target = Method(nameof(RebuildTarget));
			var installs = new List<MethodBase>();
			SetProvider(method => method == standin || method == target, method => method.GetMethodBody(),
				(method, replacement, _) => { installs.Add(method); PatchTools.DetourMethodDefault(method, replacement); });
			var harmony = new Harmony("test.method.body.rebuild");
			try
			{
				harmony.CreateReversePatcher(Method(nameof(RebuildSource)), new HarmonyMethod(standin)).Patch();
				harmony.Patch(target, postfix: new HarmonyMethod(Method(nameof(Postfix))));
				HarmonyLib.MethodBodyPatching.Rebuild(standin);
				HarmonyLib.MethodBodyPatching.Rebuild(target);
				Assert.That(installs, Is.EqualTo(new[] { standin, target, standin, target }));
				Assert.That(RebuildStandin(), Is.EqualTo(23), "A rebuilt stand-in keeps its reverse body.");
				Assert.That(RebuildTarget(), Is.EqualTo(2));
				Assert.Throws<InvalidOperationException>(() => HarmonyLib.MethodBodyPatching.Rebuild(Method(nameof(RebuildNever))));
			}
			finally { harmony.UnpatchAll(harmony.Id); ClearProvider(); }
		}

		static T FamilyGenericCallback<T>(T value) => value;
		static Type familyGenericCallbackType;
		static void FamilyGenericPostfix<T>(ref T __result) => familyGenericCallbackType = typeof(T);

		[Test]
		public void A_generic_callback_definition_binds_to_the_instantiation_it_is_registered_on()
		{
			if (!MethodBodyGenerator.HasDynamicIL) Assert.Ignore("Body export requires a modern Harmony target.");
			var definition = Method(nameof(FamilyGenericCallback));
			var closed = definition.MakeGenericMethod(typeof(string));
			var callback = Method(nameof(FamilyGenericPostfix));
			SetProvider(method => method == definition, method => method.GetMethodBody(), (_, _, write) => write(FakeToken));
			var harmony = new Harmony("test.method.body.family.generic.callback");
			try
			{
				harmony.Patch(closed, postfix: new HarmonyMethod(callback));
				Assert.That(Harmony.GetPatchInfo(closed).Postfixes[0].PatchMethod, Is.SameAs(callback), "Patch identity keeps the registered definition.");
				familyGenericCallbackType = null;
				Assert.That(Snapshot().MakeGenericMethod(typeof(string)).Invoke(null, ["kept"]), Is.EqualTo("kept"));
				Assert.That(familyGenericCallbackType, Is.EqualTo(typeof(string)));
				familyGenericCallbackType = null;
				Assert.That(Snapshot().MakeGenericMethod(typeof(int)).Invoke(null, [7]), Is.EqualTo(7));
				Assert.That(familyGenericCallbackType, Is.Null, "The callback runs only for its instantiation.");
			}
			finally { harmony.UnpatchAll(harmony.Id); ClearProvider(); }
		}
	}
}
