using HarmonyLib;
using NUnit.Framework;
using Patching_Infix;
using System;
using System.Linq;

namespace HarmonyLibTests.Patching
{
	[TestFixture, NonParallelizable]
	public class InfixDocumentation : TestLogger
	{
		[Test]
		public void Compiled_finalizer_example_preserves_the_waiting_outer_value()
		{
			var harmony = new Harmony("test.infix.docs.finalizer." + Guid.NewGuid());
			try
			{
				harmony.CreateClassProcessor(typeof(RecoverPatch)).Patch();
				Assert.AreEqual(8, RecoveringOuter.Total("3"));
				Assert.AreEqual(5, RecoveringOuter.Total("invalid"));
				Assert.Throws<ArgumentNullException>(() => RecoveringOuter.Total(null));
			}
			finally { harmony.UnpatchAll(harmony.Id); }
		}

		[Test]
		public void Compiled_auto_example_changes_the_live_iterator_field()
		{
			var harmony = new Harmony("test.infix.docs.generated." + Guid.NewGuid());
			try
			{
				harmony.CreateClassProcessor(typeof(SequencePatch)).Patch();
				Assert.That(Sequence.Count(5).ToArray(), Is.EqualTo(new[] { 5, 1 }));
				Assert.That(Sequence.Count(3).ToArray(), Is.EqualTo(new[] { 3, 1 }));
			}
			finally { harmony.UnpatchAll(harmony.Id); }
			Assert.That(Sequence.Count(3).ToArray(), Is.EqualTo(new[] { 3, 2, 1 }));
		}

		[Test]
		public void Compiled_capture_example_shows_the_unit_price_at_a_later_call()
		{
			var harmony = new Harmony("test.infix.docs.capture." + Guid.NewGuid());
			try
			{
				harmony.CreateClassProcessor(typeof(UnitPricePatch)).Patch();
				Assert.AreEqual("apple: 6 (3 x 2)", Receipt.Line("apple", 3));
				Assert.AreEqual("pear: 5", Receipt.Line("pear", 1));
			}
			finally { harmony.UnpatchAll(harmony.Id); }
			Assert.AreEqual("apple: 6", Receipt.Line("apple", 3));
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
