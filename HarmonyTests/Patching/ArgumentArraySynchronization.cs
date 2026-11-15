using HarmonyLib;
using NUnit.Framework;
using System;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace HarmonyLibTests.Patching
{
	[TestFixture, NonParallelizable]
	public class ArgumentArraySynchronization : TestLogger
	{
		static object[] observed;

		[TestCase(true)]
		[TestCase(false)]
		public void Prefix_argument_arrays_observe_prior_named_argument_changes(bool writerFirst)
		{
			var harmony = new Harmony("test.argument-array-sync");
			var original = AccessTools.DeclaredMethod(typeof(ValueTarget), nameof(ValueTarget.Run));
			observed = null;
			try
			{
				_ = harmony.Patch(original, prefix: Patch(nameof(WriteValues), writerFirst ? Priority.High : Priority.Low));
				_ = harmony.Patch(original, prefix: Patch(nameof(Observe), Priority.Normal));

				var result = ValueTarget.Run("original", 1, 2m);

				Assert.AreEqual(new object[] { "patched", 123, 456m }, result);
				Assert.AreEqual(writerFirst ? new object[] { "patched", 123, 456m } : ["original", 1, 2m], observed);
			}
			finally
			{
				harmony.UnpatchAll(harmony.Id);
			}
		}

		[TestCase(HarmonyPatchType.Prefix)]
		[TestCase(HarmonyPatchType.Postfix)]
		[TestCase(HarmonyPatchType.Finalizer)]
		public void Argument_array_observers_preserve_prior_ref_and_out_writes(HarmonyPatchType role)
		{
			var harmony = new Harmony("test.argument-array-sync");
			var original = AccessTools.DeclaredMethod(typeof(RefOutTarget), nameof(RefOutTarget.Run));
			observed = null;
			try
			{
				AddPatch(harmony, original, role, Patch(nameof(WriteRefOut), Priority.High));
				AddPatch(harmony, original, role, Patch(nameof(Observe), Priority.Low));

				var text = "original";
				new RefOutTarget().Run(ref text, out var flag);

				Assert.AreEqual(new object[] { "patched", true }, observed);
				Assert.AreEqual(role == HarmonyPatchType.Prefix ? "patched/original" : "patched", text);
				Assert.AreEqual(role != HarmonyPatchType.Prefix, flag);
			}
			finally
			{
				harmony.UnpatchAll(harmony.Id);
			}
		}

		[TestCase(HarmonyPatchType.Postfix, false)]
		[TestCase(HarmonyPatchType.Finalizer, false)]
		[TestCase(HarmonyPatchType.Finalizer, true)]
		public void Argument_arrays_observe_original_ref_and_out_changes(HarmonyPatchType role, bool throws)
		{
			var harmony = new Harmony("test.argument-array-sync");
			var original = AccessTools.DeclaredMethod(typeof(RefOutTarget), nameof(RefOutTarget.Run));
			observed = null;
			try
			{
				AddPatch(harmony, original, role, Patch(nameof(Observe), Priority.Normal));
				var text = "input";
				var flag = true;
				var target = new RefOutTarget(throws);

				if (throws)
					_ = Assert.Throws<InvalidOperationException>(() => target.Run(ref text, out flag));
				else
					target.Run(ref text, out flag);

				Assert.AreEqual(new object[] { "input/original", false }, observed);
				Assert.AreEqual("input/original", text);
				Assert.False(flag);
			}
			finally
			{
				harmony.UnpatchAll(harmony.Id);
			}
		}

		[Test]
		public void Argument_arrays_preserve_named_argument_changes_when_original_is_skipped()
		{
			var harmony = new Harmony("test.argument-array-sync");
			var original = AccessTools.DeclaredMethod(typeof(RefOutTarget), nameof(RefOutTarget.Run));
			observed = null;
			try
			{
				_ = harmony.Patch(original, prefix: Patch(nameof(SkipOriginal)), postfix: Patch(nameof(Observe)));
				var text = "original";
				new RefOutTarget().Run(ref text, out var flag);

				Assert.AreEqual(new object[] { "patched", true }, observed);
				Assert.AreEqual("patched", text);
				Assert.True(flag);
			}
			finally
			{
				harmony.UnpatchAll(harmony.Id);
			}
		}

		[Test]
		public void Pass_through_postfix_argument_arrays_preserve_named_argument_changes()
		{
			var harmony = new Harmony("test.argument-array-sync");
			var original = AccessTools.DeclaredMethod(typeof(ValueTarget), nameof(ValueTarget.Run));
			observed = null;
			try
			{
				_ = harmony.Patch(original, postfix: Patch(nameof(WriteValues)));
				_ = harmony.Patch(original, postfix: Patch(nameof(ObservePassThrough)));

				var result = ValueTarget.Run("original", 1, 2m);

				Assert.AreEqual(new object[] { "original", 1, 2m }, result);
				Assert.AreEqual(new object[] { "patched", 123, 456m }, observed);
			}
			finally
			{
				harmony.UnpatchAll(harmony.Id);
			}
		}

		static HarmonyMethod Patch(string name, int priority = Priority.Normal) => new(AccessTools.DeclaredMethod(typeof(ArgumentArraySynchronization), name))
		{
			priority = priority
		};

		static void AddPatch(Harmony harmony, MethodInfo original, HarmonyPatchType role, HarmonyMethod patch)
		{
			_ = role switch
			{
				HarmonyPatchType.Prefix => harmony.Patch(original, prefix: patch),
				HarmonyPatchType.Postfix => harmony.Patch(original, postfix: patch),
				_ => harmony.Patch(original, finalizer: patch)
			};
		}

		static void WriteValues(ref string text, ref int number, ref decimal amount)
		{
			text = "patched";
			number = 123;
			amount = 456m;
		}

		static void WriteRefOut(ref string text, ref bool flag)
		{
			text = "patched";
			flag = true;
		}

		static bool SkipOriginal(ref string text, ref bool flag)
		{
			WriteRefOut(ref text, ref flag);
			return false;
		}

		static void Observe(object[] __args) => observed = (object[])__args.Clone();

		static object[] ObservePassThrough(object[] __result, object[] __args)
		{
			Observe(__args);
			return __result;
		}

		public static class ValueTarget
		{
			[MethodImpl(MethodImplOptions.NoInlining)]
			public static object[] Run(string text, int number, decimal amount) => [text, number, amount];
		}

		public class RefOutTarget(bool throws = false)
		{
			[MethodImpl(MethodImplOptions.NoInlining)]
			public void Run(ref string text, out bool flag)
			{
				text += "/original";
				flag = false;
				if (throws)
					throw new InvalidOperationException("original failure");
			}
		}
	}
}
