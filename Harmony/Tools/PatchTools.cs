using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Harmony
{
	public static class PatchTools
	{
		// this holds all the objects we want to keep alive so they don't get garbage-collected
		static Dictionary<object, object> objectReferences = new Dictionary<object, object>();
		public static void RememberObject(object key, object value)
		{
			objectReferences[key] = value;
		}

		public static void ForgetObject(object key)
		{
			objectReferences.Remove(key);
		}

		public static bool RecallObject(object key, out object value)
		{
			return objectReferences.TryGetValue(key, out value);
		}

		public static MethodInfo GetPatchMethod<T>(Type patchType, string name, Type[] parameters = null)
		{
			var method = patchType.GetMethods(AccessTools.all)
				.FirstOrDefault(m => m.GetCustomAttributes(typeof(T), true).Count() > 0);
			if (method == null)
				method = AccessTools.Method(patchType, name, parameters);
			return method;
		}

		public static void GetPatches(Type patchType, MethodBase original, out MethodInfo prefix, out MethodInfo postfix, out MethodInfo transpiler)
		{
			var type = original.DeclaringType;
			var methodName = original.Name;

			prefix = GetPatchMethod<HarmonyPrefix>(patchType, "Prefix");
			postfix = GetPatchMethod<HarmonyPostfix>(patchType, "Postfix");
			transpiler = GetPatchMethod<HarmonyTranspiler>(patchType, "Transpiler");
		}
	}
}