using HarmonyLib;
using HarmonyTests.Tools.Assets;
using NUnit.Framework;
using NUnit.Framework.Internal;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using static HarmonyLib.Code;

namespace HarmonyLibTests.Tools
{
	[TestFixture, NonParallelizable]
	public class Test_CodeMatcher : TestLogger
	{
		static List<CodeInstruction> testInstructions;
		static int testInstructionsCount = 0;
		static MethodInfo mFoo = SymbolExtensions.GetMethodInfo(() => CodeMatcherClass.Foo());
		static MethodInfo mBar = SymbolExtensions.GetMethodInfo(() => CodeMatcherClass.Bar(""));

		static Test_CodeMatcher()
		{
			// make sure Debug and Release both return the same instructions
			var method = SymbolExtensions.GetMethodInfo(() => CodeMatcherClass.Method());
			testInstructions = [.. PatchProcessor
				.GetOriginalInstructions(method)
				.Where(instr => instr.opcode != OpCodes.Nop)];
			testInstructionsCount = testInstructions.Count;
			// 00: CALL		Foo()
			// 01: LDSTR	"A"
			// 02: CALL		Bar(String)
			// 03: LDSTR	"B"
			// 04: CALL		Bar(String)
			// 05: LDSTR	"C"
			// 06: CALL		Bar(String) -- bar-foo-1
			// 07: CALL		Foo()
			// 08: LDSTR	"D"
			// 09: CALL		Bar(String) -- bar-foo-2
			// 10: CALL		Foo()
			// 11: LDSTR	"E"
			// 12: CALL		Bar(String)
			// 13: LDSTR	"F"
			// 14: CALL		Bar(String)
			// 15: LDSTR	"G"
			// 16: CALL		Bar(String)
			// 17: LDSTR	"H"
			// 18: CALL		Bar(String) -- bar-foo-3
			// 19: CALL		Foo()
			// 20: RET
		}

		static List<CodeInstruction> Instructions => [.. testInstructions];

		[Test]
		public void Test_CodeMatch_Setup() => Assert.AreEqual(testInstructionsCount, 21);

		[Test]
		public void Test_CodeMatch()
		{
			var match = new CodeMatch(OpCodes.Call, mBar, "something");
			Assert.AreEqual(match.opcode, OpCodes.Call);
			Assert.AreEqual(match.opcodeSet, new HashSet<OpCode>() { OpCodes.Call });
			Assert.AreEqual(match.operand, mBar);
			Assert.AreEqual(match.operands, new[] { mBar });
			Assert.AreEqual(match.name, "something");
		}

		[Test]
		public void Test_Code_Without_Argument()
		{
			var match = Ldc_I4_0;
			Assert.AreEqual(match.opcode, OpCodes.Ldc_I4_0);
			Assert.AreEqual(match.opcodeSet, new HashSet<OpCode>() { OpCodes.Ldc_I4_0 });
			Assert.IsNull(match.operand);
			Assert.IsEmpty(match.operands);
		}

		[Test]
		public void Test_Code_With_Argument()
		{
			var code = Call[mBar];
			Assert.AreEqual(code.opcode, OpCodes.Call);
			Assert.AreEqual(code.opcodeSet, new HashSet<OpCode>() { OpCodes.Call });
			Assert.AreEqual(code.operand, mBar);
			Assert.AreEqual(code.operands, new[] { mBar });
		}

		[Test]
		public void Test_Advance_Start_End()
		{
			var matcher = new CodeMatcher(Instructions)
				.Start()
				.Do(m => Assert.AreEqual(0, m.Pos))
				.Advance(2)
				.Do(m => Assert.AreEqual(2, m.Pos))
				.End()
				.Do(m => Assert.AreEqual(20, m.Pos))
				.Advance(1);
			Assert.IsTrue(matcher.IsInvalid);
		}

		[Test]
		public void Test_MatchStartForward_Code()
		{
			var instructions = Instructions;
			_ = new CodeMatcher(instructions)
				.MatchStartForward(Call[mBar], Call[mFoo])
				.ThrowIfNotMatch("not found")
				.Do(m =>
				{
					Assert.True(instructions[m.Pos].Is(OpCodes.Call, mBar));
					Assert.AreEqual(6, m.Pos);
				});
		}

		[Test]
		public void Test_MatchEndForward_CodeMatch()
		{
			var instructions = Instructions;
			_ = new CodeMatcher(instructions)
				.MatchEndForward(Call[mBar], Call[mFoo])
				.ThrowIfNotMatch("not found")
				.Do(m =>
				{
					Assert.True(instructions[m.Pos].Is(OpCodes.Call, mFoo));
					Assert.AreEqual(7, m.Pos);
				});
		}

		[Test]
		public void Test_MatchStartForward_Repeat_Code()
		{
			var expectedPositions = new[] { 6, 9, 18 };
			var instructions = Instructions;
			var count = 0;
			var err = 0;
			_ = new CodeMatcher(instructions)
				.Start()
				.MatchStartForward(Call[mBar], Call[mFoo])
				.Repeat(
					m =>
					{
						Assert.AreEqual(expectedPositions[count++], m.Pos);
						Assert.True(instructions[m.Pos].Is(OpCodes.Call, mBar));
						_ = m.Advance(2);
					},
					_ => err++
				);
			Assert.AreEqual(3, count);
			Assert.AreEqual(0, err);
		}

		[Test]
		public void Test_MatchStartForward_Repeat_With_Prepare_Code()
		{
			var expectedPositions = new[] { 0, 6, 9, 18 };
			var instructions = Instructions;
			var count = 0;
			var err = 0;
			_ = new CodeMatcher(instructions)
				.Start()
				.PrepareMatchStartForward(Call[mBar], Call[mFoo])
				.Repeat(
					m =>
					{
						Assert.AreEqual(expectedPositions[count++], m.Pos);
						if (m.Pos > 0)
							Assert.True(instructions[m.Pos].Is(OpCodes.Call, mBar));
						_ = m.Advance(2);
					},
					_ => err++
				);
			Assert.AreEqual(4, count);
			Assert.AreEqual(0, err);
		}

		[Test]
		public void Test_SearchForward_And_Backward()
		{
			_ = new CodeMatcher(Instructions)
				.SearchForward(ci => ci.OperandIs("D"))
				.Do(m => Assert.AreEqual(8, m.Pos))
				.SearchBackwards(ci => ci.opcode == OpCodes.Ldstr)
				.Do(m => Assert.AreEqual(8, m.Pos))
				.SearchBackwards(ci => ci.OperandIs("C"))
				.Do(m => Assert.AreEqual(5, m.Pos));
		}

		[Test]
		public void Test_RemoveSearchForward()
		{
			_ = new CodeMatcher(Instructions)
				.SearchForward(ci => ci.OperandIs("D"))
				.RemoveSearchForward(ci => ci.OperandIs("F"))
				.Do(m =>
				{
					Assert.AreEqual(8, m.Pos);
					Assert.AreEqual(testInstructionsCount - 5, m.Length);
					Assert.AreEqual(m.Operand, "F");
				});
		}

		[Test]
		public void Test_RemoveSearchForward_NotFound()
		{
			_ = new CodeMatcher(Instructions)
				.Do(m => Assert.True(m.IsInvalid))
				.SearchForward(ci => ci.OperandIs("D"))
				.Do(m => Assert.True(m.IsValid))
				.RemoveSearchForward(ci => ci.OperandIs("X"))
				.Do(m => Assert.True(m.IsInvalid));
		}

		[Test]
		public void Test_RemoveSearchBackward()
		{
			_ = new CodeMatcher(Instructions)
				.SearchForward(ci => ci.OperandIs("F"))
				.RemoveSearchBackward(ci => ci.OperandIs("D"))
				.Do(m =>
				{
					Assert.AreEqual(8, m.Pos);
					Assert.AreEqual(testInstructionsCount - 5, m.Length);
					Assert.AreEqual(m.Operand, "D");
				});
		}

		[Test]
		public void Test_RemoveSearchBackward_NotFound()
		{
			_ = new CodeMatcher(Instructions)
				.Do(m => Assert.True(m.IsInvalid))
				.SearchForward(ci => ci.OperandIs("F"))
				.Do(m => Assert.True(m.IsValid))
				.RemoveSearchBackward(ci => ci.OperandIs("X"))
				.Do(m => Assert.True(m.IsInvalid));
		}

		[Test]
		public void Test_RemoveUntilForward()
		{
			_ = new CodeMatcher(Instructions)
				.Start()
				.RemoveUntilForward(Call[mBar], Call[mFoo])
				.Do(m =>
				{
					Assert.AreEqual(0, m.Pos);
					Assert.AreEqual(testInstructionsCount - 6, m.Length);
					Assert.AreEqual(m.Operand, mBar);
				});
		}

		[Test]
		public void Test_RemoveUntilForward_NotFound()
		{
			_ = new CodeMatcher(Instructions)
				.Do(m => Assert.True(m.IsInvalid))
				.SearchForward(ci => ci.OperandIs("F"))
				.Do(m => Assert.True(m.IsValid))
				.RemoveUntilForward(Ldstr["X"], Ldstr["Y"])
				.Do(m => Assert.True(m.IsInvalid));
		}

		[Test]
		public void Test_RemoveUntilBackward()
		{
			_ = new CodeMatcher(Instructions)
				.End()
				.RemoveUntilBackward(Call[mBar], Ldstr["H"])
				.Do(m =>
				{
					Assert.AreEqual(17, m.Pos);
					Assert.AreEqual(testInstructionsCount - 3, m.Length);
					Assert.AreEqual(m.Operand, "H");
				});
		}

		[Test]
		public void Test_RemoveUntilBackward_NotFound()
		{
			_ = new CodeMatcher(Instructions)
				.Do(m => Assert.True(m.IsInvalid))
				.SearchForward(ci => ci.OperandIs("F"))
				.Do(m => Assert.True(m.IsValid))
				.RemoveUntilBackward(Ldstr["X"], Ldstr["Y"])
				.Do(m => Assert.True(m.IsInvalid));
		}
	}
}
