using HarmonyLib;
using HarmonyLibTests.Assets;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace HarmonyLibTests.Patching
{
	[TestFixture, NonParallelizable]
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

			var transpiler = AccessTools.Method(typeof(Transpiling), nameof(TestTranspiler));
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
					instruction.blocks = [];

					var log = AccessTools.DeclaredField(typeof(Class3), "log");
					var tst = typeof(string);
					var concat = AccessTools.Method(typeof(string), nameof(string.Concat), [tst, tst]);
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

		public class Skills
		{
			public static int learned;
			public void Learn(float xp) => learned++;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		public static void NullConditionalLearn(Skills skills) => skills?.Learn(1f);

		// Like a RimWorld mod transpiler: it adds its own null check and expects callvirt Learn to place the second label.
		// The compiler already checks for null and uses call, so that label is never added to an instruction.
		public static IEnumerable<CodeInstruction> UnmarkedLabelTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator il)
		{
			var learn = AccessTools.Method(typeof(Skills), nameof(Skills.Learn));
			var afterCheck = il.DefineLabel();
			var afterLearn = il.DefineLabel();
			bool inserted = false, labelNext = false, labelAfterLearn = false;
			foreach (var instruction in instructions)
			{
				if (labelNext) { instruction.labels.Add(afterCheck); labelNext = false; }
				else if (labelAfterLearn) { instruction.labels.Add(afterLearn); labelAfterLearn = false; }
				yield return instruction;
				if (!inserted && instruction.opcode == OpCodes.Ldarg_0)
				{
					yield return new CodeInstruction(OpCodes.Dup);
					yield return new CodeInstruction(OpCodes.Brtrue_S, afterCheck);
					yield return new CodeInstruction(OpCodes.Pop);
					yield return new CodeInstruction(OpCodes.Br_S, afterLearn);
					inserted = labelNext = true;
				}
				else if (instruction.opcode == OpCodes.Callvirt && instruction.Calls(learn)) labelAfterLearn = true;
			}
		}

		[Test]
		public void Test_BranchToALabelNoInstructionCarriesIsRejected()
		{
			var original = AccessTools.Method(typeof(Transpiling), nameof(NullConditionalLearn));
			var instance = new Harmony("test-unmarked-label");
			try
			{
				var error = Assert.Catch(() => instance.Patch(original, transpiler: new HarmonyMethod(typeof(Transpiling), nameof(UnmarkedLabelTranspiler))));
				while (error is not InvalidProgramException && error.InnerException is not null) error = error.InnerException;
				Assert.That(error, Is.TypeOf<InvalidProgramException>());
				Assert.That(error.Message, Does.Contain("branches to a label that no instruction carries")
					.And.Contain($"{typeof(Transpiling).FullName}.{nameof(UnmarkedLabelTranspiler)}"));
				Assert.That(Harmony.GetPatchInfo(original), Is.Null);

				Skills.learned = 0;
				NullConditionalLearn(null);
				NullConditionalLearn(new Skills());
				Assert.That(Skills.learned, Is.EqualTo(1));
			}
			finally { instance.UnpatchAll(instance.Id); }
		}
	}
}
