using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace Harmony
{
	public delegate void RegisterPatch(string owner, MethodInfo original, HarmonyMethod prefixPatch, HarmonyMethod postfixPatch);

	public class PatchInfo
	{
		public readonly ReadOnlyCollection<Patch> Prefixes;
		public readonly ReadOnlyCollection<Patch> Postfixes;

		public ReadOnlyCollection<string> Owners
		{
			get
			{
				var result = new HashSet<string>();
				result.UnionWith(Prefixes.Select(p => p.owner));
				result.UnionWith(Postfixes.Select(p => p.owner));
				return result.ToList().AsReadOnly();
			}
		}

		public PatchInfo(List<Patch> prefixes, List<Patch> postfixes)
		{
			Prefixes = prefixes.AsReadOnly();
			Postfixes = postfixes.AsReadOnly();
		}
	}

	class HarmonyRegistry
	{
		class Patches
		{
			readonly MethodInfo original;
			readonly DynamicMethod copy;
			readonly MethodInfo copyDelegate;

			readonly List<Patch> prefixes;
			readonly List<Patch> postfixes;

			DynamicMethod wrapper;
			MethodInfo wrapperDelegate;

			public Patches(MethodInfo method)
			{
				original = method;
				copy = PatchTools.CreateMethodCopy(method);
				if (copy == null) throw new MissingMethodException("Cannot create copy of " + method);
				copyDelegate = PatchTools.PrepareDynamicMethod(original, copy);

				prefixes = new List<Patch>();
				postfixes = new List<Patch>();
			}

			public void AddPrefix(string owner, HarmonyMethod info)
			{
				if (info == null || info.method == null) return;

				var priority = info.prioritiy == -1 ? Priority.Normal : info.prioritiy;
				var before = info.before != null ? new HashSet<string>(info.before) : null;
				var after = info.after != null ? new HashSet<string>(info.after) : null;
				var patch = new Patch(prefixes.Count, owner, info.method, priority, before, after);
				prefixes.Add(patch);
			}

			public void AddPostfix(string owner, HarmonyMethod info)
			{
				if (info == null || info.method == null) return;

				var priority = info.prioritiy == -1 ? Priority.Normal : info.prioritiy;
				var before = info.before != null ? new HashSet<string>(info.before) : null;
				var after = info.after != null ? new HashSet<string>(info.after) : null;
				var patch = new Patch(postfixes.Count, owner, info.method, priority, before, after);
				postfixes.Add(patch);
			}

			public void UpdateWrapper()
			{
				var sortedPrefixes = prefixes.Where(p => p.patch != null).OrderBy(p => p).Select(p => p.patch).ToList();
				var sortedPostfixes = postfixes.Where(p => p.patch != null).OrderBy(p => p).Select(p => p.patch).ToList();

				wrapper = PatchTools.CreatePatchWrapper(original, copyDelegate, sortedPrefixes, sortedPostfixes);
				wrapperDelegate = PatchTools.PrepareDynamicMethod(original, wrapper);
				PatchTools.Detour(original, wrapperDelegate);
			}

			public PatchInfo GetInfo()
			{
				return new PatchInfo(prefixes, postfixes);
			}
		}

		readonly Dictionary<string, HarmonyInstance> instances;
		readonly Dictionary<MethodInfo, Patches> allPatches;
		readonly RegisterPatch registerCallback;

		public HarmonyRegistry()
		{
			instances = new Dictionary<string, HarmonyInstance>();
			allPatches = new Dictionary<MethodInfo, Patches>();

			registerCallback = delegate (
				string owner,
				MethodInfo original,
				HarmonyMethod prefix,
				HarmonyMethod postfix)
				{
					AddPatch(owner, original, prefix, postfix);
				};
		}

		public RegisterPatch GetRegisterPatch()
		{
			return registerCallback;
		}

		void AddPatch(string owner, MethodInfo original, HarmonyMethod prefix, HarmonyMethod postfix)
		{
			if (original == null) throw new ArgumentNullException("original");

			Patches patches;
			allPatches.TryGetValue(original, out patches);
			if (patches == null)
				patches = new Patches(original);

			patches.AddPrefix(owner, prefix);
			patches.AddPostfix(owner, postfix);
			patches.UpdateWrapper();
			allPatches.Add(original, patches);
		}

		public void Add(HarmonyInstance instance)
		{
			if (instance == null) throw new ArgumentNullException("instance");

			var id = instance.Id;
			if (id == null) throw new ArgumentNullException("instance.id");

			if (instances.ContainsKey(id))
			{
				var info = instance.Contact;
				throw new ArgumentException("ID " + id + " must be unique and is already registered by " + info);
			}

			instances.Add(id, instance);
		}

		public PatchInfo IsPatched(MethodInfo method)
		{
			if (method == null) throw new ArgumentNullException("method");
			if (allPatches.ContainsKey(method) == false) return null;
			return allPatches[method].GetInfo();
		}
	}
}