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
		static List<PatchCall> GetSortedPatchCalls(MethodBase original, Patch[] patches, bool debug, Patch[] previousPostfixes = null)
		{
			var unchangedPairs = new HashSet<(MethodInfo, MethodInfo)>();
			MethodInfo previous = null;
			if (previousPostfixes is not null)
				try
				{
					if (previousPostfixes.All(patch => patch.PatchMethod is not null))
						foreach (var patch in new PatchSorter(previousPostfixes, false).Sort())
						{
							var method = patch.PatchMethod;
							if (method.ReturnType == typeof(void)) continue;
							// Keep factories as barriers without invoking them: their previous callback is not in shared state.
							unchangedPairs.Add((previous, method));
							previous = method;
						}
				}
				catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or TypeLoadException)
				{
					// A removed callback may no longer resolve. Check the remaining bindings instead of blocking its removal.
					unchangedPairs.Clear();
				}
			var calls = new List<PatchCall>();
			previous = null;
			var previousCandidate = false;
			foreach (var patch in new PatchSorter(patches, debug).Sort())
			{
				var method = patch.GetMethod(original);
				var candidate = patch.candidate || Patch.IsFactory(patch.PatchMethod);
				var unchanged = !candidate && !previousCandidate && unchangedPairs.Contains((previous, method));
				calls.Add(new PatchCall(method, patch.uncheckedReferenceBinding, candidate, unchanged));
				if (method.ReturnType == typeof(void)) continue;
				previous = method;
				previousCandidate = candidate;
			}
			return calls;
		}

		private static List<Infix> GetInfixes(Patch[] patches) => [.. patches.Select(p => new Infix(p))];

		internal static MethodInfo UpdateWrapper(MethodBase original, PatchInfo patchInfo)
		{
			patchInfo.ValidateSurvivingMetadata();
			patchInfo.VersionCount++;
			var bytes = patchInfo.SerializeValidated();
			var debug = patchInfo.Debugging || Harmony.DEBUG;

			var sortedPrefixes = GetSortedPatchCalls(original, patchInfo.prefixes, debug);
			var previousPostfixes = patchInfo.postfixes.Length == 0 ? null : HarmonySharedState.GetPatchInfo(original)?.postfixes;
			var sortedPostfixes = GetSortedPatchCalls(original, patchInfo.postfixes, debug, previousPostfixes);
			var sortedTranspilers = GetSortedPatchMethods(original, patchInfo.transpilers, debug);
			var sortedFinalizers = GetSortedPatchCalls(original, patchInfo.finalizers, debug);
			var sortedInnerPrefixes = GetInfixes(patchInfo.innerprefixes);
			var sortedInnerPostfixes = GetInfixes(patchInfo.innerpostfixes);
			var sortedInnerFinalizers = GetInfixes(patchInfo.innerfinalizers);

			var patcher = new MethodCreator(new MethodCreatorConfig(
				original,
				null,
				sortedPrefixes,
				sortedPostfixes,
				sortedTranspilers,
				sortedFinalizers,
				sortedInnerPrefixes,
				sortedInnerPostfixes,
				sortedInnerFinalizers,
				debug
			));
			var (replacement, finalInstructions) = patcher.CreateReplacement();
			if (replacement is null) throw new MissingMethodException($"Cannot create replacement for {original.FullDescription()}");
			using var persistence = PersistentStateHooks.Prepare(original, patcher.config.persistence);

			try
			{
				PatchTools.DetourMethod(original, replacement);
			}
			catch (Exception ex)
			{
				var enriched = HarmonyException.Create(ex, finalInstructions);
				if (ReferenceEquals(enriched, ex)) throw;
				throw enriched;
			}
			HarmonySharedState.UpdatePatchInfo(original, replacement, bytes);
			persistence?.Commit();
			return replacement;
		}

		internal static MethodInfo ReversePatch(HarmonyMethod standin, MethodBase original, MethodInfo postTranspiler)
		{
			if (standin is null)
				throw new ArgumentNullException(nameof(standin));
			if (standin.method is null)
				throw new ArgumentNullException(nameof(standin), $"{nameof(standin)}.{nameof(standin.method)} is NULL");
			AttributePatch.ValidateOrdinary(standin);

			var debug = (standin.debug ?? false) || Harmony.DEBUG;

			var transpilers = new List<MethodInfo>();
			if (standin.reversePatchType == HarmonyReversePatchType.Snapshot)
			{
				var info = Harmony.GetPatchInfo(original);
				transpilers.AddRange(GetSortedPatchMethods(original, [.. info.Transpilers], debug));
			}
			if (postTranspiler is not null) transpilers.Add(postTranspiler);

			var emptyFix = new List<PatchCall>();
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
				var enriched = HarmonyException.Create(ex, finalInstructions);
				if (ReferenceEquals(enriched, ex)) throw;
				throw enriched;
			}

			return replacement;
		}
	}
}
