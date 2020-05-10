using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace HarmonyLib
{
	/// <summary>Patch function helpers</summary>
	internal static class PatchFunctions
	{
		/// <summary>Adds a prefix</summary>
		/// <param name="patchInfo">The patch info</param>
		/// <param name="owner">The owner (Harmony ID)</param>
		/// <param name="info">The annotation info</param>
		///
		internal static void AddPrefix(PatchInfo patchInfo, string owner, HarmonyMethod info)
		{
			if (info == null || info.method == null) return;

			var priority = info.priority == -1 ? Priority.Normal : info.priority;
			var before = info.before ?? new string[0];
			var after = info.after ?? new string[0];
			var debug = info.debug ?? false;

			patchInfo.AddPrefix(info.method, owner, priority, before, after, debug);
		}

		/// <summary>Removes a prefix</summary>
		/// <param name="patchInfo">The patch info</param>
		/// <param name="owner">The owner (Harmony ID)</param>
		///
		internal static void RemovePrefix(PatchInfo patchInfo, string owner)
		{
			patchInfo.RemovePrefix(owner);
		}

		/// <summary>Adds a postfix</summary>
		/// <param name="patchInfo">The patch info</param>
		/// <param name="owner">The owner (Harmony ID)</param>
		/// <param name="info">The annotation info</param>
		///
		internal static void AddPostfix(PatchInfo patchInfo, string owner, HarmonyMethod info)
		{
			if (info == null || info.method == null) return;

			var priority = info.priority == -1 ? Priority.Normal : info.priority;
			var before = info.before ?? new string[0];
			var after = info.after ?? new string[0];
			var debug = info.debug ?? false;

			patchInfo.AddPostfix(info.method, owner, priority, before, after, debug);
		}

		/// <summary>Removes a postfix</summary>
		/// <param name="patchInfo">The patch info</param>
		/// <param name="owner">The owner (Harmony ID)</param>
		///
		internal static void RemovePostfix(PatchInfo patchInfo, string owner)
		{
			patchInfo.RemovePostfix(owner);
		}

		/// <summary>Adds a transpiler</summary>
		/// <param name="patchInfo">The patch info</param>
		/// <param name="owner">The owner (Harmony ID)</param>
		/// <param name="info">The annotation info</param>
		///
		internal static void AddTranspiler(PatchInfo patchInfo, string owner, HarmonyMethod info)
		{
			if (info == null || info.method == null) return;

			var priority = info.priority == -1 ? Priority.Normal : info.priority;
			var before = info.before ?? new string[0];
			var after = info.after ?? new string[0];
			var debug = info.debug ?? false;

			patchInfo.AddTranspiler(info.method, owner, priority, before, after, debug);
		}

		/// <summary>Removes a transpiler</summary>
		/// <param name="patchInfo">The patch info</param>
		/// <param name="owner">The owner (Harmony ID)</param>
		///
		internal static void RemoveTranspiler(PatchInfo patchInfo, string owner)
		{
			patchInfo.RemoveTranspiler(owner);
		}

		/// <summary>Adds a finalizer</summary>
		/// <param name="patchInfo">The patch info</param>
		/// <param name="owner">The owner (Harmony ID)</param>
		/// <param name="info">The annotation info</param>
		///
		internal static void AddFinalizer(PatchInfo patchInfo, string owner, HarmonyMethod info)
		{
			if (info == null || info.method == null) return;

			var priority = info.priority == -1 ? Priority.Normal : info.priority;
			var before = info.before ?? new string[0];
			var after = info.after ?? new string[0];
			var debug = info.debug ?? false;

			patchInfo.AddFinalizer(info.method, owner, priority, before, after, debug);
		}

		/// <summary>Removes a finalizer</summary>
		/// <param name="patchInfo">The patch info</param>
		/// <param name="owner">The owner (Harmony ID)</param>
		///
		internal static void RemoveFinalizer(PatchInfo patchInfo, string owner)
		{
			patchInfo.RemoveFinalizer(owner);
		}

		/// <summary>Removes a patch method</summary>
		/// <param name="patchInfo">The patch info</param>
		/// <param name="patch">The patch method</param>
		///
		internal static void RemovePatch(PatchInfo patchInfo, MethodInfo patch)
		{
			patchInfo.RemovePatch(patch);
		}

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
			if (replacement == null) throw new MissingMethodException($"Cannot create replacement for {original.FullDescription()}");

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

		internal static MethodInfo ReversePatch(HarmonyMethod standin, MethodBase original, MethodInfo postTranspiler)
		{
			if (standin == null)
				throw new ArgumentNullException(nameof(standin));
			if (standin.method == null)
				throw new ArgumentNullException($"{nameof(standin)}.{nameof(standin.method)}");

			var debug = (standin.debug ?? false) || Harmony.DEBUG;

			var transpilers = new List<MethodInfo>();
			if (standin.reversePatchType == HarmonyReversePatchType.Snapshot)
			{
				var info = Harmony.GetPatchInfo(original);
				transpilers.AddRange(GetSortedPatchMethods(original, info.Transpilers.ToArray(), debug));
			}
			if (postTranspiler != null) transpilers.Add(postTranspiler);

			var empty = new List<MethodInfo>();
			var patcher = new MethodPatcher(standin.method, original, empty, empty, transpilers, empty, debug);
			var replacement = patcher.CreateReplacement(out var finalInstructions);
			if (replacement == null) throw new MissingMethodException($"Cannot create replacement for {standin.method.FullDescription()}");

			try
			{
				var errorString = Memory.DetourMethod(standin.method, replacement);
				if (errorString != null)
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