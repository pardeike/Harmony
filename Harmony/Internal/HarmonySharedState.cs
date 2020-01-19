using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace HarmonyLib
{
	internal static class HarmonySharedState
	{
		static readonly string name = "HarmonySharedState";
		internal static readonly int internalVersion = 100;
		internal static int actualVersion = -1;

		static Dictionary<MethodBase, byte[]> GetState()
		{
			lock (name)
			{
				var assembly = SharedStateAssembly();
				if (assembly == null)
				{
					var assemblyBuilder = PatchTools.DefineDynamicAssembly(name);
					var moduleBuilder = assemblyBuilder.DefineDynamicModule(name);
					var typeAttributes = TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Sealed | TypeAttributes.Abstract;
					var typeBuilder = moduleBuilder.DefineType(name, typeAttributes);
					typeBuilder.DefineField("state", typeof(Dictionary<MethodBase, byte[]>), FieldAttributes.Static | FieldAttributes.Public);
					typeBuilder.DefineField("version", typeof(int), FieldAttributes.Static | FieldAttributes.Public).SetConstant(internalVersion);
#if NETSTANDARD2_0
					typeBuilder.CreateTypeInfo().AsType();
#else
					typeBuilder.CreateType();
#endif

					assembly = SharedStateAssembly();
					if (assembly == null) throw new Exception("Cannot find or create harmony shared state");
				}

				var type = assembly.GetType(name);

				var versionField = type.GetField("version");
				if (versionField == null) throw new Exception("Cannot find harmony state version field");
				actualVersion = (int)versionField.GetValue(null);

				var stateField = type.GetField("state");
				if (stateField == null) throw new Exception("Cannot find harmony state field");
				if (stateField.GetValue(null) == null) stateField.SetValue(null, new Dictionary<MethodBase, byte[]>());

				return (Dictionary<MethodBase, byte[]>)stateField.GetValue(null);
			}
		}

		static Assembly SharedStateAssembly()
		{
			return AppDomain.CurrentDomain.GetAssemblies()
				.Where(a => a.FullName.StartsWith("Microsoft.VisualStudio") == false)
				.FirstOrDefault(a => a.GetName().Name.Contains(name));
		}

		internal static PatchInfo GetPatchInfo(MethodBase method)
		{
			var bytes = GetState().GetValueSafe(method);
			if (bytes == null) return null;
			return PatchInfoSerialization.Deserialize(bytes);
		}

		internal static IEnumerable<MethodBase> GetPatchedMethods()
		{
			return GetState().Keys.AsEnumerable();
		}

		internal static void UpdatePatchInfo(MethodBase method, PatchInfo patchInfo)
		{
			GetState()[method] = patchInfo.Serialize();
		}
	}
}