using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Harmony
{
	public delegate void PatchCallback(MethodInfo original, HarmonyMethod prefixPatch, HarmonyMethod postfixPatch);

	public class HarmonyInstance
	{
		static readonly HarmonyRegistry registry;

		static HarmonyInstance()
		{
			registry = new HarmonyRegistry();
		}

		readonly string id;
		public string Id => id;

		readonly ContactInfo contact;
		public ContactInfo Contact => contact;

		readonly Patcher patcher;

		private HarmonyInstance(string id, ContactInfo contact)
		{
			this.id = id;
			this.contact = contact;
			patcher = new Patcher(this, delegate (MethodInfo original, HarmonyMethod prefixPatch, HarmonyMethod postfixPatch)
			{
				var register = registry.GetRegisterPatch();
				register(id, original, prefixPatch, postfixPatch);
			});
		}

		public static HarmonyInstance Register(string id, ContactInfo info)
		{
			if (id == null) throw new ArgumentNullException("id");
			var instance = new HarmonyInstance(id, info);
			registry.Add(instance);
			return instance;
		}

		public static HarmonyInstance RegisterWithID(string id)
		{
			return Register(id, new ContactInfo());
		}

		public static HarmonyInstance RegisterWithFile(string filepath)
		{
			string id = null;
			var info = new ContactInfo();
			var trv = Traverse.Create(info);

			foreach (var row in File.ReadAllLines(filepath))
			{
				if (row.Contains("="))
				{
					var parts = row.Split('=');
					var key = parts[0].Trim();
					var val = string.Join("=", parts.Skip(1).ToArray()).Trim();
					if (key == "id")
						id = val;
					else
						trv.Field(key).SetValue(val);
				}
			}
			if (id == null) throw new FileLoadException("ID not found. Config file must contain at least id=...", filepath);

			return Register(id, info);
		}

		public void PatchAll(Module module)
		{
			patcher.PatchAll(module);
		}

		public void Patch(MethodInfo original, HarmonyMethod prefix, HarmonyMethod postfix)
		{
			patcher.Patch(original, prefix, postfix);
		}

		public PatchInfo IsPatched(MethodInfo method)
		{
			return registry.IsPatched(method);
		}
	}

	public class ContactInfo
	{
		public string name;
		public string email;
		public string twitter;
		public string facebook;
		public string steam;

		public string githubURL;
		public string steamURL;

		public string website;

		public override string ToString()
		{
			var trv = Traverse.Create(this);
			var parts = AccessTools.GetFieldNames(this)
				.Select(f => trv.Field(f).GetValue().ToString())
				.Where(s => s != null && s != "")
				.ToArray();
			return "[" + string.Join(",", parts) + "]";
		}
	}
}