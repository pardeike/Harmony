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
			module.GetTypes().ToList()
				.ForEach(type =>
				{
					var attrList = type.GetCustomAttributes(true)
						.Where(attr => attr is HarmonyPatch)
						.Cast<HarmonyPatch>().ToList();
					if (attrList != null && attrList.Count() > 0)
					{

						var prepare = PatchTools.GetPatchMethod<HarmonyPrepare>(type, "Prepare", new Type[] { typeof(HarmonyInstance) });
						if (prepare != null)
							prepare.Invoke(null, new object[] { instance });

						prepare = PatchTools.GetPatchMethod<HarmonyPrepare>(type, "Prepare", Type.EmptyTypes);
						if (prepare != null)
							prepare.Invoke(null, Type.EmptyTypes);

						var info = HarmonyPatch.Merge(attrList);
						if (info.type != null || info.methodName != null || info.parameter != null)
						{
							if (info.type == null) throw new ArgumentException("HarmonyPatch(type) not specified for class " + type.FullName);
							if (info.methodName == null) throw new ArgumentException("HarmonyPatch(string) not specified for class " + type.FullName);
							if (info.parameter == null) throw new ArgumentException("HarmonyPatch(Type[]) not specified for class " + type.FullName);

							var methodName = info.methodName;
							var paramTypes = info.parameter;
							var original = type.GetMethod(methodName, AccessTools.all, null, paramTypes, null);
							if (original == null)
							{
								var paramList = "(" + string.Join(",", paramTypes.Select(t => t.FullName).ToArray()) + ")";
								throw new ArgumentException("No method found for " + type.FullName + "." + methodName + paramList);
							}

							MethodInfo prefix;
							MethodInfo postfix;
							PatchTools.GetPatches(type, original, out prefix, out postfix);
							Patch(original, prefix, postfix);
						}
					}
				});
		}

		public void Patch(MethodInfo original, MethodInfo prefix, MethodInfo postfix)
		{
			patchCallback(original, prefix, postfix);
		}
	}
}