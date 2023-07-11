using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace HarmonyLib
{
	internal static class InternalPatches
	{
		internal static void Patch()
		{
			var getExecutingAssemblyMethod = SymbolExtensions.GetMethodInfo(() => Assembly.GetExecutingAssembly());
			if (PatchProcessor.ReadMethodBody(getExecutingAssemblyMethod).Any() == false)
				return;

			var getExecutingAssemblyPostfix = SymbolExtensions.GetMethodInfo((Assembly dummy) => GetExecutingAssemblyPostfix(ref dummy));

			var empty = new List<MethodInfo>();
			var patcher = new MethodPatcher(getExecutingAssemblyMethod, null, empty, new List<MethodInfo> { getExecutingAssemblyPostfix }, empty, empty, false);
			var replacement = patcher.CreateReplacement(out _);
			PatchTools.DetourMethod(getExecutingAssemblyMethod, replacement);
		}

		internal static void GetExecutingAssemblyPostfix(ref Assembly __result)
		{
			var frame = new StackTrace().GetFrames()?.Skip(2).FirstOrDefault();
			var patch = HarmonySharedState.FindReplacement(frame) ?? frame?.GetMethod();
			if (patch is MethodInfo methodInfo)
				patch = Harmony.GetOriginalMethod(methodInfo) ?? patch;
			if (patch != null)
				__result = patch.Module.Assembly;
		}
	}
}
