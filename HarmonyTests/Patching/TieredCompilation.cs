using HarmonyLib;
using NUnit.Framework;
using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;

namespace HarmonyLibTests.Patching
{
	// Issue #794: on .NET 11, a tier-up published its new code over the detour.
	[TestFixture, NonParallelizable]
	public class TieredCompilation : TestLogger
	{
		[MethodImpl(MethodImplOptions.NoInlining)]
		static int HotBeforePatch(int x) => x + 1001;
		[MethodImpl(MethodImplOptions.NoInlining)]
		static int HotAfterPatch(int x) => x + 1001;

		static void Postfix(ref int __result) => __result *= 2;

		static void MakeHot(Func<int, int> method)
		{
			for (var round = 0; round < 5; round++)
			{
				for (var i = 0; i < 2000; i++)
					_ = method(2);
				Thread.Sleep(250);
			}
		}

		static void RequireTiering()
		{
			if (AccessTools.IsMonoRuntime || AccessTools.IsNetFrameworkRuntime) Assert.Ignore("Tiered compilation is a CoreCLR feature.");
			if (AppContext.TryGetSwitch("System.Runtime.TieredCompilation", out var enabled) && !enabled) Assert.Ignore("Tiered compilation is off.");
			// A Debug build disables JIT optimization, so its methods never tier up.
			if (typeof(TieredCompilation).Assembly.GetCustomAttribute<DebuggableAttribute>()?.IsJITOptimizerDisabled == true) Assert.Ignore("Methods of a Debug build do not tier up.");
		}

		[Test]
		public void A_hot_method_can_be_patched()
		{
			RequireTiering();
			MakeHot(HotBeforePatch);
			var harmony = new Harmony("test.tiered.hot");
			try
			{
				_ = harmony.Patch(SymbolExtensions.GetMethodInfo(() => HotBeforePatch(0)), postfix: new HarmonyMethod(typeof(TieredCompilation), nameof(Postfix)));
				Assert.That(HotBeforePatch(2), Is.EqualTo(2006));
			}
			finally { harmony.UnpatchAll(harmony.Id); }
			Assert.That(HotBeforePatch(2), Is.EqualTo(1003));
		}

		[Test]
		public void A_patch_survives_tier_up_and_unpatches()
		{
			RequireTiering();
			var harmony = new Harmony("test.tiered.promote");
			try
			{
				_ = harmony.Patch(SymbolExtensions.GetMethodInfo(() => HotAfterPatch(0)), postfix: new HarmonyMethod(typeof(TieredCompilation), nameof(Postfix)));
				Assert.That(HotAfterPatch(2), Is.EqualTo(2006));
				MakeHot(HotAfterPatch);
				Assert.That(HotAfterPatch(2), Is.EqualTo(2006));
			}
			finally { harmony.UnpatchAll(harmony.Id); }
			Assert.That(HotAfterPatch(2), Is.EqualTo(1003));
		}
	}
}
