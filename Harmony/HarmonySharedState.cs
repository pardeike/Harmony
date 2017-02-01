using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace Harmony
{
	public static class HarmonySharedState
	{
		static readonly string name = "HarmonySharedState";

		static Dictionary<object, byte[]> GetState()
		{
			lock (name)
			{
				var assembly = AppDomain.CurrentDomain.GetAssemblies()
					.Where(a => a.GetName().Name.Contains(name))
					.FirstOrDefault();
				if (assembly == null)
				{
					var assemblyBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(new AssemblyName(name), AssemblyBuilderAccess.Run);
					var moduleBuilder = assemblyBuilder.DefineDynamicModule(name);
					var typeAttributes = TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Sealed | TypeAttributes.Abstract;
					var typeBuilder = moduleBuilder.DefineType(name, typeAttributes);
					typeBuilder.DefineField(name, typeof(Dictionary<object, byte[]>), FieldAttributes.Static | FieldAttributes.Public);
					typeBuilder.CreateType();

					assembly = AppDomain.CurrentDomain.GetAssemblies()
						.Where(a => a.GetName().Name == name)
						.FirstOrDefault();
					if (assembly == null) throw new Exception("Cannot find or create harmony shared state");
				}
				var field = assembly.GetType(name).GetField(name);
				if (field == null) throw new Exception("Cannot find harmony shared state field");
				if (field.GetValue(null) == null) field.SetValue(null, new Dictionary<object, byte[]>());
				return (Dictionary<object, byte[]>)field.GetValue(null);
			}
		}

		public static PatchInfo GetPatchInfo(MethodBase method)
		{
			var bytes = GetState().GetValueSafe(method);
			if (bytes == null) return null;
			return PatchInfoSerialization.Deserialize(bytes);
		}

		internal static void SetPatchInfo(MethodBase method, PatchInfo info)
		{
			GetState()[method] = PatchInfoSerialization.Serialize(info);
		}
	}
}