using HarmonyLib;
using HarmonyLibTests.Assets;
using NUnit.Framework;
using System;
using System.Collections;
using System.Reflection;
using System.Reflection.Emit;

namespace HarmonyLibTests.IL
{
	[TestFixture, NonParallelizable]
	public class TestMethodBodyReader : TestLogger
	{
		public static void WeirdMethodWithGoto()
		{
			LABEL:
			try
			{
				for (; ; )
				{
				}
			}
			catch (Exception)
			{
				goto LABEL;
			}
		}

		[Test]
		public void Test_Read_WeirdMethodWithGoto()
		{
			var method = SymbolExtensions.GetMethodInfo(() => WeirdMethodWithGoto());
			Assert.NotNull(method);
			var instructions = PatchProcessor.GetOriginalInstructions(method);
			Assert.NotNull(instructions);
			Assert.Greater(instructions.Count, 0);
		}

		[Test]
		public void Test_CanGetInstructionsWithNoILGenerator()
		{
			var method = typeof(Class12).GetMethod(nameof(Class12.FizzBuzz));
			var newGenerator = PatchProcessor.CreateILGenerator(method);

			var instrsNoGen = MethodBodyReader.GetInstructions(generator: null, method);
			var instrsHasGen = MethodBodyReader.GetInstructions(newGenerator, method);

			Assert.AreEqual(instrsNoGen.Count, instrsHasGen.Count);
			for (var i = 0; i < instrsNoGen.Count; i++)
			{
				var instrNoGen = instrsNoGen[i];
				var instrHasGen = instrsHasGen[i];
				Assert.AreEqual(instrNoGen.offset, instrHasGen.offset, "offset @ {0} ({1})", i, instrNoGen);
				Assert.AreEqual(instrNoGen.opcode, instrHasGen.opcode, "opcode @ {0} ({1})", i, instrNoGen);
				AssertAreEqual(instrNoGen.operand, instrHasGen.operand, "operand", i, instrNoGen);
				CollectionAssert.AreEqual(instrNoGen.labels, instrHasGen.labels, "labels @ {0}", i);
				CollectionAssert.AreEqual(instrNoGen.blocks, instrHasGen.blocks, "blocks @ {0}", i);
				AssertAreEqual(instrNoGen.argument, instrHasGen.argument, "argument", i, instrNoGen);

				// The only difference between w/o gen and w/ gen is this:
				var operandType = instrNoGen.opcode.OperandType;
				if ((operandType == OperandType.ShortInlineVar || operandType == OperandType.InlineVar) && instrNoGen.argument is not null)
				{
#if NETCOREAPP || NET5_0_OR_GREATER
					Assert.AreEqual("System.Reflection.RuntimeLocalVariableInfo", instrNoGen.argument.GetType().FullName, "w/o generator argument type @ {0} ({1})", i, instrNoGen);
#else
					Assert.AreEqual("System.Reflection.LocalVariableInfo", instrNoGen.argument.GetType().FullName, "w/o generator argument type @ {0} ({1})", i, instrNoGen);
#endif
#if NET9_0_OR_GREATER
					Assert.AreEqual(Type.GetType("System.Reflection.Emit.RuntimeLocalBuilder"), instrHasGen.argument.GetType(), "w/ generator argument type @ {0} ({1})", i, instrNoGen);
#else
					Assert.AreEqual(typeof(LocalBuilder), instrHasGen.argument.GetType(), "w/ generator argument type @ {0} ({1})", i, instrNoGen);
#endif
				}
			}
		}

		static void AssertAreEqual(object x, object y, string label, int currentIndex, ILInstruction currentInstr)
		{
			if (x is ILInstruction xInstr && y is ILInstruction yInstr)
				Assert.AreEqual(xInstr.offset, yInstr.offset, "{0} @ {1} ({2})", label, currentIndex, currentInstr);
			else if (x is ILInstruction[] xInstrs && y is ILInstruction[] yInstrs)
				CollectionAssert.AreEqual(xInstrs, yInstrs, new ILInstructionOffsetComparer(), "{0} @ {1} ({2})", label, currentIndex, currentInstr);
			else if (x is LocalVariableInfo xLocal && y is LocalVariableInfo yLocal)
				AssertAreEqual(xLocal, yLocal, label, currentIndex, currentInstr);
			else
				Assert.AreEqual(x, y, "{0} @ {1} ({2})", label, currentIndex, currentInstr);
		}

		static void AssertAreEqual(LocalVariableInfo x, LocalVariableInfo y, string label, int currentIndex, ILInstruction currentInstr)
		{
			Assert.AreEqual(x.LocalType, y.LocalType, "{0}.{1} @ {2} ({3})", label, "LocalType", currentIndex, currentInstr);
			Assert.AreEqual(x.IsPinned, y.IsPinned, "{0}.{1} @ {2} ({3})", label, "IsPinned", currentIndex, currentInstr);
			Assert.AreEqual(x.LocalIndex, y.LocalIndex, "{0}.{1} @ {2} ({3})", label, "LocalIndex", currentIndex, currentInstr);
		}

		struct ILInstructionOffsetComparer : IComparer
		{
			public int Compare(object x, object y) => ((ILInstruction)x).offset.CompareTo(((ILInstruction)y).offset);
		}
	}
}
