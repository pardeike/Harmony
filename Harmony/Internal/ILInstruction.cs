using System;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace HarmonyLib
{
	internal class ILInstruction
	{
		internal int offset;
		internal OpCode opcode;
		internal object operand;
		internal object argument;

		internal List<Label> labels = new List<Label>();
		internal List<ExceptionBlock> blocks = new List<ExceptionBlock>();

		internal ILInstruction(OpCode opcode, object operand = null)
		{
			this.opcode = opcode;
			this.operand = operand;
			argument = operand;
		}

		internal CodeInstruction GetCodeInstruction()
		{
			var instr = new CodeInstruction(opcode, argument);
			if (opcode.OperandType == OperandType.InlineNone)
				instr.operand = null;
			instr.labels = labels;
			instr.blocks = blocks;
			return instr;
		}

		internal int GetSize()
		{
			var size = opcode.Size;

			switch (opcode.OperandType)
			{
				case OperandType.InlineSwitch:
					size += (1 + ((Array)operand).Length) * 4;
					break;

				case OperandType.InlineI8:
				case OperandType.InlineR:
					size += 8;
					break;

				case OperandType.InlineBrTarget:
				case OperandType.InlineField:
				case OperandType.InlineI:
				case OperandType.InlineMethod:
				case OperandType.InlineSig:
				case OperandType.InlineString:
				case OperandType.InlineTok:
				case OperandType.InlineType:
				case OperandType.ShortInlineR:
					size += 4;
					break;

				case OperandType.InlineVar:
					size += 2;
					break;

				case OperandType.ShortInlineBrTarget:
				case OperandType.ShortInlineI:
				case OperandType.ShortInlineVar:
					size += 1;
					break;
			}

			return size;
		}

		public override string ToString()
		{
			var instruction = "";

			AppendLabel(ref instruction, this);
			instruction += $": {opcode.Name}";

			if (operand is null)
				return instruction;

			instruction += " ";

			switch (opcode.OperandType)
			{
				case OperandType.ShortInlineBrTarget:
				case OperandType.InlineBrTarget:
					AppendLabel(ref instruction, operand);
					break;

				case OperandType.InlineSwitch:
					var switchLabels = (ILInstruction[])operand;
					for (var i = 0; i < switchLabels.Length; i++)
					{
						if (i > 0)
							instruction += ",";

						AppendLabel(ref instruction, switchLabels[i]);
					}
					break;

				case OperandType.InlineString:
					instruction += $"\"{operand}\"";
					break;

				default:
					instruction += operand;
					break;
			}

			return instruction;
		}

		static void AppendLabel(ref string str, object argument)
		{
			var instruction = argument as ILInstruction;
			str += $"IL_{instruction?.offset.ToString("X4") ?? argument}";
		}
	}
}
