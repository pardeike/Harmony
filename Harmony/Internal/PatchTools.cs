using MonoMod.Core;
using MonoMod.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace HarmonyLib
{
	internal static class PatchTools
	{
		private static readonly Dictionary<MethodBase, ICoreDetour> detours = [];
		internal static readonly string harmonyMethodFullName = typeof(HarmonyMethod).FullName;
		internal static readonly string harmonyAttributeFullName = typeof(HarmonyAttribute).FullName;
		internal static readonly string harmonyPatchAllFullName = typeof(HarmonyPatchAll).FullName;

		internal static void DetourMethod(MethodBase method, MethodBase replacement)
		{
			lock (detours)
			{
				if (detours.TryGetValue(method, out var detour))
					detour.Dispose();
				detours[method] = DetourFactory.Current.CreateDetour(method, replacement);
			}
		}

		internal static readonly MethodInfo m_GetExecutingAssemblyReplacementTranspiler = SymbolExtensions.GetMethodInfo(() => GetExecutingAssemblyTranspiler(null));
		internal static readonly MethodInfo m_GetExecutingAssembly = SymbolExtensions.GetMethodInfo(() => Assembly.GetExecutingAssembly());
		internal static readonly MethodInfo m_GetExecutingAssemblyReplacement = SymbolExtensions.GetMethodInfo(() => GetExecutingAssemblyReplacement());
		static Assembly GetExecutingAssemblyReplacement()
		{
			var frames = new StackTrace().GetFrames();
			if (frames?.Skip(1).FirstOrDefault() is { } frame && Harmony.GetMethodFromStackframe(frame) is { } original)
				return original.Module.Assembly;
			return Assembly.GetExecutingAssembly();
		}
		internal static IEnumerable<CodeInstruction> GetExecutingAssemblyTranspiler(IEnumerable<CodeInstruction> instructions) => instructions.MethodReplacer(m_GetExecutingAssembly, m_GetExecutingAssemblyReplacement);

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
#if NETCOREAPP || NETSTANDARD || NET5_0_OR_GREATER
			return AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
#else
			return AppDomain.CurrentDomain.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
#endif
		}

		internal static List<AttributePatch> GetPatchMethods(Type type)
		{
			return AccessTools.GetDeclaredMethods(type)
				.Select(AttributePatch.Create)
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
						if (string.IsNullOrEmpty(attr.methodName))
							return null;
						return AccessTools.DeclaredMethod(attr.declaringType, attr.methodName, attr.argumentTypes);

					case MethodType.Getter:
						if (string.IsNullOrEmpty(attr.methodName))
							return AccessTools.DeclaredIndexer(attr.declaringType, attr.argumentTypes).GetGetMethod(true);
						return AccessTools.DeclaredProperty(attr.declaringType, attr.methodName).GetGetMethod(true);

					case MethodType.Setter:
						if (string.IsNullOrEmpty(attr.methodName))
							return AccessTools.DeclaredIndexer(attr.declaringType, attr.argumentTypes).GetSetMethod(true);
						return AccessTools.DeclaredProperty(attr.declaringType, attr.methodName).GetSetMethod(true);

					case MethodType.Constructor:
						return AccessTools.DeclaredConstructor(attr.declaringType, attr.argumentTypes);

					case MethodType.StaticConstructor:
						return AccessTools.GetDeclaredConstructors(attr.declaringType)
							.Where(c => c.IsStatic)
							.FirstOrDefault();

					case MethodType.Enumerator:
						if (string.IsNullOrEmpty(attr.methodName))
							return null;
						var enumMethod = AccessTools.DeclaredMethod(attr.declaringType, attr.methodName, attr.argumentTypes);
						return AccessTools.EnumeratorMoveNext(enumMethod);

#if NET45_OR_GREATER || NETSTANDARD || NETCOREAPP
					case MethodType.Async:
						if (string.IsNullOrEmpty(attr.methodName))
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
