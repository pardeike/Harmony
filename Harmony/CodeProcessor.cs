using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace Harmony
{
	public interface ICodeProcessor
	{
		List<CodeInstruction> Start(ILGenerator generator, MethodBase original);
		List<CodeInstruction> Process(CodeInstruction instruction);
		List<CodeInstruction> End(ILGenerator generator, MethodBase original);
	}

	public abstract class CodeProcessor : ICodeProcessor
	{
		public virtual List<CodeInstruction> Start(ILGenerator generator, MethodBase original)
		{
			return null;
		}

		public virtual List<CodeInstruction> Process(CodeInstruction instruction)
		{
			return null;
		}

		public virtual List<CodeInstruction> End(ILGenerator generator, MethodBase original)
		{
			return null;
		}
	}

	public class MethodReplacer : CodeProcessor
	{
		MethodInfo from;
		MethodInfo to;

		public MethodReplacer(MethodInfo from, MethodInfo to)
		{
			if (from == null) throw new Exception("From-method cannot be null");
			if (to == null) throw new Exception("To-method cannot be null");
			this.from = from;
			this.to = to;
		}

		public new List<CodeInstruction> Process(CodeInstruction instruction)
		{
			if (instruction == null) return new List<CodeInstruction>();
			if (instruction.operand == from)
				instruction.operand = to;
			return new List<CodeInstruction> { instruction };
		}
	}
}