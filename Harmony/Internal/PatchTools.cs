using MonoMod.Core;
using MonoMod.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace HarmonyLib
{
	internal static class PatchTools
	{
		private static readonly Dictionary<MethodBase, ICoreDetour> detours = new();

		internal static void DetourMethod(MethodBase method, MethodBase replacement)
		{
			lock (detours)
			{
				if (detours.TryGetValue(method, out var detour))
					detour.Dispose();
				detours[method] = DetourFactory.Current.CreateDetour(method, replacement);
			}
		}

		public static MethodInfo CreateMethod(string name, Type returnType, List<KeyValuePair<string, Type>> parameters, Action<ILGenerator> generator)
		{
			var parameterTypes = parameters.Select(p => p.Value).ToArray();
			var dynamicMethod = new DynamicMethodDefinition(name, returnType, parameterTypes);

			for (var i = 0; i < parameters.Count; i++)
				dynamicMethod.Definition.Parameters[i].Name = parameters[i].Key;

			var il = dynamicMethod.GetILGenerator();
			generator(il);

			return dynamicMethod.Generate();
		}

		internal static MethodInfo GetPatchMethod(Type patchType, string attributeName)
		{
			var method = patchType.GetMethods(AccessTools.all)
				.FirstOrDefault(m => m.GetCustomAttributes(true).Any(a => a.GetType().FullName == attributeName));
			if (method is null)
			{
				// not-found is common and normal case, don't use AccessTools which will generate not-found warnings
				var methodName = attributeName.Replace("HarmonyLib.Harmony", "");
				method = patchType.GetMethod(methodName, AccessTools.all);
			}
			return method;
		}

		internal static AssemblyBuilder DefineDynamicAssembly(string name)
		{
			var assemblyName = new AssemblyName(name);
#if NETCOREAPP2_0 || NETCOREAPP3_0 || NETCOREAPP3_1 || NETSTANDARD2_0 || NET50_OR_GREATER
			return AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
#else
			return AppDomain.CurrentDomain.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
#endif
		}

		internal static List<AttributePatch> GetPatchMethods(Type type)
		{
			return AccessTools.GetDeclaredMethods(type)
				.Select(method => AttributePatch.Create(method))
				.Where(attributePatch => attributePatch is not null)
				.ToList();
		}

		internal static MethodBase GetOriginalMethod(this HarmonyMethod attr)
		{
			try
			{
				switch (attr.methodType)
				{
					case MethodType.Normal:
						if (attr.methodName is null)
							return null;
						return AccessTools.DeclaredMethod(attr.declaringType, attr.methodName, attr.argumentTypes);

					case MethodType.Getter:
						if (attr.methodName is null)
							return AccessTools.DeclaredIndexer(attr.declaringType, attr.argumentTypes).GetGetMethod(true);
						return AccessTools.DeclaredProperty(attr.declaringType, attr.methodName).GetGetMethod(true);

					case MethodType.Setter:
						if (attr.methodName is null)
							return AccessTools.DeclaredIndexer(attr.declaringType, attr.argumentTypes).GetSetMethod(true);
						return AccessTools.DeclaredProperty(attr.declaringType, attr.methodName).GetSetMethod(true);

					case MethodType.Constructor:
						return AccessTools.DeclaredConstructor(attr.declaringType, attr.argumentTypes);

					case MethodType.StaticConstructor:
						return AccessTools.GetDeclaredConstructors(attr.declaringType)
							.Where(c => c.IsStatic)
							.FirstOrDefault();

					case MethodType.Enumerator:
						if (attr.methodName is null)
							return null;
						var enumMethod = AccessTools.DeclaredMethod(attr.declaringType, attr.methodName, attr.argumentTypes);
						return AccessTools.EnumeratorMoveNext(enumMethod);

#if NET45_OR_GREATER
					case MethodType.Async:
						if (attr.methodName is null)
							return null;
						var asyncMethod = AccessTools.DeclaredMethod(attr.declaringType, attr.methodName, attr.argumentTypes);
						return AccessTools.AsyncMoveNext(asyncMethod);
#endif
				}
			}
			catch (AmbiguousMatchException ex)
			{
				throw new HarmonyException($"Ambiguous match for HarmonyMethod[{attr.Description()}]", ex.InnerException ?? ex);
			}

			return null;
		}
	}
}
