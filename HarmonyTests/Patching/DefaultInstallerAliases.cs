using HarmonyLib;
using NUnit.Framework;
using System.Runtime.CompilerServices;

namespace HarmonyLibTests.Patching
{
	[TestFixture, NonParallelizable]
	public class DefaultInstallerAliases : TestLogger
	{
		public class Base
		{
			[MethodImpl(MethodImplOptions.NoInlining)]
			public int Slot() => -1;
		}
		public class Derived : Base { }
		[MethodImpl(MethodImplOptions.NoInlining)]
		public static int Source() => 23;
		public static void Postfix(ref int __result) => __result++;

		[Test]
		public void Reflected_aliases_keep_default_reverse_patch_and_unpatch_behavior()
		{
			var harmony = new Harmony("test.default.installer.alias");
			var alias = typeof(Derived).GetMethod(nameof(Base.Slot));
			var declared = typeof(Base).GetMethod(nameof(Base.Slot));
			var source = typeof(DefaultInstallerAliases).GetMethod(nameof(Source));
			var instance = new Derived();
			try
			{
				harmony.CreateReversePatcher(source, new HarmonyMethod(alias)).Patch();
				Assert.That(instance.Slot(), Is.EqualTo(23));
				harmony.Patch(declared, postfix: new HarmonyMethod(typeof(DefaultInstallerAliases).GetMethod(nameof(Postfix))));
				Assert.That(instance.Slot(), Is.EqualTo(0));
				harmony.UnpatchAll(harmony.Id);
				Assert.That(instance.Slot(), Is.EqualTo(-1));
				harmony.CreateReversePatcher(source, new HarmonyMethod(alias)).Patch();
				Assert.That(instance.Slot(), Is.EqualTo(23));
				harmony.CreateProcessor(declared).Patch();
				Assert.That(instance.Slot(), Is.EqualTo(-1));
			}
			finally { harmony.UnpatchAll(harmony.Id); }
		}
		[MethodImpl(MethodImplOptions.NoInlining)]
		public static int ChangedSource() => 47;

		[TearDown]
		public void ClearProvider() => HarmonyLib.MethodBodyPatching.ClearProvider();

		[TestCase(false), TestCase(true)]
		public void Rebuild_reads_a_changed_baseline_through_a_reflected_alias(bool reverse)
		{
			var alias = typeof(Derived).GetMethod(nameof(Base.Slot));
			var declared = typeof(Base).GetMethod(nameof(Base.Slot));
			var source = typeof(DefaultInstallerAliases).GetMethod(nameof(Source));
			var baseline = source;
			HarmonyLib.MethodBodyPatching.SetEligibility(method => method == declared || method == source);
			HarmonyLib.MethodBodyPatching.SetBodySource(_ => baseline.GetMethodBody());
			var harmony = new Harmony("test.default.alias.baseline." + reverse);
			var instance = new Derived();
			try
			{
				if (reverse)
				{
					Assert.That(HarmonyLib.MethodBodyPatching.Prepare(source), Is.True);
					harmony.CreateReversePatcher(source, new HarmonyMethod(alias)).Patch();
				}
				else harmony.Patch(declared, postfix: new HarmonyMethod(typeof(DefaultInstallerAliases).GetMethod(nameof(Postfix))));
				Assert.That(instance.Slot(), Is.EqualTo(reverse ? 23 : 24));
				baseline = typeof(DefaultInstallerAliases).GetMethod(nameof(ChangedSource));
				HarmonyLib.MethodBodyPatching.Rebuild(alias);
				Assert.That(instance.Slot(), Is.EqualTo(reverse ? 47 : 48));
			}
			finally
			{
				harmony.UnpatchAll(harmony.Id);
				ClearProvider();
				harmony.CreateProcessor(declared).Patch();
			}
		}

	}
}
