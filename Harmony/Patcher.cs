using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace Harmony
{
	public class Patcher
	{
		public static void PatchAll(Module module)
		{
			module.GetTypes().ToList()
				.ForEach(type =>
				{
					var attrList = type.GetCustomAttributes(true)
						.Where(attr => attr is HarmonyPatch)
						.Cast<HarmonyPatch>().ToList();
					if (attrList != null && attrList.Count() > 0)
					{
						var prepare = PatchTools.GetPatchMethod<HarmonyPrepare>(type, "Prepare", Type.EmptyTypes);
						if (prepare != null)
							prepare.Invoke(null, Type.EmptyTypes);

						var info = HarmonyPatch.Merge(attrList);
						if (info.type != null || info.methodName != null || info.parameter != null)
						{
							if (info.type == null) throw new ArgumentException("HarmonyPatch(type) not specified for class " + type.FullName);
							if (info.methodName == null) throw new ArgumentException("HarmonyPatch(string) not specified for class " + type.FullName);
							if (info.parameter == null) throw new ArgumentException("HarmonyPatch(Type[]) not specified for class " + type.FullName);
							PatchTools.Patch(type, info.type, info.methodName, info.parameter);
						}
					}
				});
		}
	}

	public class PatchedMethod
	{
		static List<PatchedMethod> patchedMethods = new List<PatchedMethod>();

		MethodInfo original;
		List<MethodInfo> prepatches;
		List<MethodInfo> postpatches;

		DynamicMethod copy;
		MethodInfo copyDelegate;

		DynamicMethod wrapper;
		MethodInfo wrapperDelegate;

		PatchedMethod(MethodInfo method)
		{
			original = method;

			copy = PatchTools.CreateMethodCopy(method);
			if (copy == null) throw new MissingMethodException("Cannot create copy of " + method);
			copyDelegate = PatchTools.PrepareDynamicMethod(original, copy);

			prepatches = new List<MethodInfo>();
			postpatches = new List<MethodInfo>();
		}

		public static void Patch(MethodInfo original, MethodInfo prepatch, MethodInfo postpatch)
		{
			var patchInfo = patchedMethods.FirstOrDefault(pm => pm.original == original);
			if (patchInfo == null)
				patchInfo = new PatchedMethod(original);
			patchInfo.prepatches.Add(prepatch);
			patchInfo.postpatches.Add(postpatch);
			patchedMethods.Add(patchInfo);

			patchInfo.wrapper = PatchTools.CreatePatchWrapper(original, patchInfo);
			patchInfo.wrapperDelegate = PatchTools.PrepareDynamicMethod(original, patchInfo.wrapper);

			PatchTools.Detour(original, patchInfo.wrapperDelegate);
		}

		public MethodInfo GetOriginalCopy()
		{
			return copyDelegate;
		}

		public List<MethodInfo> GetPrefixPatches()
		{
			return prepatches.Where(p => p != null).ToList();
		}

		public List<MethodInfo> GetPostfixPatches()
		{
			return postpatches.Where(p => p != null).ToList();
		}
	}
}