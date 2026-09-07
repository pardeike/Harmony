using HarmonyLib;
using NUnit.Framework;
using Patching_Infix;
using System;

namespace HarmonyLibTests.Patching
{
	[TestFixture, NonParallelizable]
	public class InfixDocumentation : TestLogger
	{
		[Test]
		public void Compiled_capture_example_bridges_constructor_and_literal_sites()
		{
			var harmony = new Harmony("test.infix.docs.capture." + Guid.NewGuid());
			try
			{
				harmony.CreateClassProcessor(typeof(ReportPatch)).Patch();
				Assert.AreEqual("summary: Alice: StatsReport_FinalValue", Report.Build("Alice"));
				Assert.AreEqual("summary: Bob: StatsReport_FinalValue", Report.Build("Bob"));
			}
			finally { harmony.UnpatchAll(harmony.Id); }
		}
		[TestCase(0), TestCase(1)]
		public void Compiled_attribute_example_has_documented_trace(int mode)
		{
			var harmony = new Harmony("test.infix.docs." + Guid.NewGuid());
			DecidePatch.Events.Clear();
			try
			{
				Installation.Install(harmony);
				Assert.That(Outer.Run("hello", mode), Is.EqualTo(mode != 0));
				Assert.That(DecidePatch.Events, Is.EqualTo(mode == 0
					? new[] { "high priority", "low priority: False", "postfix: False, False" }
					: new[] { "high priority", "low priority: True", "call: hello.", "postfix: True, True" }));
			}
			finally { harmony.UnpatchAll(harmony.Id); }
		}
		[Test]
		public void Compiled_manual_example_installs_the_same_prefix()
		{
			var harmony = new Harmony("test.infix.docs.manual." + Guid.NewGuid());
			DecidePatch.Events.Clear();
			try
			{
				Installation.InstallManual(harmony);
				Assert.That(Outer.Run("hello", 0), Is.False);
				Assert.That(DecidePatch.Events, Is.EqualTo(new[] { "high priority" }));
			}
			finally { harmony.UnpatchAll(harmony.Id); }
		}
	}
}
