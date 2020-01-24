using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace HarmonyLib
{
	static class PatchProcessorExtensions
	{
		/// <summary>Creates an empty patch processor</summary>
		/// <param name="instance">The Harmony instance</param>
		/// <param name="original">An optional original method</param>
		///
		public static PatchProcessor CreateProcessor(this Harmony instance, MethodBase original)
		{
			return new PatchProcessor(instance, original);
		}
	}

	/// <summary>A PatchProcessor handles patches on a method/constructor</summary>
	public class PatchProcessor
	{
		readonly Harmony instance;
		readonly MethodBase original;

		HarmonyMethod prefix;
		HarmonyMethod postfix;
		HarmonyMethod transpiler;
		HarmonyMethod finalizer;

		internal static readonly object locker = new object();

		/// <summary>Creates an empty patch processor</summary>
		/// <param name="instance">The Harmony instance</param>
		/// <param name="original">An optional original method</param>
		///
		public PatchProcessor(Harmony instance, MethodBase original)
		{
			this.instance = instance;
			this.original = original;
		}

		/// <summary>Add a prefix</summary>
		/// <param name="prefix">The prefix.</param>
		///
		public PatchProcessor AddPrefix(HarmonyMethod prefix)
		{
			this.prefix = prefix;
			return this;
		}

		/// <summary>Add a prefix</summary>
		/// <param name="fixMethod">The method.</param>
		///
		public PatchProcessor AddPrefix(MethodInfo fixMethod)
		{
			prefix = new HarmonyMethod(fixMethod);
			return this;
		}

		/// <summary>Add a postfix</summary>
		/// <param name="postfix">The postfix.</param>
		///
		public PatchProcessor AddPostfix(HarmonyMethod postfix)
		{
			this.postfix = postfix;
			return this;
		}

		/// <summary>Add a postfix</summary>
		/// <param name="fixMethod">The method.</param>
		///
		public PatchProcessor AddPostfix(MethodInfo fixMethod)
		{
			postfix = new HarmonyMethod(fixMethod);
			return this;
		}

		/// <summary>Add a transpiler</summary>
		/// <param name="transpiler">The transpiler.</param>
		///
		public PatchProcessor AddTranspiler(HarmonyMethod transpiler)
		{
			this.transpiler = transpiler;
			return this;
		}

		/// <summary>Add a transpiler</summary>
		/// <param name="fixMethod">The method.</param>
		///
		public PatchProcessor AddTranspiler(MethodInfo fixMethod)
		{
			transpiler = new HarmonyMethod(fixMethod);
			return this;
		}

		/// <summary>Add a finalizer</summary>
		/// <param name="finalizer">The finalizer.</param>
		///
		public PatchProcessor AddFinalizer(HarmonyMethod finalizer)
		{
			this.finalizer = finalizer;
			return this;
		}

		/// <summary>Add a finalizer</summary>
		/// <param name="fixMethod">The method.</param>
		///
		public PatchProcessor AddFinalizer(MethodInfo fixMethod)
		{
			finalizer = new HarmonyMethod(fixMethod);
			return this;
		}

		/// <summary>Gets all patched original methods in the appdomain</summary>
		/// <returns>An enumeration of patched original methods</returns>
		///
		public static IEnumerable<MethodBase> GetAllPatchedMethods()
		{
			lock (locker)
			{
				return HarmonySharedState.GetPatchedMethods();
			}
		}

		/// <summary>Applies the patch</summary>
		/// <returns>A list of all created dynamic methods</returns>
		///
		public DynamicMethod Patch()
		{
			if (original == null)
				throw new NullReferenceException($"Null method for {instance.Id}");

			if (original.IsDeclaredMember() == false)
			{
				var declaredMember = original.GetDeclaredMember();
				throw new ArgumentException($"You can only patch implemented methods/constructors. Path the declared method {declaredMember.FullDescription()} instead.");
			}

			lock (locker)
			{
				var patchInfo = HarmonySharedState.GetPatchInfo(original);
				if (patchInfo == null) patchInfo = new PatchInfo();

				PatchFunctions.AddPrefix(patchInfo, instance.Id, prefix);
				PatchFunctions.AddPostfix(patchInfo, instance.Id, postfix);
				PatchFunctions.AddTranspiler(patchInfo, instance.Id, transpiler);
				PatchFunctions.AddFinalizer(patchInfo, instance.Id, finalizer);
				var dynamicMethod = PatchFunctions.UpdateWrapper(original, patchInfo, instance.Id);

				HarmonySharedState.UpdatePatchInfo(original, patchInfo);
				return dynamicMethod;
			}
		}

		/// <summary>Unpatches patches of a given type and/or Harmony ID</summary>
		/// <param name="type">The patch type</param>
		/// <param name="harmonyID">Harmony ID or (*) for any</param>
		///
		public PatchProcessor Unpatch(HarmonyPatchType type, string harmonyID)
		{
			lock (locker)
			{
				var patchInfo = HarmonySharedState.GetPatchInfo(original);
				if (patchInfo == null) patchInfo = new PatchInfo();

				if (type == HarmonyPatchType.All || type == HarmonyPatchType.Prefix)
					PatchFunctions.RemovePrefix(patchInfo, harmonyID);
				if (type == HarmonyPatchType.All || type == HarmonyPatchType.Postfix)
					PatchFunctions.RemovePostfix(patchInfo, harmonyID);
				if (type == HarmonyPatchType.All || type == HarmonyPatchType.Transpiler)
					PatchFunctions.RemoveTranspiler(patchInfo, harmonyID);
				if (type == HarmonyPatchType.All || type == HarmonyPatchType.Finalizer)
					PatchFunctions.RemoveFinalizer(patchInfo, harmonyID);
				_ = PatchFunctions.UpdateWrapper(original, patchInfo, instance.Id);

				HarmonySharedState.UpdatePatchInfo(original, patchInfo);
				return this;
			}
		}

		/// <summary>Unpatches the given patch</summary>
		/// <param name="patch">The patch</param>
		///
		public PatchProcessor Unpatch(MethodInfo patch)
		{
			lock (locker)
			{
				var patchInfo = HarmonySharedState.GetPatchInfo(original);
				if (patchInfo == null) patchInfo = new PatchInfo();

				PatchFunctions.RemovePatch(patchInfo, patch);
				_ = PatchFunctions.UpdateWrapper(original, patchInfo, instance.Id);

				HarmonySharedState.UpdatePatchInfo(original, patchInfo);
				return this;
			}
		}

		/// <summary>Gets patch information</summary>
		/// <param name="method">The original method</param>
		/// <returns>The patch information</returns>
		///
		public static Patches GetPatchInfo(MethodBase method)
		{
			PatchInfo patchInfo;
			lock (locker) { patchInfo = HarmonySharedState.GetPatchInfo(method); }
			if (patchInfo == null) return null;
			return new Patches(patchInfo.prefixes, patchInfo.postfixes, patchInfo.transpilers, patchInfo.finalizers);
		}

		/// <summary>Gets Harmony version for all active Harmony instances</summary>
		/// <param name="currentVersion">[out] The current Harmony version</param>
		/// <returns>A dictionary containing assembly versions keyed by Harmony IDs</returns>
		///
		public static Dictionary<string, Version> VersionInfo(out Version currentVersion)
		{
			currentVersion = typeof(Harmony).Assembly.GetName().Version;
			var assemblies = new Dictionary<string, Assembly>();
			GetAllPatchedMethods().Do(method =>
			{
				PatchInfo info;
				lock (locker) { info = HarmonySharedState.GetPatchInfo(method); }
				info.prefixes.Do(fix => assemblies[fix.owner] = fix.patch.DeclaringType.Assembly);
				info.postfixes.Do(fix => assemblies[fix.owner] = fix.patch.DeclaringType.Assembly);
				info.transpilers.Do(fix => assemblies[fix.owner] = fix.patch.DeclaringType.Assembly);
				info.finalizers.Do(fix => assemblies[fix.owner] = fix.patch.DeclaringType.Assembly);
			});

			var result = new Dictionary<string, Version>();
			assemblies.Do(info =>
			{
				var assemblyName = info.Value.GetReferencedAssemblies().FirstOrDefault(a => a.FullName.StartsWith("0Harmony, Version", StringComparison.Ordinal));
				if (assemblyName != null)
					result[info.Key] = assemblyName.Version;
			});
			return result;
		}
	}
}