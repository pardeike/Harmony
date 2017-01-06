using System;
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
			module.GetTypes().ToList().ForEach(ownerType =>
			{
				var baseMethodInfos = ownerType.GetHarmonyMethods();
				if (baseMethodInfos != null && baseMethodInfos.Count() > 0)
				{
					var baseInfo = HarmonyMethod.Merge(baseMethodInfos);

					RunMethod<HarmonyPrepare, bool>(ownerType);

					MethodInfo original = RunMethod<HarmonyTargetMethod, MethodInfo>(ownerType);
					if (original == null)
						original = GetOriginalMethod(ownerType, baseInfo);
					if (original != null)
					{
						HarmonyMethod prefixInfo = baseInfo.Clone();
						HarmonyMethod postfixInfo = baseInfo.Clone();
						PatchTools.GetPatches(ownerType, original, out prefixInfo.method, out postfixInfo.method);

						if (prefixInfo.method != null)
						{
							var prefixAttributes = prefixInfo.method.GetHarmonyMethods();
							baseInfo.Merge(HarmonyMethod.Merge(prefixAttributes)).CopyTo(prefixInfo);
						}

						if (postfixInfo.method != null)
						{
							var postfixAttributes = postfixInfo.method.GetHarmonyMethods();
							baseInfo.Merge(HarmonyMethod.Merge(postfixAttributes)).CopyTo(postfixInfo);
						}

						Patch(original, prefixInfo, postfixInfo);
					}
				}
			});
		}

		public T RunMethod<S, T>(Type type)
		{
			var name = typeof(S).Name.Replace("Harmony", "");

			var method = PatchTools.GetPatchMethod<S>(type, name, new Type[] { typeof(HarmonyInstance) });
			if (method != null && typeof(T).IsAssignableFrom(method.ReturnType))
				return (T)method.Invoke(null, new object[] { instance });

			method = PatchTools.GetPatchMethod<S>(type, name, Type.EmptyTypes);
			if (method != null && typeof(T).IsAssignableFrom(method.ReturnType))
				return (T)method.Invoke(null, Type.EmptyTypes);

			return default(T);
		}

		private static MethodInfo GetOriginalMethod(Type ownerType, HarmonyMethod baseInfo)
		{
			if (baseInfo.originalType == null) throw new ArgumentException("HarmonyPatch(type) not specified for class " + ownerType.FullName);
			if (baseInfo.methodName == null) throw new ArgumentException("HarmonyPatch(string) not specified for class " + ownerType.FullName);

			if (baseInfo.parameter == null)
			{
				var original = baseInfo.originalType.GetMethod(baseInfo.methodName, AccessTools.all);
				if (original == null)
					throw new ArgumentException("No method found for " + baseInfo.originalType.FullName + "." + baseInfo.methodName);
				return original;
			}
			else
			{
				var original = baseInfo.originalType.GetMethod(baseInfo.methodName, AccessTools.all, null, baseInfo.parameter, null);
				if (original == null)
				{
					var paramList = "(" + string.Join(",", baseInfo.parameter.Select(t => t.FullName).ToArray()) + ")";
					throw new ArgumentException("No method found for " + baseInfo.originalType.FullName + "." + baseInfo.methodName + paramList);
				}
				return original;
			}
		}

		public void Patch(MethodInfo original, HarmonyMethod prefix, HarmonyMethod postfix)
		{
			patchCallback(original, prefix, postfix);
		}
	}
}