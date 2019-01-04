using Harmony.ILCopying;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace Harmony
{
	/// <summary>Patch function helpers</summary>
	public static class PatchFunctions
	{
		/// <summary>Adds a prefix</summary>
		/// <param name="patchInfo">The patch info</param>
		/// <param name="owner">The owner (Harmony ID)</param>
		/// <param name="info">The annotation info</param>
		///
		public static void AddPrefix(PatchInfo patchInfo, string owner, HarmonyMethod info)
		{
			if (info == null || info.method == null) return;

			var priority = info.priority == -1 ? Priority.Normal : info.priority;
			var before = info.before ?? new string[0];
			var after = info.after ?? new string[0];

			patchInfo.AddPrefix(info.method, owner, priority, before, after);
		}

		/// <summary>Removes a prefix</summary>
		/// <param name="patchInfo">The patch info</param>
		/// <param name="owner">The owner (Harmony ID)</param>
		///
		public static void RemovePrefix(PatchInfo patchInfo, string owner)
		{
			patchInfo.RemovePrefix(owner);
		}

		/// <summary>Adds a postfix</summary>
		/// <param name="patchInfo">The patch info</param>
		/// <param name="owner">The owner (Harmony ID)</param>
		/// <param name="info">The annotation info</param>
		///
		public static void AddPostfix(PatchInfo patchInfo, string owner, HarmonyMethod info)
		{
			if (info == null || info.method == null) return;

			var priority = info.priority == -1 ? Priority.Normal : info.priority;
			var before = info.before ?? new string[0];
			var after = info.after ?? new string[0];

			patchInfo.AddPostfix(info.method, owner, priority, before, after);
		}

		/// <summary>Removes a postfix</summary>
		/// <param name="patchInfo">The patch info</param>
		/// <param name="owner">The owner (Harmony ID)</param>
		///
		public static void RemovePostfix(PatchInfo patchInfo, string owner)
		{
			patchInfo.RemovePostfix(owner);
		}

		/// <summary>Adds a transpiler</summary>
		/// <param name="patchInfo">The patch info</param>
		/// <param name="owner">The owner (Harmony ID)</param>
		/// <param name="info">The annotation info</param>
		///
		public static void AddTranspiler(PatchInfo patchInfo, string owner, HarmonyMethod info)
		{
			if (info == null || info.method == null) return;

			var priority = info.priority == -1 ? Priority.Normal : info.priority;
			var before = info.before ?? new string[0];
			var after = info.after ?? new string[0];

			patchInfo.AddTranspiler(info.method, owner, priority, before, after);
		}

		/// <summary>Removes a transpiler</summary>
		/// <param name="patchInfo">The patch info</param>
		/// <param name="owner">The owner (Harmony ID)</param>
		///
		public static void RemoveTranspiler(PatchInfo patchInfo, string owner)
		{
			patchInfo.RemoveTranspiler(owner);
		}

		/// <summary>Removes a patch method</summary>
		/// <param name="patchInfo">The patch info</param>
		/// <param name="patch">The patch method</param>
		///
		public static void RemovePatch(PatchInfo patchInfo, MethodInfo patch)
		{
			patchInfo.RemovePatch(patch);
		}

		/// <summary>Gets all instructions from a method</summary>
		/// <param name="generator">The generator (for defining labels)</param>
		/// <param name="method">The original method</param>
		/// <returns>The instructions</returns>
		///
		public static List<ILInstruction> GetInstructions(ILGenerator generator, MethodBase method)
		{
			return MethodBodyReader.GetInstructions(generator, method);
		}

		/// <summary>Gets sorted patch methods</summary>
		/// <param name="original">The original method</param>
		/// <param name="patches">Patches to sort</param>
		/// <returns>The sorted patch methods</returns>
		///
		public static List<MethodInfo> GetSortedPatchMethods(MethodBase original, Patch[] patches)
		{
			return patches
				.Where(p => p.patch != null)
				.OrderBy(p => p)
				.Select(p => p.GetMethod(original))
				.ToList();
		}

		/// <summary>Creates new dynamic method with the latest patches and detours the original method</summary>
		/// <param name="original">The original method</param>
		/// <param name="patchInfo">Information describing the patches</param>
		/// <param name="instanceID">Harmony ID</param>
		/// <returns>The newly created dynamic method</returns>
		///
		public static DynamicMethod UpdateWrapper(MethodBase original, PatchInfo patchInfo, string instanceID)
		{
			var sortedPrefixes = GetSortedPatchMethods(original, patchInfo.prefixes);
			var sortedPostfixes = GetSortedPatchMethods(original, patchInfo.postfixes);
			var sortedTranspilers = GetSortedPatchMethods(original, patchInfo.transpilers);

			var replacement = MethodPatcher.CreatePatchedMethod(original, instanceID, sortedPrefixes, sortedPostfixes, sortedTranspilers);
			if (replacement == null) throw new MissingMethodException("Cannot create dynamic replacement for " + original.FullDescription());

			var errorString = Memory.DetourMethod(original, replacement);
			if (errorString != null)
				throw new FormatException("Method " + original.FullDescription() + " cannot be patched. Reason: " + errorString);

			PatchTools.RememberObject(original, replacement); // no gc for new value + release old value to gc

			return replacement;
		}
	}
}