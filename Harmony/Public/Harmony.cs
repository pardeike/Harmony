using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace HarmonyLib
{
	/// <summary>The Harmony instance is the main entry to Harmony. After creating one with an unique identifier, it is used to patch and query the current application domain</summary>
	///
	public class Harmony
	{
		/// <summary>The unique identifier</summary>
		///
		public string Id { get; private set; }

		/// <summary>Set to true before instantiating Harmony to debug Harmony or use an environment variable to set HARMONY_DEBUG to '1' like this: cmd /C "set HARMONY_DEBUG=1 &amp;&amp; game.exe"</summary>
		/// <remarks>This is for full debugging. To debug only specific patches, use the <see cref="HarmonyDebug"/> attribute</remarks>
		///
		public static bool DEBUG;

		/// <summary>Creates a new Harmony instance</summary>
		/// <param name="id">A unique identifier (you choose your own)</param>
		/// <returns>A Harmony instance</returns>
		///
		public Harmony(string id)
		{
			if (string.IsNullOrEmpty(id)) throw new ArgumentException($"{nameof(id)} cannot be null or empty");

			try
			{
				var envDebug = Environment.GetEnvironmentVariable("HARMONY_DEBUG");
				if (envDebug is not null && envDebug.Length > 0)
				{
					envDebug = envDebug.Trim();
					DEBUG = envDebug == "1" || bool.Parse(envDebug);
				}
			}
			catch
			{
			}

			if (DEBUG)
			{
				var assembly = typeof(Harmony).Assembly;
				var version = assembly.GetName().Version;
				var location = assembly.Location;
				var environment = Environment.Version.ToString();
				var platform = Environment.OSVersion.Platform.ToString();
#if !NET5_0_OR_GREATER
				if (string.IsNullOrEmpty(location)) location = new Uri(assembly.CodeBase).LocalPath;
#endif
				FileLog.Log($"### Harmony id={id}, version={version}, location={location}, env/clr={environment}, platform={platform}");
				var callingMethod = AccessTools.GetOutsideCaller();
				if (callingMethod.DeclaringType is not null)
				{
					var callingAssembly = callingMethod.DeclaringType.Assembly;
					location = callingAssembly.Location;
#if !NET5_0_OR_GREATER
					if (string.IsNullOrEmpty(location)) location = new Uri(callingAssembly.CodeBase).LocalPath;
#endif
					FileLog.Log($"### Started from {callingMethod.FullDescription()}, location {location}");
					FileLog.Log($"### At {DateTime.Now:yyyy-MM-dd hh.mm.ss}");
				}
			}

			Id = id;

			// FOR TESTING: enable switch to building methods with CECIL
			// Switches.SetSwitchValue(Switches.DMDType, "cecil");
		}

		/// <summary>Searches the current assembly for Harmony annotations and uses them to create patches</summary>
		/// <remarks>This method can fail to use the correct assembly when being inlined. It calls StackTrace.GetFrame(1) which can point to the wrong method/assembly. If you are unsure or run into problems, use <code>PatchAll(Assembly.GetExecutingAssembly())</code> instead.</remarks>
		///
		public void PatchAll()
		{
			var method = new StackTrace().GetFrame(1).GetMethod();
			var assembly = method.ReflectedType.Assembly;
			PatchAll(assembly);
		}

		/// <summary>Creates a empty patch processor for an original method</summary>
		/// <param name="original">The original method/constructor</param>
		/// <returns>A new <see cref="PatchProcessor"/> instance</returns>
		///
		public PatchProcessor CreateProcessor(MethodBase original) => new(this, original);

		/// <summary>Creates a patch class processor from a class</summary>
		/// <param name="type">The class/type</param>
		/// <returns>A new <see cref="PatchClassProcessor"/> instance</returns>
		///
		public PatchClassProcessor CreateClassProcessor(Type type) => new(this, type);

		/// <summary>Creates a reverse patcher for one of your stub methods</summary>
		/// <param name="original">The original method/constructor</param>
		/// <param name="standin">The stand-in stub method as <see cref="HarmonyMethod"/></param>
		/// <returns>A new <see cref="ReversePatcher"/> instance</returns>
		///
		public ReversePatcher CreateReversePatcher(MethodBase original, HarmonyMethod standin) => new(this, original, standin);

		/// <summary>Searches an assembly for HarmonyPatch-annotated classes/structs and uses them to create patches</summary>
		/// <param name="assembly">The assembly</param>
		///
		public void PatchAll(Assembly assembly) => AccessTools.GetTypesFromAssembly(assembly).DoIf(type => type.HasHarmonyAttribute(), type => CreateClassProcessor(type).Patch());

		/// <summary>Searches an assembly for HarmonyPatch-annotated classes/structs without category annotations and uses them to create patches</summary>
		///
		public void PatchAllUncategorized()
		{
			var method = new StackTrace().GetFrame(1).GetMethod();
			var assembly = method.ReflectedType.Assembly;
			PatchAllUncategorized(assembly);
		}

		/// <summary>Searches an assembly for HarmonyPatch-annotated classes/structs without category annotations and uses them to create patches</summary>
		/// <param name="assembly">The assembly</param>
		///
		public void PatchAllUncategorized(Assembly assembly)
		{
			var patchClasses = AccessTools.GetTypesFromAssembly(assembly).Where(type => type.HasHarmonyAttribute()).Select(CreateClassProcessor).ToArray();
			patchClasses.DoIf(patchClass => string.IsNullOrEmpty(patchClass.Category), patchClass => patchClass.Patch());
		}

		/// <summary>Searches the current assembly for Harmony annotations with a specific category and uses them to create patches</summary>
		/// <param name="category">Name of patch category</param>
		///
		public void PatchCategory(string category)
		{
			var method = new StackTrace().GetFrame(1).GetMethod();
			var assembly = method.ReflectedType.Assembly;
			PatchCategory(assembly, category);
		}

		private static readonly ConditionalWeakTable<Assembly, Dictionary<string, List<Type>>> AssemblyCachedCategories = new();

		/// <summary>Searches an assembly for HarmonyPatch-annotated classes/structs with a specific category and uses them to create patches</summary>
		/// <param name="assembly">The assembly</param>
		/// <param name="category">Name of patch category</param>
		///
		public void PatchCategory(Assembly assembly, string category)
		{
			var categoryCache = AssemblyCachedCategories.GetValue(assembly, BuildCategoryCache);
			if (categoryCache.TryGetValue(category, out var toPatch))
			{
				toPatch.Do(type => CreateClassProcessor(type).Patch());
			}
		}

		private static Dictionary<string, List<Type>> BuildCategoryCache(Assembly assembly)
		{
			Dictionary<string, List<Type>> toBuild = [];
			foreach (var type in AccessTools.GetTypesFromAssembly(assembly))
			{
				var harmonyAttributes = HarmonyMethodExtensions.GetFromType(type);
				if (harmonyAttributes.Count == 0) continue;
				var containerAttributes = HarmonyMethod.Merge(harmonyAttributes);
				var category = containerAttributes.category;
				if (!string.IsNullOrEmpty(category))
				{
					if (!toBuild.TryGetValue(category, out var typeList))
					{
						typeList ??= [];
					}
					typeList.Add(type);
					toBuild[category] = typeList;
				}
			}
			return toBuild;
		}

		/// <summary>Creates patches by manually specifying the methods</summary>
		/// <param name="original">The original method/constructor</param>
		/// <param name="prefix">An optional prefix method wrapped in a <see cref="HarmonyMethod"/> object</param>
		/// <param name="postfix">An optional postfix method wrapped in a <see cref="HarmonyMethod"/> object</param>
		/// <param name="transpiler">An optional transpiler method wrapped in a <see cref="HarmonyMethod"/> object</param>
		/// <param name="finalizer">An optional finalizer method wrapped in a <see cref="HarmonyMethod"/> object</param>
		/// <returns>The replacement method that was created to patch the original method</returns>
		///
		public MethodInfo Patch(MethodBase original, HarmonyMethod prefix = null, HarmonyMethod postfix = null, HarmonyMethod transpiler = null, HarmonyMethod finalizer = null/*, HarmonyMethod infix = null*/)
		{
			var processor = CreateProcessor(original);
			_ = processor.AddPrefix(prefix);
			_ = processor.AddPostfix(postfix);
			_ = processor.AddTranspiler(transpiler);
			_ = processor.AddFinalizer(finalizer);
			//_ = processor.AddInfix(infix);
			return processor.Patch();
		}

		/// <summary>Patches a foreign method onto a stub method of yours and optionally applies transpilers during the process</summary>
		/// <param name="original">The original method/constructor you want to duplicate</param>
		/// <param name="standin">Your stub method as <see cref="HarmonyMethod"/> that will become the original. Needs to have the correct signature (either original or whatever your transpilers generates)</param>
		/// <param name="transpiler">An optional transpiler as method that will be applied during the process</param>
		/// <returns>The replacement method that was created to patch the stub method</returns>
		///
		public static MethodInfo ReversePatch(MethodBase original, HarmonyMethod standin, MethodInfo transpiler = null) => PatchFunctions.ReversePatch(standin, original, transpiler);

		/// <summary>Unpatches methods by patching them with zero patches. Fully unpatching is not supported. Be careful, unpatching is global</summary>
		/// <param name="harmonyID">The optional Harmony ID to restrict unpatching to a specific Harmony instance</param>
		/// <remarks>This method could be static if it wasn't for the fact that unpatching creates a new replacement method that contains your harmony ID</remarks>
		///
		public void UnpatchAll(string harmonyID = null)
		{
			bool IDCheck(Patch patchInfo) => harmonyID is null || patchInfo.owner == harmonyID;

			var originals = GetAllPatchedMethods().ToList(); // keep as is to avoid "Collection was modified"
			foreach (var original in originals)
			{
				var hasBody = original.HasMethodBody();
				var info = GetPatchInfo(original);
				if (hasBody)
				{
					info.Postfixes.DoIf(IDCheck, patchInfo => Unpatch(original, patchInfo.PatchMethod));
					info.Prefixes.DoIf(IDCheck, patchInfo => Unpatch(original, patchInfo.PatchMethod));
					info.InnerPostfixes.DoIf(IDCheck, patchInfo => Unpatch(original, patchInfo.PatchMethod));
					info.InnerPrefixes.DoIf(IDCheck, patchInfo => Unpatch(original, patchInfo.PatchMethod));
				}
				info.Transpilers.DoIf(IDCheck, patchInfo => Unpatch(original, patchInfo.PatchMethod));
				if (hasBody)
					info.Finalizers.DoIf(IDCheck, patchInfo => Unpatch(original, patchInfo.PatchMethod));
			}
		}

		/// <summary>Unpatches a method by patching it with zero patches. Fully unpatching is not supported. Be careful, unpatching is global</summary>
		/// <param name="original">The original method/constructor</param>
		/// <param name="type">The <see cref="HarmonyPatchType"/></param>
		/// <param name="harmonyID">The optional Harmony ID to restrict unpatching to a specific Harmony instance</param>
		///
		public void Unpatch(MethodBase original, HarmonyPatchType type, string harmonyID = "*")
		{
			var processor = CreateProcessor(original);
			_ = processor.Unpatch(type, harmonyID);
		}

		/// <summary>Unpatches a method by patching it with zero patches. Fully unpatching is not supported. Be careful, unpatching is global</summary>
		/// <param name="original">The original method/constructor</param>
		/// <param name="patch">The patch method as method to remove</param>
		///
		public void Unpatch(MethodBase original, MethodInfo patch)
		{
			var processor = CreateProcessor(original);
			_ = processor.Unpatch(patch);
		}

		/// <summary>Searches the current assembly for types with a specific category annotation and uses them to unpatch existing patches. Fully unpatching is not supported. Be careful, unpatching is global</summary>
		/// <param name="category">Name of patch category</param>
		///
		public void UnpatchCategory(string category)
		{
			var method = new StackTrace().GetFrame(1).GetMethod();
			var assembly = method.ReflectedType.Assembly;
			UnpatchCategory(assembly, category);
		}

		/// <summary>Searches an assembly for HarmonyPatch-annotated classes/structs with a specific category annotation and uses them to unpatch existing patches. Fully unpatching is not supported. Be careful, unpatching is global</summary>
		/// <param name="assembly">The assembly</param>
		/// <param name="category">Name of patch category</param>
		///
		public void UnpatchCategory(Assembly assembly, string category)
		{
			var categoryCache = AssemblyCachedCategories.GetValue(assembly, BuildCategoryCache);
			if (categoryCache.TryGetValue(category, out var toPatch))
			{
				toPatch.Do(type => CreateClassProcessor(type).Unpatch());
			}
		}

		/// <summary>Test for patches from a specific Harmony ID</summary>
		/// <param name="harmonyID">The Harmony ID</param>
		/// <returns>True if patches for this ID exist</returns>
		///
		public static bool HasAnyPatches(string harmonyID)
		{
			return GetAllPatchedMethods()
				.Select(GetPatchInfo)
				.Any(info => info.Owners.Contains(harmonyID));
		}

		/// <summary>Gets patch information for a given original method</summary>
		/// <param name="method">The original method/constructor</param>
		/// <returns>The patch information as <see cref="Patches"/></returns>
		///
		public static Patches GetPatchInfo(MethodBase method) => PatchProcessor.GetPatchInfo(method);

		/// <summary>Gets the methods this instance has patched</summary>
		/// <returns>An enumeration of original methods/constructors</returns>
		///
		public IEnumerable<MethodBase> GetPatchedMethods()
		{
			return GetAllPatchedMethods()
				.Where(original => GetPatchInfo(original).Owners.Contains(Id));
		}

		/// <summary>Gets all patched original methods in the appdomain</summary>
		/// <returns>An enumeration of patched original methods/constructors</returns>
		///
		public static IEnumerable<MethodBase> GetAllPatchedMethods() => PatchProcessor.GetAllPatchedMethods();

		/// <summary>Gets the original method from a given replacement method</summary>
		/// <param name="replacement">A replacement method (patched original method)</param>
		/// <returns>The original method/constructor or <c>null</c> if not found</returns>
		///
		public static MethodBase GetOriginalMethod(MethodInfo replacement)
		{
			if (replacement == null) throw new ArgumentNullException(nameof(replacement));
			return HarmonySharedState.GetRealMethod(replacement, useReplacement: false);
		}

		/// <summary>Tries to get the method from a stackframe including dynamic replacement methods</summary>
		/// <param name="frame">The <see cref="StackFrame"/></param>
		/// <returns>For normal frames, <c>frame.GetMethod()</c> is returned. For frames containing patched methods, the replacement method is returned or <c>null</c> if no method can be found</returns>
		///
		public static MethodBase GetMethodFromStackframe(StackFrame frame)
		{
			if (frame == null) throw new ArgumentNullException(nameof(frame));
			return HarmonySharedState.GetStackFrameMethod(frame, useReplacement: true);
		}

		/// <summary>Gets the original method from the stackframe and uses original if method is a dynamic replacement</summary>
		/// <param name="frame">The <see cref="StackFrame"/></param>
		/// <returns>The original method from that stackframe</returns>
		public static MethodBase GetOriginalMethodFromStackframe(StackFrame frame)
		{
			if (frame == null) throw new ArgumentNullException(nameof(frame));
			return HarmonySharedState.GetStackFrameMethod(frame, useReplacement: false);
		}

		/// <summary>Gets Harmony version for all active Harmony instances</summary>
		/// <param name="currentVersion">[out] The current Harmony version</param>
		/// <returns>A dictionary containing assembly versions keyed by Harmony IDs</returns>
		///
		public static Dictionary<string, Version> VersionInfo(out Version currentVersion)
			=> PatchProcessor.VersionInfo(out currentVersion);

		/// <summary>Sets a MonoMod switch value (e.g., "DMDDebug", "DMDDumpTo")</summary>
		/// <param name="name">The switch name</param>
		/// <param name="value">The value to set (bool, string, etc.)</param>
		///
		public static void SetSwitch(string name, object value)
			=> MonoMod.Switches.SetSwitchValue(name, value);

		/// <summary>Clears a MonoMod switch value</summary>
		/// <param name="name">The switch name</param>
		///
		public static void ClearSwitch(string name)
			=> MonoMod.Switches.ClearSwitchValue(name);

		/// <summary>Tries to get a MonoMod switch value</summary>
		/// <param name="name">The switch name</param>
		/// <param name="value">The switch value if found</param>
		/// <returns>True if the switch was found, false otherwise</returns>
		///
		public static bool TryGetSwitch(string name, out object value)
			=> MonoMod.Switches.TryGetSwitchValue(name, out value);

		/// <summary>Tries to determine if a MonoMod switch is enabled</summary>
		/// <param name="name">The switch name</param>
		/// <param name="isEnabled">True if the switch is enabled, false otherwise</param>
		/// <returns>True if the switch enablement state could be determined, false otherwise</returns>
		///
		public static bool TryIsSwitchEnabled(string name, out bool isEnabled)
			=> MonoMod.Switches.TryGetSwitchEnabled(name, out isEnabled);
	}
}
