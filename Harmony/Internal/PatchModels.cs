using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace HarmonyLib
{
	// PatchJobs holds the information during correlation
	// of methods and patches while processing attribute patches
	//
	internal class PatchJobs<T>
	{
		internal class Job
		{
			internal MethodBase original;
			internal T replacement;
			internal List<HarmonyMethod> prefixes = new List<HarmonyMethod>();
			internal List<HarmonyMethod> postfixes = new List<HarmonyMethod>();
			internal List<HarmonyMethod> transpilers = new List<HarmonyMethod>();
			internal List<HarmonyMethod> finalizers = new List<HarmonyMethod>();

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
				}
			}
		}

		internal Dictionary<MethodBase, Job> state = new Dictionary<MethodBase, Job>();

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
			return state.Values.Where(job =>
				job.prefixes.Count +
				job.postfixes.Count +
				job.transpilers.Count +
				job.finalizers.Count > 0
			).ToList();
		}

		internal List<T> GetReplacements()
		{
			return state.Values.Select(job => job.replacement).ToList();
		}
	}

	// AttributePatch contains all information for a patch defined by attributes
	//
	internal class AttributePatch
	{
		static readonly HarmonyPatchType[] allPatchTypes = new[] {
			HarmonyPatchType.Prefix,
			HarmonyPatchType.Postfix,
			HarmonyPatchType.Transpiler,
			HarmonyPatchType.Finalizer,
			HarmonyPatchType.ReversePatch,
		};

		internal HarmonyMethod info;
		internal HarmonyPatchType? type;

		static readonly string harmonyAttributeName = typeof(HarmonyAttribute).FullName;
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
				.Where(attr => attr.GetType().BaseType.FullName == harmonyAttributeName)
				.Select(attr =>
				{
					var f_info = AccessTools.Field(attr.GetType(), nameof(HarmonyAttribute.info));
					return f_info.GetValue(attr);
				})
				.Select(harmonyInfo => AccessTools.MakeDeepCopy<HarmonyMethod>(harmonyInfo))
				.ToList();
			var info = HarmonyMethod.Merge(list);
			info.method = patch;

			return new AttributePatch() { info = info, type = type };
		}

		static HarmonyPatchType? GetPatchType(string methodName, object[] allAttributes)
		{
			var harmonyAttributes = new HashSet<string>(allAttributes
				.Select(attr => attr.GetType().FullName)
				.Where(name => name.StartsWith("Harmony")));

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
