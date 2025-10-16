using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace HarmonyLib
{
	internal class PatchJobs<T>
	{
		internal class Job
		{
			internal MethodBase original;
			internal T replacement;
			internal List<HarmonyMethod> prefixes = [];
			internal List<HarmonyMethod> postfixes = [];
			internal List<HarmonyMethod> transpilers = [];
			internal List<HarmonyMethod> finalizers = [];
			internal List<AttributePatch> innerpatches = [];

			internal void AddPatch(AttributePatch patch)
			{
				switch (patch.type)
				{
					case HarmonyPatchType.Prefix:
						prefixes.Add(patch.info);
						break;
					case HarmonyPatchType.Postfix:
						postfixes.Add(patch.info);
						break;
					case HarmonyPatchType.Transpiler:
						transpilers.Add(patch.info);
						break;
					case HarmonyPatchType.Finalizer:
						finalizers.Add(patch.info);
						break;
					case HarmonyPatchType.InnerPrefix:
					case HarmonyPatchType.InnerPostfix:
						innerpatches.Add(patch);
						break;
				}
			}

			// Get inner prefixes as HarmonyMethod array for backwards compatibility
			internal HarmonyMethod[] GetInnerPrefixes() => 
				innerpatches.Where(p => p.type == HarmonyPatchType.InnerPrefix).Select(p => p.info).ToArray();
			
			// Get inner postfixes as HarmonyMethod array for backwards compatibility  
			internal HarmonyMethod[] GetInnerPostfixes() =>
				innerpatches.Where(p => p.type == HarmonyPatchType.InnerPostfix).Select(p => p.info).ToArray();
		}

		internal Dictionary<MethodBase, Job> state = [];

		internal Job GetJob(MethodBase method)
		{
			if (method is null) return null;
			if (state.TryGetValue(method, out var job) is false)
			{
				job = new Job() { original = method };
				state[method] = job;
			}
			return job;
		}

		internal List<Job> GetJobs()
		{
			return [.. state.Values.Where(job =>
				job.prefixes.Count +
				job.postfixes.Count +
				job.transpilers.Count +
				job.finalizers.Count +
				job.innerpatches.Count
				> 0
			)];
		}

		internal List<T> GetReplacements() => [.. state.Values.Select(job => job.replacement)];
	}

	// AttributePatch contains all information for a patch defined by attributes
	//
	internal class AttributePatch
	{
		static readonly HarmonyPatchType[] allPatchTypes = [
			HarmonyPatchType.Prefix,
			HarmonyPatchType.Postfix,
			HarmonyPatchType.Transpiler,
			HarmonyPatchType.Finalizer,
			HarmonyPatchType.ReversePatch,
			HarmonyPatchType.InnerPrefix,
			HarmonyPatchType.InnerPostfix
		];

		internal HarmonyMethod info;
		internal HarmonyPatchType? type;
		internal InnerMethod innerMethod;

		internal static AttributePatch Create(MethodInfo patch)
		{
			if (patch is null)
				throw new NullReferenceException("Patch method cannot be null");

			var allAttributes = patch.GetCustomAttributes(true);
			var methodName = patch.Name;
			var type = GetPatchType(methodName, allAttributes);
			if (type is null)
				return null;

			if (type != HarmonyPatchType.ReversePatch && patch.IsStatic is false)
				throw new ArgumentException("Patch method " + patch.FullDescription() + " must be static");

			var list = allAttributes
				.Where(attr => attr.GetType().BaseType.FullName == PatchTools.harmonyAttributeFullName)
				.Select(attr =>
				{
					var f_info = AccessTools.Field(attr.GetType(), nameof(HarmonyAttribute.info));
					return f_info.GetValue(attr);
				})
				.Select(AccessTools.MakeDeepCopy<HarmonyMethod>)
				.ToList();
			var info = HarmonyMethod.Merge(list);
			info.method = patch;

			// Extract inner method from HarmonyInfixTarget attribute for infix patches
			InnerMethod innerMethod = null;
			if (type == HarmonyPatchType.InnerPrefix || type == HarmonyPatchType.InnerPostfix)
			{
				// Look for HarmonyInnerPatch attribute first
				var innerPatchAttr = allAttributes
					.FirstOrDefault(attr => attr.GetType().FullName == "HarmonyLib.HarmonyInnerPatch") as HarmonyInnerPatch;
				
				if (innerPatchAttr != null)
				{
					var positions = innerPatchAttr.indices.Length == 0 ? new int[0] : innerPatchAttr.indices;
					innerMethod = new InnerMethod((MethodInfo)innerPatchAttr.method, positions);
				}
				else
				{
					// Fall back to checking HarmonyInnerPrefix/HarmonyInnerPostfix attributes for method info
					if (type == HarmonyPatchType.InnerPrefix)
					{
						var innerPrefixAttr = allAttributes
							.FirstOrDefault(attr => attr.GetType().FullName == "HarmonyLib.HarmonyInnerPrefix") as HarmonyInnerPrefix;
						if (innerPrefixAttr?.method != null)
						{
							var positions = innerPrefixAttr.indices.Length == 0 ? new int[0] : innerPrefixAttr.indices;
							innerMethod = new InnerMethod((MethodInfo)innerPrefixAttr.method, positions);
						}
					}
					else if (type == HarmonyPatchType.InnerPostfix)
					{
						var innerPostfixAttr = allAttributes
							.FirstOrDefault(attr => attr.GetType().FullName == "HarmonyLib.HarmonyInnerPostfix") as HarmonyInnerPostfix;
						if (innerPostfixAttr?.method != null)
						{
							var positions = innerPostfixAttr.indices.Length == 0 ? new int[0] : innerPostfixAttr.indices;
							innerMethod = new InnerMethod((MethodInfo)innerPostfixAttr.method, positions);
						}
					}
				}
			}

			return new AttributePatch() { info = info, type = type, innerMethod = innerMethod };
		}

		static HarmonyPatchType? GetPatchType(string methodName, object[] allAttributes)
		{
			var harmonyAttributes = new HashSet<string>(allAttributes
				.Select(attr => attr.GetType().FullName)
				.Where(name => name.StartsWith("Harmony")));

			// Check for inner prefix/postfix attributes first
			if (harmonyAttributes.Contains("HarmonyLib.HarmonyInnerPrefix"))
				return HarmonyPatchType.InnerPrefix;
			if (harmonyAttributes.Contains("HarmonyLib.HarmonyInnerPostfix"))
				return HarmonyPatchType.InnerPostfix;

			// Fall back to standard method name or attribute-based detection
			HarmonyPatchType? type = null;
			foreach (var patchType in allPatchTypes)
			{
				var name = patchType.ToString();
				if (name == methodName || harmonyAttributes.Contains($"HarmonyLib.Harmony{name}"))
				{
					type = patchType;
					break;
				}
			}
			return type;
		}
	}
}
