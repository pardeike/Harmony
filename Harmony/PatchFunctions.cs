using Harmony.ILCopying;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Harmony
{
	public static class PatchFunctions
	{
		public static PatchInfo CreateNewPatchInfo()
		{
			var patchInfo = new PatchInfo();
			patchInfo.prefixes = new Patch[0];
			patchInfo.postfixes = new Patch[0];
			patchInfo.modifiers = new Modifier[0];
			return patchInfo;
		}

		public static void AddPrefix(PatchInfo patchInfo, string owner, HarmonyMethod info)
		{
			if (info == null || info.method == null) return;

			var priority = info.prioritiy == -1 ? Priority.Normal : info.prioritiy;
			var before = info.before ?? new string[0];
			var after = info.after ?? new string[0];

			var patch = new Patch(patchInfo.prefixes.Length, owner, info.method, priority, before, after);
			patchInfo.prefixes = patchInfo.prefixes.AddToArray(patch);
		}

		public static void AddPostfix(PatchInfo patchInfo, string owner, HarmonyMethod info)
		{
			if (info == null || info.method == null) return;

			var priority = info.prioritiy == -1 ? Priority.Normal : info.prioritiy;
			var before = info.before ?? new string[0];
			var after = info.after ?? new string[0];

			var patch = new Patch(patchInfo.postfixes.Length, owner, info.method, priority, before, after);
			patchInfo.postfixes = patchInfo.postfixes.AddToArray(patch);
		}

		public static void AddModifier(PatchInfo patchInfo, string owner, HarmonyModifier info)
		{
			if (info == null || info.search == null || info.replace == null) return;

			var priority = info.prioritiy == -1 ? Priority.Normal : info.prioritiy;
			var before = info.before ?? new string[0];
			var after = info.after ?? new string[0];

			var modifier = new Modifier(patchInfo.modifiers.Length, owner, info.search, info.replace, priority, before, after);
			patchInfo.modifiers = patchInfo.modifiers.AddToArray(modifier);
		}

		public static List<MethodInfo> GetSortedPatchMethods(Patch[] patches)
		{
			return patches
				.ToList()
				.Where(p => p.patch != null)
				.OrderBy(p => p)
				.Select(p => p.patch)
				.ToList();
		}

		public static void UpdateWrapper(MethodBase original, PatchInfo patchInfo)
		{
			var sortedPrefixes = GetSortedPatchMethods(patchInfo.prefixes);
			var sortedPostfixes = GetSortedPatchMethods(patchInfo.postfixes);

			var modifiers = new List<ILCode[]>(); // TODO hook up modifiers to our API
			var replacement = MethodPatcher.CreatePatchedMethod(original, sortedPostfixes, sortedPostfixes, modifiers);
			if (replacement == null) throw new MissingMethodException("Cannot create dynamic replacement for " + original);
			PatchTools.KeepAliveForever(replacement);

			var originalCodeStart = Memory.GetMethodStart(original);
			var patchCodeStart = Memory.GetMethodStart(replacement);
			Memory.WriteJump(originalCodeStart, patchCodeStart);
		}
	}
}