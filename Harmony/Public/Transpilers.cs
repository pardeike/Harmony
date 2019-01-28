using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace Harmony
{
	/// <summary>A collection of commonly used transpilers</summary>
	public static class Transpilers
	{
		/// <summary>A transpiler that replaces all occurrences of a given method with another one</summary>
		/// <param name="instructions">The instructions to act on</param>
		/// <param name="from">Method or constructor to search for</param>
		/// <param name="to">Method or constructor to replace with</param>
		/// <returns>Modified instructions</returns>
		///
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

		/// <summary>A transpiler that logs a text at the beginning of the method</summary>
		/// <param name="instructions">The instructions to act on</param>
		/// <param name="text">The log text</param>
		/// <returns>Modified instructions</returns>
		///
		public static IEnumerable<CodeInstruction> DebugLogger(this IEnumerable<CodeInstruction> instructions, string text)
		{
			yield return new CodeInstruction(OpCodes.Ldstr, text);
			yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(FileLog), nameof(FileLog.Log)));
			foreach (var instruction in instructions) yield return instruction;
		}

		// more added soon
	}
}