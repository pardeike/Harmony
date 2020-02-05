using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

class TypicalExample
{
	#pragma warning disable CS0252 // Possible unintended reference comparison; to get a value comparison, cast the left hand side to type 'FieldInfo'
	
	// <typical>
	static FieldInfo f_someField = AccessTools.Field(typeof(SomeType), "someField");
	static MethodInfo m_MyExtraMethod = SymbolExtensions.GetMethodInfo(() => Tools.MyExtraMethod());

	// looks for STDFLD someField and inserts CALL MyExtraMethod before it
	static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
	{
		var found = false;
		foreach (var instruction in instructions)
		{
			if (instruction.opcode == OpCodes.Stfld && instruction.operand == f_someField)
			{
				yield return new CodeInstruction(OpCodes.Call, m_MyExtraMethod);
				found = true;
			}
			yield return instruction;
		}
		if (found == false)
			ReportError("Cannot find <Stdfld someField> in OriginalType.OriginalMethod");
	}
	// </typical>

	class SomeType {}

	class Tools
	{
		public static MethodInfo MyExtraMethod() { return null; }
	}

	static void ReportError(string s) {}
}

class CaravanExample
{
	// <caravan>
	[HarmonyPatch(typeof(Dialog_FormCaravan))]
	[HarmonyPatch("CheckForErrors")]
	public static class Dialog_FormCaravan_CheckForErrors_Patch
	{
		static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			var foundMassUsageMethod = false;
			int startIndex = -1, endIndex = -1;

			var codes = new List<CodeInstruction>(instructions);
			for (int i = 0; i < codes.Count; i++)
			{
				if (codes[i].opcode == OpCodes.Ret)
				{
					if (foundMassUsageMethod)
					{
						Log.Error("END " + i);

						endIndex = i; // include current 'ret'
						break;
					}
					else
					{
						Log.Error("START " + (i + 1));

						startIndex = i + 1; // exclude current 'ret'

						for (int j = startIndex; j < codes.Count; j++)
						{
							if (codes[j].opcode == OpCodes.Ret)
								break;
							var strOperand = codes[j].operand as String;
							if (strOperand == "TooBigCaravanMassUsage")
							{
								foundMassUsageMethod = true;
								break;
							}
						}
					}
				}
			}
			if (startIndex > -1 && endIndex > -1)
			{
				// we cannot remove the first code of our range since some jump actually jumps to
				// it, so we replace it with a no-op instead of fixing that jump (easier).
				codes[startIndex].opcode = OpCodes.Nop;
				codes.RemoveRange(startIndex + 1, endIndex - startIndex - 1);
			}

			return codes.AsEnumerable();
		}
	}
	// </caravan>

	class Dialog_FormCaravan {}

	class Log
	{
		public static void Error(string s) {}
	}
}
