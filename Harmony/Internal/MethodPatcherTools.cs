using MonoMod.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace HarmonyLib
{
	internal class MethodPatcherTools
	{
		// Special parameter names that can be used in prefix, postfix and infix methods
		internal const string INSTANCE_PARAM = "__instance";
		internal const string ORIGINAL_METHOD_PARAM = "__originalMethod";
		internal const string ARGS_ARRAY_VAR = "__args";
		internal const string RESULT_VAR = "__result";
		internal const string RESULT_REF_VAR = "__resultRef";
		internal const string STATE_VAR = "__state";
		internal const string EXCEPTION_VAR = "__exception";
		internal const string RUN_ORIGINAL_VAR = "__runOriginal";
		internal const string PARAM_INDEX_PREFIX = "__";
		internal const string INSTANCE_FIELD_PREFIX = "___";

		internal static DynamicMethodDefinition CreateDynamicMethod(MethodBase original, string suffix, bool debug)
		{
			if (original is null) throw new ArgumentNullException(nameof(original));

			var patchName = $"{original.DeclaringType?.FullName ?? "GLOBALTYPE"}.{original.Name}{suffix}";
			patchName = patchName.Replace("<>", "");

			var parameters = original.GetParameters();
			var parameterTypes = new List<Type>();

			parameterTypes.AddRange(parameters.Types());
			if (original.IsStatic is false)
			{
				if (AccessTools.IsStruct(original.DeclaringType))
					parameterTypes.Insert(0, original.DeclaringType.MakeByRefType());
				else
					parameterTypes.Insert(0, original.DeclaringType);
			}

			var returnType = AccessTools.GetReturnedType(original);

			var method = new DynamicMethodDefinition(
				 patchName,
				 returnType,
				 [.. parameterTypes]
			);

			var offset = original.IsStatic ? 0 : 1;
			if (!original.IsStatic)
				method.Definition.Parameters[0].Name = "this";
			for (var i = 0; i < parameters.Length; i++)
			{
				var param = method.Definition.Parameters[i + offset];
				param.Attributes = (Mono.Cecil.ParameterAttributes)parameters[i].Attributes;
				param.Name = parameters[i].Name;
			}

			if (debug)
			{
				var parameterStrings = parameterTypes.Select(p => p.FullDescription()).ToList();
				if (parameterTypes.Count == method.Definition.Parameters.Count)
					for (var i = 0; i < parameterTypes.Count; i++)
						parameterStrings[i] += $" {method.Definition.Parameters[i].Name}";
				FileLog.Log($"### Replacement: static {returnType.FullDescription()} {original.DeclaringType?.FullName ?? "GLOBALTYPE"}::{patchName}({parameterStrings.Join()})");
			}

			return method;
		}

		internal static IEnumerable<(ParameterInfo info, string realName)> OriginalParameters(MethodInfo method)
		{
			var baseArgs = method.GetArgumentAttributes();
			if (method.DeclaringType is not null)
				baseArgs = baseArgs.Union(method.DeclaringType.GetArgumentAttributes());
			return method.GetParameters().Select(p =>
			{
				var arg = p.GetArgumentAttribute();
				if (arg != null)
					return (p, arg.OriginalName ?? p.Name);
				return (p, baseArgs.GetRealName(p.Name, null) ?? p.Name);
			});
		}

		internal static Dictionary<string, string> RealNames(MethodInfo method)
			 => OriginalParameters(method).ToDictionary(pair => pair.info.Name, pair => pair.realName);

		internal static LocalBuilder[] DeclareOriginalLocalVariables(ILGenerator il, MethodBase member)
		{
			var vars = member.GetMethodBody()?.LocalVariables;
			if (vars is null)
				return [];
			return vars.Select(lvi => il.DeclareLocal(lvi.LocalType, lvi.IsPinned)).ToArray();
		}

		internal static bool PrefixAffectsOriginal(MethodInfo fix)
		{
			if (fix.ReturnType == typeof(bool))
				return true;

			return MethodPatcherTools.OriginalParameters(fix).Any(pair =>
			{
				var p = pair.info;
				var name = pair.realName;
				var type = p.ParameterType;

				if (name == INSTANCE_PARAM) return false;
				if (name == ORIGINAL_METHOD_PARAM) return false;
				if (name == STATE_VAR) return false;

				if (p.IsOut || p.IsRetval) return true;
				if (type.IsByRef) return true;
				if (AccessTools.IsValue(type) is false && AccessTools.IsStruct(type) is false) return true;

				return false;
			});
		}

		static readonly MethodInfo m_GetMethodFromHandle1 = typeof(MethodBase).GetMethod("GetMethodFromHandle", [typeof(RuntimeMethodHandle)]);
		static readonly MethodInfo m_GetMethodFromHandle2 = typeof(MethodBase).GetMethod("GetMethodFromHandle", [typeof(RuntimeMethodHandle), typeof(RuntimeTypeHandle)]);
		internal static bool EmitOriginalBaseMethod(MethodBase original, Emitter emitter)
		{
			if (original is MethodInfo method)
				emitter.Emit(OpCodes.Ldtoken, method);
			else if (original is ConstructorInfo constructor)
				emitter.Emit(OpCodes.Ldtoken, constructor);
			else return false;

			var type = original.ReflectedType;
			if (type.IsGenericType) emitter.Emit(OpCodes.Ldtoken, type);
			emitter.Emit(OpCodes.Call, type.IsGenericType ? m_GetMethodFromHandle2 : m_GetMethodFromHandle1);
			return true;
		}

		internal static OpCode LoadIndOpCodeFor(Type type)
		{
			if (type.IsEnum)
				return OpCodes.Ldind_I4;

			if (type == typeof(float)) return OpCodes.Ldind_R4;
			if (type == typeof(double)) return OpCodes.Ldind_R8;

			if (type == typeof(byte)) return OpCodes.Ldind_U1;
			if (type == typeof(ushort)) return OpCodes.Ldind_U2;
			if (type == typeof(uint)) return OpCodes.Ldind_U4;
			if (type == typeof(ulong)) return OpCodes.Ldind_I8;

			if (type == typeof(sbyte)) return OpCodes.Ldind_I1;
			if (type == typeof(short)) return OpCodes.Ldind_I2;
			if (type == typeof(int)) return OpCodes.Ldind_I4;
			if (type == typeof(long)) return OpCodes.Ldind_I8;

			return OpCodes.Ldind_Ref;
		}

		internal static OpCode StoreIndOpCodeFor(Type type)
		{
			if (type.IsEnum)
				return OpCodes.Stind_I4;

			if (type == typeof(float)) return OpCodes.Stind_R4;
			if (type == typeof(double)) return OpCodes.Stind_R8;

			if (type == typeof(byte)) return OpCodes.Stind_I1;
			if (type == typeof(ushort)) return OpCodes.Stind_I2;
			if (type == typeof(uint)) return OpCodes.Stind_I4;
			if (type == typeof(ulong)) return OpCodes.Stind_I8;

			if (type == typeof(sbyte)) return OpCodes.Stind_I1;
			if (type == typeof(short)) return OpCodes.Stind_I2;
			if (type == typeof(int)) return OpCodes.Stind_I4;
			if (type == typeof(long)) return OpCodes.Stind_I8;

			return OpCodes.Stind_Ref;
		}
	}
}
