using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection.Emit;

namespace HarmonyLib
{
	/// <summary>A CodeInstruction match</summary>
	public class CodeMatch : CodeInstruction
	{
		/// <summary>The name of the match</summary>
		public string name;

		/// <summary>The matched opcodes</summary>
		public List<OpCode> opcodes = new();

		/// <summary>The matched operands</summary>
		public List<object> operands = new();

		/// <summary>The jumps from the match</summary>
		public List<int> jumpsFrom = new();

		/// <summary>The jumps to the match</summary>
		public List<int> jumpsTo = new();

		/// <summary>The match predicate</summary>
		public Func<CodeInstruction, bool> predicate;

		// used by HarmonyLib.Code
		internal CodeMatch Set(object operand, string name)
		{
			this.operand ??= operand;
			this.name ??= name;
			return this;
		}

		/// <summary>Creates a code match</summary>
		/// <param name="opcode">The optional opcode</param>
		/// <param name="operand">The optional operand</param>
		/// <param name="name">The optional name</param>
		///
		public CodeMatch(OpCode? opcode = null, object operand = null, string name = null)
		{
			if (opcode is OpCode opcodeValue)
			{
				this.opcode = opcodeValue;
				opcodes.Add(opcodeValue);
			}
			if (operand != null)
				operands.Add(operand);
			this.operand = operand;
			this.name = name;
		}

		/// <summary>Creates a code match that calls a method</summary>
		/// <param name="expression">The lambda expression using the method</param>
		/// <param name="name">The optional name</param>
		///
		public CodeMatch(Expression<Action> expression, string name = null)
		{
			opcodes.AddRange(new[]
			{
				OpCodes.Call,
				OpCodes.Callvirt,
			});
			operand = SymbolExtensions.GetMethodInfo(expression);
			this.name = name;
		}

		/// <summary>Creates a code match that calls a method</summary>
		/// <param name="expression">The lambda expression using the method</param>
		/// <param name="name">The optional name</param>
		///
		public CodeMatch(LambdaExpression expression, string name = null)
		{
			opcodes.AddRange(new[]
			{
				OpCodes.Call,
				OpCodes.Callvirt,
			});
			operand = SymbolExtensions.GetMethodInfo(expression);
			this.name = name;
		}

		/// <summary>Creates a code match</summary>
		/// <param name="instruction">The CodeInstruction</param>
		/// <param name="name">An optional name</param>
		///
		public CodeMatch(CodeInstruction instruction, string name = null) : this(instruction.opcode, instruction.operand, name)
		{
		}

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
				result += $"{name}: ";
			if (opcodes.Count > 0)
				result += $"opcodes={opcodes.Join()} ";
			if (operands.Count > 0)
				result += $"operands={operands.Join()} ";
			if (labels.Count > 0)
				result += $"labels={labels.Join()} ";
			if (blocks.Count > 0)
				result += $"blocks={blocks.Join()} ";
			if (jumpsFrom.Count > 0)
				result += $"jumpsFrom={jumpsFrom.Join()} ";
			if (jumpsTo.Count > 0)
				result += $"jumpsTo={jumpsTo.Join()} ";
			if (predicate != null)
				result += "predicate=yes ";
			return $"{result.TrimEnd()}]";
		}
	}
}
