using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Harmony
{
	public static class PatchTools
	{
		// this holds all the objects we want to keep alive so they don't get garbage-collected
		static object[] objectReferences;
		[MethodImpl(MethodImplOptions.Synchronized)]
		public static void KeepAliveForever(object obj)
		{
			if (objectReferences == null)
				objectReferences = new object[0];
			objectReferences.Add(obj);
		}

		public static MethodInfo GetPatchMethod<T>(Type patchType, string name, Type[] parameter)
		{
			var method = patchType.GetMethods(AccessTools.all)
				.FirstOrDefault(m => m.GetCustomAttributes(typeof(T), true).Count() > 0);
			if (method == null)
				method = patchType.GetMethod(name, AccessTools.all, null, parameter, null);
			return method;
		}

		public static void GetPatches(Type patchType, MethodBase original, out MethodInfo prefix, out MethodInfo postfix)
		{
			var type = original.DeclaringType;
			var methodName = original.Name;

			var parameters = original.GetParameters();
			var prefixParams = new List<Type>();
			var postfixParams = new List<Type>();
			if (original.IsStatic == false)
			{
				prefixParams.Add(type);
				postfixParams.Add(type);
			}
			var returnedType = AccessTools.GetReturnedType(original);
			if (returnedType != typeof(void))
			{
				var retRef = returnedType.MakeByRefType();
				prefixParams.Add(retRef);
				postfixParams.Add(retRef);
			}
			parameters.Do(pi =>
			{
				var paramRef = pi.ParameterType.MakeByRefType();
				if (pi.IsOut == false) // prefix patches should not get out-parameters
					prefixParams.Add(paramRef);
				postfixParams.Add(paramRef);
			});

			prefix = GetPatchMethod<HarmonyPrefix>(patchType, "Prefix", prefixParams.ToArray());
			postfix = GetPatchMethod<HarmonyPostfix>(patchType, "Postfix", postfixParams.ToArray());
			if (prefix == null && postfix == null)
			{
				var prefixMethod = "Prefix(" + string.Join(", ", prefixParams.Select(p => p.FullName).ToArray()) + ")";
				var postfixMethod = "Postfix(" + string.Join(", ", postfixParams.Select(p => p.FullName).ToArray()) + ")";
				throw new MissingMethodException("No prefix/postfix patch for " + type.FullName + "." + methodName + "() found that matches " + prefixMethod + " or " + postfixMethod);
			}

			if (prefix != null && prefix.ReturnType != typeof(bool))
				throw new MissingMethodException("Prefix() must return bool (return true to execute original method)");
			if (postfix != null && postfix.ReturnType != typeof(void))
				throw new MissingMethodException("Postfix() must not return anything");
		}
	}
}