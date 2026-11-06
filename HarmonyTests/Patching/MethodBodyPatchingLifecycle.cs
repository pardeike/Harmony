#if NET7_0_OR_GREATER
using HarmonyLib;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using Protocol = HarmonyLib.MethodBodyPatching;

namespace HarmonyLibTests.Patching
{
	[TestFixture, NonParallelizable]
	public class MethodBodyPatchingLifecycle : TestLogger
	{
		Harmony harmony;
		readonly Dictionary<MethodBase, MethodInfo> bodies = [];
		Action<MethodBase, MethodInfo> install;
		static bool rejectConstruction;
		static bool checkLock;
		static MethodInfo Method(string name) => AccessTools.DeclaredMethod(typeof(MethodBodyPatchingLifecycle), name);
		static HarmonyMethod Patch(string name) => new(Method(name));
		static void Postfix(ref int __result) => __result++;
		static void Observe() { }
		static int Source() => 23;
		static int OtherSource() => 42;
		static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			if (checkLock) Assert.That(Monitor.IsEntered(Protocol.ProtocolLock), Is.True, "Construction must hold the protocol lock.");
			if (rejectConstruction) throw new InvalidOperationException("construction rejected");
			return instructions;
		}

		[SetUp]
		public void SetUp()
		{
			Protocol.ClearProvider();
			harmony = new Harmony("test.method.body.lifecycle." + Guid.NewGuid());
			bodies.Clear();
			install = null;
			rejectConstruction = false;
			checkLock = false;
		}

		[TearDown]
		public void TearDown()
		{
			install = null;
			rejectConstruction = false;
			checkLock = false;
			try { harmony.UnpatchAll(harmony.Id); }
			finally { Protocol.ClearProvider(); }
		}

		void Host(Func<MethodBase, bool> accepts)
		{
			Protocol.SetEligibility(accepts);
			Protocol.SetInstaller((method, replacement, _) => { install?.Invoke(method, replacement); bodies[method] = replacement; });
		}

		[Test]
		public void New_shared_record_satisfies_the_delivered_three_slot_validator()
		{
			const string key = "Harmony.MethodBodyPatching.v1";
			var previous = AppDomain.CurrentDomain.GetData(key);
			AppDomain.CurrentDomain.SetData(key, null);
			try
			{
				var record = Protocol.Shared();
				// This is the prefix validator in delivered package 3.0.0-rc.4.runtime-il.20261104.
				Assert.That(record, Has.Length.GreaterThanOrEqualTo(3));
				Assert.That(record[1], Is.InstanceOf<Dictionary<MethodBase, bool>>());
				Assert.That(record[2], Is.InstanceOf<List<Func<MethodBase, bool>>>());
				Assert.That((List<Func<MethodBase, bool>>)record[2], Is.Empty);
			}
			finally { AppDomain.CurrentDomain.SetData(key, previous); }
		}

		static int FamilyRebuild<T>() => 1;
		[Test]
		public void Rebuild_definition_uses_member_records()
		{
			var definition = Method(nameof(FamilyRebuild));
			var member = definition.MakeGenericMethod(typeof(int));
			Host(method => method == definition);
			MethodInfo snapshot = null;
			Protocol.observeBody = (_, create) => snapshot = create();
			harmony.Patch(member, postfix: Patch(nameof(Postfix)));
			Assert.That(Harmony.GetPatchInfo(definition), Is.Null);
			bodies.Clear();
			Protocol.Rebuild(definition);
			Assert.That(bodies.Keys, Is.EqualTo(new[] { definition }));
			Assert.That(snapshot.MakeGenericMethod(typeof(int)).Invoke(null, null), Is.EqualTo(2));
			Assert.That(snapshot.MakeGenericMethod(typeof(string)).Invoke(null, null), Is.EqualTo(1));
			Assert.That(Harmony.GetPatchInfo(definition), Is.Null, "Rebuilding must preserve per-member metadata.");
		}

		static int LateTarget() => 1;
		static int LateFamily<T>() => 1;
		[TestCase(false), TestCase(true)]
		public void Registration_cannot_move_accepted_targets(bool generic)
		{
			var target = generic ? Method(nameof(LateFamily)).MakeGenericMethod(typeof(int)) : Method(nameof(LateTarget));
			Protocol.SetEligibility(_ => true);
			Assert.That(Protocol.Prepare(target), Is.True);
			Assert.Throws<InvalidOperationException>(() => Protocol.SetInstaller((_, _, _) => { }));
			Assert.Throws<InvalidOperationException>(() => Protocol.SetBodySource(method => method.GetMethodBody()));
			Assert.That(Protocol.FamilyOwner(target), Is.Null);
			Assert.DoesNotThrow(() => Protocol.SetInstaller(null));
		}

		static int FailedInstall() => 1;
		static int FailedReverse() => 1;
		[TestCase(false), TestCase(true)]
		public void Installer_failure_keeps_selection_without_publishing_metadata(bool reverse)
		{
			var target = Method(reverse ? nameof(FailedReverse) : nameof(FailedInstall));
			var eligible = true;
			var selections = 0;
			var attempts = 0;
			Host(method => { selections++; return method == target && eligible; });
			install = (_, _) => { attempts++; throw new InvalidOperationException("host changed the body then failed"); };
			void Apply()
			{
				if (reverse) Harmony.ReversePatch(Method(nameof(Source)), new HarmonyMethod(target));
				else harmony.Patch(target, postfix: Patch(nameof(Postfix)));
			}
			Assert.Catch(Apply);
			Assert.That(Harmony.GetPatchInfo(target), Is.Null);
			Assert.That(Protocol.IsProviderTarget(target), Is.True);
			eligible = false;
			Assert.Catch(Apply);
			Assert.That(attempts, Is.EqualTo(2));
			Assert.That(selections, Is.EqualTo(1), "A failed host must not be replaced by the default detour.");
			Assert.That(Harmony.GetPatchInfo(target), Is.Null);
		}

		static int FailedFamily<T>() => 1;
		[Test]
		public void Construction_failure_releases_member_and_definition()
		{
			var definition = Method(nameof(FailedFamily));
			var member = definition.MakeGenericMethod(typeof(int));
			Host(method => method == definition);
			rejectConstruction = true;
			Assert.Catch(() => harmony.Patch(member, transpiler: Patch(nameof(Transpiler))));
			Assert.That(Protocol.IsProviderTarget(member), Is.False);
			Assert.That(Protocol.IsProviderTarget(definition), Is.False);
			Assert.That(Protocol.Instantiations(definition), Is.Empty);
		}

		static int LockedReverse() => 1;
		[Test]
		public void Reverse_construction_holds_protocol_lock()
		{
			var target = Method(nameof(LockedReverse));
			Host(method => method == target);
			checkLock = true;
			Harmony.ReversePatch(Method(nameof(Source)), new HarmonyMethod(target), Method(nameof(Transpiler)));
			Assert.That(bodies[target].Invoke(null, null), Is.EqualTo(23));
		}

		static int LockedWrapper() => 1;
		[Test]
		public void Reflective_wrapper_construction_holds_protocol_lock()
		{
			var target = Method(nameof(LockedWrapper));
			Host(method => method == target);
			checkLock = true;
			var info = new PatchInfo();
			info.AddTranspilers(harmony.Id, Patch(nameof(Transpiler)));
			PatchFunctions.UpdateWrapper(target, info);
			Assert.That(bodies[target].Invoke(null, null), Is.EqualTo(1));
		}

		static int GenericStandin<T>() => 1;
		[TestCase(false), TestCase(true)]
		public void Generic_reverse_standins_are_rejected_before_pinning(bool open)
		{
			var definition = Method(nameof(GenericStandin));
			var target = open ? definition : definition.MakeGenericMethod(typeof(int));
			Host(method => method == definition);
			var error = Assert.Throws<NotSupportedException>(() => Harmony.ReversePatch(Method(nameof(Source)), new HarmonyMethod(target)));
			Assert.That(error.Message, Does.Contain("Reverse patches into host-owned generic targets are not supported"));
			Assert.That(Protocol.IsProviderTarget(target), Is.False);
			Assert.That(Protocol.IsProviderTarget(definition), Is.False);
			Assert.That(bodies, Is.Empty);
		}

		static int SupersededReverse() => 1;
		[Test]
		public void Ordinary_patch_invalidates_reverse_recipe()
		{
			var target = Method(nameof(SupersededReverse));
			Host(method => method == target);
			Harmony.ReversePatch(Method(nameof(Source)), new HarmonyMethod(target));
			Assert.That(bodies[target].Invoke(null, null), Is.EqualTo(23));
			harmony.Patch(target, postfix: Patch(nameof(Postfix)));
			Assert.That(bodies[target].Invoke(null, null), Is.EqualTo(2));
			Protocol.Rebuild(target);
			Assert.That(bodies[target].Invoke(null, null), Is.EqualTo(2));
		}

		[MethodImpl(MethodImplOptions.NoInlining)] static int DefaultReverse() => 1;
		[Test]
		public void Default_reverse_recipe_can_be_rebuilt()
		{
			var target = Method(nameof(DefaultReverse));
			Harmony.ReversePatch(Method(nameof(Source)), new HarmonyMethod(target));
			Protocol.Rebuild(target);
			Assert.That(DefaultReverse(), Is.EqualTo(23));
		}

		static int ReentrantTarget() => 1;
		[Test]
		public void Installer_cannot_rebuild_or_patch_its_target()
		{
			var target = Method(nameof(ReentrantTarget));
			Host(method => method == target);
			harmony.Patch(target, postfix: Patch(nameof(Postfix)));
			var checking = false;
			install = (_, _) =>
			{
				if (checking) return;
				checking = true;
				try
				{
					Assert.Throws<InvalidOperationException>(() => Protocol.Rebuild(target));
					Assert.Throws<InvalidOperationException>(() => harmony.Patch(target, postfix: Patch(nameof(Postfix))));
					Assert.Throws<InvalidOperationException>(() => Harmony.ReversePatch(Method(nameof(Source)), new HarmonyMethod(target)));
				}
				finally { checking = false; }
			};
			harmony.Patch(target, postfix: Patch(nameof(Postfix)));
			Assert.That(Harmony.GetPatchInfo(target).Postfixes.Count, Is.EqualTo(2));
			Assert.That(bodies[target].Invoke(null, null), Is.EqualTo(3));
		}

		static int ReentrantFamily<T>() => 1;
		[Test]
		public void Installer_cannot_patch_a_sibling_in_its_family()
		{
			var definition = Method(nameof(ReentrantFamily));
			var first = definition.MakeGenericMethod(typeof(int));
			var sibling = definition.MakeGenericMethod(typeof(string));
			Host(method => method == definition);
			var checking = false;
			install = (_, _) =>
			{
				if (checking) return;
				checking = true;
				try
				{
					Assert.Throws<InvalidOperationException>(() => harmony.Patch(sibling, postfix: Patch(nameof(Postfix))));
					Assert.Throws<InvalidOperationException>(() => Protocol.Rebuild(definition));
					Assert.Throws<InvalidOperationException>(() => Protocol.Rebuild(first));
				}
				finally { checking = false; }
			};
			harmony.Patch(first, postfix: Patch(nameof(Postfix)));
			Assert.That(Harmony.GetPatchInfo(sibling), Is.Null);
			Assert.That(Protocol.Instantiations(definition), Is.EqualTo(new[] { first }));
		}

		class Constructed<T> { internal Constructed() { } }
		[Test]
		public void Family_constructor_patch_returns_null()
		{
			var definition = AccessTools.DeclaredConstructor(typeof(Constructed<>));
			var closed = AccessTools.DeclaredConstructor(typeof(Constructed<int>));
			Host(method => method == definition);
			Assert.That(harmony.Patch(closed, postfix: Patch(nameof(Observe))), Is.Null);
			Assert.That(bodies.Keys, Is.EqualTo(new[] { definition }));
		}

		static int Call(int value) => value;
		static IEnumerable<int> Iterator<T>() { yield return Call(1); yield return Call(2); }
		static void Keep([HarmonyOuter, HarmonyArgument("saved", ArgumentMode.Persistent)] ref object value) => value ??= new object();
		[TestCase(false), TestCase(true)]
		public void Removing_closed_infix_clears_registration_slots_and_dispose_hook(bool keepPrefix)
		{
			using var iterator = (keepPrefix ? Iterator<string>() : Iterator<int>()).GetEnumerator();
			var member = AccessTools.DeclaredMethod(iterator.GetType(), "MoveNext");
			var definition = member.Module.ResolveMethod(member.MetadataToken);
			var dispose = iterator.GetType().GetInterfaceMap(typeof(IDisposable)).TargetMethods.Single();
			Host(method => method.DeclaringType == definition.DeclaringType);
			if (keepPrefix) harmony.Patch(member, prefix: Patch(nameof(Observe)));
			harmony.CreateProcessor(member).AddInnerPrefix(new HarmonyMethod(Method(nameof(Keep)))
			{ innerMethod = new InnerMethod(Method(nameof(Call))) }).Patch();
			Assert.That(PersistentState.Registrations.ContainsKey(member), Is.True);
			Assert.That(Harmony.GetPatchInfo(dispose).Finalizers, Has.Count.EqualTo(1));
			var frame = PersistentState.Enter(member, iterator, PersistentState.Kind.Iterator);
			PersistentState.Slot<object>(frame, typeof(MethodBodyPatchingLifecycle), "saved")[0] = new object();
			PersistentState.Exit(frame, true, true);
			harmony.Unpatch(member, keepPrefix ? HarmonyPatchType.InnerPrefix : HarmonyPatchType.All, harmony.Id);
			Assert.That(Harmony.GetPatchInfo(member).Prefixes, Has.Count.EqualTo(keepPrefix ? 1 : 0));
			Assert.That(PersistentState.Registrations.ContainsKey(member), Is.False);
			Assert.That(Harmony.GetPatchInfo(dispose)?.Finalizers ?? [], Is.Empty);
			Assert.That(((Dictionary<Type, Dictionary<string, object>>)((object[])frame[0])[0]), Is.Empty);
		}
		static int SupersededByCopy() => 1;
		static int ReverseFromCopy() => 1;
		[TestCase(false), TestCase(true)]
		public void Rebuild_follows_the_installation_from_another_copy(bool ordinary)
		{
			var target = Method(ordinary ? nameof(SupersededByCopy) : nameof(ReverseFromCopy));
			Host(method => method == target);
			Harmony.ReversePatch(Method(nameof(Source)), new HarmonyMethod(target));
			var context = new System.Runtime.Loader.AssemblyLoadContext("lifecycle-" + Guid.NewGuid());
			// A distinct identity avoids MonoMod's documented same-identity ILGeneratorProxy binding failure.
			var moduleType = typeof(Harmony).Assembly.GetType("Mono.Cecil.ModuleDefinition", throwOnError: true);
			using var module = (IDisposable)moduleType.GetMethod("ReadModule", [typeof(string)]).Invoke(null, [typeof(Harmony).Assembly.Location]);
			var assembly = moduleType.GetProperty("Assembly").GetValue(module);
			var name = assembly.GetType().GetProperty("Name").GetValue(assembly);
			name.GetType().GetProperty("Name").SetValue(name, "HarmonyLifecycleCopy_" + Guid.NewGuid().ToString("N"));
			using var image = new System.IO.MemoryStream();
			moduleType.GetMethod("Write", [typeof(System.IO.Stream)]).Invoke(module, [image]);
			image.Position = 0;
			var copy = context.LoadFromStream(image);
			var harmonyType = copy.GetType(typeof(Harmony).FullName);
			var methodType = copy.GetType(typeof(HarmonyMethod).FullName);
			object Wrap(MethodInfo method) => Activator.CreateInstance(methodType, [method]);
			if (ordinary)
			{
				var other = Activator.CreateInstance(harmonyType, [harmony.Id]);
				harmonyType.GetMethod(nameof(Harmony.Patch)).Invoke(other, [target, null, Wrap(Method(nameof(Postfix))), null, null]);
				Protocol.Rebuild(target);
				Assert.That(bodies[target].Invoke(null, null), Is.EqualTo(2));
			}
			else
			{
				harmonyType.GetMethod(nameof(Harmony.ReversePatch)).Invoke(null, [Method(nameof(OtherSource)), Wrap(target), null]);
				Assert.That(bodies[target].Invoke(null, null), Is.EqualTo(42));
				Assert.Throws<NotSupportedException>(() => Protocol.Rebuild(target));
				Assert.That(bodies[target].Invoke(null, null), Is.EqualTo(42));
			}
		}

		static int RollbackFamily<T>() => 1;
		static int RollbackDisposalFamily<T>() => 1;
		static void CleanupA() { }
		static void CleanupB() { }
		static void CleanupC() { }
		static void CleanupD() { }
		static void CleanupE() { }
		static void CleanupF() { }
		[TestCase(false), TestCase(true)]
		public void Failed_closed_preparation_rolls_back_completed_preparations(bool rejectRollback)
		{
			const string owner = "Harmony.Infix.PersistentState";
			var definition = Method(rejectRollback ? nameof(RollbackDisposalFamily) : nameof(RollbackFamily));
			var members = new[] { typeof(int), typeof(string), typeof(object) }.Select(type => definition.MakeGenericMethod(type)).ToArray();
			var disposals = (rejectRollback ? new[] { nameof(CleanupD), nameof(CleanupE), nameof(CleanupF) }
				: new[] { nameof(CleanupA), nameof(CleanupB), nameof(CleanupC) }).Select(Method).ToArray();
			Host(method => method == definition || disposals.Contains(method));
			foreach (var member in members) harmony.Patch(member, transpiler: Patch(nameof(Transpiler)));
			var hooks = new Harmony(owner);
			// Controlled prior hook records isolate rollback from open state-machine construction, which is unsupported.
			for (var i = 0; i < members.Length; i++)
			{
				hooks.Patch(disposals[i], finalizer: Patch(nameof(Observe)));
				PersistentState.Registrations.Add(members[i], disposals[i]);
			}
			var updates = new int[3];
			install = (method, _) =>
			{
				var index = Array.IndexOf(disposals, method);
				if (index < 0) return;
				updates[index]++;
				if (index == 2 || rejectRollback && index == 0 && updates[index] == 2)
					throw new InvalidOperationException("cleanup update rejected");
			};
			try
			{
				Assert.Catch(() => Protocol.Rebuild(definition));
				Assert.That(updates, Is.EqualTo(new[] { 2, 2, 1 }), "Every completed preparation must be disposed, even if rollback throws.");
				Assert.That(members.All(PersistentState.Registrations.ContainsKey), Is.True);
				for (var i = 0; i < disposals.Length; i++)
					Assert.That(Harmony.GetPatchInfo(disposals[i]).Finalizers, Has.Count.EqualTo(rejectRollback && i == 0 ? 0 : 1));
			}
			finally
			{
				install = null;
				foreach (var member in members) PersistentState.Registrations.Remove(member);
				foreach (var dispose in disposals) hooks.Unpatch(dispose, HarmonyPatchType.Finalizer, owner);
			}
		}

		static int FailedInstallFamily<T>() => 1;
		[Test]
		public void Failed_family_installation_retains_both_pins_and_can_retry()
		{
			var definition = Method(nameof(FailedInstallFamily));
			var member = definition.MakeGenericMethod(typeof(int));
			var eligible = true;
			var selections = 0;
			Host(method => { selections++; return method == definition && eligible; });
			install = (_, _) => throw new InvalidOperationException("family installation rejected");
			Assert.Catch(() => harmony.Patch(member, postfix: Patch(nameof(Postfix))));
			Assert.That(Protocol.IsProviderTarget(member), Is.True);
			Assert.That(Protocol.IsProviderTarget(definition), Is.True);
			Assert.That(Protocol.Instantiations(definition), Is.EqualTo(new[] { member }));
			Assert.That(Harmony.GetPatchInfo(member), Is.Null);
			var error = Assert.Throws<InvalidOperationException>(() => Protocol.Rebuild(definition));
			Assert.That(error.Message, Does.StartWith("Harmony has no rebuild information for "));
			eligible = false;
			install = null;
			harmony.Patch(member, postfix: Patch(nameof(Postfix)));
			Protocol.Rebuild(definition);
			Assert.That(selections, Is.EqualTo(1));
			Assert.That(Harmony.GetPatchInfo(member).Postfixes, Has.Count.EqualTo(1));
		}

	}
}
#endif
