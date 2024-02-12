using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace HarmonyLib
{
	/// <summary>A reverse patcher</summary>
	/// 
	/// <remarks>Creates a reverse patcher</remarks>
	/// <param name="instance">The Harmony instance</param>
	/// <param name="original">The original method/constructor</param>
	/// <param name="standin">Your stand-in stub method as <see cref="HarmonyMethod"/></param>
	///
	public class ReversePatcher(Harmony instance, MethodBase original, HarmonyMethod standin)
	{
		readonly Harmony instance = instance;
		readonly MethodBase original = original;
		readonly HarmonyMethod standin = standin;

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
