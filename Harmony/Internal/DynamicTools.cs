using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using MonoMod.Utils;

namespace HarmonyLib
{
	/// <summary>Creating dynamic methods</summary>
	internal static class DynamicTools
	{
		internal static DynamicMethodDefinition CreateDynamicMethod(MethodBase original, string suffix)
		{
			if (original == null) throw new ArgumentNullException(nameof(original));
			var patchName = original.Name + suffix;
			patchName = patchName.Replace("<>", "");

			var parameters = original.GetParameters();
			var parameterTypes = parameters.Types().ToList();
			if (original.IsStatic == false)
			{
				if (AccessTools.IsStruct(original.DeclaringType))
					parameterTypes.Insert(0, original.DeclaringType.MakeByRefType());
				else
					parameterTypes.Insert(0, original.DeclaringType);
			}

			var firstArgIsReturnBuffer = NativeThisPointer.NeedsNativeThisPointerFix(original);
			if (firstArgIsReturnBuffer)
				parameterTypes.Insert(0, typeof(IntPtr));
			var returnType = firstArgIsReturnBuffer ? typeof(void) : AccessTools.GetReturnedType(original);

			var method = new DynamicMethodDefinition(
				patchName,
				returnType,
				parameterTypes.ToArray()
			);

#if NETSTANDARD2_0 || NETCOREAPP2_0
#else
			var offset = (original.IsStatic ? 0 : 1) + (firstArgIsReturnBuffer ? 1 : 0);
			for (var i = 0; i < parameters.Length; i++)
			{
				var param = method.Definition.Parameters[i + offset];
				param.Attributes = (Mono.Cecil.ParameterAttributes)parameters[i].Attributes;
				param.Name = parameters[i].Name;
			}
#endif

			return method;
		}

		internal static LocalBuilder[] DeclareLocalVariables(MethodBase original, ILGenerator generator)
		{
			var vars = original.GetMethodBody()?.LocalVariables;
			if (vars == null)
				return new LocalBuilder[0];
			return vars.Select(lvi => generator.DeclareLocal(lvi.LocalType, lvi.IsPinned)).ToArray();
		}

		internal static LocalBuilder DeclareLocalVariable(ILGenerator generator, Type type)
		{
			if (type.IsByRef) type = type.GetElementType();
			if (type.IsEnum) type = Enum.GetUnderlyingType(type);

			if (AccessTools.IsClass(type))
			{
				var v = generator.DeclareLocal(type);
				Emitter.Emit(generator, OpCodes.Ldnull);
				Emitter.Emit(generator, OpCodes.Stloc, v);
				return v;
			}
			if (AccessTools.IsStruct(type))
			{
				var v = generator.DeclareLocal(type);
				Emitter.Emit(generator, OpCodes.Ldloca, v);
				Emitter.Emit(generator, OpCodes.Initobj, type);
				return v;
			}
			if (AccessTools.IsValue(type))
			{
				var v = generator.DeclareLocal(type);
				if (type == typeof(float))
					Emitter.Emit(generator, OpCodes.Ldc_R4, (float)0);
				else if (type == typeof(double))
					Emitter.Emit(generator, OpCodes.Ldc_R8, (double)0);
				else if (type == typeof(long) || type == typeof(ulong))
					Emitter.Emit(generator, OpCodes.Ldc_I8, (long)0);
				else
					Emitter.Emit(generator, OpCodes.Ldc_I4, 0);
				Emitter.Emit(generator, OpCodes.Stloc, v);
				return v;
			}
			return null;
		}
	}
}