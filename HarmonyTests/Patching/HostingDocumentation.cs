using HarmonyLib;
using Hosting;
using NUnit.Framework;

namespace HarmonyLibTests.Patching
{
	[TestFixture, NonParallelizable]
	public class HostingDocumentation : TestLogger
	{
		[Test]
		public void The_documented_host_receives_a_complete_body_and_installs_nothing()
		{
			if (!MethodBodyGenerator.HasDynamicIL) Assert.Ignore("Body export requires a modern Harmony target.");
			MyHost.Register();
			var harmony = new Harmony("hosting-example");
			try
			{
				harmony.CreateClassProcessor(typeof(ScorePatch)).Patch();
				var score = typeof(Game).GetMethod(nameof(Game.Score));
				Assert.That(MyRuntime.Bodies.ContainsKey(score), Is.True);
				// A fat CLI header: the first two bits are set.
				Assert.That(MyRuntime.Bodies[score][0] & 0x03, Is.EqualTo(0x03));
				// The runtime stand-in installs nothing, so the original keeps running.
				Assert.That(new Game().Score(1), Is.EqualTo(1));
				harmony.UnpatchAll(harmony.Id);
				Assert.That(Harmony.GetPatchInfo(score).Postfixes, Is.Empty);
			}
			finally
			{
				harmony.UnpatchAll(harmony.Id);
				HarmonyLib.MethodBodyPatching.ClearProvider();
				MyRuntime.Bodies.Clear();
			}
		}
	}
}
