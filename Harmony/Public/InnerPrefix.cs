using System;
using System.Collections.Generic;
using System.Reflection;

namespace HarmonyLib
{
	/// <summary>An inner prefix that is applied inside some method call inside a method</summary>
	/// 
	public class InnerPrefix : InnerFix
	{
		internal override InnerFixType Type {
			get => InnerFixType.Prefix;
			set => throw new NotImplementedException();
		}

		/// <summary>Creates an infix for an implicit defined method call</summary>
		/// <param name="innerMethod">The method call to apply the fix to</param>
		/// 
		public InnerPrefix(InnerMethod innerMethod) : base(InnerFixType.Prefix, innerMethod) { }

		/// <summary>Creates an infix for an indirectly defined method call</summary>
		/// <param name="targetFinder">Calculates Target from a given methods content</param>
		/// 
		public InnerPrefix(Func<IEnumerable<CodeInstruction>, InnerMethod> targetFinder) : base(InnerFixType.Prefix, targetFinder) { }

		internal override IEnumerable<CodeInstruction> Apply(MethodBase original, IEnumerable<CodeInstruction> instructions)
		{
			_ = original;
			foreach (var instruction in instructions) yield return instruction;
		}
	}
}
