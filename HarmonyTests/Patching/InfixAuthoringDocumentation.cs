using HarmonyLib;
using NUnit.Framework;
using Patching_Infix_Authoring;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace HarmonyLibTests.Patching
{
	[TestFixture, NonParallelizable]
	public class InfixAuthoringDocumentation : TestLogger
	{
		[SetUp] public void ClearEvents() => Probe.Events.Clear();
		static MethodInfo Recipe(string name) => AccessTools.DeclaredMethod(typeof(Recipes), name);

		[TestCase(nameof(Recipes.AroundFirst), "before,tick,after,tick,tick")]
		[TestCase(nameof(Recipes.ReplaceFirst), "replacement,tick,tick")]
		[TestCase(nameof(Recipes.DeleteFirst), "tick,tick")]
		[TestCase(nameof(Recipes.RequireTwoCalls), "replacement,replacement,replacement")]
		[TestCase(nameof(Recipes.FirstTwoCalls), "replacement,replacement,tick")]
		[TestCase(nameof(Recipes.EntryAndExit), "enter,tick,tick,tick,exit")]
		public void Compiled_recipes_make_the_documented_edits(string recipe, string expected)
		{
			var harmony = new Harmony("test.infix.authoring." + Guid.NewGuid());
			try
			{
				var wrapper = harmony.Patch(Recipes.Method(nameof(Probe.ThreeCalls)), transpiler: new HarmonyMethod(Recipe(recipe)));
				wrapper.Invoke(null, null);
				Assert.That(Probe.Events, Is.EqualTo(expected.Split(',')));
			}
			finally { harmony.UnpatchAll(harmony.Id); }
		}

		[TestCase(-4), TestCase(4)]
		public void Entry_exit_recipe_preserves_return_values(int value)
		{
			var harmony = new Harmony("test.infix.authoring.returns." + Guid.NewGuid());
			try
			{
				var wrapper = harmony.Patch(Recipes.Method(nameof(Probe.Branch)), transpiler: new HarmonyMethod(Recipe(nameof(Recipes.EntryAndExit))));
				Assert.That(wrapper.Invoke(null, [value]), Is.EqualTo(4));
				Assert.That(Probe.Events, Is.EqualTo(new[] { "enter", "exit" }));
			}
			finally { harmony.UnpatchAll(harmony.Id); }
		}

		static void RunBranch(Func<IEnumerable<CodeInstruction>, IEnumerable<CodeInstruction>> recipe, bool targetReturn)
		{
			var config = new MethodCreatorConfig(Recipes.Method(nameof(Probe.OneCall)), null, [], [], [], [], [], [], false);
			var creator = new MethodCreator(config);
			var target = config.DefineLabel();
			var tick = new CodeInstruction(OpCodes.Call, Recipes.Method(nameof(Probe.Tick)));
			var ret = new CodeInstruction(OpCodes.Ret);
			(targetReturn ? ret : tick).labels.Add(target);
			var input = new[] { new CodeInstruction(OpCodes.Br, target), tick, ret };
			creator.EmitCodes(new Emitter(config.il), recipe(input).ToList());
			config.GenerateMethod().Invoke(null, null);
		}

		[Test]
		public void Branches_to_a_matched_call_run_inserted_before_logic()
		{
			RunBranch(Recipes.AroundFirst, false);
			Assert.That(Probe.Events, Is.EqualTo(new[] { "before", "tick", "after" }));
		}

		[Test]
		public void Removing_a_labeled_void_call_preserves_the_branch_destination()
		{
			RunBranch(Recipes.DeleteFirst, false);
			Assert.That(Probe.Events, Is.Empty);
		}

		[Test]
		public void Branches_to_a_return_run_inserted_exit_logic()
		{
			RunBranch(Recipes.EntryAndExit, true);
			Assert.That(Probe.Events, Is.EqualTo(new[] { "enter", "exit" }));
		}

		[Test]
		public void Minimum_and_first_N_have_different_missing_match_behavior()
		{
			var input = new[] { new CodeInstruction(OpCodes.Call, Recipes.Method(nameof(Probe.Tick))), new CodeInstruction(OpCodes.Ret) };
			Assert.Throws<InvalidOperationException>(() => Recipes.RequireTwoCalls(input).ToArray());
			Assert.That(Recipes.FirstTwoCalls(input).Count(instruction => instruction.Calls(Recipes.Method(nameof(Probe.Replacement)))), Is.EqualTo(1));
			Assert.That(Recipes.FirstTwoCalls([new CodeInstruction(OpCodes.Ret)]).Count(), Is.EqualTo(1));
		}

		[Test]
		public void Cookbook_scope_explicitly_rejects_exception_boundaries()
		{
			var input = new[] { new CodeInstruction(OpCodes.Nop).WithBlocks(new ExceptionBlock(ExceptionBlockType.BeginExceptionBlock)) };
			Assert.Throws<InvalidOperationException>(() => Recipes.EntryAndExit(input).ToArray());
		}

		[Test]
		public void Removing_a_group_keeps_other_owners_and_removes_all_its_outer_targets()
		{
			var other = new Harmony("test.infix.authoring.other." + Guid.NewGuid());
			try
			{
				Recipes.InstallGroup();
				other.Patch(Recipes.Method(nameof(Probe.OneCall)), prefix: new HarmonyMethod(Recipes.Method(nameof(Probe.Before))));
				Probe.OneCall();
				Assert.That(Probe.Events, Is.EqualTo(new[] { "before", "group", "tick" }));
				Recipes.RemoveGroup();
				Probe.Events.Clear();
				Probe.OneCall();
				Probe.ThreeCalls();
				Assert.That(Probe.Events, Is.EqualTo(new[] { "before", "tick", "tick", "tick", "tick" }));
			}
			finally { Recipes.RemoveGroup(); other.UnpatchAll(other.Id); }
		}
	}
}
