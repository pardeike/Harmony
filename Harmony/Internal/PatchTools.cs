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

			if (AccessTools.IsMonoRuntime && HarmonyLib.Tools.isWindows == false)
			{
				var assemblyName = new AssemblyName("TempAssembly");

#if NET2 || NET35
				var assemblyBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
#else
				var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
#endif

				var moduleBuilder = assemblyBuilder.DefineDynamicModule("TempModule");
				var typeBuilder = moduleBuilder.DefineType("TempType", TypeAttributes.Public);

				var methodBuilder = typeBuilder.DefineMethod(name,
					 MethodAttributes.Public | MethodAttributes.Static,
					 returnType, parameterTypes);

				for (var i = 0; i < parameters.Count; i++)
					methodBuilder.DefineParameter(i + 1, ParameterAttributes.None, parameters[i].Key);

				generator(methodBuilder.GetILGenerator());

#if NETSTANDARD2_0
				var createdType = typeBuilder.CreateTypeInfo().AsType();
#else
				var createdType = typeBuilder.CreateType();
#endif
				return createdType.GetMethod(name, BindingFlags.Public | BindingFlags.Static);
			}

			var dynamicMethod = new DynamicMethodDefinition(name, returnType, parameterTypes);

			for (var i = 0; i < parameters.Count; i++)
				dynamicMethod.Definition.Parameters[i].Name = parameters[i].Key;

			generator(dynamicMethod.GetILGenerator());
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
			return [.. AccessTools.GetDeclaredMethods(type)
				.Select(AttributePatch.Create)
				.Where(attributePatch => attributePatch is not null)];
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
							return AccessTools.DeclaredIndexerGetter(attr.declaringType, attr.argumentTypes);
						return AccessTools.DeclaredPropertyGetter(attr.declaringType, attr.methodName);

					case MethodType.Setter:
						if (string.IsNullOrEmpty(attr.methodName))
							return AccessTools.DeclaredIndexerSetter(attr.declaringType, attr.argumentTypes);
						return AccessTools.DeclaredPropertySetter(attr.declaringType, attr.methodName);

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
					case MethodType.Finalizer:
						return AccessTools.DeclaredFinalizer(attr.declaringType);

					case MethodType.EventAdd:
						if (string.IsNullOrEmpty(attr.methodName))
							return null;
						return AccessTools.DeclaredEventAdder(attr.declaringType, attr.methodName);

					case MethodType.EventRemove:
						if (string.IsNullOrEmpty(attr.methodName))
							return null;
						return AccessTools.DeclaredEventRemover(attr.declaringType, attr.methodName);

					case MethodType.OperatorImplicit:
						return AccessTools.DeclaredMethod(attr.declaringType, "op_Implicit", attr.argumentTypes);

					case MethodType.OperatorExplicit:
						return AccessTools.DeclaredMethod(attr.declaringType, "op_Explicit", attr.argumentTypes);

					case MethodType.OperatorUnaryPlus:
						return AccessTools.DeclaredMethod(attr.declaringType, "op_UnaryPlus", attr.argumentTypes);

					case MethodType.OperatorUnaryNegation:
						return AccessTools.DeclaredMethod(attr.declaringType, "op_UnaryNegation", attr.argumentTypes);

					case MethodType.OperatorLogicalNot:
						return AccessTools.DeclaredMethod(attr.declaringType, "op_LogicalNot", attr.argumentTypes);

					case MethodType.OperatorOnesComplement:
						return AccessTools.DeclaredMethod(attr.declaringType, "op_OnesComplement", attr.argumentTypes);

					case MethodType.OperatorIncrement:
						return AccessTools.DeclaredMethod(attr.declaringType, "op_Increment", attr.argumentTypes);

					case MethodType.OperatorDecrement:
						return AccessTools.DeclaredMethod(attr.declaringType, "op_Decrement", attr.argumentTypes);

					case MethodType.OperatorTrue:
						return AccessTools.DeclaredMethod(attr.declaringType, "op_True", attr.argumentTypes);

					case MethodType.OperatorFalse:
						return AccessTools.DeclaredMethod(attr.declaringType, "op_False", attr.argumentTypes);

					case MethodType.OperatorAddition:
						return AccessTools.DeclaredMethod(attr.declaringType, "op_Addition", attr.argumentTypes);

					case MethodType.OperatorSubtraction:
						return AccessTools.DeclaredMethod(attr.declaringType, "op_Subtraction", attr.argumentTypes);

					case MethodType.OperatorMultiply:
						return AccessTools.DeclaredMethod(attr.declaringType, "op_Multiply", attr.argumentTypes);

					case MethodType.OperatorDivision:
						return AccessTools.DeclaredMethod(attr.declaringType, "op_Division", attr.argumentTypes);

					case MethodType.OperatorModulus:
						return AccessTools.DeclaredMethod(attr.declaringType, "op_Modulus", attr.argumentTypes);

					case MethodType.OperatorBitwiseAnd:
						return AccessTools.DeclaredMethod(attr.declaringType, "op_BitwiseAnd", attr.argumentTypes);

					case MethodType.OperatorBitwiseOr:
						return AccessTools.DeclaredMethod(attr.declaringType, "op_BitwiseOr", attr.argumentTypes);

					case MethodType.OperatorExclusiveOr:
						return AccessTools.DeclaredMethod(attr.declaringType, "op_ExclusiveOr", attr.argumentTypes);

					case MethodType.OperatorLeftShift:
						return AccessTools.DeclaredMethod(attr.declaringType, "op_LeftShift", attr.argumentTypes);

					case MethodType.OperatorRightShift:
						return AccessTools.DeclaredMethod(attr.declaringType, "op_RightShift", attr.argumentTypes);

					case MethodType.OperatorEquality:
						return AccessTools.DeclaredMethod(attr.declaringType, "op_Equality", attr.argumentTypes);

					case MethodType.OperatorInequality:
						return AccessTools.DeclaredMethod(attr.declaringType, "op_Inequality", attr.argumentTypes);

					case MethodType.OperatorGreaterThan:
						return AccessTools.DeclaredMethod(attr.declaringType, "op_GreaterThan", attr.argumentTypes);

					case MethodType.OperatorLessThan:
						return AccessTools.DeclaredMethod(attr.declaringType, "op_LessThan", attr.argumentTypes);

					case MethodType.OperatorGreaterThanOrEqual:
						return AccessTools.DeclaredMethod(attr.declaringType, "op_GreaterThanOrEqual", attr.argumentTypes);

					case MethodType.OperatorLessThanOrEqual:
						return AccessTools.DeclaredMethod(attr.declaringType, "op_LessThanOrEqual", attr.argumentTypes);

					case MethodType.OperatorComma:
						return AccessTools.DeclaredMethod(attr.declaringType, "op_Comma", attr.argumentTypes);
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
