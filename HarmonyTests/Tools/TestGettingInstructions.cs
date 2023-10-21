using HarmonyLib;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace HarmonyLibTests.Tools
{
	public class InstructionTest
	{
		[MethodImpl(MethodImplOptions.NoInlining)]
		public int Method(string input)
		{
			try
			{
				var count = 0;
				for (var i = 0; i < input.Length; i++)
					if (input[i] == ' ')
						count++;
				return count;
			}
			catch
			{
				return -1;
			}
		}

		[HarmonyPatch]
		public class Patch
		{
			[HarmonyPatch(typeof(InstructionTest), nameof(Method))]
			static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
			{
				return Transpilers.Manipulator(instructions, instr => instr.OperandIs((int)' '), instr => instr.operand = (int)'*');
			}
		}
	}

	[TestFixture, NonParallelizable]
	public class Test_GettingInstructions : TestLogger
	{
		[Test]
		public void Test_GetCurrentInstructions()
		{
			var instance = new InstructionTest();
			var method = SymbolExtensions.GetMethodInfo(() => instance.Method(""));

			Assert.AreEqual(1, instance.Method("Foo Bar"), "method output 1");

			var originalInstructions = PatchProcessor.GetCurrentInstructions(method);
			var m_get_Chars = AccessTools.Method("System.String:get_Chars");
			Assert.IsTrue(originalInstructions.Any(instr => instr.Calls(m_get_Chars)));
			var m_get_Length = AccessTools.Method("System.String:get_Length");
			Assert.IsTrue(originalInstructions.Any(instr => instr.Calls(m_get_Length)));

			var processor = new PatchClassProcessor(new Harmony("instructions"), typeof(InstructionTest.Patch));
			var patches = processor.Patch();
			Assert.AreEqual(1, patches.Count, "patch count");

			Assert.AreEqual(1, instance.Method("Foo*Bar"), "method output 2");

			var newInstructions = PatchProcessor.GetCurrentInstructions(method);
			Assert.AreEqual(originalInstructions.Count, newInstructions.Count, "instruction count");

			var changed = new List<CodeInstruction>();
			for (var i = 0; i < originalInstructions.Count; i++)
				if (originalInstructions[i].ToString() != newInstructions[i].ToString())
					changed.Add(newInstructions[i]);
			Assert.AreEqual(1, changed.Count, "change count");
			Assert.AreEqual('*', changed[0].operand);

			var unchangedInstructions = PatchProcessor.GetCurrentInstructions(method, 0);
			Assert.AreEqual(originalInstructions.Count, unchangedInstructions.Count, "unchanged count");

			for (var i = 0; i < originalInstructions.Count; i++)
				if (originalInstructions[i].ToString() != unchangedInstructions[i].ToString())
					Assert.Fail("Instruction " + i + " differs");
		}
	}
}
