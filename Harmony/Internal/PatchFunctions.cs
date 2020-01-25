using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

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

			patchInfo.AddPrefix(info.method, owner, priority, before, after);
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

			patchInfo.AddPostfix(info.method, owner, priority, before, after);
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

			patchInfo.AddTranspiler(info.method, owner, priority, before, after);
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

			patchInfo.AddFinalizer(info.method, owner, priority, before, after);
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

		/// <summary>Gets all instructions from a method</summary>
		/// <param name="generator">The generator (for defining labels)</param>
		/// <param name="method">The original method</param>
		/// <returns>The instructions</returns>
		///
		internal static List<ILInstruction> GetInstructions(ILGenerator generator, MethodBase method)
		{
			return MethodBodyReader.GetInstructions(generator, method);
		}

		/// <summary>Gets sorted patch methods</summary>
		/// <param name="original">The original method</param>
		/// <param name="patches">Patches to sort</param>
		/// <returns>The sorted patch methods</returns>
		///
		internal static List<MethodInfo> GetSortedPatchMethods(MethodBase original, Patch[] patches)
		{
			return new PatchSorter(patches).Sort(original);
		}

		/// <summary>Creates new dynamic method with the latest patches and detours the original method</summary>
		/// <param name="original">The original method</param>
		/// <param name="patchInfo">Information describing the patches</param>
		/// <param name="instanceID">Harmony ID</param>
		/// <returns>The newly created dynamic method</returns>
		///
		internal static MethodInfo UpdateWrapper(MethodBase original, PatchInfo patchInfo, string instanceID)
		{
			var sortedPrefixes = GetSortedPatchMethods(original, patchInfo.prefixes);
			var sortedPostfixes = GetSortedPatchMethods(original, patchInfo.postfixes);
			var sortedTranspilers = GetSortedPatchMethods(original, patchInfo.transpilers);
			var sortedFinalizers = GetSortedPatchMethods(original, patchInfo.finalizers);

			var replacement = MethodPatcher.CreatePatchedMethod(original, null, instanceID, sortedPrefixes, sortedPostfixes, sortedTranspilers, sortedFinalizers);
			if (replacement == null) throw new MissingMethodException($"Cannot create dynamic replacement for {original.FullDescription()}");

			Memory.DetourMethodAndPersist(original, replacement);
			return replacement;
		}

		internal static MethodInfo ReversePatch(HarmonyMethod standin, MethodBase original, Harmony instance, MethodInfo postTranspiler)
		{
			if (standin == null)
				throw new ArgumentNullException(nameof(standin));
			if (standin.method == null)
				throw new ArgumentNullException($"{nameof(standin)}.{nameof(standin.method)}");

			var processor = instance.CreateProcessor(standin.method);
			if (standin.reversePatchType == HarmonyReversePatchType.Snapshot)
			{
				var info = Harmony.GetPatchInfo(original);
				foreach (var tr in info.Transpilers)
				{
					var transpiler = new HarmonyMethod(tr.GetMethod(original))
					{
						before = tr.before,
						after = tr.after,

						priority = tr.priority
					};
					_ = processor.AddTranspiler(transpiler);
				}
			}
			if (postTranspiler != null)
			{
				var transpiler = new HarmonyMethod(postTranspiler) { priority = int.MinValue };
				_ = processor.AddTranspiler(transpiler);
			}
			return processor.Patch();
		}
	}
}