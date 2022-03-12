using HarmonyLib;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using static HarmonyLib.Code;

namespace HarmonyLibTests.IL
{
	[TestFixture, NonParallelizable]
	public class Instructions : TestLogger
	{
		[Test]
		public void Test_MalformedStringOperand()
		{
			var expectedOperand = "this should not fail {4}";
			var inst = new CodeInstruction(OpCodes.Ldstr, expectedOperand);
			Assert.AreEqual($"ldstr \"{expectedOperand}\"", inst.ToString());
		}

		[Test]
		public void Test_Code()
		{
			var c0 = Operand;
			Assert.False(c0.opcode.IsValid());
			Assert.AreEqual(null, c0.operand);
			Assert.AreEqual(null, c0.name);

			var c1 = Nop;
			Assert.AreEqual(OpCodes.Nop, c1.opcode);
			Assert.AreEqual(null, c1.operand);
			Assert.AreEqual(null, c1.name);

			var c2 = Nop["test"];
			Assert.AreEqual(OpCodes.Nop, c2.opcode);
			Assert.AreEqual("test", c2.operand);
			Assert.AreEqual(null, c2.name);

			var c3 = Nop[name: "test"];
			Assert.AreEqual(OpCodes.Nop, c3.opcode);
			Assert.AreEqual(null, c3.operand);
			Assert.AreEqual("test", c3.name);

			var c4 = Nop[typeof(void), "test2"];
			Assert.AreEqual(OpCodes.Nop, c4.opcode);
			Assert.AreEqual(typeof(void), c4.operand);
			Assert.AreEqual("test2", c4.name);

			var c5 = Nop[123][name: "test"];
			Assert.AreEqual(OpCodes.Nop, c5.opcode);
			Assert.AreEqual(123, c5.operand);
			Assert.AreEqual("test", c5.name);

			var label = new Label();
			var c6 = Nop.WithLabels(label);
			Assert.AreEqual(1, c6.labels.Count);
			Assert.AreEqual(label, c6.labels[0]);

			static IEnumerable<CodeInstruction> Emitter()
			{
				yield return Nop;
			}
			var list = Emitter().ToList();
			Assert.AreEqual(1, list.Count);
			Assert.AreEqual(OpCodes.Nop, list[0].opcode);
		}
	}
}
