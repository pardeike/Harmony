using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace Harmony.ILCopying
{
	public interface IILProcessor
	{
		List<ILInstruction> Start(ILGenerator generator, MethodBase original);
		List<ILInstruction> Process(ILInstruction instruction);
		List<ILInstruction> End(ILGenerator generator, MethodBase original);
	}

	[Serializable]
	public class MethodReplacer : IILProcessor
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

		public List<ILInstruction> Start(ILGenerator generator, MethodBase original)
		{
			return new List<ILInstruction>();
		}

		public List<ILInstruction> Process(ILInstruction instruction)
		{
			if (instruction == null) return new List<ILInstruction>();
			if (instruction.argument == from)
			{
				instruction.argument = to;
				instruction.operand = to;
			}
			return new List<ILInstruction> { instruction };
		}

		public List<ILInstruction> End(ILGenerator generator, MethodBase original)
		{
			return new List<ILInstruction>();
		}
	}
}