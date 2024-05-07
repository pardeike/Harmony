using HarmonyLib;
using HarmonyTests.Tools.Assets;
using NUnit.Framework;
using NUnit.Framework.Internal;
using System.Collections.Generic;
using System.Reflection.Emit;
using static HarmonyLib.Code;

namespace HarmonyLibTests.Tools
{
	[TestFixture, NonParallelizable]
	public class Test_CodeMatcher : TestLogger
	{
		[Test]
		public void Test_CodeMatch()
		{
			var method = SymbolExtensions.GetMethodInfo(() => CodeMatcherClass.Bar(""));
			var match = new CodeMatch(OpCodes.Call, method);
			Assert.AreEqual(match.opcode, OpCodes.Call);
			Assert.AreEqual(match.opcodeSet, new HashSet<OpCode>() { OpCodes.Call });
			Assert.AreEqual(match.operand, method);
			Assert.AreEqual(match.operands, new[] { method });
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
			var method = SymbolExtensions.GetMethodInfo(() => CodeMatcherClass.Bar(""));
			var code = Call[method];
			Assert.AreEqual(code.opcode, OpCodes.Call);
			Assert.AreEqual(code.opcodeSet, new HashSet<OpCode>() { OpCodes.Call });
			Assert.AreEqual(code.operand, method);
			Assert.AreEqual(code.operands, new[] { method });
		}

		[Test]
		public void Test_MatchStartForward_Code()
		{
			var method = SymbolExtensions.GetMethodInfo(() => CodeMatcherClass.Method());
			var instructions = PatchProcessor.GetOriginalInstructions(method);

			var mFoo = SymbolExtensions.GetMethodInfo(() => CodeMatcherClass.Foo());
			var mBar = SymbolExtensions.GetMethodInfo(() => CodeMatcherClass.Bar(""));

			var matcher = new CodeMatcher(instructions).MatchStartForward(Call[mBar]).ThrowIfNotMatch("not found");
			Assert.AreEqual(OpCodes.Call, instructions[matcher.Pos].opcode);
			Assert.AreEqual(mBar, instructions[matcher.Pos].operand);
		}

		[Test]
		public void Test_MatchStartForward_CodeMatch()
		{
			var method = SymbolExtensions.GetMethodInfo(() => CodeMatcherClass.Method());
			var instructions = PatchProcessor.GetOriginalInstructions(method);

			var mFoo = SymbolExtensions.GetMethodInfo(() => CodeMatcherClass.Foo());
			var mBar = SymbolExtensions.GetMethodInfo(() => CodeMatcherClass.Bar(""));

			var matcher = new CodeMatcher(instructions).MatchStartForward(new CodeMatch(OpCodes.Call, mBar)).ThrowIfNotMatch("not found");
			Assert.AreEqual(OpCodes.Call, instructions[matcher.Pos].opcode);
			Assert.AreEqual(mBar, instructions[matcher.Pos].operand);
		}
	}
}
