using Harmony.ILCopying;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Harmony
{
	public static class PatchFunctions
	{
		public static void AddPrefix(PatchInfo patchInfo, string owner, HarmonyMethod info)
		{
			if (info == null || info.method == null) return;

			var priority = info.prioritiy == -1 ? Priority.Normal : info.prioritiy;
			var before = info.before ?? new string[0];
			var after = info.after ?? new string[0];

			var patch = new Patch(info.method, patchInfo.prefixes.Count() + 1, owner, priority, before, after);
			patchInfo.prefixes.Add(patch);
		}

		public static void AddPostfix(PatchInfo patchInfo, string owner, HarmonyMethod info)
		{
			if (info == null || info.method == null) return;

			var priority = info.prioritiy == -1 ? Priority.Normal : info.prioritiy;
			var before = info.before ?? new string[0];
			var after = info.after ?? new string[0];

			var patch = new Patch(info.method, patchInfo.postfixes.Count() + 1, owner, priority, before, after);
			patchInfo.postfixes.Add(patch);
		}

		public static void AddInfix(PatchInfo patchInfo, string owner, HarmonyProcessor infix)
		{
			if (infix == null) return;

			var priority = infix.priority == -1 ? Priority.Normal : infix.priority;
			var before = infix.before ?? new string[0];
			var after = infix.after ?? new string[0];

			infix.processors.ForEach(processor =>
			{
				var p = new Processor(processor, patchInfo.processors.Count() + 1, owner, priority, before, after);
				patchInfo.processors.Add(p);
			});
		}

		public static List<MethodInfo> GetSortedPatchMethods(List<Patch> patches)
		{
			return patches
				.Where(p => p.patch != null)
				.OrderBy(p => p)
				.Select(p => p.patch)
				.ToList();
		}

		public static List<IILProcessor> GetSortedProcessors(List<Processor> processors)
		{
			return processors.OrderBy(p => p).Select(p => p.processor).ToList();
		}

		public static void UpdateWrapper(MethodBase original, PatchInfo patchInfo)
		{
			var sortedPrefixes = GetSortedPatchMethods(patchInfo.prefixes);
			var sortedPostfixes = GetSortedPatchMethods(patchInfo.postfixes);
			var sortedProcessors = GetSortedProcessors(patchInfo.processors);

			var replacement = MethodPatcher.CreatePatchedMethod(original, sortedPostfixes, sortedPostfixes, sortedProcessors);
			if (replacement == null) throw new MissingMethodException("Cannot create dynamic replacement for " + original);
			PatchTools.KeepAliveForever(replacement);

			var originalCodeStart = Memory.GetMethodStart(original);
			var patchCodeStart = Memory.GetMethodStart(replacement);
			Memory.WriteJump(originalCodeStart, patchCodeStart);
		}
	}
}