using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Harmony
{
	internal static class PatchTools
	{
		static readonly Dictionary<object, object> objectReferences = new Dictionary<object, object>();

		internal static void RememberObject(object key, object value)
		{
			objectReferences[key] = value;
		}

		internal static MethodInfo GetPatchMethod<T>(Type patchType, string name, Type[] parameters = null)
		{
			var attributeType = typeof(T).FullName;
			var method = patchType.GetMethods(AccessTools.all)
				.FirstOrDefault(m => m.GetCustomAttributes(true).Any(a => a.GetType().FullName == attributeType));
			if (method == null)
				method = AccessTools.Method(patchType, name, parameters);
			return method;
		}

		[UpgradeToLatestVersion(1)]
		internal static void GetPatches(Type patchType, out MethodInfo prefix, out MethodInfo postfix, out MethodInfo transpiler)
		{
			prefix = GetPatchMethod<HarmonyPrefix>(patchType, "Prefix");
			postfix = GetPatchMethod<HarmonyPostfix>(patchType, "Postfix");
			transpiler = GetPatchMethod<HarmonyTranspiler>(patchType, "Transpiler");
		}
	}
}