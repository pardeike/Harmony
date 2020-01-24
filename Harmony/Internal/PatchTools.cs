using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace HarmonyLib
{
	internal static class PatchTools
	{
		static readonly Dictionary<object, object> objectReferences = new Dictionary<object, object>();

		internal static void RememberObject(object key, object value)
		{
			objectReferences[key] = value;
		}

		internal static MethodInfo GetPatchMethod(Type patchType, string attributeName)
		{
			var method = patchType.GetMethods(AccessTools.all)
				.FirstOrDefault(m => m.GetCustomAttributes(true).Any(a => a.GetType().FullName == attributeName));
			if (method == null)
			{
				// not-found is common and normal case, don't use AccessTools which will generate not-found warnings
				var methodName = attributeName.Replace("HarmonyLib.Harmony", "");
				method = patchType.GetMethod(methodName, AccessTools.all);
			}
			return method;
		}

		internal static AssemblyBuilder DefineDynamicAssembly(string name)
		{
			var assemblyName = new AssemblyName(name);
#if NETCOREAPP2_0 || NETCOREAPP3_0 || NETCOREAPP3_1 || NETSTANDARD2_0
			return AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
#else
			return AppDomain.CurrentDomain.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
#endif
		}

		internal static List<AttributePatch> GetPatchMethods(Type type)
		{
			var harmonyPatchName = typeof(HarmonyPatch).FullName;
			return AccessTools.GetDeclaredMethods(type)
				.Select(method => AttributePatch.Create(method))
				.Where(attributePatch => attributePatch != null)
				.ToList();
		}

		internal static List<MethodInfo> GetReversePatches(Type patchType)
		{
			var attr = typeof(HarmonyReversePatch).FullName;
			return patchType.GetMethods(AccessTools.all)
				.Where(m => m.GetCustomAttributes(true).Any(a => a.GetType().FullName == attr))
				.ToList();
		}
	}
}