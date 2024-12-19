using MonoMod.Utils;
using System;
using System.Reflection;
using System.Reflection.Emit;

namespace HarmonyLib
{
	// Based on https://github.com/MonoMod/MonoMod/blob/master/MonoMod.Utils/FastReflectionHelper.cs

	/// <summary>A delegate to invoke a method</summary>
	/// <param name="target">The instance</param>
	/// <param name="parameters">The method parameters</param>
	/// <returns>The method result</returns>
	public delegate object FastInvokeHandler(object target, params object[] parameters);

	/// <summary>A helper class to invoke method with delegates</summary>
	public static class MethodInvoker
	{
		/// <summary>Creates a fast invocation handler from a method</summary>
		/// <param name="methodInfo">The method to invoke</param>
		/// <param name="directBoxValueAccess">Controls if boxed value object is accessed/updated directly</param>
		/// <returns>The <see cref="FastInvokeHandler"/></returns>
		/// <remarks>
		/// <para>
		/// The <c>directBoxValueAccess</c> option controls how value types passed by reference (e.g. ref int, out my_struct) are handled in the arguments array
		/// passed to the fast invocation handler.
		/// Since the arguments array is an object array, any value types contained within it are actually references to a boxed value object.
		/// Like any other object, there can be other references to such boxed value objects, other than the reference within the arguments array.
		/// <example>For example,
		/// <code>
		/// var val = 5;
		/// var box = (object)val;
		/// var arr = new object[] { box };
		/// handler(arr); // for a method with parameter signature: ref/out/in int
		/// </code>
		/// </example>
		/// </para>
		/// <para>
		/// If <c>directBoxValueAccess</c> is <c>true</c>, the boxed value object is accessed (and potentially updated) directly when the handler is called,
		/// such that all references to the boxed object reflect the potentially updated value.
		/// In the above example, if the method associated with the handler updates the passed (boxed) value to 10, both <c>box</c> and <c>arr[0]</c>
		/// now reflect the value 10. Note that the original <c>val</c> is not updated, since boxing always copies the value into the new boxed value object.
		/// </para>
		/// <para>
		/// If <c>directBoxValueAccess</c> is <c>false</c> (default), the boxed value object in the arguments array is replaced with a "reboxed" value object,
		/// such that potential updates to the value are reflected only in the arguments array.
		/// In the above example, if the method associated with the handler updates the passed (boxed) value to 10, only <c>arr[0]</c> now reflects the value 10.
		/// </para>
		/// </remarks>
		public static FastInvokeHandler GetHandler(MethodInfo methodInfo, bool directBoxValueAccess = false)
		{
			var dynamicMethod = new DynamicMethodDefinition($"FastInvoke_{methodInfo.Name}_{(directBoxValueAccess ? "direct" : "indirect")}", typeof(object), [typeof(object), typeof(object[])]);
			var il = dynamicMethod.GetILGenerator();

			if (!methodInfo.IsStatic)
			{
				Emit(il, OpCodes.Ldarg_0);
				EmitUnboxIfNeeded(il, methodInfo.DeclaringType);
			}

			var generateLocalBoxObject = true;
			var ps = methodInfo.GetParameters();
			for (var i = 0; i < ps.Length; i++)
			{
				var argType = ps[i].ParameterType;
				var argIsByRef = argType.IsByRef;
				if (argIsByRef)
					argType = argType.GetElementType();
				var argIsValueType = argType.IsValueType;

				if (argIsByRef && argIsValueType && !directBoxValueAccess)
				{
					// used later when storing back the reference to the new box in the array.
					Emit(il, OpCodes.Ldarg_1);
					EmitFastInt(il, i);
				}

				Emit(il, OpCodes.Ldarg_1);
				EmitFastInt(il, i);

				if (argIsByRef && !argIsValueType)
				{
					Emit(il, OpCodes.Ldelema, typeof(object));
				}
				else
				{
					Emit(il, OpCodes.Ldelem_Ref);
					if (argIsValueType)
					{
						if (!argIsByRef || !directBoxValueAccess)
						{
							// if !directBoxValueAccess, create a new box if required
							Emit(il, OpCodes.Unbox_Any, argType);
							if (argIsByRef)
							{
								// the following ensures that any references to the boxed value still retain the same boxed value,
								// and that only the boxed value within the parameters array can be changed
								// this is done by "reboxing" the value and replacing the original boxed value in the parameters array with this reboxed value

								// box back
								Emit(il, OpCodes.Box, argType);

								// for later stelem.ref
								Emit(il, OpCodes.Dup);

								// store the "rebox" in an object local
								if (generateLocalBoxObject)
								{
									generateLocalBoxObject = false;
									_ = il.DeclareLocal(typeof(object), false);
								}
								Emit(il, OpCodes.Stloc_0);

								// arr and index set up already
								Emit(il, OpCodes.Stelem_Ref);

								// load the "rebox" and emit unbox (get unboxed value address)
								Emit(il, OpCodes.Ldloc_0);
								Emit(il, OpCodes.Unbox, argType);
							}
						}
						else
						{
							// if directBoxValueAccess, emit unbox (get value address)
							Emit(il, OpCodes.Unbox, argType);
						}
					}
				}
			}

			if (methodInfo.IsStatic)
				EmitCall(il, OpCodes.Call, methodInfo);
			else
				EmitCall(il, OpCodes.Callvirt, methodInfo);

			if (methodInfo.ReturnType == typeof(void))
				Emit(il, OpCodes.Ldnull);
			else
				EmitBoxIfNeeded(il, methodInfo.ReturnType);

			Emit(il, OpCodes.Ret);

			var invoder = dynamicMethod.Generate().CreateDelegate<FastInvokeHandler>();
			return invoder;
		}

		internal static void Emit(ILGenerator il, OpCode opcode) => il.Emit(opcode);

		internal static void Emit(ILGenerator il, OpCode opcode, Type type) => il.Emit(opcode, type);

		internal static void EmitCall(ILGenerator il, OpCode opcode, MethodInfo methodInfo) => il.EmitCall(opcode, methodInfo, null);

		static void EmitUnboxIfNeeded(ILGenerator il, Type type)
		{
			if (type.IsValueType)
				Emit(il, OpCodes.Unbox_Any, type);
		}

		static void EmitBoxIfNeeded(ILGenerator il, Type type)
		{
			if (type.IsValueType)
				Emit(il, OpCodes.Box, type);
		}

		internal static void EmitFastInt(ILGenerator il, int value)
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
