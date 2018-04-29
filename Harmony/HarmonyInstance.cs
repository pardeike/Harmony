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
				result.UnionWith(Postfixes.Select(p => p.owner));
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

		HarmonyInstance(string id)
		{
			this.id = id;
			SelfPatching.PatchOldHarmonyMethods();
		}

		public static HarmonyInstance Create(string id)
		{
			if (id == null) throw new Exception("id cannot be null");
			return new HarmonyInstance(id);
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

		public DynamicMethod Patch(MethodBase original, HarmonyMethod prefix, HarmonyMethod postfix, HarmonyMethod transpiler = null)
		{
			var processor = new PatchProcessor(this, new List<MethodBase> { original }, prefix, postfix, transpiler);
			return processor.Patch().FirstOrDefault();
		}

		public void RemovePatch(MethodBase original, HarmonyPatchType type, string harmonyID = null)
		{
			var processor = new PatchProcessor(this, new List<MethodBase> { original });
			processor.Unpatch(type, harmonyID);
		}

		public void RemovePatch(MethodBase original, MethodInfo patch)
		{
			var processor = new PatchProcessor(this, new List<MethodBase> { original });
			processor.Unpatch(patch);
		}

		//

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
			currentVersion = Assembly.GetExecutingAssembly().GetName().Version;
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