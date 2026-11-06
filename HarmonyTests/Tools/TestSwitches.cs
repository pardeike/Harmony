using HarmonyLib;
using System;
using NUnit.Framework;

namespace HarmonyLibTests.Tools
{
	[TestFixture, NonParallelizable]
	public class Test_Switches : TestLogger
	{
		[TestCase("1", true), TestCase("0", false), TestCase(" true ", true), TestCase("False", false), TestCase("", null), TestCase("yes", null), TestCase(null, null)]
		public void Test_ValidateStackEnvironmentValues(string value, bool? expected) => Assert.That(Harmony.ParseSwitch(value), Is.EqualTo(expected));

		[Test]
		public void Test_ValidateStackProperty()
		{
			if (Environment.GetEnvironmentVariable("HARMONY_VALIDATE_STACK") is not null) Assert.Ignore("HARMONY_VALIDATE_STACK overrides the property");
			Assert.That(Harmony.ValidateStack, Is.True);
			try
			{
				Harmony.ValidateStack = false;
				Assert.That(Harmony.ValidateStack, Is.False);
			}
			finally { Harmony.ValidateStack = true; }
		}

		[Test]
		public void Test_SetAndGetSwitch()
		{
			Harmony.SetSwitch("DMDDebug", true);
			var result = Harmony.TryGetSwitch("DMDDebug", out var value);
			Assert.IsTrue(result);
			Assert.IsTrue(value is bool);
			Assert.IsTrue((bool)value);
		}

		[Test]
		public void Test_SetAndGetStringSwitch()
		{
			var testPath = "Path\\To\\Dir\\Where\\Should\\Go";
			Harmony.SetSwitch("DMDDumpTo", testPath);
			var result = Harmony.TryGetSwitch("DMDDumpTo", out var value);
			Assert.IsTrue(result);
			Assert.IsTrue(value is string);
			Assert.AreEqual(testPath, (string)value);
		}

		[Test]
		public void Test_ClearSwitch()
		{
			Harmony.SetSwitch("DMDDebug", true);
			var resultBefore = Harmony.TryGetSwitch("DMDDebug", out var valueBefore);
			Assert.IsTrue(resultBefore);
			Assert.IsNotNull(valueBefore);

			Harmony.ClearSwitch("DMDDebug");
			var resultAfter = Harmony.TryGetSwitch("DMDDebug", out var valueAfter);
			Assert.IsFalse(resultAfter);
		}

		[Test]
		public void Test_TryIsSwitchEnabled()
		{
			Harmony.SetSwitch("DMDDebug", true);
			var result = Harmony.TryIsSwitchEnabled("DMDDebug", out var isEnabled);
			Assert.IsTrue(result);
			Assert.IsTrue(isEnabled);

			Harmony.SetSwitch("DMDDebug", false);
			result = Harmony.TryIsSwitchEnabled("DMDDebug", out isEnabled);
			Assert.IsTrue(result);
			Assert.IsFalse(isEnabled);
		}

		[Test]
		public void Test_GetNonExistentSwitch()
		{
			var result = Harmony.TryGetSwitch("NonExistentSwitch", out var value);
			Assert.IsFalse(result);
		}

		[Test]
		public void Test_IsSwitchEnabledNonExistent()
		{
			var result = Harmony.TryIsSwitchEnabled("NonExistentSwitch", out var isEnabled);
			Assert.IsFalse(result);
		}
	}
}
