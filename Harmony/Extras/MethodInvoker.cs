using System;
using System.Reflection;
using System.Reflection.Emit;

namespace Harmony
{
	// Based on https://www.codeproject.com/Articles/14593/A-General-Fast-Method-Invoker

	public delegate object FastInvokeHandler(object target, object[] paramters);

	class MethodInvoker
	{
		public static FastInvokeHandler GetHandler(DynamicMethod methodInfo, Module module)
		{
			return Handler(methodInfo, module);
		}

		public static FastInvokeHandler GetHandler(MethodInfo methodInfo)
		{
			return Handler(methodInfo, methodInfo.DeclaringType.Module);
		}

		static FastInvokeHandler Handler(MethodInfo methodInfo, Module module)
		{
			var dynamicMethod = new DynamicMethod("FastInvoke_" + methodInfo.Name, typeof(object), new Type[] { typeof(object), typeof(object[]) }, module);
			var il = dynamicMethod.GetILGenerator();

			var ps = methodInfo.GetParameters();
			var paramTypes = new Type[ps.Length];
			for (int i = 0; i < paramTypes.Length; i++)
			{
				if (ps[i].ParameterType.IsByRef)
					paramTypes[i] = ps[i].ParameterType.GetElementType();
				else
					paramTypes[i] = ps[i].ParameterType;
			}

			var locals = new LocalBuilder[paramTypes.Length];
			for (int i = 0; i < paramTypes.Length; i++)
				locals[i] = il.DeclareLocal(paramTypes[i], true);

			for (int i = 0; i < paramTypes.Length; i++)
			{
				il.Emit(OpCodes.Ldarg_1);
				EmitFastInt(il, i);
				il.Emit(OpCodes.Ldelem_Ref);
				EmitCastToReference(il, paramTypes[i]);
				il.Emit(OpCodes.Stloc, locals[i]);
			}

			if (!methodInfo.IsStatic)
				il.Emit(OpCodes.Ldarg_0);

			for (int i = 0; i < paramTypes.Length; i++)
			{
				if (ps[i].ParameterType.IsByRef)
					il.Emit(OpCodes.Ldloca_S, locals[i]);
				else
					il.Emit(OpCodes.Ldloc, locals[i]);
			}

#pragma warning disable XS0001
			if (methodInfo.IsStatic)
				il.EmitCall(OpCodes.Call, methodInfo, null);
			else
				il.EmitCall(OpCodes.Callvirt, methodInfo, null);
#pragma warning restore XS0001

			if (methodInfo.ReturnType == typeof(void))
				il.Emit(OpCodes.Ldnull);
			else
				EmitBoxIfNeeded(il, methodInfo.ReturnType);

			for (int i = 0; i < paramTypes.Length; i++)
			{
				if (ps[i].ParameterType.IsByRef)
				{
					il.Emit(OpCodes.Ldarg_1);
					EmitFastInt(il, i);
					il.Emit(OpCodes.Ldloc, locals[i]);
					if (locals[i].LocalType.IsValueType)
						il.Emit(OpCodes.Box, locals[i].LocalType);
					il.Emit(OpCodes.Stelem_Ref);
				}
			}

			il.Emit(OpCodes.Ret);

			var invoder = (FastInvokeHandler)dynamicMethod.CreateDelegate(typeof(FastInvokeHandler));
			return invoder;
		}

		static void EmitCastToReference(ILGenerator il, Type type)
		{
			if (type.IsValueType)
				il.Emit(OpCodes.Unbox_Any, type);
			else
				il.Emit(OpCodes.Castclass, type);
		}

		static void EmitBoxIfNeeded(ILGenerator il, Type type)
		{
			if (type.IsValueType)
				il.Emit(OpCodes.Box, type);
		}

		static void EmitFastInt(ILGenerator il, int value)
		{
			switch (value)
			{
				case -1:
					il.Emit(OpCodes.Ldc_I4_M1);
					return;
				case 0:
					il.Emit(OpCodes.Ldc_I4_0);
					return;
				case 1:
					il.Emit(OpCodes.Ldc_I4_1);
					return;
				case 2:
					il.Emit(OpCodes.Ldc_I4_2);
					return;
				case 3:
					il.Emit(OpCodes.Ldc_I4_3);
					return;
				case 4:
					il.Emit(OpCodes.Ldc_I4_4);
					return;
				case 5:
					il.Emit(OpCodes.Ldc_I4_5);
					return;
				case 6:
					il.Emit(OpCodes.Ldc_I4_6);
					return;
				case 7:
					il.Emit(OpCodes.Ldc_I4_7);
					return;
				case 8:
					il.Emit(OpCodes.Ldc_I4_8);
					return;
			}

			if (value > -129 && value < 128)
				il.Emit(OpCodes.Ldc_I4_S, (sbyte)value);
			else
				il.Emit(OpCodes.Ldc_I4, value);
		}
	}
}