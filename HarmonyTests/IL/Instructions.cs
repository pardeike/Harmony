using Harmony;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Reflection.Emit;

namespace HarmonyTests.IL
{
	[TestClass]
   public class Instructions
   {
		[TestMethod]
		public void TestMalformedStringOperand()
	   {
			string expectedOperand = "this should not fail {4}";
			var inst = new CodeInstruction(OpCodes.Ldstr, expectedOperand);
			Assert.AreEqual($"ldstr \"{expectedOperand}\"", inst.ToString());
	   }
   }
}
