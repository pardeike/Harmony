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
	public class Transpiling : TestLogger
	{
		static CodeInstruction[] savedInstructions = null;

#if DEBUG
		static readonly OpCode insertLoc = OpCodes.Stloc_3;
		const int codeLength = 75;
#else
		static readonly OpCode insertLoc = OpCodes.Stloc_1;
		const int codeLength = 61;
#endif

		[Test]
		public void Test_TranspilerException1()
		{
			var test = new Class3();

			test.TestMethod("start");
			Assert.AreEqual(test.GetLog, "start,test,ex:DivideByZeroException,finally,end");

			var original = AccessTools.Method(typeof(Class3), nameof(Class3.TestMethod));
			Assert.NotNull(original);

			var transpiler = AccessTools.Method(typeof(Transpiling), nameof(Transpiling.TestTranspiler));
			Assert.NotNull(transpiler);

			var instance = new Harmony("test-exception1");
			_ = instance.Patch(original, null, null, new HarmonyMethod(transpiler));
			Assert.NotNull(savedInstructions);
			Assert.AreEqual(savedInstructions.Length, codeLength);

			test.TestMethod("restart");
			Assert.AreEqual(test.GetLog, "restart,test,patch,ex:DivideByZeroException,finally,end");
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

		[Test]
		public void Test_LazyTranspilerOnlyRunsOncePerPatch()
		{
			var original = AccessTools.Method(typeof(LazyTranspilerRunsOnce_Class), nameof(LazyTranspilerRunsOnce_Class.Method));
			Assert.NotNull(original);

			var transpiler = AccessTools.Method(typeof(Transpiling), nameof(LazyTranspiler));
			Assert.NotNull(transpiler);

			var instance = new Harmony("test-lazytranspiler");
			// Add the transpiler twice.
			LazyTranspilerRunsOnce_Class.counter = 0;
			_ = instance.Patch(original, transpiler: new HarmonyMethod(transpiler));
			Assert.AreEqual(LazyTranspilerRunsOnce_Class.counter, 1);
			LazyTranspilerRunsOnce_Class.counter = 0;
			_ = instance.Patch(original, transpiler: new HarmonyMethod(transpiler));
			Assert.AreEqual(LazyTranspilerRunsOnce_Class.counter, 2);
		}

		public static IEnumerable<CodeInstruction> LazyTranspiler(IEnumerable<CodeInstruction> instructions)
		{
			LazyTranspilerRunsOnce_Class.counter++;
			foreach (var instruction in instructions)
				yield return instruction;
			_ = instructions.ToList(); // just to iterate the instructions again
		}
	}
}
