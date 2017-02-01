using System.Reflection;
using System.Reflection.Emit;

namespace Harmony
{
	public class HarmonyModifier
	{
		public ModifierItem search;
		public ModifierItem replace;

		public int prioritiy = -1;
		public string[] before;
		public string[] after;

		public HarmonyModifier(MethodInfo replaceMethod, MethodInfo withMethod)
		{
			search = new ModifierItem(OpCodes.Nop, false, replaceMethod, true);
			replace = new ModifierItem(OpCodes.Nop, false, withMethod, true);
		}

		public HarmonyModifier(OpCode searchCode, object searchOperand, object replaceOperand)
		{
			search = new ModifierItem(searchCode, true, searchOperand, true);
			replace = new ModifierItem(searchCode, false, replaceOperand, true);
		}

		public HarmonyModifier(object searchOperand, object replaceOperand)
		{
			search = new ModifierItem(OpCodes.Nop, false, searchOperand, true);
			replace = new ModifierItem(OpCodes.Nop, false, replaceOperand, true);
		}

		public HarmonyModifier(OpCode searchCode, bool hasSearchCode, object searchOperand, bool hasSearchOperand, OpCode replaceCode, bool hasReplaceCode, object replaceOperand, bool hasReplaceOperand)
		{
			search = new ModifierItem(searchCode, hasSearchCode, searchOperand, hasSearchOperand);
			replace = new ModifierItem(replaceCode, hasReplaceCode, replaceOperand, hasReplaceOperand);
		}
	}
}