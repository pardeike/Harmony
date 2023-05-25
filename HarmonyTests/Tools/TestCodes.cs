using HarmonyLib;
using NUnit.Framework;
using System.Reflection.Emit;
using static HarmonyLib.Code;

namespace HarmonyLibTests.Tools
{
	[TestFixture]
	public class Test_Codes : TestLogger
	{
		[Test]
		public void Test_Basic_Code_Usage()
		{
			var code1 = Ldstr["hello"];
			Assert.AreEqual(OpCodes.Ldstr, code1.opcode);
			Assert.AreEqual("hello", code1.operand);
			Assert.AreEqual(0, code1.labels.Count);
			Assert.AreEqual(0, code1.blocks.Count);
			Assert.AreEqual(0, code1.jumpsFrom.Count);
			Assert.AreEqual(0, code1.jumpsTo.Count);
			Assert.AreEqual(null, code1.predicate);

			var code2 = Ldarg_0;
			Assert.AreEqual(OpCodes.Ldarg_0, code2.opcode);
			Assert.AreEqual(null, code2.operand);
			Assert.AreEqual(0, code2.labels.Count);
			Assert.AreEqual(0, code2.blocks.Count);
			Assert.AreEqual(0, code2.jumpsFrom.Count);
			Assert.AreEqual(0, code2.jumpsTo.Count);
			Assert.AreEqual(null, code2.predicate);
		}

		[Test]
		public void Test_CodeMatch_Usage()
		{
			var code = Ldstr["test", "foo"];
			var match = new CodeMatch(OpCodes.Ldstr, "test", "foo");
			Assert.AreEqual("[foo: opcodes=ldstr operands=test]", match.ToString());
			Assert.AreEqual("[foo: opcodes=ldstr operands=test]", code.ToString());
		}
	}
}
