using MonoMod.Core.Platforms;
using System;
using System.Reflection;
using System.Threading;

namespace HarmonyLib
{
	// On .NET 11 the runtime compiles a second, runtime-async body for Task/ValueTask methods. MonoMod detours only the ordinary body,
	// so callers compiled with runtime-async bypass the patches (https://github.com/MonoMod/MonoMod/issues/344). This only reports it.
	internal static class RuntimeAsyncCompileWatch
	{
		static int subscribed;

		internal static void EnsureSubscribed()
		{
			try
			{
				if (Environment.Version.Major < 11) return;
				if (Interlocked.Exchange(ref subscribed, 1) != 0) return;
				PlatformTriple.Current.Runtime.OnMethodCompiled += OnMethodCompiled;
			}
			catch
			{
			}
		}

		internal static void OnMethodCompiled(RuntimeMethodHandle methodHandle, MethodBase method, IntPtr codeStart, IntPtr codeRw, ulong codeSize)
		{
			try
			{
				if (IsUnpatchedVariantCompile(methodHandle, method, HarmonySharedState.IsPatched) == false) return;
				if (Harmony.DEBUG)
					FileLog.Log($"Runtime-async callers bypass the patches on {method.FullDescription()}: " +
						"the runtime compiled its async variant, which is not patched (see https://github.com/MonoMod/MonoMod/issues/344)");
			}
			catch
			{
			}
		}

		internal static bool IsUnpatchedVariantCompile(RuntimeMethodHandle methodHandle, MethodBase method, Func<MethodBase, bool> isPatched)
		{
			if (method is null || method.IsGenericMethod) return false;
			if (method.DeclaringType is { IsGenericType: true }) return false;
			if (method.MethodHandle.Value == methodHandle.Value) return false;
			return isPatched(method);
		}
	}
}
