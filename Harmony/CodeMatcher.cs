using Harmony.ILCopying;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace Harmony
{
	public class CodeMatch
	{
		public string name = null;

		public List<OpCode> opcodes = new List<OpCode>();
		public List<object> operands = new List<object>();
		public List<Label> labels = new List<Label>();
		public List<ExceptionBlock> blocks = new List<ExceptionBlock>();

		public List<int> jumpsFrom = new List<int>();
		public List<int> jumpsTo = new List<int>();

		public Func<CodeInstruction, bool> predicate = null;

		public CodeMatch(OpCode? opcode = null, object operand = null, string name = null)
		{
			if (opcode is OpCode opcodeValue) opcodes.Add(opcodeValue);
			if (operand != null) operands.Add(operand);
			this.name = name;
		}

		public CodeMatch(CodeInstruction instruction, string name = null)
			: this(instruction.opcode, instruction.operand, name) { }

		public CodeMatch(Func<CodeInstruction, bool> predicate, string name = null)
		{
			this.predicate = predicate;
			this.name = name;
		}

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
			if (name != null)
				result += name + ": ";
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
		private Dictionary<string, CodeInstruction> lastMatches = new Dictionary<string, CodeInstruction>();
		private string lastError = null;
		private bool lastUseEnd = false;
		private CodeMatch[] lastCodeMatches = null;

		private void FixStart() { Pos = Math.Max(0, Pos); }
		private void SetOutOfBounds(int direction) { Pos = direction > 0 ? Length : -1; }

		public int Length => codes.Count;
		public bool IsValid => Pos >= 0 && Pos < Length;
		public bool IsInvalid => Pos < 0 || Pos >= Length;
		public int Remaining => Length - Math.Max(0, Pos);

		public ref OpCode Opcode => ref codes[Pos].opcode;
		public ref object Operand => ref codes[Pos].operand;
		public ref List<Label> Labels => ref codes[Pos].labels;
		public ref List<ExceptionBlock> Blocks => ref codes[Pos].blocks;

		public CodeMatcher()
		{
		}

		// make a deep copy of all instructions and settings
		//
		public CodeMatcher(IEnumerable<CodeInstruction> instructions, ILGenerator generator = null)
		{
			this.generator = generator;
			codes = instructions.Select(c => new CodeInstruction(c)).ToList();
		}

		public CodeMatcher Clone()
		{
			return new CodeMatcher(codes, generator)
			{
				Pos = Pos,
				lastMatches = lastMatches,
				lastError = lastError,
				lastUseEnd = lastUseEnd,
				lastCodeMatches = lastCodeMatches
			};
		}

		// reading instructions out ---------------------------------------------

		public CodeInstruction Instruction => codes[Pos];

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

		public bool ReportFailure(MethodBase method, Action<string> logger)
		{
			if (IsValid) return false;
			var err = lastError ?? "Unexpected code";
			logger(err + " in " + method);
			return true;
		}

		// edit operation -------------------------------------------------------

		public CodeMatcher SetInstruction(CodeInstruction instruction)
		{
			codes[Pos] = instruction;
			return this;
		}

		public CodeMatcher SetInstructionAndAdvance(CodeInstruction instruction)
		{
			SetInstruction(instruction);
			Pos++;
			return this;
		}

		public CodeMatcher Set(OpCode opcode, object operand)
		{
			Opcode = opcode;
			Operand = operand;
			return this;
		}

		public CodeMatcher SetAndAdvance(OpCode opcode, object operand)
		{
			Set(opcode, operand);
			Pos++;
			return this;
		}

		public CodeMatcher SetOpcodeAndAdvance(OpCode opcode)
		{
			Opcode = opcode;
			Pos++;
			return this;
		}

		public CodeMatcher SetOperandAndAdvance(object operand)
		{
			Operand = operand;
			Pos++;
			return this;
		}

		public CodeMatcher CreateLabel(out Label label)
		{
			label = generator.DefineLabel();
			Labels.Add(label);
			return this;
		}

		public CodeMatcher CreateLabelAt(int position, out Label label)
		{
			label = generator.DefineLabel();
			codes[position].labels.Add(label);
			return this;
		}

		public CodeMatcher AddLabels(IEnumerable<Label> labels)
		{
			Labels.AddRange(labels);
			return this;
		}

		public CodeMatcher AddLabelsAt(int position, IEnumerable<Label> labels)
		{
			codes[position].labels.AddRange(labels);
			return this;
		}

		public CodeMatcher SetJumpTo(int destination, out Label label)
		{
			CreateLabelAt(destination, out label);
			Labels.Add(label);
			return this;
		}

		// insert operations ----------------------------------------------------

		public CodeMatcher Insert(params CodeInstruction[] instructions)
		{
			codes.InsertRange(Pos, instructions);
			return this;
		}

		public CodeMatcher Insert(IEnumerable<CodeInstruction> instructions)
		{
			codes.InsertRange(Pos, instructions);
			return this;
		}

		public CodeMatcher InsertBranch(OpCode opcode, int destination)
		{
			CreateLabelAt(destination, out var label);
			codes.Insert(Pos, new CodeInstruction(opcode, label));
			return this;
		}

		public CodeMatcher InsertAndAdvance(params CodeInstruction[] instructions)
		{
			instructions.Do(instruction =>
			{
				Insert(instruction);
				Pos++;
			});
			return this;
		}

		public CodeMatcher InsertAndAdvance(IEnumerable<CodeInstruction> instructions)
		{
			instructions.Do(instruction => InsertAndAdvance(instruction));
			return this;
		}

		public CodeMatcher InsertBranchAndAdvance(OpCode opcode, int destination)
		{
			InsertBranch(opcode, destination);
			Pos++;
			return this;
		}

		// delete operations --------------------------------------------------------

		public CodeMatcher RemoveInstruction()
		{
			codes.RemoveAt(Pos);
			return this;
		}

		public CodeMatcher RemoveInstructions(int count)
		{
			codes.RemoveRange(Pos, Pos + count - 1);
			return this;
		}

		public CodeMatcher RemoveInstructionsInRange(int start, int end)
		{
			if (start > end) { var tmp = start; start = end; end = tmp; }
			codes.RemoveRange(start, end - start + 1);
			return this;
		}

		public CodeMatcher RemoveInstructionsWithOffsets(int startOffset, int endOffset)
		{
			RemoveInstructionsInRange(Pos + startOffset, Pos + endOffset);
			return this;
		}

		// moving around ------------------------------------------------------------

		public CodeMatcher Advance(int offset)
		{
			Pos += offset;
			if (IsValid == false) SetOutOfBounds(offset);
			return this;
		}

		public CodeMatcher Start()
		{
			Pos = 0;
			return this;
		}

		public CodeMatcher End()
		{
			Pos = Length - 1;
			return this;
		}

		public CodeMatcher SearchForward(Func<CodeInstruction, bool> predicate) => Search(predicate, 1);
		public CodeMatcher SearchBack(Func<CodeInstruction, bool> predicate) => Search(predicate, -1);
		private CodeMatcher Search(Func<CodeInstruction, bool> predicate, int direction)
		{
			FixStart();
			while (IsValid && predicate(Instruction) == false)
				Pos += direction;
			lastError = IsInvalid ? "Cannot find " + predicate : null;
			return this;
		}

		public CodeMatcher MatchForward(bool useEnd, params CodeMatch[] matches) => Match(matches, 1, useEnd);
		public CodeMatcher MatchBack(bool useEnd, params CodeMatch[] matches) => Match(matches, -1, useEnd);
		private CodeMatcher Match(CodeMatch[] matches, int direction, bool useEnd)
		{
			FixStart();
			while (IsValid)
			{
				lastUseEnd = useEnd;
				lastCodeMatches = matches;
				if (MatchSequence(Pos, matches))
				{
					if (useEnd) Pos += matches.Count() - 1;
					break;
				}
				Pos += direction;
			}
			lastError = IsInvalid ? "Cannot find " + matches.Join() : null;
			return this;
		}

		public CodeMatcher Repeat(Action<CodeMatcher> matchAction, Action<string> notFoundAction = null)
		{
			var count = 0;
			if (lastCodeMatches == null)
				throw new InvalidOperationException("No previous Match operation - cannot repeat");

			while (IsValid)
			{
				matchAction(this);
				MatchForward(lastUseEnd, lastCodeMatches);
				count++;
			}
			lastCodeMatches = null;

			if (count == 0 && notFoundAction != null)
				notFoundAction(lastError);

			return this;
		}

		public CodeInstruction NamedMatch(string name)
			=> lastMatches[name];

		private bool MatchSequence(int start, CodeMatch[] matches)
		{
			if (start < 0) return false;
			lastMatches = new Dictionary<string, CodeInstruction>();
			foreach (var match in matches)
			{
				if (start >= Length || match.Matches(codes, codes[start]) == false)
					return false;
				if (match.name != null)
					lastMatches.Add(match.name, codes[start]);
				start++;
			}
			return true;
		}
	}
}