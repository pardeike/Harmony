using Harmony.ILCopying;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;

namespace Harmony
{
	public class CodeInstruction
	{
		public OpCode opcode;
		public object operand;
		public List<Label> labels = new List<Label>();
		public List<ExceptionBlock> blocks = new List<ExceptionBlock>();

		public CodeInstruction(OpCode opcode, object operand = null)
		{
			this.opcode = opcode;
			this.operand = operand;
		}

		public CodeInstruction(CodeInstruction instruction)
		{
			opcode = instruction.opcode;
			operand = instruction.operand;
			labels = instruction.labels.ToArray().ToList();
		}

		public override string ToString()
		{
			var list = new List<string>();
			foreach (var label in labels)
				list.Add("Label" + label.GetHashCode());
			foreach (var block in blocks)
				list.Add("EX_" + block.blockType.ToString().Replace("Block", ""));

			var extras = list.Count > 0 ? " [" + string.Join(", ", list.ToArray()) + "]" : "";
			var operandStr = Emitter.FormatArgument(operand);
			if (operandStr != "") operandStr = " " + operandStr;
			return string.Format(opcode + operandStr + extras);
		}
	}
}