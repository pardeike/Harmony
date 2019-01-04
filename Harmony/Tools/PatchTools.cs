using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Harmony
{
	/// <summary>Patch tools</summary>
	public static class PatchTools
	{
		/// <summary>This holds all the objects we want to keep alive so they don't get garbage-collected</summary>
		static readonly Dictionary<object, object> objectReferences = new Dictionary<object, object>();

		/// <summary>Remember an object so it does not get garbage collected</summary>
		/// <param name="key">A key to pin the value to</param>
		/// <param name="value">The value</param>
		///
		public static void RememberObject(object key, object value)
		{
			objectReferences[key] = value;
		}

		/// <summary>Helper that returns a specific patch method</summary>
		/// <typeparam name="T">The type (prefix, postfix or transpiler) of the patch</typeparam>
		/// <param name="patchType">The class where the patch method is declared</param>
		/// <param name="name">The name of the method</param>
		/// <param name="parameters">Optional argument types for overloads</param>
		/// <returns>The patch method</returns>
		///
		public static MethodInfo GetPatchMethod<T>(Type patchType, string name, Type[] parameters = null)
		{
			var method = patchType.GetMethods(AccessTools.all)
				.FirstOrDefault(m => m.GetCustomAttributes(typeof(T), true).Any());
			if (method == null)
				method = AccessTools.Method(patchType, name, parameters);
			return method;
		}

		/// <summary>Gets all patch methods declared in a patch class</summary>
		/// <param name="patchType">The class that declares the patch methods</param>
		/// <param name="prefix">[out] The prefix patch</param>
		/// <param name="postfix">[out] The postfix patch</param>
		/// <param name="transpiler">[out] The transpiler patch</param>
		///
		public static void GetPatches(Type patchType, out MethodInfo prefix, out MethodInfo postfix, out MethodInfo transpiler)
		{
			prefix = GetPatchMethod<HarmonyPrefix>(patchType, "Prefix");
			postfix = GetPatchMethod<HarmonyPostfix>(patchType, "Postfix");
			transpiler = GetPatchMethod<HarmonyTranspiler>(patchType, "Transpiler");
		}
	}
}