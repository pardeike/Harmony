using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace HarmonyLib
{
	/// <summary>A CodeInstruction matcher</summary>
	///
	public class CodeMatcher
	{
		/// <summary>Delegate for error handling</summary>
		/// <param name="matcher">The current code matcher</param>
		/// <param name="error">The error message</param>
		/// <returns>True if the error should be suppressed and the matcher should continue (if possible)</returns>
		///
		public delegate bool ErrorHandler(CodeMatcher matcher, string error);

		private readonly ILGenerator generator;
		private readonly List<CodeInstruction> codes = [];

		private enum MatchPosition
		{
			Start,
			End
		}

		/// <summary>The current position</summary>
		/// <value>The index or -1 if out of bounds</value>
		///
		public int Pos { get; private set; } = -1;

		private Dictionary<string, CodeInstruction> lastMatches = [];
		private string lastError;
		private delegate CodeMatcher MatchDelegate();
		private MatchDelegate lastMatchCall;
		private ErrorHandler errorHandler;

		private void FixStart() => Pos = Math.Max(0, Pos);

		private T HandleException<T>(string error, T defaultValue)
		{
			if (errorHandler != null)
			{
				if (errorHandler(this, error))
					return defaultValue;
			}
			lastError = error;
			throw new InvalidOperationException(error);
		}

		private void HandleException(string error)
		{
			lastError = error;
			if (errorHandler != null)
			{
				errorHandler(this, error);
				return;
			}
			throw new InvalidOperationException(error);
		}

		private void SetOutOfBounds(int direction) => Pos = direction > 0 ? Length : -1;

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
		///
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
			codes = [.. instructions.Select(c => new CodeInstruction(c))];
		}

		/// <summary>Makes a clone of this instruction matcher</summary>
		/// <returns>A copy of this matcher</returns>
		///
		public CodeMatcher Clone()
		{
			return new CodeMatcher(codes, generator)
			{
				Pos = Pos,
				lastMatches = new Dictionary<string, CodeInstruction>(lastMatches),
				lastError = lastError,
				lastMatchCall = lastMatchCall,
				errorHandler = errorHandler
			};
		}

		/// <summary>Resets the current position to -1 and clears last matches and errors</summary>
		/// <param name="atFirstInstruction">If true, sets position to 0, otherwise sets it to -1</param>
		/// <returns>The same code matcher</returns>
		///
		public CodeMatcher Reset(bool atFirstInstruction = true)
		{
			Pos = atFirstInstruction ? 0 : -1;
			lastMatches.Clear();
			lastError = null;
			lastMatchCall = null;
			return this;
		}

		/// <summary>Gets instructions at the current position</summary>
		/// <value>The instruction</value>
		///
		public CodeInstruction Instruction => codes[Pos];

		/// <summary>Gets instructions at the current position with offset</summary>
		/// <param name="offset">The offset</param>
		/// <returns>The instruction</returns>
		///
		public CodeInstruction InstructionAt(int offset) => codes[Pos + offset];

		/// <summary>Gets all instructions</summary>
		/// <returns>A list of instructions</returns>
		///
		public List<CodeInstruction> Instructions() => codes;

		/// <summary>Gets all instructions as an enumeration</summary>
		/// <returns>A list of instructions</returns>
		///
		public IEnumerable<CodeInstruction> InstructionEnumeration() => codes.AsEnumerable();

		/// <summary>Gets some instructions counting from current position</summary>
		/// <param name="count">Number of instructions</param>
		/// <returns>A list of instructions</returns>
		///
		public List<CodeInstruction> Instructions(int count)
		{
			if (Pos < 0 || Pos + count > Length)
				return HandleException<List<CodeInstruction>>("Cannot retrieve instructions: range is out-of-bounds.", []);

			return [.. codes.GetRange(Pos, count).Select(c => new CodeInstruction(c))];
		}

		/// <summary>Gets all instructions within a range</summary>
		/// <param name="start">The start index</param>
		/// <param name="end">The end index</param>
		/// <returns>A list of instructions</returns>
		///
		public List<CodeInstruction> InstructionsInRange(int start, int end)
		{
			var instructions = codes;
			if (start > end)
				(end, start) = (start, end);

			if (start < 0 || end >= Length)
				return HandleException<List<CodeInstruction>>("Cannot retrieve instructions: range is out-of-bounds.", []);

			instructions = instructions.GetRange(start, end - start + 1);
			return [.. instructions.Select(c => new CodeInstruction(c))];
		}

		/// <summary>Gets all instructions within a range (relative to current position)</summary>
		/// <param name="startOffset">The start offset</param>
		/// <param name="endOffset">The end offset</param>
		/// <returns>A list of instructions</returns>
		///
		public List<CodeInstruction> InstructionsWithOffsets(int startOffset, int endOffset) => InstructionsInRange(Pos + startOffset, Pos + endOffset);

		/// <summary>Gets a list of all distinct labels</summary>
		/// <param name="instructions">The instructions (transpiler argument)</param>
		/// <returns>A list of Labels</returns>
		///
		public List<Label> DistinctLabels(IEnumerable<CodeInstruction> instructions) => [.. instructions.SelectMany(instruction => instruction.labels).Distinct()];

		/// <summary>Reports a failure</summary>
		/// <param name="method">The method involved</param>
		/// <param name="logger">The logger</param>
		/// <returns>True if current position is invalid and error was logged</returns>
		///
		public bool ReportFailure(MethodBase method, Action<string> logger)
		{
			if (IsValid)
				return false;
			var err = lastError ?? "Unexpected code";
			logger($"{err} in {method}");
			return true;
		}

		/// <summary>Throw an InvalidOperationException if current state is invalid (position out of bounds / last match failed)</summary>
		/// <param name="explanation">Explanation of where/why the exception was thrown that will be added to the exception message</param>
		/// <returns>The same code matcher</returns>
		///
		public CodeMatcher ThrowIfInvalid(string explanation)
		{
			if (explanation == null)
				throw new ArgumentNullException(nameof(explanation));
			if (IsInvalid)
				return HandleException(explanation + " - Current state is invalid", this);
			return this;
		}

		/// <summary>Throw an InvalidOperationException if current state is invalid (position out of bounds / last match failed),
		/// or if the matches do not match at current position</summary>
		/// <param name="explanation">Explanation of where/why the exception was thrown that will be added to the exception message</param>
		/// <param name="matches">Some code matches</param>
		/// <returns>The same code matcher</returns>
		///
		public CodeMatcher ThrowIfNotMatch(string explanation, params CodeMatch[] matches)
		{
			_ = ThrowIfInvalid(explanation);
			if (!MatchSequence(Pos, matches))
				return HandleException(explanation + " - Match failed", this);
			return this;
		}

		private void ThrowIfNotMatch(string explanation, int direction, CodeMatch[] matches)
		{
			_ = ThrowIfInvalid(explanation);
			var tempPos = Pos;
			try
			{
				if (Match(matches, direction, MatchPosition.Start, false).IsInvalid)
				{
					HandleException(explanation + " - Match failed");
					return;
				}
			}
			finally
			{
				Pos = tempPos;
			}
		}

		/// <summary>Throw an InvalidOperationException if current state is invalid (position out of bounds / last match failed),
		/// or if the matches do not match at any point between current position and the end</summary>
		/// <param name="explanation">Explanation of where/why the exception was thrown that will be added to the exception message</param>
		/// <param name="matches">Some code matches</param>
		/// <returns>The same code matcher</returns>
		///
		public CodeMatcher ThrowIfNotMatchForward(string explanation, params CodeMatch[] matches)
		{
			ThrowIfNotMatch(explanation, 1, matches);
			return this;
		}

		/// <summary>Throw an InvalidOperationException if current state is invalid (position out of bounds / last match failed),
		/// or if the matches do not match at any point between current position and the start</summary>
		/// <param name="explanation">Explanation of where/why the exception was thrown that will be added to the exception message</param>
		/// <param name="matches">Some code matches</param>
		/// <returns>The same code matcher</returns>
		///
		public CodeMatcher ThrowIfNotMatchBack(string explanation, params CodeMatch[] matches)
		{
			ThrowIfNotMatch(explanation, -1, matches);
			return this;
		}

		/// <summary>Throw an InvalidOperationException if current state is invalid (position out of bounds / last match failed),
		/// or if the check function returns false</summary>
		/// <param name="explanation">Explanation of where/why the exception was thrown that will be added to the exception message</param>
		/// <param name="stateCheckFunc">Function that checks validity of current state. If it returns false, an exception is thrown</param>
		/// <returns>The same code matcher</returns>
		///
		public CodeMatcher ThrowIfFalse(string explanation, Func<CodeMatcher, bool> stateCheckFunc)
		{
			if (stateCheckFunc == null)
				throw new ArgumentNullException(nameof(stateCheckFunc));
			_ = ThrowIfInvalid(explanation);
			if (!stateCheckFunc(this))
				return HandleException(explanation + " - Check function returned false", this);
			return this;
		}

		/// <summary>Runs some code when chaining <see cref="CodeMatcher"/> at the current position</summary>
		/// <param name="action">The <see cref="Action{CodeMatcher}"/> to run</param>
		/// <returns>The same code matcher</returns>
		///
		public CodeMatcher Do(Action<CodeMatcher> action)
		{
			if (action == null)
				throw new ArgumentNullException(nameof(action));
			action(this);
			return this;
		}

		/// <summary>Registers an error handler that is invoked instead of throwing an exception</summary>
		/// <param name="errorHandler">The <see cref="ErrorHandler"/> to register or <c>null</c> to remove the current handler</param>
		/// <returns>The same code matcher</returns>
		public CodeMatcher OnError(ErrorHandler errorHandler)
		{
			this.errorHandler = errorHandler;
			return this;
		}

		/// <summary>Sets an instruction at current position</summary>
		/// <param name="instruction">The instruction to set</param>
		/// <returns>The same code matcher</returns>
		///
		public CodeMatcher SetInstruction(CodeInstruction instruction)
		{
			if (IsInvalid)
				return HandleException("Cannot set instruction/opcode at invalid position.", this);

			codes[Pos] = instruction;
			return this;
		}

		/// <summary>Sets instruction at current position and advances</summary>
		/// <param name="instruction">The instruction</param>
		/// <returns>The same code matcher</returns>
		///
		public CodeMatcher SetInstructionAndAdvance(CodeInstruction instruction)
		{
			_ = SetInstruction(instruction);
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
			if (IsInvalid)
				return HandleException("Cannot set values at invalid position.", this);

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
			_ = Set(opcode, operand);
			Pos++;
			return this;
		}

		/// <summary>Sets opcode at current position and advances</summary>
		/// <param name="opcode">The opcode</param>
		/// <returns>The same code matcher</returns>
		///
		public CodeMatcher SetOpcodeAndAdvance(OpCode opcode)
		{
			if (IsInvalid)
				return HandleException("Cannot set opcode at invalid position.", this);

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
			if (IsInvalid)
				return HandleException("Cannot set operand at invalid position.", this);

			Operand = operand;
			Pos++;
			return this;
		}

		/// <summary>Declares a local variable but does not add it</summary>
		/// <param name="variableType">The variable type</param>
		/// <param name="localVariable">[out] The new local variable</param>
		/// <returns>The same code matcher</returns>
		///
		public CodeMatcher DeclareLocal(Type variableType, out LocalBuilder localVariable)
		{
			if (generator is null)
			{
				localVariable = default;
				return HandleException("Generator must be provided to use this method", this);
			}

			localVariable = generator.DeclareLocal(variableType);
			return this;
		}

		/// <summary>Declares a new label but does not add it</summary>
		/// <param name="label">[out] The new label</param>
		/// <returns>The same code matcher</returns>
		///
		public CodeMatcher DefineLabel(out Label label)
		{
			if (generator is null)
			{
				label = default;
				return HandleException("Generator must be provided to use this method", this);
			}

			label = generator.DefineLabel();
			return this;
		}

		/// <summary>Creates a label at current position</summary>
		/// <param name="label">[out] The label</param>
		/// <returns>The same code matcher</returns>
		///
		public CodeMatcher CreateLabel(out Label label)
		{
			if (generator is null)
			{
				label = default;
				return HandleException("Generator must be provided to use this method", this);
			}

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
			if (generator is null)
			{
				label = default;
				return HandleException("Generator must be provided to use this method", this);
			}

			label = generator.DefineLabel();
			_ = AddLabelsAt(position, [label]);
			return this;
		}

		/// <summary>Creates a label at the given offset from the current position</summary>
		/// <param name="offset">The offset</param>
		/// <param name="label">[out] The new label</param>
		/// <returns>The same code matcher</returns>
		///

		public CodeMatcher CreateLabelWithOffsets(int offset, out Label label)
		{
			if (generator is null)
			{
				label = default;
				return HandleException("Generator must be provided to use this method", this);
			}

			label = generator.DefineLabel();
			return AddLabelsAt(Pos + offset, [label]);
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
			if (position < 0 || position >= Length)
				return HandleException("Cannot add labels at invalid position.", this);

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
			_ = CreateLabelAt(destination, out label);
			return Set(opcode, label);
		}

		/// <summary>Inserts some instructions at the current position</summary>
		/// <param name="instructions">The instructions</param>
		/// <returns>The same code matcher</returns>
		///
		public CodeMatcher Insert(params CodeInstruction[] instructions)
		{
			if (instructions == null || instructions.Any(i => i == null))
				throw new ArgumentNullException(nameof(instructions));

			if (IsInvalid)
				return HandleException("Cannot insert instructions at invalid position.", this);

			codes.InsertRange(Pos, instructions);
			return this;
		}

		/// <summary>Inserts an enumeration of instructions at the current position</summary>
		/// <param name="instructions">The instructions</param>
		/// <returns>The same code matcher</returns>
		///
		public CodeMatcher Insert(IEnumerable<CodeInstruction> instructions)
		{
			if (instructions == null || instructions.Any(i => i == null))
				throw new ArgumentNullException(nameof(instructions));

			if (IsInvalid)
				return HandleException("Cannot insert instructions at invalid position.", this);

			codes.InsertRange(Pos, instructions);
			return this;
		}

		/// <summary>Inserts a branch at the current position</summary>
		/// <param name="opcode">The branch opcode</param>
		/// <param name="destination">Branch destination</param>
		/// <returns>The same code matcher</returns>
		///
		public CodeMatcher InsertBranch(OpCode opcode, int destination)
		{
			if (IsInvalid)
				return HandleException("Cannot insert instructions at invalid position.", this);

			_ = CreateLabelAt(destination, out var label);
			codes.Insert(Pos, new CodeInstruction(opcode, label));
			return this;
		}

		/// <summary>Inserts some instructions at the current position and advances it</summary>
		/// <param name="instructions">The instructions</param>
		/// <returns>The same code matcher</returns>
		///
		public CodeMatcher InsertAndAdvance(params CodeInstruction[] instructions)
		{
			if (instructions == null || instructions.Any(i => i == null))
				throw new ArgumentNullException(nameof(instructions));

			foreach (var instruction in instructions)
			{
				_ = Insert(instruction);
				Pos++;
			}

			return this;
		}

		/// <summary>Inserts an enumeration of instructions at the current position and advances it</summary>
		/// <param name="instructions">The instructions</param>
		/// <returns>The same code matcher</returns>
		///
		public CodeMatcher InsertAndAdvance(IEnumerable<CodeInstruction> instructions)
		{
			if (instructions == null || instructions.Any(i => i == null))
				throw new ArgumentNullException(nameof(instructions));

			foreach (var instruction in instructions)
				_ = InsertAndAdvance(instruction);
			return this;
		}

		/// <summary>Inserts a branch at the current position and advances it</summary>
		/// <param name="opcode">The branch opcode</param>
		/// <param name="destination">Branch destination</param>
		/// <returns>The same code matcher</returns>
		///
		public CodeMatcher InsertBranchAndAdvance(OpCode opcode, int destination)
		{
			_ = InsertBranch(opcode, destination);
			Pos++;
			return this;
		}

		/// <summary>Inserts instructions immediately after the current position</summary>
		/// <param name="instructions">The instructions</param>
		/// <returns>The same code matcher</returns>
		///
		public CodeMatcher InsertAfter(params CodeInstruction[] instructions)
		{
			if (instructions == null || instructions.Any(i => i == null))
				throw new ArgumentNullException(nameof(instructions));

			if (IsInvalid)
				return HandleException("Cannot insert instructions at invalid position.", this);

			codes.InsertRange(Pos + 1, instructions);
			return this;
		}

		/// <summary>Inserts an enumeration of instructions immediately after the current position</summary>
		/// <param name="instructions">The instructions</param>
		/// <returns>The same code matcher</returns>
		///
		public CodeMatcher InsertAfter(IEnumerable<CodeInstruction> instructions)
		{
			if (instructions == null || instructions.Any(i => i == null))
				return HandleException("Cannot insert null instructions.", this);

			if (IsInvalid)
				return HandleException("Cannot insert instructions at invalid position.", this);

			codes.InsertRange(Pos + 1, instructions);
			return this;
		}

		/// <summary>Inserts a branch instruction immediately after the current position</summary>
		/// <param name="opcode">The branch opcode</param>
		/// <param name="destination">Branch destination index</param>
		/// <returns>The same code matcher</returns>
		///
		public CodeMatcher InsertBranchAfter(OpCode opcode, int destination)
		{
			if (IsInvalid)
				return HandleException("Cannot insert instructions at invalid position.", this);

			_ = CreateLabelAt(destination, out var label);
			codes.Insert(Pos + 1, new CodeInstruction(opcode, label));
			return this;
		}

		/// <summary>Inserts instructions immediately after the current position and advances to the last inserted instruction</summary>
		/// <param name="instructions">The instructions</param>
		/// <returns>The same code matcher</returns>
		///
		public CodeMatcher InsertAfterAndAdvance(params CodeInstruction[] instructions)
		{
			_ = InsertAfter(instructions);
			Pos += instructions.Length;
			return this;
		}

		/// <summary>Inserts an enumeration of instructions immediately after the current position and advances to the last inserted instruction</summary>
		/// <param name="instructions">The instructions</param>
		/// <returns>The same code matcher</returns>
		///
		public CodeMatcher InsertAfterAndAdvance(IEnumerable<CodeInstruction> instructions)
		{
			if (instructions == null || instructions.Any(i => i == null))
				return HandleException("Cannot insert null instructions.", this);

			var instructionList = instructions.ToList();
			_ = InsertAfter(instructionList);
			Pos += instructionList.Count;
			return this;
		}

		/// <summary>Inserts a branch instruction immediately after the current position and advances the position</summary>
		/// <param name="opcode">The branch opcode</param>
		/// <param name="destination">Branch destination index</param>
		/// <returns>The same code matcher</returns>
		///
		public CodeMatcher InsertBranchAfterAndAdvance(OpCode opcode, int destination)
		{
			_ = InsertBranchAfter(opcode, destination);
			Pos++;
			return this;
		}

		/// <summary>Removes current instruction</summary>
		/// <returns>The same code matcher</returns>
		///
		public CodeMatcher RemoveInstruction()
		{
			if (IsInvalid)
				return HandleException("Cannot remove instructions from an invalid position.", this);

			codes.RemoveAt(Pos);
			return this;
		}

		/// <summary>Removes some instruction from current position by count</summary>
		/// <param name="count">Number of instructions</param>
		/// <returns>The same code matcher</returns>
		///
		public CodeMatcher RemoveInstructions(int count)
		{
			if (IsInvalid || Pos + count > Length)
				return HandleException("Cannot remove instructions from an invalid or out-of-range position.", this);

			codes.RemoveRange(Pos, count);
			return this;
		}

		/// <summary>Removes the instructions in a range</summary>
		/// <param name="start">The start</param>
		/// <param name="end">The end</param>
		/// <returns>The same code matcher</returns>
		///
		public CodeMatcher RemoveInstructionsInRange(int start, int end)
		{
			if (start > end)
				(end, start) = (start, end);

			if (start < 0 || end >= Length)
				return HandleException("Cannot remove instructions: range is out-of-bounds.", this);

			codes.RemoveRange(start, end - start + 1);
			return this;
		}

		/// <summary>Removes the instructions in an offset range</summary>
		/// <param name="startOffset">The start offset</param>
		/// <param name="endOffset">The end offset</param>
		/// <returns>The same code matcher</returns>
		///
		public CodeMatcher RemoveInstructionsWithOffsets(int startOffset, int endOffset) => RemoveInstructionsInRange(Pos + startOffset, Pos + endOffset);

		/// <summary>Advances the current position</summary>
		/// <param name="offset">The offset</param>
		/// <returns>The same code matcher</returns>
		///
		public CodeMatcher Advance(int offset = 1)
		{
			Pos += offset;
			if (IsValid == false)
				SetOutOfBounds(offset);
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
		/// <param name="predicate">A function to test each instruction for a match</param>
		/// <returns>The same code matcher</returns>
		///
		public CodeMatcher SearchForward(Func<CodeInstruction, bool> predicate) => Search(predicate, 1);

		/// <summary>Searches backwards with a predicate and moves the position</summary>
		/// <param name="predicate">A function to test each instruction for a match</param>
		/// <returns>The same code matcher</returns>
		///
		public CodeMatcher SearchBackwards(Func<CodeInstruction, bool> predicate) => Search(predicate, -1);

		private CodeMatcher Search(Func<CodeInstruction, bool> predicate, int direction)
		{
			FixStart();
			while (IsValid && predicate(Instruction) == false)
				Pos += direction;
			lastError = IsInvalid ? $"Cannot find {predicate}" : null;
			return this;
		}

		/// <summary>Matches forward and advances position to beginning of matching sequence</summary>
		/// <param name="matches">Some code matches</param>
		/// <returns>The same code matcher</returns>
		///
		public CodeMatcher MatchStartForward(params CodeMatch[] matches) => Match(matches, 1, MatchPosition.Start, false);

		/// <summary>Prepares matching forward and advancing position to beginning of matching sequence</summary>
		/// <param name="matches">Some code matches</param>
		/// <returns>The same code matcher</returns>
		///
		public CodeMatcher PrepareMatchStartForward(params CodeMatch[] matches) => Match(matches, 1, MatchPosition.Start, true);

		/// <summary>Matches forward and advances position to ending of matching sequence</summary>
		/// <param name="matches">Some code matches</param>
		/// <returns>The same code matcher</returns>
		///
		public CodeMatcher MatchEndForward(params CodeMatch[] matches) => Match(matches, 1, MatchPosition.End, false);

		/// <summary>Prepares matching forward and advancing position to ending of matching sequence</summary>
		/// <param name="matches">Some code matches</param>
		/// <returns>The same code matcher</returns>
		///
		public CodeMatcher PrepareMatchEndForward(params CodeMatch[] matches) => Match(matches, 1, MatchPosition.End, true);

		/// <summary>Matches backwards and moves the position to beginning of matching sequence</summary>
		/// <param name="matches">Some code matches</param>
		/// <returns>The same code matcher</returns>
		///
		public CodeMatcher MatchStartBackwards(params CodeMatch[] matches) => Match(matches, -1, MatchPosition.Start, false);

		/// <summary>Prepares matching backwards and reversing position to beginning of matching sequence</summary>
		/// <param name="matches">Some code matches</param>
		/// <returns>The same code matcher</returns>
		///
		public CodeMatcher PrepareMatchStartBackwards(params CodeMatch[] matches) => Match(matches, -1, MatchPosition.Start, true);

		/// <summary>Matches backwards and moves the position to ending of matching sequence</summary>
		/// <param name="matches">Some code matches</param>
		/// <returns>The same code matcher</returns>
		///
		public CodeMatcher MatchEndBackwards(params CodeMatch[] matches) => Match(matches, -1, MatchPosition.End, false);

		/// <summary>Prepares matching backwards and reversing position to ending of matching sequence</summary>
		/// <param name="matches">Some code matches</param>
		/// <returns>The same code matcher</returns>
		///
		public CodeMatcher PrepareMatchEndBackwards(params CodeMatch[] matches) => Match(matches, -1, MatchPosition.End, true);

		/// <summary>Removes instructions from the current position forward until a predicate is matched. The matched instruction is not removed</summary>
		/// <param name="predicate">A function to test each instruction for a match</param>
		/// <returns>The same code matcher</returns>
		///
		public CodeMatcher RemoveSearchForward(Func<CodeInstruction, bool> predicate)
		{
			if (IsInvalid)
				return HandleException("Cannot remove instructions from an invalid position.", this);

			var originalPos = Pos;
			var finder = Clone().SearchForward(predicate);
			if (finder.IsInvalid)
			{
				lastError = finder.lastError;
				SetOutOfBounds(1);
				return this;
			}

			var end = finder.Pos - 1; // stop before the matching instruction
			if (end >= originalPos)
				_ = RemoveInstructionsInRange(originalPos, end);
			return this;
		}

		/// <summary>Removes instructions from the current position backward until a predicate is matched. The matched instruction is not removed</summary>
		/// <param name="predicate">A function to test each instruction for a match</param>
		/// <returns>The same code matcher</returns>
		///
		public CodeMatcher RemoveSearchBackward(Func<CodeInstruction, bool> predicate)
		{
			if (IsInvalid)
				return HandleException("Cannot remove instructions from an invalid position.", this);

			var originalPos = Pos;
			var finder = Clone().SearchBackwards(predicate);
			if (finder.IsInvalid)
			{
				lastError = finder.lastError;
				SetOutOfBounds(-1);
				return this;
			}

			var matchPos = finder.Pos;
			var start = matchPos + 1;
			if (originalPos >= start)
				_ = RemoveInstructionsInRange(start, originalPos);
			Pos = matchPos;
			return this;
		}

		/// <summary>Removes instructions from the current position up to the next match (exclusive)</summary>
		/// <param name="matches">Some code matches</param>
		/// <returns>The same code matcher</returns>
		///
		public CodeMatcher RemoveUntilForward(params CodeMatch[] matches)
		{
			if (IsInvalid)
				return HandleException("Cannot remove instructions from an invalid position.", this);

			var originalPos = Pos;
			var finder = Clone().MatchStartForward(matches);
			if (finder.IsInvalid)
			{
				lastError = finder.lastError;
				SetOutOfBounds(1);
				return this;
			}

			var end = finder.Pos - 1;
			if (end >= originalPos)
				_ = RemoveInstructionsInRange(originalPos, end);
			return this;
		}

		/// <summary>Removes instructions backwards from the current position to the previous match (exclusive)</summary>
		/// <param name="matches">Some code matches</param>
		/// <returns>The same code matcher</returns>
		///
		public CodeMatcher RemoveUntilBackward(params CodeMatch[] matches)
		{
			if (IsInvalid)
				return HandleException("Cannot remove instructions from an invalid position.", this);

			var originalPos = Pos;
			var finder = Clone().MatchEndBackwards(matches);
			if (finder.IsInvalid)
			{
				lastError = finder.lastError;
				SetOutOfBounds(-1);
				return this;
			}

			var start = finder.Pos;
			if (originalPos > start)
				_ = RemoveInstructionsInRange(start + 1, originalPos);
			Pos = start;
			return this;
		}

		private CodeMatcher Match(CodeMatch[] matches, int direction, MatchPosition mode, bool prepareOnly)
		{
			lastMatchCall = delegate ()
			{
				while (IsValid)
				{
					if (MatchSequence(Pos, matches))
					{
						if (mode == MatchPosition.End)
							Pos += matches.Length - 1;
						break;
					}

					Pos += direction;
				}

				lastError = IsInvalid ? $"Cannot find {matches.Join()}" : null;
				return this;
			};
			if (prepareOnly)
				return this;
			FixStart();
			return lastMatchCall();
		}

		/// <summary>Repeats a match action until boundaries are met</summary>
		/// <param name="matchAction">The match action</param>
		/// <param name="notFoundAction">An optional action that is executed when no match is found</param>
		/// <returns>The same code matcher</returns>
		///
		public CodeMatcher Repeat(Action<CodeMatcher> matchAction, Action<string> notFoundAction = null)
		{
			var count = 0;
			if (lastMatchCall == null)
				return HandleException("No previous Match operation - cannot repeat", this);

			while (IsValid)
			{
				matchAction(this);
				_ = lastMatchCall();
				count++;
			}

			lastMatchCall = null;

			if (count == 0 && notFoundAction != null)
				notFoundAction(lastError);

			return this;
		}

		/// <summary>Gets a match by its name</summary>
		/// <param name="name">The match name</param>
		/// <returns>An instruction</returns>
		///
		public CodeInstruction NamedMatch(string name) => lastMatches[name];

		private bool MatchSequence(int start, CodeMatch[] matches)
		{
			if (start < 0)
				return false;
			lastMatches = [];
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