using Harmony.Tools;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace Harmony
{
	public class Patches
	{
		public readonly ReadOnlyCollection<Patch> Prefixes;
		public readonly ReadOnlyCollection<Patch> Postfixes;
		public readonly ReadOnlyCollection<Patch> Transpilers;

		public ReadOnlyCollection<string> Owners
		{
			get
			{
				var result = new HashSet<string>();
				result.UnionWith(Prefixes.Select(p => p.owner));
				result.UnionWith(Postfixes.Select(p => p.owner));
				result.UnionWith(Transpilers.Select(p => p.owner));
				return result.ToList().AsReadOnly();
			}
		}

		public Patches(Patch[] prefixes, Patch[] postfixes, Patch[] transpilers)
		{
			if (prefixes == null) prefixes = new Patch[0];
			if (postfixes == null) postfixes = new Patch[0];
			if (transpilers == null) transpilers = new Patch[0];

			Prefixes = prefixes.ToList().AsReadOnly();
			Postfixes = postfixes.ToList().AsReadOnly();
			Transpilers = transpilers.ToList().AsReadOnly();
		}
	}

	public class HarmonyInstance
	{
		readonly string id;
		public string Id => id;
		public static bool DEBUG = false;

		private static bool selfPatchingDone = false;

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
				SelfPatching.PatchOldHarmonyMethods();
			}
		}

		public static HarmonyInstance Create(string id)
		{
			if (id == null) throw new Exception("id cannot be null");
			return new HarmonyInstance(id);
		}

		private MethodBase GetOutsideCaller()
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

		//

		public void PatchAll()
		{
			var method = new StackTrace().GetFrame(1).GetMethod();
			var assembly = method.ReflectedType.Assembly;
			PatchAll(assembly);
		}

		public void PatchAll(Assembly assembly)
		{
			assembly.GetTypes().Do(type =>
			{
				var parentMethodInfos = type.GetHarmonyMethods();
				if (parentMethodInfos != null && parentMethodInfos.Count() > 0)
				{
					var info = HarmonyMethod.Merge(parentMethodInfos);
					var processor = new PatchProcessor(this, type, info);
					processor.Patch();
				}
			});
		}

		public DynamicMethod Patch(MethodBase original, HarmonyMethod prefix = null, HarmonyMethod postfix = null, HarmonyMethod transpiler = null)
		{
			var processor = new PatchProcessor(this, new List<MethodBase> { original }, prefix, postfix, transpiler);
			return processor.Patch().FirstOrDefault();
		}

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

		public void Unpatch(MethodBase original, HarmonyPatchType type, string harmonyID = null)
		{
			var processor = new PatchProcessor(this, new List<MethodBase> { original });
			processor.Unpatch(type, harmonyID);
		}

		public void Unpatch(MethodBase original, MethodInfo patch)
		{
			var processor = new PatchProcessor(this, new List<MethodBase> { original });
			processor.Unpatch(patch);
		}

		//

		public bool HasAnyPatches(string harmonyID)
		{
			return GetPatchedMethods()
				.Select(original => GetPatchInfo(original))
				.Any(info => info.Owners.Contains(harmonyID));
		}

		public Patches GetPatchInfo(MethodBase method)
		{
			return PatchProcessor.GetPatchInfo(method);
		}

		public IEnumerable<MethodBase> GetPatchedMethods()
		{
			return HarmonySharedState.GetPatchedMethods();
		}

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
