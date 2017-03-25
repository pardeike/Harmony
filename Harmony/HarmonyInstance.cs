using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

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
		}

		public static HarmonyInstance Create(string id)
		{
			if (id == null) throw new Exception("id cannot be null");
			return new HarmonyInstance(id);
		}

		//

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

		public void Patch(MethodBase original, HarmonyMethod prefix, HarmonyMethod postfix, HarmonyMethod transpiler = null)
		{
			var processor = new PatchProcessor(this, original, prefix, postfix, transpiler);
			processor.Patch();
		}

		//

		public Patches IsPatched(MethodBase method)
		{
			return PatchProcessor.IsPatched(method);
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