using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace HarmonyLib
{
	/// <summary>The Harmony instance is the main entry to Harmony. After creating one with an unique identifier, it is used to patch and query the current application domain</summary>
	public class Harmony
	{
		/// <summary>The unique identifier</summary>
		public string Id { get; private set; }

		/// <summary>Set to true before instantiating Harmony to debug Harmony</summary>
		public static bool DEBUG;

		/// <summary>Set to false before instantiating Harmony to prevent Harmony from patching other older instances of itself</summary>
		public static bool SELF_PATCHING = true;

		/// <summary>Creates a new Harmony instance</summary>
		/// <param name="id">A unique identifier</param>
		/// <returns>A Harmony instance</returns>
		///
		public Harmony(string id)
		{
			if (string.IsNullOrEmpty(id)) throw new ArgumentException($"{nameof(id)} cannot be null or empty");

			if (DEBUG)
			{
				var assembly = typeof(Harmony).Assembly;
				var version = assembly.GetName().Version;
				var location = assembly.Location;
				if (string.IsNullOrEmpty(location)) location = new Uri(assembly.CodeBase).LocalPath;
				FileLog.Log($"### Harmony id={id}, version={version}, location={location}");
				var callingMethod = AccessTools.GetOutsideCaller();
				if (callingMethod.DeclaringType != null)
				{
					var callingAssembly = callingMethod.DeclaringType.Assembly;
					location = callingAssembly.Location;
					if (string.IsNullOrEmpty(location)) location = new Uri(callingAssembly.CodeBase).LocalPath;
					FileLog.Log($"### Started from {callingMethod.FullDescription()}, location {location}");
					FileLog.Log($"### At {DateTime.Now.ToString("yyyy-MM-dd hh.mm.ss")}");
				}
			}

			Id = id;
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
		public PatchClassProcessor ProcessorForAnnotatedClass(Type type)
		{
			return new PatchClassProcessor(this, type);
		}

		/// <summary>Searches an assembly for Harmony annotations and uses them to create patches</summary>
		/// <param name="assembly">The assembly</param>
		/// 
		public void PatchAll(Assembly assembly)
		{
			assembly.GetTypes().Do(type => ProcessorForAnnotatedClass(type).Patch());
		}

		/// <summary>Creates patches by manually specifying the methods</summary>
		/// <param name="original">The original method/constructor</param>
		/// <param name="prefix">An optional prefix method wrapped in a HarmonyMethod object</param>
		/// <param name="postfix">An optional postfix method wrapped in a HarmonyMethod object</param>
		/// <param name="transpiler">An optional transpiler method wrapped in a HarmonyMethod object</param>
		/// <param name="finalizer">An optional finalizer method wrapped in a HarmonyMethod object</param>
		/// <returns>The dynamic method that was created to patch the original method</returns>
		///
		public MethodInfo Patch(MethodBase original, HarmonyMethod prefix = null, HarmonyMethod postfix = null, HarmonyMethod transpiler = null, HarmonyMethod finalizer = null)
		{
			var processor = this.CreateProcessor(original);
			_ = processor.AddPrefix(prefix);
			_ = processor.AddPostfix(postfix);
			_ = processor.AddTranspiler(transpiler);
			_ = processor.AddFinalizer(finalizer);
			return processor.Patch();
		}

		/// <summary>Patches a foreign method onto a stub method of yours and optionally applies transpilers during the process</summary>
		/// <param name="original">The original method/constructor you want to duplicate</param>
		/// <param name="standin">Your stub method that will become the original. Needs to have the correct signature (either original or whatever your transpilers generates)</param>
		/// <param name="transpiler">An optional transpiler that will be applied during the process</param>
		public MethodInfo ReversePatch(MethodBase original, HarmonyMethod standin, MethodInfo transpiler = null)
		{
			return PatchFunctions.ReversePatch(standin, original, this, transpiler);
		}

		/// <summary>Unpatches methods</summary>
		/// <param name="harmonyID">The optional Harmony ID to restrict unpatching to a specific instance</param>
		/// <remarks>This method could be static if it wasn't for the fact that unpatching creates a new replacement method that contains your harmony ID</remarks>
		///
		public void UnpatchAll(string harmonyID = null)
		{
			bool IDCheck(Patch patchInfo) => harmonyID == null || patchInfo.owner == harmonyID;

			var originals = GetAllPatchedMethods().ToList();
			foreach (var original in originals)
			{
				var info = GetPatchInfo(original);
				info.Prefixes.DoIf(IDCheck, patchInfo => Unpatch(original, patchInfo.patch));
				info.Postfixes.DoIf(IDCheck, patchInfo => Unpatch(original, patchInfo.patch));
				info.Transpilers.DoIf(IDCheck, patchInfo => Unpatch(original, patchInfo.patch));
				info.Finalizers.DoIf(IDCheck, patchInfo => Unpatch(original, patchInfo.patch));
			}
		}

		/// <summary>Unpatches a method</summary>
		/// <param name="original">The original method</param>
		/// <param name="type">The patch type</param>
		/// <param name="harmonyID">The optional Harmony ID to restrict unpatching to a specific instance</param>
		///
		public void Unpatch(MethodBase original, HarmonyPatchType type, string harmonyID = null)
		{
			var processor = this.CreateProcessor(original);
			_ = processor.Unpatch(type, harmonyID);
		}

		/// <summary>Unpatches a method</summary>
		/// <param name="original">The original method</param>
		/// <param name="patch">The patch method to remove</param>
		///
		public void Unpatch(MethodBase original, MethodInfo patch)
		{
			var processor = this.CreateProcessor(original);
			_ = processor.Unpatch(patch);
		}

		/// <summary>Test for patches from a specific Harmony ID</summary>
		/// <param name="harmonyID">The Harmony ID</param>
		/// <returns>True if patches for this ID exist</returns>
		///
		public static bool HasAnyPatches(string harmonyID)
		{
			return GetAllPatchedMethods()
				.Select(original => GetPatchInfo(original))
				.Any(info => info.Owners.Contains(harmonyID));
		}

		/// <summary>Gets patch information for a given original method</summary>
		/// <param name="method">The original method</param>
		/// <returns>The patch information</returns>
		///
		public static Patches GetPatchInfo(MethodBase method)
		{
			return PatchProcessor.GetPatchInfo(method);
		}

		/// <summary>Gets the methods this instance has patched</summary>
		/// <returns>An enumeration of original methods</returns>
		///
		public IEnumerable<MethodBase> GetPatchedMethods()
		{
			return GetAllPatchedMethods()
				.Where(original => GetPatchInfo(original).Owners.Contains(Id));
		}

		/// <summary>Gets all patched original methods in the appdomain</summary>
		/// <returns>An enumeration of patched original methods</returns>
		///
		public static IEnumerable<MethodBase> GetAllPatchedMethods()
		{
			return PatchProcessor.GetAllPatchedMethods();
		}

		/// <summary>Gets Harmony version for all active Harmony instances</summary>
		/// <param name="currentVersion">[out] The current Harmony version</param>
		/// <returns>A dictionary containing assembly versions keyed by Harmony IDs</returns>
		///
		public static Dictionary<string, Version> VersionInfo(out Version currentVersion)
		{
			return PatchProcessor.VersionInfo(out currentVersion);
		}
	}
}