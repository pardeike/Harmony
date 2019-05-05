using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace HarmonyLib
{
	static class ReversePatcherExtensions
	{
		/// <summary>Creates an empty reverse patcher</summary>
		/// <param name="instance">The Harmony instance</param>
		/// <param name="original">The original method</param>
		/// <param name="standin">The stand-in method</param>
		///
		public static ReversePatcher CreateReversePatcher(this Harmony instance, MethodBase original, MethodInfo standin)
		{
			return new ReversePatcher(instance, original, standin);
		}
	}

	/// <summary>A reverse patcher</summary>
	public class ReversePatcher
	{
		readonly Harmony instance;
		readonly MethodBase original;
		readonly MethodInfo standin;

		/// <summary>Creates an empty reverse patcher</summary>
		/// <param name="instance">The Harmony instance</param>
		/// <param name="original">The original method</param>
		/// <param name="standin">The stand-in method</param>
		///
		public ReversePatcher(Harmony instance, MethodBase original, MethodInfo standin)
		{
			this.instance = instance;
			this.original = original;
			this.standin = standin;
		}

		/// <summary>Applies the patch</summary>
		///
		public void Patch(HarmonyReversePatchType type = HarmonyReversePatchType.Original)
		{
			if (original == null)
				throw new NullReferenceException("Null method for " + instance.Id);

			var transpiler = GetTranspiler(standin);
			PatchFunctions.ReversePatch(standin, original, instance.Id, transpiler);
		}

		internal MethodInfo GetTranspiler(MethodInfo method)
		{
			var methodName = method.Name;
			var type = method.DeclaringType;
			var methods = AccessTools.GetDeclaredMethods(type);
			var ici = typeof(IEnumerable<CodeInstruction>);
			return methods.FirstOrDefault(m =>
			{
				if (m.ReturnType != ici) return false;
				return m.Name.StartsWith('<' + methodName + '>');
			});
		}
	}
}