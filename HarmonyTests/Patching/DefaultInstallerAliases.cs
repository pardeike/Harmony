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
	}
}
