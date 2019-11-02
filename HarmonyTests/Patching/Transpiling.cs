using HarmonyLib;
using HarmonyLibTests.Assets;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;

namespace HarmonyLibTests.Patching
{
	[TestFixture]
	public class Transpiling
	{
		static CodeInstruction[] savedInstructions = null;

#if DEBUG
		static OpCode insertLoc = OpCodes.Stloc_3;
		static readonly int codeLength = 75;
#else
		static readonly OpCode insertLoc = OpCodes.Stloc_1;
		static readonly int codeLength = 61;
#endif

		[TestCase(nameof(TestTranspiler))]
		[TestCase(nameof(TestTranspilerICollection))]
		[TestCase(nameof(TestTranspilerIList))]
		[TestCase(nameof(TestTranspilerList))]
		public void TestTranspilerException1(string transpilerMethodName)
		{
			var test = new Class3();

			test.TestMethod("start");
			Assert.AreEqual(test.GetLog, "start,test,ex:DivideByZeroException,finally,end");

			var original = AccessTools.Method(typeof(Class3), nameof(Class3.TestMethod));
			Assert.IsNotNull(original);

			var transpiler = AccessTools.Method(typeof(Transpiling), transpilerMethodName);
			Assert.IsNotNull(transpiler);

			var instance = new Harmony("test-exception1");
			instance.Patch(original, null, null, new HarmonyMethod(transpiler));
			Assert.IsNotNull(savedInstructions);
			Assert.AreEqual(savedInstructions.Length, codeLength);

			test.TestMethod("restart");
			Assert.AreEqual(test.GetLog, "restart,test,patch,ex:DivideByZeroException,finally,end");
			instance.UnpatchAll("test-exception1");
		}

		public static IEnumerable<CodeInstruction> TestTranspiler(IEnumerable<CodeInstruction> instructions)
		{
			savedInstructions = new CodeInstruction[instructions.Count()];
			instructions.ToList().CopyTo(savedInstructions);

			foreach (var instruction in instructions)
			{
				if (instruction.opcode == insertLoc)
				{
					var blocks = instruction.blocks;
					instruction.blocks = new List<ExceptionBlock>();

					var log = AccessTools.DeclaredField(typeof(Class3), "log");
					var tst = typeof(string);
					var concat = AccessTools.Method(typeof(string), nameof(string.Concat), new Type[] { tst, tst });
					yield return new CodeInstruction(OpCodes.Ldarg_0) { blocks = blocks };
					yield return new CodeInstruction(OpCodes.Ldarg_0);
					yield return new CodeInstruction(OpCodes.Ldfld, log);
					yield return new CodeInstruction(OpCodes.Ldstr, ",patch");
					yield return new CodeInstruction(OpCodes.Call, concat);
					yield return new CodeInstruction(OpCodes.Stfld, log);
				}

				yield return instruction;
			}
		}

		public static IEnumerable<CodeInstruction> TestTranspilerICollection(ICollection<CodeInstruction> instructions) => TestTranspiler(instructions);

		public static IEnumerable<CodeInstruction> TestTranspilerIList(IList<CodeInstruction> instructions) => TestTranspiler(instructions);

		public static IEnumerable<CodeInstruction> TestTranspilerList(List<CodeInstruction> instructions) => TestTranspiler(instructions);
	}
}