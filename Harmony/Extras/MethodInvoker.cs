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
	///
	public delegate object FastInvokeHandler(object target, object[] parameters);

	/// <summary>A helper class to invoke method with delegates</summary>
	public class MethodInvoker
	{
		/// <summary>Creates a fast invocation handler from a method and a module</summary>
		/// <param name="methodInfo">The method to invoke</param>
		/// <param name="module">The module context</param>
		/// <returns>The fast invocation handler</returns>
		///
		public static FastInvokeHandler GetHandler(DynamicMethod methodInfo, Module module)
		{
			return defaultInstance.GetHandler(methodInfo, module);
		}

		/// <summary>Creates a fast invocation handler from a method and a module</summary>
		/// <param name="methodInfo">The method to invoke</param>
		/// <returns>The fast invocation handler</returns>
		///
		public static FastInvokeHandler GetHandler(MethodInfo methodInfo)
		{
			return defaultInstance.GetHandler(methodInfo, methodInfo.DeclaringType.Module);
		}

		static readonly MethodInvoker defaultInstance = new MethodInvoker(directBoxValueAccess: false);

		readonly bool directBoxValueAccess;

		public MethodInvoker(bool directBoxValueAccess)
		{
			this.directBoxValueAccess = directBoxValueAccess;
		}

		public FastInvokeHandler GetHandler(MethodInfo methodInfo, Module module)
		{
			var dynamicMethod = new DynamicMethod("FastInvoke_" + methodInfo.Name + "_" + (directBoxValueAccess ? "direct" : "indirect"), typeof(object), new Type[] { typeof(object), typeof(object[]) }, module, true);
			var il = dynamicMethod.GetILGenerator();

			if (!methodInfo.IsStatic)
			{
				Emit(il, OpCodes.Ldarg_0);
				EmitUnboxIfNeeded(il, methodInfo.DeclaringType);
			}

			var generateLocalBoxValuePtr = true;
			var ps = methodInfo.GetParameters();
			for (var i = 0; i < ps.Length; i++)
			{
				var argType = ps[i].ParameterType;
				var argIsByRef = argType.IsByRef;
				if (argIsByRef)
					argType = argType.GetElementType();
				var argIsValueType = argType.IsValueType;

				// START DEBUG
				//LocalBuilder boxedVar = null, reboxedVar = null;
				//if (argIsByRef && argIsValueType && !directBoxValueAccess)
				//{
				//	// make sure the pinned void* local is declared first so it has local index 0
				//	if (generateLocalBoxValuePtr)
				//	{
				//		generateLocalBoxValuePtr = false;
				//		// Yes, you're seeing this right - a pinned local of type void* to store the box value address!
				//		il.DeclareLocal(typeof(void*), true);
				//	}
				//	boxedVar = il.DeclareLocal(typeof(object), false);
				//	reboxedVar = il.DeclareLocal(typeof(object), false);
				//}
				// END DEBUG

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
							// START DEBUG
							//if (argIsByRef)
							//{
							//	il.Emit(OpCodes.Dup);
							//	il.Emit(OpCodes.Stloc, boxedVar);
							//	il.Emit(OpCodes.Dup);
							//	il.Emit(OpCodes.Call, typeof(MethodInvoker).GetMethod(nameof(AddressOf), AccessTools.all));
							//	il.Emit(OpCodes.Ldstr, $"[{i}:{ps[i].ParameterType}] boxed value pointer address (a)");
							//	il.Emit(OpCodes.Call, typeof(MethodInvoker).GetMethod(nameof(OutAddress), AccessTools.all));
							//	il.Emit(OpCodes.Dup);
							//	il.Emit(OpCodes.Unbox, argType);
							//	il.Emit(OpCodes.Conv_I8);
							//	il.Emit(OpCodes.Ldstr, $"[{i}:{ps[i].ParameterType}] unboxed value pointer address (a)");
							//	il.Emit(OpCodes.Call, typeof(MethodInvoker).GetMethod(nameof(OutAddress), AccessTools.all));
							//}
							// END DEBUG
							// if !directBoxValueAccess, create a new box if required
							Emit(il, OpCodes.Unbox_Any, argType);
							if (argIsByRef)
							{
								// the following ensures that any references to the boxed value still retain the same boxed value,
								// and that only the boxed value within the parameters array can be changed
								// this is done by "reboxing" the value and replacing the original boxed value in the parameters array with this reboxed value

								// box back
								Emit(il, OpCodes.Box, argType);

								// START DEBUG
								//il.Emit(OpCodes.Dup);
								//il.Emit(OpCodes.Stloc_S, reboxedVar);
								//il.Emit(OpCodes.Dup);
								//il.Emit(OpCodes.Call, typeof(MethodInvoker).GetMethod(nameof(AddressOf), AccessTools.all));
								//il.Emit(OpCodes.Ldstr, $"[{i}:{ps[i].ParameterType}] reboxed value pointer address (a)");
								//il.Emit(OpCodes.Call, typeof(MethodInvoker).GetMethod(nameof(OutAddress), AccessTools.all));
								// END DEBUG

								// store new box value address to local 0
								Emit(il, OpCodes.Dup);
								Emit(il, OpCodes.Unbox, argType);

								// START DEBUG
								//il.Emit(OpCodes.Dup);
								//il.Emit(OpCodes.Conv_I8);
								//il.Emit(OpCodes.Ldstr, $"[{i}:{ps[i].ParameterType}] unreboxed value pointer address (a)");
								//il.Emit(OpCodes.Call, typeof(MethodInvoker).GetMethod(nameof(OutAddress), AccessTools.all));
								// END DEBUG

								if (generateLocalBoxValuePtr)
								{
									generateLocalBoxValuePtr = false;
									// Yes, you're seeing this right - a pinned local of type void* to store the box value address!
									il.DeclareLocal(typeof(void*), true);
								}
								Emit(il, OpCodes.Stloc_0);

								// arr and index set up already
								Emit(il, OpCodes.Stelem_Ref);

								// START DEBUG
								//il.Emit(OpCodes.Ldstr, $"[{i}:{ps[i].ParameterType}] TryMoveAddressesViaGC");
								//il.Emit(OpCodes.Call, typeof(MethodInvoker).GetMethod(nameof(Out), AccessTools.all));
								//il.Emit(OpCodes.Ldloc_S, boxedVar);
								//il.Emit(OpCodes.Dup);
								//il.Emit(OpCodes.Call, typeof(MethodInvoker).GetMethod(nameof(AddressOf), AccessTools.all));
								//il.Emit(OpCodes.Ldstr, $"[{i}:{ps[i].ParameterType}] boxed value pointer address (b)");
								//il.Emit(OpCodes.Call, typeof(MethodInvoker).GetMethod(nameof(OutAddress), AccessTools.all));
								//il.Emit(OpCodes.Unbox, argType);
								//il.Emit(OpCodes.Conv_I8);
								//il.Emit(OpCodes.Ldstr, $"[{i}:{ps[i].ParameterType}] unboxed value pointer address (b)");
								//il.Emit(OpCodes.Call, typeof(MethodInvoker).GetMethod(nameof(OutAddress), AccessTools.all));
								//il.Emit(OpCodes.Ldarg_1);
								//EmitFastInt(il, i);
								//il.Emit(OpCodes.Ldelem_Ref);
								//il.Emit(OpCodes.Dup);
								//il.Emit(OpCodes.Call, typeof(MethodInvoker).GetMethod(nameof(AddressOf), AccessTools.all));
								//il.Emit(OpCodes.Ldstr, $"[{i}:{ps[i].ParameterType}] reboxed value pointer address (b1)");
								//il.Emit(OpCodes.Call, typeof(MethodInvoker).GetMethod(nameof(OutAddress), AccessTools.all));
								//il.Emit(OpCodes.Unbox, argType);
								//il.Emit(OpCodes.Conv_I8);
								//il.Emit(OpCodes.Ldstr, $"[{i}:{ps[i].ParameterType}] reunboxed value pointer address (b1)");
								//il.Emit(OpCodes.Call, typeof(MethodInvoker).GetMethod(nameof(OutAddress), AccessTools.all));
								//il.Emit(OpCodes.Ldloc_S, reboxedVar);
								//il.Emit(OpCodes.Dup);
								//il.Emit(OpCodes.Call, typeof(MethodInvoker).GetMethod(nameof(AddressOf), AccessTools.all));
								//il.Emit(OpCodes.Ldstr, $"[{i}:{ps[i].ParameterType}] reboxed value pointer address (b2)");
								//il.Emit(OpCodes.Call, typeof(MethodInvoker).GetMethod(nameof(OutAddress), AccessTools.all));
								//il.Emit(OpCodes.Unbox, argType);
								//il.Emit(OpCodes.Conv_I8);
								//il.Emit(OpCodes.Ldstr, $"[{i}:{ps[i].ParameterType}] unreboxed value pointer address (b2)");
								//il.Emit(OpCodes.Call, typeof(MethodInvoker).GetMethod(nameof(OutAddress), AccessTools.all));
								//il.Emit(OpCodes.Ldloc_0);
								//il.Emit(OpCodes.Conv_I8);
								//il.Emit(OpCodes.Ldstr, $"[{i}:{ps[i].ParameterType}] unreboxed value pointer address UNMANAGED (b)");
								//il.Emit(OpCodes.Call, typeof(MethodInvoker).GetMethod(nameof(OutAddress), AccessTools.all));
								// END DEBUG

								// load address back to stack
								Emit(il, OpCodes.Ldloc_0);
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

			var invoder = (FastInvokeHandler)dynamicMethod.CreateDelegate(typeof(FastInvokeHandler));
			return invoder;
		}

		unsafe static long AddressOf(object obj)
		{
			TypedReference tr = __makeref(obj);
			return (long)**(IntPtr**)(&tr);
		}

		static void OutAddress(long address, string label) => Out($"{label}: {address:X}");

		static void Out(string str) => Console.WriteLine(str);

		protected virtual void Emit(ILGenerator il, OpCode opcode) => il.Emit(opcode);

		protected virtual void Emit(ILGenerator il, OpCode opcode, Type type) => il.Emit(opcode, type);

		protected virtual void EmitCall(ILGenerator il, OpCode opcode, MethodInfo methodInfo) => il.EmitCall(opcode, methodInfo, null);

		void EmitUnboxIfNeeded(ILGenerator il, Type type)
		{
			if (type.IsValueType)
				Emit(il, OpCodes.Unbox_Any, type);
		}

		void EmitBoxIfNeeded(ILGenerator il, Type type)
		{
			if (type.IsValueType)
				Emit(il, OpCodes.Box, type);
		}

		protected virtual void EmitFastInt(ILGenerator il, int value)
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