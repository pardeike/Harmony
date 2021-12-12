using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace HarmonyLib
{
	/// <summary>A reverse patcher</summary>
	/// 
	public class ReversePatcher
	{
		readonly Harmony instance;
		readonly MethodBase original;
		readonly HarmonyMethod standin;

		/// <summary>Creates a reverse patcher</summary>
		/// <param name="instance">The Harmony instance</param>
		/// <param name="original">The original method/constructor</param>
		/// <param name="standin">Your stand-in stub method as <see cref="HarmonyMethod"/></param>
		///
		public ReversePatcher(Harmony instance, MethodBase original, HarmonyMethod standin)
		{
			this.instance = instance;
			this.original = original;
			this.standin = standin;
		}

		/// <summary>Applies the patch</summary>
		/// <param name="type">The type of patch, see <see cref="HarmonyReversePatchType"/></param>
		/// <returns>The generated replacement method</returns>
		///
		public MethodInfo Patch(HarmonyReversePatchType type = HarmonyReversePatchType.Original)
		{
			if (original is null)
				throw new NullReferenceException($"Null method for {instance.Id}");

			standin.reversePatchType = type;
			var transpiler = GetTranspiler(standin.method);
			return PatchFunctions.ReversePatch(standin, original, transpiler);
		}

		internal static MethodInfo GetTranspiler(MethodInfo method)
		{
			var methodName = method.Name;
			var type = method.DeclaringType;
			var methods = AccessTools.GetDeclaredMethods(type);
			var ici = typeof(IEnumerable<CodeInstruction>);
			return methods.FirstOrDefault(m =>
			{
				if (m.ReturnType != ici) return false;
				return m.Name.StartsWith($"<{methodName }>");
			});
		}
	}
}
