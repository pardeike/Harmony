using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;

namespace Harmony
{
	public class Patches
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

		public Patches(Patch[] prefixes, Patch[] postfixes)
		{
			if (prefixes == null) prefixes = new Patch[0];
			if (postfixes == null) postfixes = new Patch[0];

			Prefixes = prefixes.ToList().AsReadOnly();
			Postfixes = postfixes.ToList().AsReadOnly();
		}
	}

	public class HarmonyInstance
	{
		readonly string id;
		public string Id => id;

		readonly Patcher patcher;

		HarmonyInstance(string id)
		{
			this.id = id;
			patcher = new Patcher(this);
		}

		public static HarmonyInstance Create(string id)
		{
			if (id == null) throw new Exception("id cannot be null");
			return new HarmonyInstance(id);
		}

		public void PatchAll(Module module)
		{
			patcher.PatchAll(module);
		}

		public void Patch(MethodBase original, HarmonyMethod prefix, HarmonyMethod postfix)
		{
			patcher.Patch(original, prefix, postfix);
		}

		public Patches IsPatched(MethodBase method)
		{
			return patcher.IsPatched(method);
		}
	}
}