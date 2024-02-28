using System;
using System.Collections.Generic;
using System.Reflection;

namespace HarmonyLib
{
	/// <summary>An inner prefix that is applied inside some method call inside a method</summary>
	/// 
	public class InnerPrefix : InnerFix
	{
		/// <summary>This InnerFix is a InnerFixType.Prefix</summary>
		/// 
		public override PatchType Type => PatchType.Prefix;

		/// <summary>Creates an inner prefix for an implicit defined method call</summary>
		/// <param name="target">The method call to patch</param>
		/// <param name="patch">The patch to apply</param>
		/// 
		public InnerPrefix(InnerMethod target, MethodInfo patch) : base(target, patch) { }

		/// <summary>Creates an inner prefix for an indirectly defined method call</summary>
		/// <param name="targetFinder">Calculates Target from a given methods content</param>
		/// <param name="patch">The patch to apply</param>
		/// 
		public InnerPrefix(Func<IEnumerable<CodeInstruction>, InnerMethod> targetFinder, MethodInfo patch) : base(targetFinder, patch) { }
	}
}
