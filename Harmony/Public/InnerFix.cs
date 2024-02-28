using System;
using System.Collections.Generic;
using System.Reflection;

namespace HarmonyLib
{
	/// <summary>The base class for InnerPrefix and InnerPostfix</summary>
	///
	[Serializable]
	public abstract partial class InnerFix
	{
		internal abstract InnerFixType Type { get; set; }

		/// <summary>The method call to patch</summary>
		/// 
		public InnerMethod InnerMethod { get; set; }

		/// <summary>If defined will be used to calculate Target from a given methods content</summary>
		/// 
		public Func<IEnumerable<CodeInstruction>, InnerMethod> TargetFinder { get; set; }

		internal InnerFix(InnerFixType type, InnerMethod innerMethod)
		{
			Type = type;
			InnerMethod = innerMethod;
			TargetFinder = null;
		}

		/// <summary>Creates an infix for an indirectly defined method call</summary>
		/// /// <param name="type">The type of infix</param>
		/// <param name="targetFinder">Calculates Target from a given methods content</param>
		/// 
		internal InnerFix(InnerFixType type, Func<IEnumerable<CodeInstruction>, InnerMethod> targetFinder)
		{
			Type = type;
			InnerMethod = null;
			TargetFinder = targetFinder;
		}

		internal abstract IEnumerable<CodeInstruction> Apply(MethodBase original, IEnumerable<CodeInstruction> instructions);
	}
}
