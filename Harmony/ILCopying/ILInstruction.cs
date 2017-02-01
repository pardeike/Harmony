using System.Collections.Generic;
using System.Reflection.Emit;

namespace Harmony.ILCopying
{
	public class ILCode
	{
		readonly OpCode opcode;
		readonly bool hasOpcode;
		readonly object operand;
		readonly bool hasOperand;

		public ILCode(OpCode opcode)
		{
			this.opcode = opcode;
			hasOpcode = true;
		}

		public ILCode(object operand)
		{
			this.operand = operand;
			hasOperand = true;
		}

		public ILCode(OpCode opcode, object operand)
		{
			this.opcode = opcode;
			hasOpcode = true;
			this.operand = operand;
			hasOperand = true;
		}

		public ILCode(ILInstruction instruction)
		{
			opcode = instruction.OpCode;
			hasOpcode = true;
			operand = instruction.Operand;
			hasOperand = true;
		}

		public void Apply(ILInstruction instruction)
		{
			if (hasOpcode) instruction.OpCode = opcode;
			if (hasOperand)
			{
				instruction.Operand = operand;
				instruction.Argument = operand;
			}
		}

		public bool Equals(ILCode other)
		{
			var result = (!hasOpcode || !other.hasOpcode || opcode == other.opcode) && (!hasOperand || !other.hasOperand || operand == other.operand);
			//if (result) FileLog.Log("# " + this.ToString() + " == " + other.ToString() + "  is " + result);
			return result;
		}

		public override string ToString()
		{
			return "" + (hasOpcode ? opcode.Name : "*") + "_" + (hasOperand ? (operand == null ? "NULL" : operand.GetType().Name) : "*");
		}
	}

	public class ILInstruction
	{
		int offset;
		OpCode opcode;
		object operand;
		object argument;

		ILInstruction previous;
		ILInstruction next;

		List<Label> labels;

		public int Offset
		{
			get { return offset; }
			set { offset = value; }
		}

		public OpCode OpCode
		{
			get { return opcode; }
			set { opcode = value; }
		}

		public object Operand
		{
			get { return operand; }
			set { operand = value; }
		}

		public object Argument
		{
			get { return argument; }
			set { argument = value; }
		}

		public List<Label> Labels
		{
			get { return labels; }
		}

		public ILInstruction Previous
		{
			get { return previous; }
			set { previous = value; }
		}

		public ILInstruction Next
		{
			get { return next; }
			set { next = value; }
		}

		//

		public ILInstruction(OpCode opcode, object operand = null)
		{
			this.opcode = opcode;
			this.operand = operand;
			labels = new List<Label>();
		}

		public void AddLabel(Label label)
		{
			labels.Add(label);
		}

		public int GetSize()
		{
			int size = opcode.Size;

			switch (opcode.OperandType)
			{
				case OperandType.InlineSwitch:
					size += (1 + ((int[])operand).Length) * 4;
					break;

				case OperandType.InlineI8:
				case OperandType.InlineR:
					size += 8;
					break;

				case OperandType.InlineBrTarget:
				case OperandType.InlineField:
				case OperandType.InlineI:
				case OperandType.InlineMethod:
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
					var labels = (ILInstruction[])operand;
					for (int i = 0; i < labels.Length; i++)
					{
						if (i > 0)
							instruction = instruction + ",";

						AppendLabel(ref instruction, labels[i]);
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
