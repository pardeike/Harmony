using Harmony.ILCopying;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace Harmony.Tools
{
	internal class SelfPatching
	{
		static readonly int upgradeToLatestVersionFullNameHash = typeof(UpgradeToLatestVersion).FullName.GetHashCode();

		[UpgradeToLatestVersion(1)]
		static int GetVersion(MethodBase method)
		{
			var attribute = method.GetCustomAttributes(false)
				.Where(attr => attr.GetType().FullName.GetHashCode() == upgradeToLatestVersionFullNameHash)
				.FirstOrDefault();
			if (attribute == null)
				return -1;
			return Traverse.Create(attribute).Field("version").GetValue<int>();
		}

		[UpgradeToLatestVersion(1)]
		static string MethodKey(MethodBase method)
		{
			return method.FullDescription();
		}

		[UpgradeToLatestVersion(1)]
		static bool IsHarmonyAssembly(Assembly assembly)
		{
			try
			{
				return assembly.ReflectionOnly == false && assembly.GetType(typeof(HarmonyInstance).FullName) != null;
			}
			catch (Exception)
			{
				return false;
			}
		}

		private static List<MethodBase> GetAllMethods(Assembly assembly)
		{
			var types = assembly.GetTypes();
			return types
				.SelectMany(type => type.GetMethods(AccessTools.all).Cast<MethodBase>())
				.Concat(types.SelectMany(type => type.GetConstructors(AccessTools.all)).Cast<MethodBase>())
				.Concat(types.SelectMany(type => type.GetProperties(AccessTools.all)).Select(prop => prop.GetGetMethod()).Cast<MethodBase>())
				.Concat(types.SelectMany(type => type.GetProperties(AccessTools.all)).Select(prop => prop.GetSetMethod()).Cast<MethodBase>())
				.Where(method => method != null && method.DeclaringType.Assembly == assembly)
				.OrderBy(method => method.FullDescription())
				.ToList();
		}

		private static string AssemblyInfo(Assembly assembly)
		{
			var version = assembly.GetName().Version;
			var location = assembly.Location;
			if (location == null || location == "") location = new Uri(assembly.CodeBase).LocalPath;
			return location + "(v" + version + (assembly.GlobalAssemblyCache ? ", cached" : "") + ")";
		}

		[UpgradeToLatestVersion(1)]
		public static void PatchOldHarmonyMethods()
		{
			var watch = new Stopwatch();
			watch.Start();

			var ourAssembly = new StackTrace(true).GetFrame(1).GetMethod().DeclaringType.Assembly;
			if (HarmonyInstance.DEBUG)
			{
				var originalVersion = ourAssembly.GetName().Version;
				var runningVersion = typeof(SelfPatching).Assembly.GetName().Version;
				if (runningVersion > originalVersion)
				{
					// log start because FileLog has not done it
					FileLog.Log("### Harmony v" + originalVersion + " started");
					FileLog.Log("### Self-patching unnecessary because we are already patched by v" + runningVersion);
					FileLog.Log("### At " + DateTime.Now.ToString("yyyy-MM-dd hh.mm.ss"));
					return;
				}
				FileLog.Log("Self-patching started (v" + originalVersion + ")");
			}

			var potentialMethodsToUpgrade = new Dictionary<string, MethodBase>();
			GetAllMethods(ourAssembly)
				.Where(method => method != null && method.GetCustomAttributes(false).Any(attr => attr is UpgradeToLatestVersion))
				.Do(method => potentialMethodsToUpgrade.Add(MethodKey(method), method));

			var otherHarmonyAssemblies = AppDomain.CurrentDomain.GetAssemblies()
					.Where(assembly => IsHarmonyAssembly(assembly) && assembly != ourAssembly)
					.ToList();

			if (HarmonyInstance.DEBUG)
			{
				otherHarmonyAssemblies.Do(assembly => FileLog.Log("Found Harmony " + AssemblyInfo(assembly)));

				FileLog.Log("Potential methods to upgrade:");
				potentialMethodsToUpgrade.Values.OrderBy(method => method.FullDescription()).Do(method => FileLog.Log("- " + method.FullDescription()));
			}

			var totalCounter = 0;
			var potentialCounter = 0;
			var patchedCounter = 0;
			foreach (var assembly in otherHarmonyAssemblies)
			{
				foreach (var oldMethod in GetAllMethods(assembly))
				{
					totalCounter++;

					if (potentialMethodsToUpgrade.TryGetValue(MethodKey(oldMethod), out var newMethod))
					{
						var newVersion = GetVersion(newMethod);
						potentialCounter++;

						var oldVersion = GetVersion(oldMethod);
						if (oldVersion < newVersion)
						{
							if (HarmonyInstance.DEBUG)
								FileLog.Log("Self-patching " + oldMethod.FullDescription() + " in " + AssemblyInfo(assembly));
							patchedCounter++;
							Memory.DetourMethod(oldMethod, newMethod);
						}
					}
				}
			}

			if (HarmonyInstance.DEBUG)
				FileLog.Log("Self-patched " + patchedCounter + " out of " + totalCounter + " methods (" + (potentialCounter - patchedCounter) + " skipped) in " + watch.ElapsedMilliseconds + "ms");
		}
	}
}