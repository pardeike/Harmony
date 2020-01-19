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

		internal static MethodInfo GetPatchMethod<T>(Type patchType, string name)
		{
			var attributeType = typeof(T).FullName;
			var method = patchType.GetMethods(AccessTools.all)
				.FirstOrDefault(m => m.GetCustomAttributes(true).Any(a => a.GetType().FullName == attributeType));
			if (method == null)
			{
				// not-found is common and normal case, don't use AccessTools which will generate not-found warnings
				method = patchType.GetMethod(name, AccessTools.all);
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

		internal static void GetPatches(Type patchType, out MethodInfo prefix, out MethodInfo postfix, out MethodInfo transpiler, out MethodInfo finalizer)
		{
			prefix = GetPatchMethod<HarmonyPrefix>(patchType, "Prefix");
			postfix = GetPatchMethod<HarmonyPostfix>(patchType, "Postfix");
			transpiler = GetPatchMethod<HarmonyTranspiler>(patchType, "Transpiler");
			finalizer = GetPatchMethod<HarmonyFinalizer>(patchType, "Finalizer");
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