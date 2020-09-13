using Mono.Cecil;
using MonoMod.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace HarmonyLib
{
	internal static class HarmonySharedState
	{
		const string name = "HarmonySharedState";
		static readonly object locker = new object();
		internal const int internalVersion = 100;
		internal static int actualVersion = -1;

		static Dictionary<MethodBase, byte[]> GetState()
		{
			lock (locker)
			{
				var assembly = SharedStateAssembly();
				if (assembly is null)
				{
					CreateModule();
					assembly = SharedStateAssembly();
					if (assembly is null) throw new Exception("Cannot find or create harmony shared state");
				}

				var type = assembly.GetType(name);

				var versionField = type.GetField("version");
				if (versionField is null) throw new Exception("Cannot find harmony state version field");
				actualVersion = (int)versionField.GetValue(null);

				var stateField = type.GetField("state");
				if (stateField is null) throw new Exception("Cannot find harmony state field");
				if (stateField.GetValue(null) is null) stateField.SetValue(null, new Dictionary<MethodBase, byte[]>());

				return (Dictionary<MethodBase, byte[]>)stateField.GetValue(null);
			}
		}

		static void CreateModule()
		{
			var parameters = new ModuleParameters()
			{
				Kind = ModuleKind.Dll,
				ReflectionImporterProvider = MMReflectionImporter.Provider
			};
			using var module = ModuleDefinition.CreateModule(name, parameters);
			var attr = Mono.Cecil.TypeAttributes.Public | Mono.Cecil.TypeAttributes.Abstract | Mono.Cecil.TypeAttributes.Sealed | Mono.Cecil.TypeAttributes.Class;
			var typedef = new TypeDefinition("", name, attr) { BaseType = module.TypeSystem.Object };
			module.Types.Add(typedef);

			typedef.Fields.Add(new FieldDefinition(
				"state",
				Mono.Cecil.FieldAttributes.Public | Mono.Cecil.FieldAttributes.Static,
				module.ImportReference(typeof(Dictionary<MethodBase, byte[]>))
			));

			var versionFieldDef = new FieldDefinition(
				"version",
				Mono.Cecil.FieldAttributes.Public | Mono.Cecil.FieldAttributes.Static,
				module.ImportReference(typeof(int))
			)
			{ Constant = internalVersion };
			typedef.Fields.Add(versionFieldDef);

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
			var state = GetState();
			byte[] bytes;
			lock (state) bytes = state.GetValueSafe(method);
			if (bytes is null) return null;
			return PatchInfoSerialization.Deserialize(bytes);
		}

		internal static IEnumerable<MethodBase> GetPatchedMethods()
		{
			var state = GetState();
			lock (state) return state.Keys.ToArray();
		}

		internal static void UpdatePatchInfo(MethodBase method, PatchInfo patchInfo)
		{
			var bytes = patchInfo.Serialize();
			var state = GetState();
			lock (state) state[method] = bytes;
		}
	}
}
