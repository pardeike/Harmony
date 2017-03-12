using Harmony.ILCopying;
using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace Harmony
{
	public static class DynamicTools
	{
		public static DynamicMethod CreateDynamicMethod(MethodBase original, string suffix)
		{
			if (original == null) throw new Exception("original cannot be null");
			var patchName = original.Name + suffix;
			patchName = patchName.Replace("<>", "");

			var parameters = original.GetParameters();
			var result = parameters.Types().ToList();
			if (original.IsStatic == false)
				result.Insert(0, typeof(object));
			var paramTypes = result.ToArray();

			var method = new DynamicMethod(
				patchName,
				MethodAttributes.Public | (original.IsStatic ? MethodAttributes.Static : 0),
				CallingConventions.Standard,
				AccessTools.GetReturnedType(original),
				paramTypes,
				original.DeclaringType,
				true
			);

			for (int i = 0; i < parameters.Length; i++)
				method.DefineParameter(i + 1, parameters[i].Attributes, parameters[i].Name);

			return method;
		}

		public static LocalBuilder[] DeclareLocalVariables(MethodBase original, ILGenerator il)
		{
			return original.GetMethodBody().LocalVariables.Select(lvi =>
			{
				var localBuilder = il.DeclareLocal(lvi.LocalType, lvi.IsPinned);
				Emitter.LogLastLocalVariable(il);
				return localBuilder;
			}).ToArray();
		}

		public static LocalBuilder DeclareLocalVariable(ILGenerator il, Type type)
		{
			if (type.IsByRef) type = type.GetElementType();

			if (AccessTools.isClass(type))
			{
				var v = il.DeclareLocal(type);
				Emitter.LogLastLocalVariable(il);
				Emitter.Emit(il, OpCodes.Ldnull);
				Emitter.Emit(il, OpCodes.Stloc, v);
				return v;
			}
			if (AccessTools.isStruct(type))
			{
				var v = il.DeclareLocal(type);
				Emitter.LogLastLocalVariable(il);
				Emitter.Emit(il, OpCodes.Ldloca, v);
				Emitter.Emit(il, OpCodes.Initobj, type);
				return v;
			}
			if (AccessTools.isValue(type))
			{
				var v = il.DeclareLocal(type);
				Emitter.LogLastLocalVariable(il);
				if (type == typeof(float))
					Emitter.Emit(il, OpCodes.Ldc_R4, (float)0);
				else if (type == typeof(double))
					Emitter.Emit(il, OpCodes.Ldc_R8, (double)0);
				else if (type == typeof(long))
					Emitter.Emit(il, OpCodes.Ldc_I8, (long)0);
				else
					Emitter.Emit(il, OpCodes.Ldc_I4, 0);
				Emitter.Emit(il, OpCodes.Stloc, v);
				return v;
			}
			return null;
		}

		public static void PrepareDynamicMethod(DynamicMethod method)
		{
			var m_CreateDynMethod = typeof(DynamicMethod).GetMethod("CreateDynMethod", BindingFlags.NonPublic | BindingFlags.Instance);
			m_CreateDynMethod.Invoke(method, new object[0]);
		}
	}
}