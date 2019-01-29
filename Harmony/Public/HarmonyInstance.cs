using Harmony.Tools;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace Harmony
{
	/// <summary>A Harmony instance</summary>
	public class HarmonyInstance
	{
		readonly string id;

		/// <summary>The unique identifier</summary>
		public string Id => id;

		/// <summary>Set to true before instantiating Harmony to debug Harmony</summary>
		public static bool DEBUG = false;

		/// <summary>Set to false before instantiating Harmony to prevent Harmony from patching other older instances of itself</summary>
		public static bool SELF_PATCHING = true;

		static bool selfPatchingDone = false;

		HarmonyInstance(string id)
		{
			if (DEBUG)
			{
				var assembly = typeof(HarmonyInstance).Assembly;
				var version = assembly.GetName().Version;
				var location = assembly.Location;
				if (location == null || location == "") location = new Uri(assembly.CodeBase).LocalPath;
				FileLog.Log("### Harmony id=" + id + ", version=" + version + ", location=" + location);
				var callingMethod = GetOutsideCaller();
				var callingAssembly = callingMethod.DeclaringType.Assembly;
				location = callingAssembly.Location;
				if (location == null || location == "") location = new Uri(callingAssembly.CodeBase).LocalPath;
				FileLog.Log("### Started from " + callingMethod.FullDescription() + ", location " + location);
				FileLog.Log("### At " + DateTime.Now.ToString("yyyy-MM-dd hh.mm.ss"));
			}

			this.id = id;

			if (!selfPatchingDone)
			{
				selfPatchingDone = true;
				if (SELF_PATCHING)
					SelfPatching.PatchOldHarmonyMethods();
			}
		}

		/// <summary>Creates a new Harmony instance</summary>
		/// <param name="id">A unique identifier</param>
		/// <returns>A Harmony instance</returns>
		///
		public static HarmonyInstance Create(string id)
		{
			if (id == null) throw new Exception("id cannot be null");
			return new HarmonyInstance(id);
		}

		MethodBase GetOutsideCaller()
		{
			var trace = new StackTrace(true);
			foreach (var frame in trace.GetFrames())
			{
				var method = frame.GetMethod();
				if (method.DeclaringType.Namespace != typeof(HarmonyInstance).Namespace)
					return method;
			}
			throw new Exception("Unexpected end of stack trace");
		}

		/// <summary>Searches current assembly for Harmony annotations and uses them to create patches</summary>
		/// 
		public void PatchAll()
		{
			var method = new StackTrace().GetFrame(1).GetMethod();
			var assembly = method.ReflectedType.Assembly;
			PatchAll(assembly);
		}

		/// <summary>Create a patch processor from an annotated class</summary>
		/// <param name="type">The class</param>
		/// 
		public PatchProcessor ProcessorForAnnotatedClass(Type type)
		{
			var parentMethodInfos = HarmonyMethodExtensions.GetFromType(type);
			if (parentMethodInfos != null && parentMethodInfos.Count() > 0)
			{
				var info = HarmonyMethod.Merge(parentMethodInfos);
				return new PatchProcessor(this, type, info);
			}
			return null;
		}

		/// <summary>Searches an assembly for Harmony annotations and uses them to create patches</summary>
		/// <param name="assembly">The assembly</param>
		/// 
		public void PatchAll(Assembly assembly)
		{
			assembly.GetTypes().Do(type => ProcessorForAnnotatedClass(type)?.Patch());
		}

		/// <summary>Creates patches by manually specifying the methods</summary>
		/// <param name="original">The original method</param>
		/// <param name="prefix">An optional prefix method wrapped in a HarmonyMethod object</param>
		/// <param name="postfix">An optional postfix method wrapped in a HarmonyMethod object</param>
		/// <param name="transpiler">An optional transpiler method wrapped in a HarmonyMethod object</param>
		/// <returns>The dynamic method that was created to patch the original method</returns>
		///
		public DynamicMethod Patch(MethodBase original, HarmonyMethod prefix = null, HarmonyMethod postfix = null, HarmonyMethod transpiler = null)
		{
			var processor = new PatchProcessor(this, new List<MethodBase> { original }, prefix, postfix, transpiler);
			return processor.Patch().FirstOrDefault();
		}

		/// <summary>Unpatches methods</summary>
		/// <param name="harmonyID">The optional Harmony ID to restrict unpatching to a specific instance</param>
		///
		public void UnpatchAll(string harmonyID = null)
		{
			bool IDCheck(Patch patchInfo) => harmonyID == null || patchInfo.owner == harmonyID;

			var originals = GetPatchedMethods().ToList();
			foreach (var original in originals)
			{
				var info = GetPatchInfo(original);
				info.Prefixes.DoIf(IDCheck, patchInfo => Unpatch(original, patchInfo.patch));
				info.Postfixes.DoIf(IDCheck, patchInfo => Unpatch(original, patchInfo.patch));
				info.Transpilers.DoIf(IDCheck, patchInfo => Unpatch(original, patchInfo.patch));
			}
		}

		/// <summary>Unpatches a method</summary>
		/// <param name="original">The original method</param>
		/// <param name="type">The patch type</param>
		/// <param name="harmonyID">The optional Harmony ID to restrict unpatching to a specific instance</param>
		///
		public void Unpatch(MethodBase original, HarmonyPatchType type, string harmonyID = null)
		{
			var processor = new PatchProcessor(this, new List<MethodBase> { original });
			processor.Unpatch(type, harmonyID);
		}

		/// <summary>Unpatches a method</summary>
		/// <param name="original">The original method</param>
		/// <param name="patch">The patch method to remove</param>
		///
		public void Unpatch(MethodBase original, MethodInfo patch)
		{
			var processor = new PatchProcessor(this, new List<MethodBase> { original });
			processor.Unpatch(patch);
		}

		/// <summary>Test for patches from a specific Harmony ID</summary>
		/// <param name="harmonyID">The Harmony ID</param>
		/// <returns>True if patches for this ID exist</returns>
		///
		public bool HasAnyPatches(string harmonyID)
		{
			return GetPatchedMethods()
				.Select(original => GetPatchInfo(original))
				.Any(info => info.Owners.Contains(harmonyID));
		}

		/// <summary>Gets patch information for a given original method</summary>
		/// <param name="method">The original method</param>
		/// <returns>The patch information</returns>
		///
		public Patches GetPatchInfo(MethodBase method)
		{
			return PatchProcessor.GetPatchInfo(method);
		}

		/// <summary>Gets all patched methods in the appdomain</summary>
		/// <returns>An enumeration of original methods</returns>
		///
		public static IEnumerable<MethodBase> GetAllPatchedMethods()
		{
			return HarmonySharedState.GetPatchedMethods();
		}

		/// <summary>Gets the methods this instance has patched</summary>
		/// <returns>An enumeration of original methods</returns>
		///
		public IEnumerable<MethodBase> GetPatchedMethods()
		{
			return HarmonySharedState.GetPatchedMethods()
				.Where(original => GetPatchInfo(original).Owners.Contains(Id));
		}

		/// <summary>Gets current version information</summary>
		/// <param name="currentVersion">[out] The current Harmony version</param>
		/// <returns>A dictionary containing assembly versions keyed by Harmony version</returns>
		///
		public Dictionary<string, Version> VersionInfo(out Version currentVersion)
		{
			currentVersion = typeof(HarmonyInstance).Assembly.GetName().Version;
			var assemblies = new Dictionary<string, Assembly>();
			GetPatchedMethods().Do(method =>
			{
				var info = HarmonySharedState.GetPatchInfo(method);
				info.prefixes.Do(fix => assemblies[fix.owner] = fix.patch.DeclaringType.Assembly);
				info.postfixes.Do(fix => assemblies[fix.owner] = fix.patch.DeclaringType.Assembly);
				info.transpilers.Do(fix => assemblies[fix.owner] = fix.patch.DeclaringType.Assembly);
			});

			var result = new Dictionary<string, Version>();
			assemblies.Do(info =>
			{
				var assemblyName = info.Value.GetReferencedAssemblies().FirstOrDefault(a => a.FullName.StartsWith("0Harmony, Version"));
				if (assemblyName != null)
					result[info.Key] = assemblyName.Version;
			});
			return result;
		}
	}
}
