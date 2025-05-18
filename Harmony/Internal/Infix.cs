using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using static HarmonyLib.Code;

namespace HarmonyLib
{
	internal class Infix
	{
		internal Patch patch;

		internal Infix(Patch patch) => this.patch = patch;

		internal MethodInfo OuterMethod => patch.PatchMethod;
		internal MethodBase InnerMethod => patch.innerMethod.Method;
		internal int[] Positions => patch.innerMethod.positions; // multiple 1-based positions, or empty array for all positions

		internal bool Matches(MethodBase method, int index, int total) // index is 1-based
		{
			if (method != InnerMethod) return false;
			if (Positions.Length == 0) return true;
			foreach (var pos in Positions)
			{
				if (pos > 0 && pos == index) return true;
				if (pos < 0 && index == total + pos + 1) return true;
			}
			return false;
		}

		internal IEnumerable<CodeInstruction> Apply(MethodCreatorConfig config, bool isPrefix)
		{
			// TODO: implement
			_ = config;
			yield return Nop[isPrefix ? "inner-prefix" : "inner-postfix"];
		}
	}

	internal static class InfixExtensions
	{
		internal static Infix[] FilterAndSort(this IEnumerable<Infix> infixes, MethodInfo innerMethod, int index, int total, bool debug)
			=> [.. new PatchSorter([..
					infixes
						.Where(fix => fix.Matches(innerMethod, index, total))
						.Select(fix => fix.patch)
				], debug)
				.Sort()
				.Select(p => new Infix(p))];
	}
}
