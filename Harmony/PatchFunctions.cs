using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace Harmony
{
	public static class PatchFunctions
	{
		// this holds all our methods alive so they don't get garbage-collected
		static PatchStorage[] allPatchReferences = new PatchStorage[0];
		class PatchStorage
		{
			public DynamicMethod copy;
			public MethodInfo copyDelegate;
			public DynamicMethod wrapper;
			public MethodInfo wrapperDelegate;
		}

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

			var patch = new PatchStorage();

			patch.copy = PatchTools.CreateMethodCopy(original);
			if (patch.copy == null) throw new MissingMethodException("Cannot create copy of " + original);
			patch.copyDelegate = PatchTools.PrepareDynamicMethod(original, patch.copy);

			patch.wrapper = PatchTools.CreatePatchWrapper(original, patch.copyDelegate, sortedPrefixes, sortedPostfixes);
			patch.wrapperDelegate = PatchTools.PrepareDynamicMethod(original, patch.wrapper);

			allPatchReferences.Add(patch); // keep things alive and referenced

			var injector = HookInjector.Create(original, patch.wrapperDelegate);
			injector.Detour(patchInfo.Serialize(), isNew);
		}
	}
}