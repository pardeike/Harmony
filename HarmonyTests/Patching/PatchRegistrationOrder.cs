using HarmonyLib;
using NUnit.Framework;
using System;
using System.Linq;

namespace HarmonyLibTests.Patching
{
	[TestFixture, NonParallelizable]
	public class PatchRegistrationOrder : TestLogger
	{
		static void Callback() { }
		static void Target() { }

		[TestCase(HarmonyPatchType.Prefix), TestCase(HarmonyPatchType.Postfix), TestCase(HarmonyPatchType.Transpiler), TestCase(HarmonyPatchType.Finalizer)]
		[TestCase(HarmonyPatchType.InnerPrefix), TestCase(HarmonyPatchType.InnerPostfix), TestCase(HarmonyPatchType.InnerFinalizer)]
		public void New_registrations_follow_survivors_after_earlier_owners_are_removed(HarmonyPatchType role)
		{
			var info = new PatchInfo();
			(Action<string, HarmonyMethod[]> add, Action<string> remove, Func<Patch[]> read) operations = role switch
			{
				HarmonyPatchType.Prefix => (info.AddPrefixes, info.RemovePrefix, () => info.prefixes),
				HarmonyPatchType.Postfix => (info.AddPostfixes, info.RemovePostfix, () => info.postfixes),
				HarmonyPatchType.Transpiler => (info.AddTranspilers, info.RemoveTranspiler, () => info.transpilers),
				HarmonyPatchType.Finalizer => (info.AddFinalizers, info.RemoveFinalizer, () => info.finalizers),
				HarmonyPatchType.InnerPrefix => (info.AddInnerPrefixes, info.RemoveInnerPrefix, () => info.innerprefixes),
				HarmonyPatchType.InnerPostfix => (info.AddInnerPostfixes, info.RemoveInnerPostfix, () => info.innerpostfixes),
				_ => (info.AddInnerFinalizers, info.RemoveInnerFinalizer, () => info.innerfinalizers)
			};
			var (add, remove, read) = operations;
			var method = new HarmonyMethod(AccessTools.DeclaredMethod(typeof(PatchRegistrationOrder), nameof(Callback)));
			if (role is HarmonyPatchType.InnerPrefix or HarmonyPatchType.InnerPostfix or HarmonyPatchType.InnerFinalizer)
				method.innerMethod = new InnerMethod(AccessTools.DeclaredMethod(typeof(PatchRegistrationOrder), nameof(Target)));
			add("A", [method, method]);
			add("B", [method]);
			remove("A");
			var survivor = read().Single();
			add("C", [null, method, null, method]);
			Assert.AreSame(survivor, read()[0]);
			Assert.AreEqual(new[] { 2, 3, 4 }, read().Select(patch => patch.index));
			Assert.AreEqual(new[] { "B", "C", "C" }, new PatchSorter(read(), false, true).Sort().Select(patch => patch.owner));
			var unchanged = read();
			add("ignored", [null]);
			Assert.AreSame(unchanged, read());
			remove("B");
			remove("C");
			add("D", [method]);
			Assert.AreEqual(0, read().Single().index);
		}

		[TestCase(int.MaxValue, 1), TestCase(int.MaxValue - 1, 2)]
		public void Exhausted_indices_fail_without_reordering_or_installing_part_of_a_batch(int index, int count)
		{
			var method = new HarmonyMethod(AccessTools.DeclaredMethod(typeof(PatchRegistrationOrder), nameof(Callback)));
			var info = new PatchInfo { prefixes = [new Patch(method, index, "survivor")] };
			var before = info.prefixes;
			Assert.Throws<OverflowException>(() => info.AddPrefixes("new", Enumerable.Repeat(method, count).ToArray()));
			Assert.AreSame(before, info.prefixes);
			Assert.DoesNotThrow(() => info.AddPrefixes("ignored", [null]));
			Assert.AreSame(before, info.prefixes);
		}
	}
}
