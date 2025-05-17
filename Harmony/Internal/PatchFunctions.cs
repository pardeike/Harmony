using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace HarmonyLib
{
	internal static class PatchFunctions
	{
		internal static List<MethodInfo> GetSortedPatchMethods(MethodBase original, Patch[] patches, bool debug)
			=> [.. new PatchSorter(patches, debug).Sort().Select(p => p.GetMethod(original))];
		private static List<Infix> GetInfixes(Patch[] patches) => [.. patches.Select(p => new Infix(p))];

		internal static MethodInfo UpdateWrapper(MethodBase original, PatchInfo patchInfo)
		{
			var debug = patchInfo.Debugging || Harmony.DEBUG;

			var sortedPrefixes = GetSortedPatchMethods(original, patchInfo.prefixes, debug);
			var sortedPostfixes = GetSortedPatchMethods(original, patchInfo.postfixes, debug);
			var sortedTranspilers = GetSortedPatchMethods(original, patchInfo.transpilers, debug);
			var sortedFinalizers = GetSortedPatchMethods(original, patchInfo.finalizers, debug);
			var sortedInnerPrefixes = GetInfixes(patchInfo.innerprefixes);
			var sortedInnerPostfixes = GetInfixes(patchInfo.innerpostfixes);

			var patcher = new MethodCreator(new MethodCreatorConfig(
				original,
				null,
				sortedPrefixes,
				sortedPostfixes,
				sortedTranspilers,
				sortedFinalizers,
				sortedInnerPrefixes,
				sortedInnerPostfixes,
				debug
			));
			var (replacement, finalInstructions) = patcher.CreateReplacement();
			if (replacement is null) throw new MissingMethodException($"Cannot create replacement for {original.FullDescription()}");

			try
			{
				PatchTools.DetourMethod(original, replacement);
			}
			catch (Exception ex)
			{
				throw HarmonyException.Create(ex, finalInstructions);
			}
			return replacement;
		}

		internal static MethodInfo ReversePatch(HarmonyMethod standin, MethodBase original, MethodInfo postTranspiler)
		{
			if (standin is null)
				throw new ArgumentNullException(nameof(standin));
			if (standin.method is null)
				throw new ArgumentNullException(nameof(standin), $"{nameof(standin)}.{nameof(standin.method)} is NULL");

			var debug = (standin.debug ?? false) || Harmony.DEBUG;

			var transpilers = new List<MethodInfo>();
			if (standin.reversePatchType == HarmonyReversePatchType.Snapshot)
			{
				var info = Harmony.GetPatchInfo(original);
				transpilers.AddRange(GetSortedPatchMethods(original, [.. info.Transpilers], debug));
			}
			if (postTranspiler is not null) transpilers.Add(postTranspiler);

			var emptyFix = new List<MethodInfo>();
			var emptyInner = new List<Infix>();
			var patcher = new MethodCreator(new MethodCreatorConfig(
				standin.method,
				original,
				emptyFix,
				emptyFix,
				transpilers,
				emptyFix,
				emptyInner,
				emptyInner,
				debug
			));
			var (replacement, finalInstructions) = patcher.CreateReplacement();
			if (replacement is null) throw new MissingMethodException($"Cannot create replacement for {standin.method.FullDescription()}");

			try
			{
				PatchTools.DetourMethod(standin.method, replacement);
			}
			catch (Exception ex)
			{
				throw HarmonyException.Create(ex, finalInstructions);
			}

			return replacement;
		}
	}
}
