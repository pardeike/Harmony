using System;
using System.Collections.Generic;
using System.Reflection.Emit;

using static HarmonyLib.Code;

namespace HarmonyLib
{
	internal class FaultBlockRewriter
	{
		static int FindMatchingBeginException(List<CodeInstruction> rewritten)
		{
			for (int j = rewritten.Count - 1, depth = 0; j >= 0; --j)
			{
				if (rewritten[j].HasBlock(ExceptionBlockType.EndExceptionBlock)) ++depth;
				if (rewritten[j].HasBlock(ExceptionBlockType.BeginExceptionBlock))
				{
					if (depth == 0) return j;
					--depth;
				}
			}
			return -1;
		}

		static int FindMatchingEndException(List<CodeInstruction> source, int start)
		{
			for (int j = start, depth = 0; j < source.Count; ++j)
			{
				if (source[j].HasBlock(ExceptionBlockType.BeginExceptionBlock)) ++depth;
				if (source[j].HasBlock(ExceptionBlockType.EndExceptionBlock))
				{
					if (depth == 0) return j;
					--depth;
				}
			}
			return -1;
		}

		static CodeInstruction CloneWithoutFaultMarker(CodeInstruction original)
		{
			var copy = new CodeInstruction(original);
			_ = copy.blocks.RemoveAll(b => b.blockType == ExceptionBlockType.BeginFaultBlock);
			return copy;
		}

		internal static List<CodeInstruction> Rewrite(List<CodeInstruction> instructions, ILGenerator generator)
		{
			if (instructions is null) throw new ArgumentNullException(nameof(instructions));
			if (generator is null) throw new ArgumentNullException(nameof(generator));

			var i = 0;
			var rewritten = new List<CodeInstruction>(instructions.Count * 2);
			while (i < instructions.Count)
			{
				var cur = instructions[i];

				if (cur.HasBlock(ExceptionBlockType.BeginFaultBlock) == false)
				{
					rewritten.Add(new CodeInstruction(cur));
					++i;
					continue;
				}

				var beginExceptionIdx = FindMatchingBeginException(rewritten);
				var endExceptionIdx = FindMatchingEndException(instructions, i + 1);

				if (beginExceptionIdx < 0 || endExceptionIdx < 0)
					throw new InvalidOperationException("Unbalanced exception markers – cannot rewrite.");

				var faultBody = new List<CodeInstruction>();
				for (var k = i; k < endExceptionIdx; ++k)
					faultBody.Add(CloneWithoutFaultMarker(instructions[k]));

				i = endExceptionIdx + 1;

				var failedLocal = generator.DeclareLocal(typeof(bool));
				var skipFault = generator.DefineLabel();

				rewritten.AddRange([
					Nop.WithBlocks(new ExceptionBlock(ExceptionBlockType.BeginCatchBlock, typeof(object))),
					Pop,
					Ldc_I4_1,
					Stloc[failedLocal.LocalIndex],
					Rethrow,
					Nop.WithBlocks(new ExceptionBlock(ExceptionBlockType.BeginFinallyBlock)),
					Ldloc[failedLocal.LocalIndex],
					Brfalse_S[skipFault],
					Nop.WithLabels(skipFault),
					Nop.WithBlocks(new ExceptionBlock(ExceptionBlockType.EndExceptionBlock))
				]);
			}

			return rewritten;
		}
	}
}
