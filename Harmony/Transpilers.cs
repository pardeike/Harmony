using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace Harmony
{
	public static class Transpilers
	{
		public static IEnumerable<CodeInstruction> MethodReplacer(this IEnumerable<CodeInstruction> instructions, MethodBase from, MethodBase to)
		{
			if (from == null)
				throw new ArgumentException("Unexpected null argument", nameof(from));
			if (to == null)
				throw new ArgumentException("Unexpected null argument", nameof(to));

			foreach (var instruction in instructions)
			{
				var method = instruction.operand as MethodBase;
				if (method == from)
				{
					instruction.opcode = to.IsConstructor ? OpCodes.Newobj : OpCodes.Call;
					instruction.operand = to;
				}
				yield return instruction;
			}
		}

		public static IEnumerable<CodeInstruction> DebugLogger(this IEnumerable<CodeInstruction> instructions, string text)
		{
			yield return new CodeInstruction(OpCodes.Ldstr, text);
			yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(FileLog), nameof(FileLog.Log)));
			foreach (var instruction in instructions) yield return instruction;
		}

		// more added soon
	}
}