using Harmony.ILCopying;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;

namespace Harmony
{
	public class CodeMatch
	{
		public List<OpCode> opcodes = new List<OpCode>();
		public List<object> operands = new List<object>();
		public List<Label> labels = new List<Label>();
		public List<ExceptionBlock> blocks = new List<ExceptionBlock>();

		public List<int> jumpsFrom = new List<int>();
		public List<int> jumpsTo = new List<int>();

		public Func<CodeInstruction, bool> predicate = null;

		public CodeMatch(OpCode? opcode = null, object operand = null)
		{
			if (opcode is OpCode opcodeValue) opcodes.Add(opcodeValue);
			if (operand != null) operands.Add(operand);
		}

		public CodeMatch(CodeInstruction instruction) : this(instruction.opcode, instruction.operand) { }

		public CodeMatch(Func<CodeInstruction, bool> predicate)
			=> this.predicate = predicate;

		public bool Matches(List<CodeInstruction> codes, CodeInstruction instruction)
		{
			if (predicate != null) return predicate(instruction);

			if (opcodes.Count > 0 && opcodes.Contains(instruction.opcode) == false) return false;
			if (operands.Count > 0 && operands.Contains(instruction.operand) == false) return false;
			if (labels.Count > 0 && labels.Intersect(instruction.labels).Any() == false) return false;
			if (blocks.Count > 0 && blocks.Intersect(instruction.blocks).Any() == false) return false;

			if (jumpsFrom.Count > 0 && jumpsFrom.Select(index => codes[index].operand).OfType<Label>()
				.Intersect(instruction.labels).Any() == false) return false;

			if (jumpsTo.Count > 0)
			{
				var operand = instruction.operand;
				if (operand == null || operand.GetType() != typeof(Label)) return false;
				var label = (Label)operand;
				var indices = Enumerable.Range(0, codes.Count).Where(idx => codes[idx].labels.Contains(label));
				if (jumpsTo.Intersect(indices).Any() == false) return false;
			}

			return true;
		}

		public override string ToString()
		{
			var result = "[";
			if (opcodes.Count > 0)
				result += "opcodes=" + opcodes.Join() + " ";
			if (operands.Count > 0)
				result += "operands=" + operands.Join() + " ";
			if (labels.Count > 0)
				result += "labels=" + labels.Join() + " ";
			if (blocks.Count > 0)
				result += "blocks=" + blocks.Join() + " ";
			if (jumpsFrom.Count > 0)
				result += "jumpsFrom=" + jumpsFrom.Join() + " ";
			if (jumpsTo.Count > 0)
				result += "jumpsTo=" + jumpsTo.Join() + " ";
			if (predicate != null)
				result += "predicate=yes ";
			return result.TrimEnd() + "]";
		}
	}

	public class CodeMatcher
	{
		private readonly ILGenerator generator;
		private readonly List<CodeInstruction> codes = new List<CodeInstruction>();
		public int Pos { get; private set; } = -1;

		private void FixStart() { Pos = Math.Max(0, Pos); }
		private void SetOutOfBounds(int direction) { Pos = direction > 0 ? Length : -1; }

		public CodeInstruction Instruction => codes[Pos];
		public int Length => codes.Count;
		public bool IsValid => Pos >= 0 && Pos < Length;
		public bool IsInvalid => IsValid == false;
		public int Remaining => Length - Math.Max(0, Pos);

		public CodeMatcher Clone => new CodeMatcher(generator, codes) { Pos = Pos };
		public CodeMatcher() { }

		public ref OpCode Opcode => ref codes[Pos].opcode;
		public ref object Operand => ref codes[Pos].operand;
		public ref List<Label> Labels => ref codes[Pos].labels;
		public ref List<ExceptionBlock> Blocks => ref codes[Pos].blocks;

		// make a deep copy of all instructions and settings
		//
		public CodeMatcher(ILGenerator generator, IEnumerable<CodeInstruction> instructions)
		{
			this.generator = generator;
			codes = instructions.Select(c => new CodeInstruction(c)).ToList();
		}

		// reading instructions out ---------------------------------------------

		public CodeInstruction InstructionAt(int offset) => codes[Pos + offset];

		public List<CodeInstruction> Instructions() => codes;

		public List<CodeInstruction> Instructions(int count)
			=> codes.GetRange(Pos, count).Select(c => new CodeInstruction(c)).ToList();

		public List<CodeInstruction> InstructionsInRange(int start, int end)
		{
			var instructions = codes;
			if (start > end) { var tmp = start; start = end; end = tmp; }
			instructions = instructions.GetRange(start, end - start + 1);
			return instructions.Select(c => new CodeInstruction(c)).ToList();
		}

		public List<CodeInstruction> InstructionsWithOffsets(int startOffset, int endOffset)
			=> InstructionsInRange(Pos + startOffset, Pos + endOffset);

		public List<Label> DistinctLabels(IEnumerable<CodeInstruction> instructions)
			=> instructions.SelectMany(instruction => instruction.labels).Distinct().ToList();

		// edit operation -------------------------------------------------------

		public void SetInstruction(CodeInstruction instruction)
			=> codes[Pos] = instruction;

		public void SetInstructionAndAdvance(CodeInstruction instruction)
		{
			SetInstruction(instruction);
			Pos++;
		}

		public void Set(OpCode opcode, object operand)
		{
			Opcode = opcode;
			Operand = operand;
		}

		public void SetAndAdvance(OpCode opcode, object operand)
		{
			Set(opcode, operand);
			Pos++;
		}

		public void SetOpcodeAndAdvance(OpCode opcode)
		{
			Opcode = opcode;
			Pos++;
		}

		public void SetOperandAndAdvance(object operand)
		{
			Operand = operand;
			Pos++;
		}

		public Label CreateLabel()
		{
			var label = generator.DefineLabel();
			Labels.Add(label);
			return label;
		}

		public Label CreateLabelAt(int position)
		{
			var label = generator.DefineLabel();
			codes[position].labels.Add(label);
			return label;
		}

		public void AddLabels(IEnumerable<Label> labels)
			=> Labels.AddRange(labels);

		public void AddLabelsAt(int position, IEnumerable<Label> labels)
			=> codes[position].labels.AddRange(labels);

		public Label SetJumpTo(int destination)
		{
			var label = CreateLabelAt(destination);
			Labels.Add(label);
			return label;
		}

		// insert operations ----------------------------------------------------

		public void Insert(params CodeInstruction[] instructions)
			=> codes.InsertRange(Pos, instructions);

		public void Insert(IEnumerable<CodeInstruction> instructions)
			=> codes.InsertRange(Pos, instructions);

		public CodeInstruction InsertBranch(OpCode opcode, int destination)
		{
			var label = CreateLabelAt(destination);
			var instruction = new CodeInstruction(opcode, label);
			codes.Insert(Pos, instruction);
			return instruction;
		}

		public void InsertAndAdvance(params CodeInstruction[] instructions)
		{
			instructions.Do(instruction =>
			{
				Insert(instruction);
				Pos++;
			});
		}

		public void InsertAndAdvance(IEnumerable<CodeInstruction> instructions)
			=> instructions.Do(instruction => InsertAndAdvance(instruction));

		public CodeInstruction InsertBranchAndAdvance(OpCode opcode, int destination)
		{
			var instruction = InsertBranch(opcode, destination);
			Pos++;
			return instruction;
		}

		// delete operations --------------------------------------------------------

		public CodeInstruction RemoveInstruction()
		{
			var instruction = new CodeInstruction(Instruction);
			codes.RemoveAt(Pos);
			return instruction;
		}

		public List<CodeInstruction> RemoveInstructions(int count)
		{
			var instructions = Instructions(count);
			codes.RemoveRange(Pos, Pos + count - 1);
			return instructions;
		}

		public List<CodeInstruction> RemoveInstructionsInRange(int start, int end)
		{
			var instructions = InstructionsInRange(start, end);
			if (start > end) { var tmp = start; start = end; end = tmp; }
			codes.RemoveRange(start, end - start + 1);
			return instructions;
		}

		public List<CodeInstruction> RemoveInstructionsWithOffsets(int startOffset, int endOffset)
			=> RemoveInstructionsInRange(Pos + startOffset, Pos + endOffset);

		// moving around ------------------------------------------------------------

		public CodeMatcher Advance(int offset)
		{
			var matcher = Clone;
			matcher.Pos += offset;
			if (matcher.IsValid == false) matcher.SetOutOfBounds(offset);
			return matcher;
		}

		public CodeMatcher Start()
		{
			var matcher = Clone;
			matcher.Pos = 0;
			return matcher;
		}

		public CodeMatcher End()
		{
			var matcher = Clone;
			matcher.Pos = Length - 1;
			return matcher;
		}

		public CodeMatcher SearchForward(Func<CodeInstruction, bool> predicate) => Search(predicate, 1);
		public CodeMatcher SearchBack(Func<CodeInstruction, bool> predicate) => Search(predicate, -1);
		private CodeMatcher Search(Func<CodeInstruction, bool> predicate, int direction)
		{
			var matcher = Clone;
			matcher.FixStart();
			while (matcher.IsValid && predicate(matcher.Instruction) == false)
				matcher.Pos += direction;
			return matcher;
		}

		public CodeMatcher MatchForward(bool useEnd, params CodeMatch[] matches) => Match(matches, 1, useEnd);
		public CodeMatcher MatchBack(bool useEnd, params CodeMatch[] matches) => Match(matches, -1, useEnd);
		private CodeMatcher Match(CodeMatch[] matches, int direction, bool useEnd)
		{
			var matcher = Clone;
			matcher.FixStart();
			while (true)
			{
				var matched = matcher.MatchSequence(matcher.Pos, matches, out var outOfBounds);
				if (outOfBounds)
				{
					matcher.SetOutOfBounds(direction);
					return matcher;
				}
				if (matched)
				{
					if (useEnd) matcher.Pos += matches.Count() - 1;
					return matcher;
				}
				matcher.Pos += direction;
			}
		}
		private bool MatchSequence(int start, CodeMatch[] matches, out bool outOfBounds)
		{
			var end = start + matches.Length - 1;
			outOfBounds = start < 0 || end >= Length;
			if (outOfBounds) return false;
			foreach (var match in matches)
				if (match.Matches(codes, codes[start++]) == false) return false;
			return true;
		}
	}
}