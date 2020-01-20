using HarmonyLib;
using NUnit.Framework;
using System.Reflection.Emit;

namespace HarmonyLibTests.IL
{
	[TestFixture]
	public class Instructions
	{
		[Test]
		public void TestMalformedStringOperand()
		{
			var expectedOperand = "this should not fail {4}";
			var inst = new CodeInstruction(OpCodes.Ldstr, expectedOperand);
			Assert.AreEqual($"ldstr \"{expectedOperand}\"", inst.ToString());
		}
	}
}