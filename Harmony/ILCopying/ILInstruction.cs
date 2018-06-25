using System;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace Harmony.ILCopying
{
	public enum ExceptionBlockType
	{
		BeginExceptionBlock,
		BeginCatchBlock,
		BeginExceptFilterBlock,
		BeginFaultBlock,
		BeginFinallyBlock,
		EndExceptionBlock
	}

	public class ExceptionBlock
	{
		public ExceptionBlockType blockType;
		public Type catchType;

		public ExceptionBlock(ExceptionBlockType blockType, Type catchType)
		{
			this.blockType = blockType;
			this.catchType = catchType;
		}
	}

	public class ILInstruction
	{
		public int offset;
		public OpCode opcode;
		public object operand;
		public object argument;

		public List<Label> labels = new List<Label>();
		public List<ExceptionBlock> blocks = new List<ExceptionBlock>();

		public ILInstruction(OpCode opcode, object operand = null)
		{
			this.opcode = opcode;
			this.operand = operand;
			argument = operand;
		}

		public CodeInstruction GetCodeInstruction()
		{
			var instr = new CodeInstruction(opcode, argument);
			if (opcode.OperandType == OperandType.InlineNone)
				instr.operand = null;
			instr.labels = labels;
			instr.blocks = blocks;
			return instr;
		}

		public int GetSize()
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
			instruction = instruction + ": " + opcode.Name;

			if (operand == null)
				return instruction;

			instruction = instruction + " ";

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
							instruction = instruction + ",";

						AppendLabel(ref instruction, switchLabels[i]);
					}
					break;

				case OperandType.InlineString:
					instruction = instruction + "\"" + operand + "\"";
					break;

				default:
					instruction = instruction + operand;
					break;
			}

			return instruction;
		}

		static void AppendLabel(ref string str, object argument)
		{
			var instruction = argument as ILInstruction;
			if (instruction != null)
				str = str + "IL_" + instruction.offset.ToString("X4");
			else
				str = str + "IL_" + argument;
		}
	}
}
