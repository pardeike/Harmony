using System;
using System.Collections.Generic;
using System.Reflection;

namespace Harmony
{
	public class PatchProcessor
	{
		static object locker = new object();

		readonly HarmonyInstance instance;

		readonly Type container;
		readonly HarmonyMethod containerAttributes;

		MethodBase original;
		HarmonyMethod prefix;
		HarmonyMethod postfix;
		HarmonyProcessor infix;

		public PatchProcessor(HarmonyInstance instance, Type type, HarmonyMethod attributes)
		{
			this.instance = instance;
			container = type;
			containerAttributes = attributes;
			prefix = containerAttributes.Clone();
			postfix = containerAttributes.Clone();
			infix = null;
			ProcessType();
		}

		public PatchProcessor(HarmonyInstance instance, MethodBase original, HarmonyMethod prefix, HarmonyMethod postfix, HarmonyProcessor infix)
		{
			this.instance = instance;
			this.original = original;
			this.prefix = prefix;
			this.postfix = postfix;
			this.infix = infix;
		}

		public static Patches IsPatched(MethodBase method)
		{
			var patchInfo = HarmonySharedState.GetPatchInfo(method);
			if (patchInfo == null) return null;
			return new Patches(patchInfo.prefixes, patchInfo.prefixes);
		}

		public void Patch()
		{
			lock (locker)
			{
				var patchInfo = HarmonySharedState.GetPatchInfo(original);
				if (patchInfo == null) patchInfo = new PatchInfo();
				PatchFunctions.AddPrefix(patchInfo, instance.Id, prefix);
				PatchFunctions.AddPostfix(patchInfo, instance.Id, postfix);
				PatchFunctions.AddInfix(patchInfo, instance.Id, infix);
				PatchFunctions.UpdateWrapper(original, patchInfo);
				HarmonySharedState.UpdatePatchInfo(original, patchInfo);
			}
		}

		bool CallPrepare()
		{
			if (original != null)
				return RunMethod<HarmonyPrepare, bool>(true, original);
			return RunMethod<HarmonyPrepare, bool>(true);
		}

		void ProcessType()
		{
			original = GetOriginalMethod();

			var patchable = CallPrepare();
			if (patchable)
			{
				if (original == null)
					original = RunMethod<HarmonyTargetMethod, MethodBase>(null);
				if (original == null)
					throw new ArgumentException("No target method specified for class " + container.FullName);

				PatchTools.GetPatches(container, original, out prefix.method, out postfix.method);

				if (prefix.method != null)
				{
					var prefixAttributes = prefix.method.GetHarmonyMethods();
					containerAttributes.Merge(HarmonyMethod.Merge(prefixAttributes)).CopyTo(prefix);
				}

				if (postfix.method != null)
				{
					var postfixAttributes = postfix.method.GetHarmonyMethods();
					containerAttributes.Merge(HarmonyMethod.Merge(postfixAttributes)).CopyTo(postfix);
				}
			}
		}

		MethodBase GetOriginalMethod()
		{
			var attr = containerAttributes;
			if (attr.originalType == null || attr.methodName == null) return null;
			if (attr.parameter == null)
				return attr.originalType.GetMethod(attr.methodName, AccessTools.all);
			return attr.originalType.GetMethod(attr.methodName, AccessTools.all, null, attr.parameter, null);
		}

		T RunMethod<S, T>(T defaultIfNotExisting, params object[] parameters)
		{
			var name = typeof(S).Name.Replace("Harmony", "");

			var paramList = new List<object> { instance };
			paramList.AddRange(parameters);
			var paramTypes = AccessTools.GetTypes(paramList.ToArray());
			var method = PatchTools.GetPatchMethod<S>(container, name, paramTypes);
			if (method != null && typeof(T).IsAssignableFrom(method.ReturnType))
				return (T)method.Invoke(null, paramList.ToArray());

			method = PatchTools.GetPatchMethod<S>(container, name, new Type[] { typeof(HarmonyInstance) });
			if (method != null && typeof(T).IsAssignableFrom(method.ReturnType))
				return (T)method.Invoke(null, new object[] { instance });

			method = PatchTools.GetPatchMethod<S>(container, name, Type.EmptyTypes);
			if (method != null)
			{
				if (typeof(T).IsAssignableFrom(method.ReturnType))
					return (T)method.Invoke(null, Type.EmptyTypes);

				method.Invoke(null, Type.EmptyTypes);
				return defaultIfNotExisting;
			}

			return defaultIfNotExisting;
		}
	}
}