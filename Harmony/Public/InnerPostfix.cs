using System;
using System.Collections.Generic;
using System.Reflection;

namespace HarmonyLib
{
	/// <summary>An inner postfix that is applied inside some method call inside a method</summary>
	public class InnerPostfix : InnerFix
	{
		/// <summary>This InnerFix is a InnerFixType.Postfix</summary>
		public override PatchType Type => PatchType.Postfix;

		/// <summary>Creates an inner postfix for an implicit defined method call</summary>
		/// <param name="target">The method call to patch</param>
		/// <param name="patch">The patch to apply</param>
		public InnerPostfix(InnerMethod target, MethodInfo patch) : base(target, patch) { }

		/// <summary>Creates an inner postfix for an indirectly defined method call</summary>
		/// <param name="targetFinder">Calculates Target from a given methods content</param>
		/// <param name="patch">The patch to apply</param>
		public InnerPostfix(Func<IEnumerable<CodeInstruction>, InnerMethod> targetFinder, MethodInfo patch) : base(targetFinder, patch) { }
	}
}
