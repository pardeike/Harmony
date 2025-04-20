using System;
using System.Collections.Generic;
using System.Reflection;

namespace HarmonyLib
{
	/// <summary>An inner postfix that is applied inside some method call inside a method</summary>
	/// 
	public class InnerPostfix : InnerFix
	{
		internal override InnerFixType Type
		{
			get => InnerFixType.Postfix;
			set => throw new NotImplementedException();
		}

		/// <summary>Creates an infix for an implicit defined method call</summary>
		/// <param name="innerMethod">The method call to apply the fix to</param>
		/// 
		public InnerPostfix(InnerMethod innerMethod) : base(InnerFixType.Postfix, innerMethod) { }

		/// <summary>Creates an infix for an indirectly defined method call</summary>
		/// <param name="targetFinder">Calculates Target from a given methods content</param>
		/// 
		public InnerPostfix(Func<IEnumerable<CodeInstruction>, InnerMethod> targetFinder) : base(InnerFixType.Postfix, targetFinder) { }

		internal override IEnumerable<CodeInstruction> Apply(MethodBase original, IEnumerable<CodeInstruction> instructions)
		{
			// TODO: implement
			_ = original;
			foreach (var instruction in instructions) yield return instruction;
		}
	}
}
