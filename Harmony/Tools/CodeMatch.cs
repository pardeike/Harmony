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
		public HashSet<OpCode> opcodeSet = [];

		// for backwards compatibility we keep
		/// <summary>The matched opcodes</summary>
		[Obsolete("Use opcodeSet instead")]
#pragma warning disable IDE1006
		public List<OpCode> opcodes
		{
			get => opcodeSet.ToList();
			set => opcodeSet = new HashSet<OpCode>(value);
		}
#pragma warning restore IDE1006

		/// <summary>The matched operands</summary>
		public List<object> operands = [];

		/// <summary>The jumps from the match</summary>
		public List<int> jumpsFrom = [];

		/// <summary>The jumps to the match</summary>
		public List<int> jumpsTo = [];

		/// <summary>The match predicate</summary>
		public Func<CodeInstruction, bool> predicate;

		// used by HarmonyLib.Code
		internal CodeMatch Set(object operand, string name)
		{
			this.operand ??= operand;
			if (operand != null) operands.Add(operand);
			this.name ??= name;
			return this;
		}
		internal CodeMatch Set(OpCode opcode, object operand, string name)
		{
			this.opcode = opcode;
			_ = opcodeSet.Add(opcode);
			this.operand ??= operand;
			if (operand != null) operands.Add(operand);
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
				_ = opcodeSet.Add(opcodeValue);
			}
			if (operand != null)
				operands.Add(operand);
			this.operand = operand;
			this.name = name;
		}

		/// <summary>Creates a code match</summary>
		/// <param name="opcodes">The opcodes</param>
		/// <param name="operand">The optional operand</param>
		/// <param name="name">The optional name</param>
		///
		public static CodeMatch WithOpcodes(HashSet<OpCode> opcodes, object operand = null, string name = null)
		{
			return new CodeMatch(null, operand, name) { opcodeSet = opcodes };
		}

		/// <summary>Creates a code match that calls a method</summary>
		/// <param name="expression">The lambda expression using the method</param>
		/// <param name="name">The optional name</param>
		///
		public CodeMatch(Expression<Action> expression, string name = null)
		{
			opcodeSet.UnionWith(CodeInstructionExtensions.opcodesCalling);
			operand = SymbolExtensions.GetMethodInfo(expression);
			if (operand != null)
				operands.Add(operand);
			this.name = name;
		}

		/// <summary>Creates a code match that calls a method</summary>
		/// <param name="expression">The lambda expression using the method</param>
		/// <param name="name">The optional name</param>
		///
		public CodeMatch(LambdaExpression expression, string name = null)
		{
			opcodeSet.UnionWith(CodeInstructionExtensions.opcodesCalling);
			operand = SymbolExtensions.GetMethodInfo(expression);
			if (operand != null)
				operands.Add(operand);
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

			if (opcodeSet.Count > 0 && opcodeSet.Contains(instruction.opcode) == false) return false;
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

		/// <summary>Creates a code match for local loads</summary>
		/// <param name="useAddress">Whether to match for address loads</param>
		/// <param name="name">An optional name</param>
		/// <returns></returns>
		public static CodeMatch LoadsLocal(bool useAddress = false, string name = null)
		{
			return WithOpcodes(useAddress ? CodeInstructionExtensions.opcodesLoadingLocalByAddress : CodeInstructionExtensions.opcodesLoadingLocalNormal, null, name);
		}

		/// <summary>Creates a code match for local stores</summary>
		/// <param name="name">An optional name</param>
		/// <returns></returns>
		public static CodeMatch StoresLocal(string name = null)
		{
			return WithOpcodes(CodeInstructionExtensions.opcodesStoringLocal, null, name);
		}

		/// <summary>Creates a code match for argument loads</summary>
		/// <param name="useAddress">Whether to match for address loads</param>
		/// <param name="name">An optional name</param>
		/// <returns></returns>
		public static CodeMatch LoadsArgument(bool useAddress = false, string name = null)
		{
			return WithOpcodes(useAddress ? CodeInstructionExtensions.opcodesLoadingArgumentByAddress : CodeInstructionExtensions.opcodesLoadingArgumentNormal, null, name);
		}

		/// <summary>Creates a code match for argument stores</summary>
		/// <param name="name">An optional name</param>
		/// <returns></returns>
		public static CodeMatch StoresArgument(string name = null)
		{
			return WithOpcodes(CodeInstructionExtensions.opcodesStoringArgument, null, name);
		}

		/// <summary>Creates a code match for branching</summary>
		/// <param name="name">An optional name</param>
		/// <returns></returns>
		public static CodeMatch Branches(string name = null)
		{
			return WithOpcodes(CodeInstructionExtensions.opcodesBranching, null, name);
		}

		/// <summary>Returns a string that represents the match</summary>
		/// <returns>A string representation</returns>
		///
		public override string ToString()
		{
			var result = "[";
			if (name != null)
				result += $"{name}: ";
			if (opcodeSet.Count > 0)
				result += $"opcodes={opcodeSet.Join()} ";
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
