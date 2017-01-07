using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Harmony
{
	public class Patcher
	{
		readonly HarmonyInstance instance;
		readonly PatchCallback patchCallback;

		public Patcher(HarmonyInstance instance, PatchCallback patchCallback)
		{
			this.instance = instance;
			this.patchCallback = patchCallback;
		}

		public void PatchAll(Module module)
		{
			module.GetTypes().Do(type =>
			{
				var baseMethodInfos = type.GetHarmonyMethods();
				if (baseMethodInfos != null && baseMethodInfos.Count() > 0)
				{
					var info = HarmonyMethod.Merge(baseMethodInfos);
					var processor = new PatchProcessor(instance, type, info);
					processor.Patch(patchCallback);
				}
			});
		}

		public void Patch(MethodInfo original, HarmonyMethod prefix, HarmonyMethod postfix)
		{
			var processor = new PatchProcessor(instance, original, prefix, postfix);
			processor.Patch(patchCallback);
		}
	}

	public class PatchProcessor
	{
		readonly HarmonyInstance instance;

		readonly Type container;
		readonly HarmonyMethod containerAttributes;

		MethodInfo targetMethod;
		HarmonyMethod prefix;
		HarmonyMethod postfix;

		public PatchProcessor(HarmonyInstance instance, Type type, HarmonyMethod attributes)
		{
			this.instance = instance;
			container = type;
			containerAttributes = attributes;
			prefix = containerAttributes.Clone();
			postfix = containerAttributes.Clone();
			ProcessType();
		}

		public PatchProcessor(HarmonyInstance instance, MethodInfo targetMethod, HarmonyMethod prefix, HarmonyMethod postfix)
		{
			this.instance = instance;
			this.targetMethod = targetMethod;
			this.prefix = prefix;
			this.postfix = postfix;
		}

		public void Patch(PatchCallback patchCallback)
		{
			patchCallback(targetMethod, prefix, postfix);
		}

		bool CallPrepare()
		{
			if (targetMethod != null)
				return RunMethod<HarmonyPrepare, bool>(true, targetMethod);
			return RunMethod<HarmonyPrepare, bool>(true);
		}

		void ProcessType()
		{
			targetMethod = GetOriginalMethod();

			var patchable = CallPrepare();
			if (patchable)
			{
				if (targetMethod == null)
					targetMethod = RunMethod<HarmonyTargetMethod, MethodInfo>(null);
				if (targetMethod == null)
					throw new ArgumentException("No target method specified for class " + container.FullName);

				PatchTools.GetPatches(container, targetMethod, out prefix.method, out postfix.method);

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

		MethodInfo GetOriginalMethod()
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

			var paramList = new List<object>() { instance };
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