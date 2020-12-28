using Mono.Cecil;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace HarmonyLib
{
	internal static class HarmonySharedState
	{
		const string name = "HarmonySharedState";
		static readonly object locker = new object();
		internal const int internalVersion = 101;
		internal static int actualVersion = -1;

		struct Info
		{
			internal Dictionary<MethodBase, byte[]> state;
			internal Dictionary<MethodInfo, MethodBase> originals;
		}

		static Info GetState()
		{
			lock (locker)
			{
				var assembly = SharedStateAssembly();
				if (assembly is null)
				{
					CreateModule();
					assembly = SharedStateAssembly();
				}

				var type = assembly.GetType(name);

				var versionField = type.GetField("version");
				if ((int)versionField.GetValue(null) == 0) versionField.SetValue(null, internalVersion);
				actualVersion = (int)versionField.GetValue(null);

				var stateField = type.GetField("state");
				if (stateField.GetValue(null) is null) stateField.SetValue(null, new Dictionary<MethodBase, byte[]>());
				var state = (Dictionary<MethodBase, byte[]>)stateField.GetValue(null);

				var originals = new Dictionary<MethodInfo, MethodBase>();
				var originalsField = type.GetField("originals");
				if (actualVersion >= internalVersion)
				{
					if (originalsField.GetValue(null) is null) originalsField.SetValue(null, new Dictionary<MethodInfo, MethodBase>());
					originals = (Dictionary<MethodInfo, MethodBase>)originalsField.GetValue(null);
				}

				return new Info { state = state, originals = originals };
			}
		}

		static void CreateModule()
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

			_ = ReflectionHelper.Load(module);
		}

		static Assembly SharedStateAssembly()
		{
			return AppDomain.CurrentDomain.GetAssemblies()
				.Where(a => a.FullName.StartsWith("Microsoft.VisualStudio") is false)
				.FirstOrDefault(a => a.GetName().Name.Contains(name));
		}

		internal static PatchInfo GetPatchInfo(MethodBase method)
		{
			var state = GetState().state;
			byte[] bytes;
			lock (state) bytes = state.GetValueSafe(method);
			if (bytes is null) return null;
			return PatchInfoSerialization.Deserialize(bytes);
		}

		internal static IEnumerable<MethodBase> GetPatchedMethods()
		{
			var state = GetState().state;
			lock (state) return state.Keys.ToArray();
		}

		internal static void UpdatePatchInfo(MethodBase original, MethodInfo replacement, PatchInfo patchInfo)
		{
			var bytes = patchInfo.Serialize();
			var info = GetState();
			lock (info.state) info.state[original] = bytes;
			lock (info.originals) info.originals[replacement] = original;
		}

		internal static MethodBase GetOriginal(MethodInfo replacement)
		{
			var info = GetState();
			lock (info.originals) return info.originals.GetValueSafe(replacement);
		}

		internal static MethodInfo FindReplacement(StackFrame frame)
		{
			var methodAddress = AccessTools.Field(typeof(StackFrame), "methodAddress");
			if (methodAddress == null) return null;
			var framePtr = (long)methodAddress.GetValue(frame);
			var info = GetState();
			lock (info.originals)
			{
				return info.originals.Keys
					.FirstOrDefault(replacement => DetourHelper.GetNativeStart(replacement).ToInt64() == framePtr);
			}
		}
	}
}
