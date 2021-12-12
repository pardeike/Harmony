using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace HarmonyLib
{
	/// <summary>Patch function helpers</summary>
	internal static class PatchFunctions
	{
		/// <summary>Sorts patch methods by their priority rules</summary>
		/// <param name="original">The original method</param>
		/// <param name="patches">Patches to sort</param>
		/// <param name="debug">Use debug mode</param>
		/// <returns>The sorted patch methods</returns>
		///
		internal static List<MethodInfo> GetSortedPatchMethods(MethodBase original, Patch[] patches, bool debug)
		{
			return new PatchSorter(patches, debug).Sort(original);
		}

		/// <summary>Creates new replacement method with the latest patches and detours the original method</summary>
		/// <param name="original">The original method</param>
		/// <param name="patchInfo">Information describing the patches</param>
		/// <returns>The newly created replacement method</returns>
		///
		internal static MethodInfo UpdateWrapper(MethodBase original, PatchInfo patchInfo)
		{
			var debug = patchInfo.Debugging || Harmony.DEBUG;

			var sortedPrefixes = GetSortedPatchMethods(original, patchInfo.prefixes, debug);
			var sortedPostfixes = GetSortedPatchMethods(original, patchInfo.postfixes, debug);
			var sortedTranspilers = GetSortedPatchMethods(original, patchInfo.transpilers, debug);
			var sortedFinalizers = GetSortedPatchMethods(original, patchInfo.finalizers, debug);

			var patcher = new MethodPatcher(original, null, sortedPrefixes, sortedPostfixes, sortedTranspilers, sortedFinalizers, debug);
			var replacement = patcher.CreateReplacement(out var finalInstructions);
			if (replacement is null) throw new MissingMethodException($"Cannot create replacement for {original.FullDescription()}");

			try
			{
				Memory.DetourMethodAndPersist(original, replacement);
			}
			catch (Exception ex)
			{
				throw HarmonyException.Create(ex, finalInstructions);
			}
			return replacement;
		}

		internal static void UpdateRecompiledMethod(MethodBase original, IntPtr codeStart, PatchInfo patchInfo)
		{
			try
			{
				var sortedPrefixes = GetSortedPatchMethods(original, patchInfo.prefixes, false);
				var sortedPostfixes = GetSortedPatchMethods(original, patchInfo.postfixes, false);
				var sortedTranspilers = GetSortedPatchMethods(original, patchInfo.transpilers, false);
				var sortedFinalizers = GetSortedPatchMethods(original, patchInfo.finalizers, false);

				var patcher = new MethodPatcher(original, null, sortedPrefixes, sortedPostfixes, sortedTranspilers, sortedFinalizers, false);
				var replacement = patcher.CreateReplacement(out var finalInstructions);
				if (replacement is null) throw new MissingMethodException($"Cannot create replacement for {original.FullDescription()}");

				Memory.DetourCompiledMethod(codeStart, replacement);
			}
			catch
			{
			}
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
				transpilers.AddRange(GetSortedPatchMethods(original, info.Transpilers.ToArray(), debug));
			}
			if (postTranspiler is object) transpilers.Add(postTranspiler);

			var empty = new List<MethodInfo>();
			var patcher = new MethodPatcher(standin.method, original, empty, empty, transpilers, empty, debug);
			var replacement = patcher.CreateReplacement(out var finalInstructions);
			if (replacement is null) throw new MissingMethodException($"Cannot create replacement for {standin.method.FullDescription()}");

			try
			{
				var errorString = Memory.DetourMethod(standin.method, replacement);
				if (errorString is object)
					throw new FormatException($"Method {standin.method.FullDescription()} cannot be patched. Reason: {errorString}");
			}
			catch (Exception ex)
			{
				throw HarmonyException.Create(ex, finalInstructions);
			}

			PatchTools.RememberObject(standin.method, replacement);
			return replacement;
		}
	}
}
