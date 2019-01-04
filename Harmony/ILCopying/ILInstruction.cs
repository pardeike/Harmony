using System;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace Harmony.ILCopying
{
	/// <summary>Exception block types</summary>
	public enum ExceptionBlockType
	{
		/// <summary>The beginning of an exception block</summary>
		BeginExceptionBlock,
		/// <summary>The beginning of a catch block</summary>
		BeginCatchBlock,
		/// <summary>The beginning of an except filter block</summary>
		BeginExceptFilterBlock,
		/// <summary>The beginning of a fault block</summary>
		BeginFaultBlock,
		/// <summary>The beginning of a finally block</summary>
		BeginFinallyBlock,
		/// <summary>The end of an exception block</summary>
		EndExceptionBlock
	}

	/// <summary>An exception block</summary>
	public class ExceptionBlock
	{
		/// <summary>Block type</summary>
		public ExceptionBlockType blockType;

		/// <summary>Catch type</summary>
		public Type catchType;

		/// <summary>Creates an exception block</summary>
		/// <param name="blockType">Block type</param>
		/// <param name="catchType">Catch type</param>
		///
		public ExceptionBlock(ExceptionBlockType blockType, Type catchType)
		{
			this.blockType = blockType;
			this.catchType = catchType;
		}
	}

	/// <summary>An intermediate language instruction</summary>
	public class ILInstruction
	{
		/// <summary>The offset</summary>
		public int offset;
		/// <summary>The opcode</summary>
		public OpCode opcode;
		/// <summary>The operand</summary>
		public object operand;
		/// <summary>The argument</summary>
		public object argument;

		/// <summary>The labels</summary>
		public List<Label> labels = new List<Label>();
		/// <summary>The blocks</summary>
		public List<ExceptionBlock> blocks = new List<ExceptionBlock>();

		/// <summary>Creates an intermediate language instruction</summary>
		/// <param name="opcode">The opcode</param>
		/// <param name="operand">The optional operand</param>
		///
		public ILInstruction(OpCode opcode, object operand = null)
		{
			this.opcode = opcode;
			this.operand = operand;
			argument = operand;
		}

		/// <summary>Gets the CodeInstruction</summary>
		/// <returns>The code instruction</returns>
		///
		public CodeInstruction GetCodeInstruction()
		{
			var instr = new CodeInstruction(opcode, argument);
			if (opcode.OperandType == OperandType.InlineNone)
				instr.operand = null;
			instr.labels = labels;
			instr.blocks = blocks;
			return instr;
		}

		/// <summary>Gets the size</summary>
		/// <returns>The size</returns>
		///
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

		/// <summary>Returns a string that represents the current object</summary>
		/// <returns>A string representation</returns>
		///
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
