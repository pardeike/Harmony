using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Harmony
{
	public static class PatchFunctions
	{
		public static PatchInfo GetPatchInfo(MethodInfo original)
		{
			var bytes = HookInjector.Create(original).GetPayload();
			if (bytes == null) return null;
			return PatchInfoSerialization.Deserialize(bytes);
		}

		public static PatchInfo CreateNewPatchInfo()
		{
			var patchInfo = new PatchInfo();
			patchInfo.prefixes = new Patch[0];
			patchInfo.postfixes = new Patch[0];
			return patchInfo;
		}

		public static PatchInfo AddPrefix(PatchInfo patchInfo, string owner, HarmonyMethod info)
		{
			if (info == null || info.method == null) return patchInfo;

			var priority = info.prioritiy == -1 ? Priority.Normal : info.prioritiy;
			var before = info.before ?? new string[0];
			var after = info.after ?? new string[0];

			var patch = new Patch(patchInfo.prefixes.Length, owner, info.method, priority, before, after);
			patchInfo.prefixes = patchInfo.prefixes.AddToArray(patch);
			return patchInfo;
		}

		public static PatchInfo AddPostfix(PatchInfo patchInfo, string owner, HarmonyMethod info)
		{
			if (info == null || info.method == null) return patchInfo;

			var priority = info.prioritiy == -1 ? Priority.Normal : info.prioritiy;
			var before = info.before ?? new string[0];
			var after = info.after ?? new string[0];

			var patch = new Patch(patchInfo.postfixes.Length, owner, info.method, priority, before, after);
			patchInfo.postfixes = patchInfo.postfixes.AddToArray(patch);
			return patchInfo;
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

		public static void UpdateWrapper(MethodInfo original, PatchInfo patchInfo, bool isNew)
		{
			var sortedPrefixes = GetSortedPatchMethods(patchInfo.prefixes);
			var sortedPostfixes = GetSortedPatchMethods(patchInfo.postfixes);

			var copy = PatchTools.CreateMethodCopy(original);
			if (copy == null) throw new MissingMethodException("Cannot create copy of " + original);
			var copyDelegate = PatchTools.PrepareDynamicMethod(original, copy);

			var wrapper = PatchTools.CreatePatchWrapper(original, copyDelegate, sortedPrefixes, sortedPostfixes);
			var wrapperDelegate = PatchTools.PrepareDynamicMethod(original, wrapper);

			var injector = HookInjector.Create(original, wrapperDelegate);
			injector.Detour(patchInfo.Serialize(), isNew);
		}
	}
}