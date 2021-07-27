using Mono.Cecil;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace HarmonyLib
{
	internal static class HarmonySharedState
	{
		const string name = "HarmonySharedState";
		static readonly Mutex mutex = new Mutex(false, name);
		static Dictionary<MethodBase, byte[]> state = null;
		static Dictionary<MethodInfo, MethodBase> originals = null;

		internal const int internalVersion = 101;
		internal static int actualVersion = -1;

		// typeof(StackFrame).methodAddress
		private static FieldInfo methodAddress = null;
		
		static T WithState<T>(Func<T> action)
		{
			T result = default;
			var acquired = false;
			try
			{
				_ = mutex.WaitOne();
				acquired = true;

				if (state is null)
				{
					DetourHelper.Runtime.OnMethodCompiled += OnCompileMethod;

					var type = CreateSharedStateType();

					var versionField = type.GetField("version");
					if ((int)versionField.GetValue(null) == 0)
						versionField.SetValue(null, internalVersion);
					actualVersion = (int)versionField.GetValue(null);

					var stateField = type.GetField("state");
					if (stateField.GetValue(null) is null)
						stateField.SetValue(null, new Dictionary<MethodBase, byte[]>());

					var originalsField = type.GetField("originals");
					if (originalsField != null && originalsField.GetValue(null) is null)
						originalsField.SetValue(null, new Dictionary<MethodInfo, MethodBase>());

					state = (Dictionary<MethodBase, byte[]>)stateField.GetValue(null);

					originals = new Dictionary<MethodInfo, MethodBase>();
					if (originalsField != null) // may not exist in older versions
						originals = (Dictionary<MethodInfo, MethodBase>)originalsField.GetValue(null);
				}

				result = action();
			}
			finally
			{
				if (acquired)
					mutex.ReleaseMutex();
			}
			return result;
		}

		static void OnCompileMethod(MethodBase method, IntPtr codeStart, ulong codeLen)
		{
			if (method == null) return;
			var info = GetPatchInfo(method);
			if (info == null) return;
			PatchFunctions.UpdateRecompiledMethod(method, codeStart, info);
		}

		static Type CreateSharedStateType()
		{
			using var module = ModuleDefinition.CreateModule(name, new ModuleParameters() { Kind = ModuleKind.Dll, ReflectionImporterProvider = MMReflectionImporter.Provider });
			var attr = Mono.Cecil.TypeAttributes.Public | Mono.Cecil.TypeAttributes.Abstract | Mono.Cecil.TypeAttributes.Sealed | Mono.Cecil.TypeAttributes.Class;
			var typedef = new TypeDefinition("", name, attr) { BaseType = module.TypeSystem.Object };
			module.Types.Add(typedef);

			typedef.Fields.Add(new FieldDefinition(
				"state",
				Mono.Cecil.FieldAttributes.Public | Mono.Cecil.FieldAttributes.Static,
				module.ImportReference(typeof(Dictionary<MethodBase, byte[]>))
			));

			typedef.Fields.Add(new FieldDefinition(
				"originals",
				Mono.Cecil.FieldAttributes.Public | Mono.Cecil.FieldAttributes.Static,
				module.ImportReference(typeof(Dictionary<MethodInfo, MethodBase>))
			));

			typedef.Fields.Add(new FieldDefinition(
				"version",
				Mono.Cecil.FieldAttributes.Public | Mono.Cecil.FieldAttributes.Static,
				module.ImportReference(typeof(int))
			));

			return ReflectionHelper.Load(module).GetType(name);
		}

		internal static PatchInfo GetPatchInfo(MethodBase method)
		{
			return WithState(() =>
			{
				var bytes = state.GetValueSafe(method);
				if (bytes is null) return null;
				return PatchInfoSerialization.Deserialize(bytes);
			});
		}

		internal static IEnumerable<MethodBase> GetPatchedMethods()
		{
			return WithState(() =>
			{
				return state.Keys.ToArray();
			});
		}

		internal static void UpdatePatchInfo(MethodBase original, MethodInfo replacement, PatchInfo patchInfo)
		{
			var bytes = patchInfo.Serialize();
			_ = WithState<object>(() =>
			{
				state[original] = bytes;
				originals[replacement] = original;
				return null;
			});
		}

		internal static MethodBase GetOriginal(MethodInfo replacement)
		{
			return WithState(() =>
			{
				return originals.GetValueSafe(replacement);
			});
		}

		internal static MethodBase FindReplacement(StackFrame frame)
		{
			methodAddress ??= typeof(StackFrame).GetField("methodAddress", BindingFlags.Instance | BindingFlags.NonPublic);

			var frameMethod = frame.GetMethod();
			var methodStart = 0L;
			
			if (frameMethod is null)
			{
				if (methodAddress == null) 
					return null;

				methodStart = (long) methodAddress.GetValue(frame);
			}
			else
			{
				var baseMethod = DetourHelper.Runtime.GetIdentifiable(frameMethod);
				methodStart = baseMethod.GetNativeStart().ToInt64();
			}

			// Failed to find any usable method, if `frameMethod` is null, we can not find any 
			// method from the stacktrace.
			if (methodStart == 0) 
				return frameMethod;
			
			return WithState(() =>
			{
				return originals.Keys
					.FirstOrDefault(replacement => replacement.GetNativeStart().ToInt64() == methodStart);
			});
		}
	}
}
