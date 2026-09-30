using HarmonyLib;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace HarmonyLibTests.Patching
{
	[TestFixture, NonParallelizable]
	public class BindingRebuilds : TestLogger
	{
		Harmony harmony;
		static bool changed;
		static int factoryCalls, observed;
		static readonly Uri uri = new("https://example.com");
		static Dictionary<MethodBase, byte[]> SharedState => (Dictionary<MethodBase, byte[]>)AccessTools.Field(typeof(HarmonySharedState), "state").GetValue(null);
		static MethodInfo Method(string name) => AccessTools.Method(typeof(BindingRebuilds), name);
		static HarmonyMethod Callback(string name, int priority = Priority.Normal, bool uncheckedBinding = false)
			=> new(Method(name)) { priority = priority, uncheckedReferenceBinding = uncheckedBinding };

		[SetUp]
		public void SetUp()
		{
			harmony = new Harmony("test." + nameof(BindingRebuilds));
			changed = false;
			factoryCalls = observed = 0;
		}

		[TearDown]
		public void TearDown() => harmony.UnpatchAll(harmony.Id);

		[TestCase(HarmonyPatchType.Prefix, false), TestCase(HarmonyPatchType.Prefix, true)]
		[TestCase(HarmonyPatchType.Postfix, false), TestCase(HarmonyPatchType.Postfix, true)]
		[TestCase(HarmonyPatchType.Finalizer, false), TestCase(HarmonyPatchType.Finalizer, true)]
		public void Factory_outputs_are_checked_on_each_rebuild_unless_opted_out(HarmonyPatchType role, bool uncheckedBinding)
		{
			var original = Method(nameof(Echo));
			var callback = Callback(nameof(Factory), uncheckedBinding: uncheckedBinding);
			harmony.Patch(original, prefix: role == HarmonyPatchType.Prefix ? callback : null,
				postfix: role == HarmonyPatchType.Postfix ? callback : null, finalizer: role == HarmonyPatchType.Finalizer ? callback : null);
			Assert.AreEqual(1, factoryCalls);
			Assert.AreEqual("value", Echo("value"));
			Assert.AreEqual(1, observed);
			var published = SharedState[original];
			changed = true;
			if (uncheckedBinding)
				harmony.Patch(original, postfix: Callback(nameof(Empty)));
			else
			{
				var error = Assert.Catch<ArgumentException>(() => harmony.Patch(original, postfix: Callback(nameof(Empty))));
				StringAssert.Contains(nameof(Incompatible), error.Message);
				Assert.AreSame(published, SharedState[original]);
			}
			Assert.AreEqual(2, factoryCalls, "A rebuild must resolve each factory only once.");
			Assert.AreEqual("value", Echo("value"));
			Assert.AreEqual(uncheckedBinding ? 2 : 1, observed, "A rejected rebuild must leave the old callback installed.");
		}

		[TestCase(false), TestCase(true)]
		public void Removing_a_bridge_checks_the_new_passthrough_edge(bool uncheckedConsumer)
		{
			var original = Method(nameof(Result));
			harmony.Patch(original, postfix: Callback(nameof(Producer), Priority.High));
			harmony.Patch(original, postfix: Callback(nameof(Bridge)));
			harmony.Patch(original, postfix: Callback(nameof(Consumer), Priority.Low, uncheckedConsumer));
			Assert.AreSame(uri, Result());
			Assert.AreEqual(1, observed);
			var published = SharedState[original];
			if (uncheckedConsumer)
				harmony.Unpatch(original, Method(nameof(Bridge)));
			else
			{
				var error = Assert.Catch<ArgumentException>(() => harmony.Unpatch(original, Method(nameof(Bridge))));
				StringAssert.Contains(nameof(Consumer), error.Message);
				Assert.AreSame(published, SharedState[original]);
			}
			Assert.AreSame(uri, Result());
			Assert.AreEqual(2, observed);
		}

		[TestCase(false), TestCase(true)]
		public void A_producers_opt_out_does_not_disable_the_consumers_check(bool consumerAlreadyInstalled)
		{
			var original = Method(nameof(Result));
			var producer = Callback(nameof(Producer), Priority.High, true);
			var consumer = Callback(nameof(Consumer), Priority.Low);
			harmony.Patch(original, postfix: consumerAlreadyInstalled ? consumer : producer);
			var published = SharedState[original];
			var error = Assert.Catch<ArgumentException>(() => harmony.Patch(original, postfix: consumerAlreadyInstalled ? producer : consumer));
			StringAssert.Contains(nameof(Consumer), error.Message);
			Assert.AreSame(published, SharedState[original]);
			if (consumerAlreadyInstalled) Assert.AreSame(uri, Result());
			else Assert.IsInstanceOf<Payload>(Result());
		}

		[Test]
		public void A_factory_cannot_change_the_input_type_of_a_checked_surviving_postfix()
		{
			var original = Method(nameof(Result));
			harmony.Patch(original, postfix: Callback(nameof(PostfixFactory), Priority.High, true));
			harmony.Patch(original, postfix: Callback(nameof(Consumer), Priority.Low));
			Assert.AreEqual(2, factoryCalls);
			Assert.AreSame(uri, Result());
			var published = SharedState[original];
			changed = true;
			Assert.Catch<ArgumentException>(() => harmony.Patch(original, prefix: Callback(nameof(Empty))));
			Assert.AreEqual(3, factoryCalls);
			Assert.AreSame(published, SharedState[original]);
			Assert.AreSame(uri, Result());
			Assert.AreEqual(2, observed);
		}

		[Test]
		public void Removing_an_unresolvable_postfix_does_not_require_its_old_callback()
		{
			var original = Method(nameof(Result));
			harmony.Patch(original, postfix: Callback(nameof(Consumer)));
			var clean = SharedState[original];
			try
			{
				var info = HarmonySharedState.GetPatchInfo(original);
				info.postfixes = [.. info.postfixes, new Patch(1, "missing.callback", Priority.High, [], [], false,
					Method(nameof(Producer)).MetadataToken, Guid.NewGuid().ToString())];
				lock (SharedState) SharedState[original] = info.SerializeValidated();
				harmony.Unpatch(original, HarmonyPatchType.Postfix, "missing.callback");
				Assert.AreSame(uri, Result());
				Assert.AreEqual(1, observed);
				Assert.AreEqual(1, Harmony.GetPatchInfo(original).Postfixes.Count);
			}
			finally { lock (SharedState) SharedState[original] = clean; }
		}

		[Test]
		public void A_global_method_supplies_a_null_instance()
		{
			var assembly = PatchTools.DefineDynamicAssembly("GlobalBinding_" + Guid.NewGuid().ToString("N"));
			var module = assembly.DefineDynamicModule("GlobalBinding");
			var method = module.DefineGlobalMethod("Run", MethodAttributes.Public | MethodAttributes.Static, typeof(int), Type.EmptyTypes);
			var il = method.GetILGenerator();
			il.Emit(OpCodes.Ldc_I4_7);
			il.Emit(OpCodes.Ret);
			module.CreateGlobalFunctions();
			var original = module.GetMethod("Run");
			// CoreCLR reports no declaring type; Mono exposes the module pseudo-type.
			Assert.That(original.DeclaringType?.Name, Is.Null.Or.EqualTo("<Module>"));
			harmony.Patch(original, prefix: Callback(nameof(GlobalInstance)));
			Assert.AreEqual(7, original.Invoke(null, null));
			Assert.AreEqual(1, observed);
		}

		class Payload { }
		[MethodImpl(MethodImplOptions.NoInlining)]
		static string Echo(string value) => value;
		[MethodImpl(MethodImplOptions.NoInlining)]
		static object Result() => new Payload();
		static void Empty() { }
		static void Compatible(string value) => observed = 1;
		static void Incompatible(Uri value) => observed = 2;
		static MethodInfo Factory(MethodBase original) { factoryCalls++; return Method(changed ? nameof(Incompatible) : nameof(Compatible)); }
		static MethodInfo PostfixFactory(MethodBase original) { factoryCalls++; return Method(changed ? nameof(Producer) : nameof(Bridge)); }
		static Payload Producer(Payload result) => result;
		static object Bridge(object result) => uri;
		// Deliberately ignores the argument: only opted-out bindings may use this incompatible declaration.
		static Uri Consumer(Uri result) { observed++; return uri; }
		static void GlobalInstance(object __instance) { Assert.IsNull(__instance); observed++; }
	}
}
