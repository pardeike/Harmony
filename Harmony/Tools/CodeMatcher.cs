using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace Harmony
{
	/// <summary>A CodeInstruction match</summary>
	public class CodeMatch
	{
		/// <summary>The name of the match</summary>
		public string name = null;

		/// <summary>The matched opcodes</summary>
		public List<OpCode> opcodes = new List<OpCode>();
		/// <summary>The matched operands</summary>
		public List<object> operands = new List<object>();
		/// <summary>The matched labels</summary>
		public List<Label> labels = new List<Label>();
		/// <summary>The matched blocks</summary>
		public List<ExceptionBlock> blocks = new List<ExceptionBlock>();

		/// <summary>The jumps from the match</summary>
		public List<int> jumpsFrom = new List<int>();
		/// <summary>The jumps to the match</summary>
		public List<int> jumpsTo = new List<int>();

		/// <summary>The match predicate</summary>
		public Func<CodeInstruction, bool> predicate = null;

		/// <summary>Creates a code match</summary>
		/// <param name="opcode">The optional opcode</param>
		/// <param name="operand">The optional operand</param>
		/// <param name="name">The optional name</param>
		///
		public CodeMatch(OpCode? opcode = null, object operand = null, string name = null)
		{
			if (opcode is OpCode opcodeValue) opcodes.Add(opcodeValue);
			if (operand != null) operands.Add(operand);
			this.name = name;
		}

		/// <summary>Creates a code match</summary>
		/// <param name="instruction">The CodeInstruction</param>
		/// <param name="name">An optional name</param>
		///
		public CodeMatch(CodeInstruction instruction, string name = null)
			: this(instruction.opcode, instruction.operand, name) { }

		/// <summary>Creates a code match</summary>
		/// <param name="predicate">The predicate</param>
		/// <param name="name">An optional name</param>
		///
		public CodeMatch(Func<CodeInstruction, bool> predicate, string name = null)
		{
			this.predicate = predicate;
			this.name = name;
		}

		internal bool Matches(List<CodeInstruction> codes, CodeInstruction instruction)
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

		/// <summary>Returns a string that represents the match</summary>
		/// <returns>A string representation</returns>
		///
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

	/// <summary>A CodeInstruction matcher</summary>
	public class CodeMatcher
	{
		readonly ILGenerator generator;
		readonly List<CodeInstruction> codes = new List<CodeInstruction>();

		/// <summary>The current position</summary>
		/// <value>The index or -1 if out of bounds</value>
		///
		public int Pos { get; private set; } = -1;
		Dictionary<string, CodeInstruction> lastMatches = new Dictionary<string, CodeInstruction>();
		string lastError = null;
		bool lastUseEnd = false;
		CodeMatch[] lastCodeMatches = null;

		void FixStart() { Pos = Math.Max(0, Pos); }
		void SetOutOfBounds(int direction) { Pos = direction > 0 ? Length : -1; }

		/// <summary>Gets the number of code instructions in this matcher</summary>
		/// <value>The count</value>
		///
		public int Length => codes.Count;

		/// <summary>Checks whether the position of this CodeMatcher is within bounds</summary>
		/// <value>True if this CodeMatcher is valid</value>
		///
		public bool IsValid => Pos >= 0 && Pos < Length;

		/// <summary>Checks whether the position of this CodeMatcher is outside its bounds</summary>
		/// <value>True if this CodeMatcher is invalid</value>
		///
		public bool IsInvalid => Pos < 0 || Pos >= Length;

		/// <summary>Gets the remaining code instructions</summary>
		/// <value>The remaining count</value>
		///
		public int Remaining => Length - Math.Max(0, Pos);

		/// <summary>Gets the opcode at the current position</summary>
		/// <value>The opcode</value>
		///
		public ref OpCode Opcode => ref codes[Pos].opcode;

		/// <summary>Gets the operand at the current position</summary>
		/// <value>The operand</value>
		///
		public ref object Operand => ref codes[Pos].operand;

		/// <summary>Gets the labels at the current position</summary>
		/// <value>The labels</value>
		///
		public ref List<Label> Labels => ref codes[Pos].labels;

		/// <summary>Gets the exception blocks at the current position</summary>
		/// <value>The blocks</value>
		///
		public ref List<ExceptionBlock> Blocks => ref codes[Pos].blocks;

		/// <summary>Creates an empty code matcher</summary>
		public CodeMatcher()
		{
		}

		/// <summary>Creates a code matcher from an enumeration of instructions</summary>
		/// <param name="instructions">The instructions (transpiler argument)</param>
		/// <param name="generator">An optional IL generator</param>
		///
		public CodeMatcher(IEnumerable<CodeInstruction> instructions, ILGenerator generator = null)
		{
			this.generator = generator;
			codes = instructions.Select(c => new CodeInstruction(c)).ToList();
		}

		/// <summary>Makes a clone of this instruction matcher</summary>
		/// <returns>A copy of this matcher</returns>
		///
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

		/// <summary>Gets instructions at the current position</summary>
		/// <value>The instruction</value>
		///
		public CodeInstruction Instruction => codes[Pos];

		/// <summary>Gets instructions at the current position with offset</summary>
		/// <param name="offset">The offset</param>
		/// <returns>The instruction</returns>
		///
		public CodeInstruction InstructionAt(int offset)
		{
			return codes[Pos + offset];
		}

		/// <summary>Gets all instructions</summary>
		/// <returns>A list of instructions</returns>
		///
		public List<CodeInstruction> Instructions()
		{
			return codes;
		}

		/// <summary>Gets some instructions counting from current position</summary>
		/// <param name="count">Number of instructions</param>
		/// <returns>A list of instructions</returns>
		///
		public List<CodeInstruction> Instructions(int count)
		{
			return codes.GetRange(Pos, count).Select(c => new CodeInstruction(c)).ToList();
		}

		/// <summary>Gets all instructions within a range</summary>
		/// <param name="start">The start index</param>
		/// <param name="end">The end index</param>
		/// <returns>A list of instructions</returns>
		///
		public List<CodeInstruction> InstructionsInRange(int start, int end)
		{
			var instructions = codes;
			if (start > end) { var tmp = start; start = end; end = tmp; }
			instructions = instructions.GetRange(start, end - start + 1);
			return instructions.Select(c => new CodeInstruction(c)).ToList();
		}

		/// <summary>Gets all instructions within a range (relative to current position)</summary>
		/// <param name="startOffset">The start offset</param>
		/// <param name="endOffset">The end offset</param>
		/// <returns>A list of instructions</returns>
		///
		public List<CodeInstruction> InstructionsWithOffsets(int startOffset, int endOffset)
		{
			return InstructionsInRange(Pos + startOffset, Pos + endOffset);
		}

		/// <summary>Gets a list of all distinct labels</summary>
		/// <param name="instructions">The instructions (transpiler argument)</param>
		/// <returns>A list of Labels</returns>
		///
		public List<Label> DistinctLabels(IEnumerable<CodeInstruction> instructions)
		{
			return instructions.SelectMany(instruction => instruction.labels).Distinct().ToList();
		}

		/// <summary>Reports a failure</summary>
		/// <param name="method">The method involved</param>
		/// <param name="logger">The logger</param>
		/// <returns>True if current position is invalid and error was logged</returns>
		///
		public bool ReportFailure(MethodBase method, Action<string> logger)
		{
			if (IsValid) return false;
			var err = lastError ?? "Unexpected code";
			logger(err + " in " + method);
			return true;
		}

		/// <summary>Sets an instruction at current position</summary>
		/// <param name="instruction">The instruction to set</param>
		/// <returns>The same code matcher</returns>
		///
		public CodeMatcher SetInstruction(CodeInstruction instruction)
		{
			codes[Pos] = instruction;
			return this;
		}

		/// <summary>Sets instruction at current position and advances</summary>
		/// <param name="instruction">The instruction</param>
		/// <returns>The same code matcher</returns>
		///
		public CodeMatcher SetInstructionAndAdvance(CodeInstruction instruction)
		{
			SetInstruction(instruction);
			Pos++;
			return this;
		}

		/// <summary>Sets opcode and operand at current position</summary>
		/// <param name="opcode">The opcode</param>
		/// <param name="operand">The operand</param>
		/// <returns>The same code matcher</returns>
		///
		public CodeMatcher Set(OpCode opcode, object operand)
		{
			Opcode = opcode;
			Operand = operand;
			return this;
		}

		/// <summary>Sets opcode and operand at current position and advances</summary>
		/// <param name="opcode">The opcode</param>
		/// <param name="operand">The operand</param>
		/// <returns>The same code matcher</returns>
		///
		public CodeMatcher SetAndAdvance(OpCode opcode, object operand)
		{
			Set(opcode, operand);
			Pos++;
			return this;
		}

		/// <summary>Sets opcode at current position and advances</summary>
		/// <param name="opcode">The opcode</param>
		/// <returns>The same code matcher</returns>
		///
		public CodeMatcher SetOpcodeAndAdvance(OpCode opcode)
		{
			Opcode = opcode;
			Pos++;
			return this;
		}

		/// <summary>Sets operand at current position and advances</summary>
		/// <param name="operand">The operand</param>
		/// <returns>The same code matcher</returns>
		///
		public CodeMatcher SetOperandAndAdvance(object operand)
		{
			Operand = operand;
			Pos++;
			return this;
		}

		/// <summary>Creates a label at current position</summary>
		/// <param name="label">[out] The label</param>
		/// <returns>The same code matcher</returns>
		///
		public CodeMatcher CreateLabel(out Label label)
		{
			label = generator.DefineLabel();
			Labels.Add(label);
			return this;
		}

		/// <summary>Creates a label at a position</summary>
		/// <param name="position">The position</param>
		/// <param name="label">[out] The new label</param>
		/// <returns>The same code matcher</returns>
		///
		public CodeMatcher CreateLabelAt(int position, out Label label)
		{
			label = generator.DefineLabel();
			AddLabelsAt(position, new[] { label });
			return this;
		}

		/// <summary>Adds an enumeration of labels to current position</summary>
		/// <param name="labels">The labels</param>
		/// <returns>The same code matcher</returns>
		///
		public CodeMatcher AddLabels(IEnumerable<Label> labels)
		{
			Labels.AddRange(labels);
			return this;
		}

		/// <summary>Adds an enumeration of labels at a position</summary>
		/// <param name="position">The position</param>
		/// <param name="labels">The labels</param>
		/// <returns>The same code matcher</returns>
		///
		public CodeMatcher AddLabelsAt(int position, IEnumerable<Label> labels)
		{
			codes[position].labels.AddRange(labels);
			return this;
		}

		/// <summary>Sets jump to</summary>
		/// <param name="opcode">Branch instruction</param>
		/// <param name="destination">Destination for the jump</param>
		/// <param name="label">[out] The created label</param>
		/// <returns>The same code matcher</returns>
		///
		public CodeMatcher SetJumpTo(OpCode opcode, int destination, out Label label)
		{
			CreateLabelAt(destination, out label);
			Set(opcode, label);
			return this;
		}

		/// <summary>Inserts some instructions</summary>
		/// <param name="instructions">The instructions</param>
		/// <returns>The same code matcher</returns>
		///
		public CodeMatcher Insert(params CodeInstruction[] instructions)
		{
			codes.InsertRange(Pos, instructions);
			return this;
		}

		/// <summary>Inserts an enumeration of instructions</summary>
		/// <param name="instructions">The instructions</param>
		/// <returns>The same code matcher</returns>
		///
		public CodeMatcher Insert(IEnumerable<CodeInstruction> instructions)
		{
			codes.InsertRange(Pos, instructions);
			return this;
		}

		/// <summary>Inserts a branch</summary>
		/// <param name="opcode">The branch opcode</param>
		/// <param name="destination">Branch destination</param>
		/// <returns>The same code matcher</returns>
		///
		public CodeMatcher InsertBranch(OpCode opcode, int destination)
		{
			CreateLabelAt(destination, out var label);
			codes.Insert(Pos, new CodeInstruction(opcode, label));
			return this;
		}

		/// <summary>Inserts some instructions and advances the position</summary>
		/// <param name="instructions">The instructions</param>
		/// <returns>The same code matcher</returns>
		///
		public CodeMatcher InsertAndAdvance(params CodeInstruction[] instructions)
		{
			instructions.Do(instruction =>
			{
				Insert(instruction);
				Pos++;
			});
			return this;
		}

		/// <summary>Inserts an enumeration of instructions and advances the position</summary>
		/// <param name="instructions">The instructions</param>
		/// <returns>The same code matcher</returns>
		///
		public CodeMatcher InsertAndAdvance(IEnumerable<CodeInstruction> instructions)
		{
			instructions.Do(instruction => InsertAndAdvance(instruction));
			return this;
		}

		/// <summary>Inserts a branch and advances the position</summary>
		/// <param name="opcode">The branch opcode</param>
		/// <param name="destination">Branch destination</param>
		/// <returns>The same code matcher</returns>
		///
		public CodeMatcher InsertBranchAndAdvance(OpCode opcode, int destination)
		{
			InsertBranch(opcode, destination);
			Pos++;
			return this;
		}

		/// <summary>Removes current instruction</summary>
		/// <returns>The same code matcher</returns>
		///
		public CodeMatcher RemoveInstruction()
		{
			codes.RemoveAt(Pos);
			return this;
		}

		/// <summary>Removes some instruction fro current position by count</summary>
		/// <param name="count">Number of instructions</param>
		/// <returns>The same code matcher</returns>
		///
		public CodeMatcher RemoveInstructions(int count)
		{
			codes.RemoveRange(Pos, Pos + count - 1);
			return this;
		}

		/// <summary>Removes the instructions in a range</summary>
		/// <param name="start">The start</param>
		/// <param name="end">The end</param>
		/// <returns>The same code matcher</returns>
		///
		public CodeMatcher RemoveInstructionsInRange(int start, int end)
		{
			if (start > end) { var tmp = start; start = end; end = tmp; }
			codes.RemoveRange(start, end - start + 1);
			return this;
		}

		/// <summary>Removes the instructions in a offset range</summary>
		/// <param name="startOffset">The start offset</param>
		/// <param name="endOffset">The end offset</param>
		/// <returns>The same code matcher</returns>
		///
		public CodeMatcher RemoveInstructionsWithOffsets(int startOffset, int endOffset)
		{
			RemoveInstructionsInRange(Pos + startOffset, Pos + endOffset);
			return this;
		}

		/// <summary>Advances the current position</summary>
		/// <param name="offset">The offset</param>
		/// <returns>The same code matcher</returns>
		///
		public CodeMatcher Advance(int offset)
		{
			Pos += offset;
			if (IsValid == false) SetOutOfBounds(offset);
			return this;
		}

		/// <summary>Moves the current position to the start</summary>
		/// <returns>The same code matcher</returns>
		///
		public CodeMatcher Start()
		{
			Pos = 0;
			return this;
		}

		/// <summary>Moves the current position to the end</summary>
		/// <returns>The same code matcher</returns>
		///
		public CodeMatcher End()
		{
			Pos = Length - 1;
			return this;
		}

		/// <summary>Searches forward with a predicate and advances position</summary>
		/// <param name="predicate">The predicate</param>
		/// <returns>The same code matcher</returns>
		///
		public CodeMatcher SearchForward(Func<CodeInstruction, bool> predicate)
		{
			return Search(predicate, 1);
		}

		/// <summary>Searches backwards with a predicate and reverses position</summary>
		/// <param name="predicate">The predicate</param>
		/// <returns>The same code matcher</returns>
		///
		public CodeMatcher SearchBack(Func<CodeInstruction, bool> predicate)
		{
			return Search(predicate, -1);
		}

		CodeMatcher Search(Func<CodeInstruction, bool> predicate, int direction)
		{
			FixStart();
			while (IsValid && predicate(Instruction) == false)
				Pos += direction;
			lastError = IsInvalid ? "Cannot find " + predicate : null;
			return this;
		}

		/// <summary>Matches forward and advances position</summary>
		/// <param name="useEnd">True to set position to end of match, false to set it to the beginning of the match</param>
		/// <param name="matches">Some code matches</param>
		/// <returns>The same code matcher</returns>
		///
		public CodeMatcher MatchForward(bool useEnd, params CodeMatch[] matches)
		{
			return Match(matches, 1, useEnd);
		}

		/// <summary>Matches backwards and reverses position</summary>
		/// <param name="useEnd">True to set position to end of match, false to set it to the beginning of the match</param>
		/// <param name="matches">Some code matches</param>
		/// <returns>The same code matcher</returns>
		///
		public CodeMatcher MatchBack(bool useEnd, params CodeMatch[] matches)
		{
			return Match(matches, -1, useEnd);
		}

		CodeMatcher Match(CodeMatch[] matches, int direction, bool useEnd)
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

		/// <summary>Repeats a match action until boundaries are met</summary>
		/// <param name="matchAction">The match action</param>
		/// <param name="notFoundAction">An optional action that is executed when no match is found</param>
		/// <returns>The same code matcher</returns>
		///
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

		/// <summary>Gets a match by its name</summary>
		/// <param name="name">The match name</param>
		/// <returns>An instruction</returns>
		///
		public CodeInstruction NamedMatch(string name)
		{
			return lastMatches[name];
		}

		bool MatchSequence(int start, CodeMatch[] matches)
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