using Harmony.ILCopying;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Harmony.Tools
{
	internal class SelfPatching
	{
		static int GetVersion(MethodInfo method)
		{
			var attribute = method.GetCustomAttributes(false).OfType<UpgradeToLatestVersion>().FirstOrDefault();
			return attribute?.version ?? -1;
		}

		static string MethodKey(MethodInfo method)
		{
			return method.DeclaringType + " " + method;
		}

		static bool IsHarmonyAssembly(Assembly assembly)
		{
			try
			{
				var attribute = assembly.GetCustomAttributes(typeof(GuidAttribute), false);
				if (attribute.Length < 1)
					return false;
				var guidAttribute = attribute.GetValue(0) as GuidAttribute;
				return guidAttribute.Value.ToString() == "69aee16a-b6e7-4642-8081-3928b32455df";
			}
			catch (Exception)
			{
				return false;
			}
		}

		// globally shared between all our identical versions
		static HashSet<MethodInfo> patchedMethods = new HashSet<MethodInfo>();
		//
		public static void PatchOldHarmonyMethods()
		{
			var potentialMethodsToUpgrade = new Dictionary<string, MethodInfo>();
			typeof(SelfPatching).Assembly.GetTypes()
				.SelectMany(type => type.GetMethods(AccessTools.all))
				.Where(method => method.GetCustomAttributes(false).Any(attr => attr is UpgradeToLatestVersion))
				.Do(method => potentialMethodsToUpgrade.Add(MethodKey(method), method));

			AppDomain.CurrentDomain.GetAssemblies()
				.Where(assembly => IsHarmonyAssembly(assembly))
				.SelectMany(assembly => assembly.GetTypes())
				.SelectMany(type => type.GetMethods(AccessTools.all))
				.Select(method =>
				{
					potentialMethodsToUpgrade.TryGetValue(MethodKey(method), out var newMethod);
					return new KeyValuePair<MethodInfo, MethodInfo>(method, newMethod);
				})
				.Do(pair =>
				{
					var oldMethod = pair.Key;
					var newMethod = pair.Value;
					if (newMethod != null && GetVersion(oldMethod) < GetVersion(newMethod))
					{
						if (patchedMethods.Contains(oldMethod) == false)
						{
							patchedMethods.Add(oldMethod);
							Memory.DetourMethod(oldMethod, newMethod);
						}
					}
				});
		}
	}
}