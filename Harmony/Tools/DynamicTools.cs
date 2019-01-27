using Harmony.ILCopying;
using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace Harmony
{
	/// <summary>Creating dynamic methods</summary>
	public static class DynamicTools
	{
		/// <summary>Creates a new dynamic method based on the signature of an existing method</summary>
		/// <param name="original">The original method</param>
		/// <param name="suffix">A suffix for the new method name</param>
		/// <returns>The new and so far empty dynamic method, ready to be implemented</returns>
		///
		[UpgradeToLatestVersion(1)]
		public static DynamicMethod CreateDynamicMethod(MethodBase original, string suffix)
		{
			if (original == null) throw new ArgumentNullException("original cannot be null");
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

			// DynamicMethod does not support byref return types
			if (returnType == null || returnType.IsByRef)
				return null;

			DynamicMethod method;
			try
			{
				method = new DynamicMethod(
				patchName,
				MethodAttributes.Public | MethodAttributes.Static,
				CallingConventions.Standard,
				returnType,
				parameterTypes.ToArray(),
				original.DeclaringType,
				true
			);
			}
			catch (Exception)
			{
				return null;
			}

			var offset = (original.IsStatic ? 0 : 1) + (firstArgIsReturnBuffer ? 1 : 0);
			for (var i = 0; i < parameters.Length; i++)
				method.DefineParameter(i + offset, parameters[i].Attributes, parameters[i].Name);

			return method;
		}

		/// <summary>Creates local variables by copying them from an original method</summary>
		/// <param name="original">The original method</param>
		/// <param name="generator">A IL generator to generate the variables with</param>
		/// <param name="logOutput">Set to true to log the actions to the debug log</param>
		/// <returns>An array of newly defined variables, each represented by a LocalBuilder</returns>
		///
		public static LocalBuilder[] DeclareLocalVariables(MethodBase original, ILGenerator generator, bool logOutput = true)
		{
			var vars = original.GetMethodBody()?.LocalVariables;
			if (vars == null)
				return new LocalBuilder[0];
			return vars.Select(lvi =>
			{
				var localBuilder = generator.DeclareLocal(lvi.LocalType, lvi.IsPinned);
				if (logOutput)
					Emitter.LogLocalVariable(generator, localBuilder);
				return localBuilder;
			}).ToArray();
		}

		/// <summary>Creates a local variable</summary>
		/// <param name="generator">A IL generator to generate the variable with</param>
		/// <param name="type">The variable type</param>
		/// <returns>A LocalBuilder representing the new variable</returns>
		///
		public static LocalBuilder DeclareLocalVariable(ILGenerator generator, Type type)
		{
			if (type.IsByRef) type = type.GetElementType();

			if (AccessTools.IsClass(type))
			{
				var v = generator.DeclareLocal(type);
				Emitter.LogLocalVariable(generator, v);
				Emitter.Emit(generator, OpCodes.Ldnull);
				Emitter.Emit(generator, OpCodes.Stloc, v);
				return v;
			}
			if (AccessTools.IsStruct(type))
			{
				var v = generator.DeclareLocal(type);
				Emitter.LogLocalVariable(generator, v);
				Emitter.Emit(generator, OpCodes.Ldloca, v);
				Emitter.Emit(generator, OpCodes.Initobj, type);
				return v;
			}
			if (AccessTools.IsValue(type))
			{
				var v = generator.DeclareLocal(type);
				Emitter.LogLocalVariable(generator, v);
				if (type == typeof(float))
					Emitter.Emit(generator, OpCodes.Ldc_R4, (float)0);
				else if (type == typeof(double))
					Emitter.Emit(generator, OpCodes.Ldc_R8, (double)0);
				else if (type == typeof(long))
					Emitter.Emit(generator, OpCodes.Ldc_I8, (long)0);
				else
					Emitter.Emit(generator, OpCodes.Ldc_I4, 0);
				Emitter.Emit(generator, OpCodes.Stloc, v);
				return v;
			}
			return null;
		}

		/// <summary>Prepares a dynamic method so it is jitted</summary>
		/// <param name="method">The dynamic method</param>
		///
		public static void PrepareDynamicMethod(DynamicMethod method)
		{
			var nonPublicInstance = BindingFlags.NonPublic | BindingFlags.Instance;
			var nonPublicStatic = BindingFlags.NonPublic | BindingFlags.Static;

			// on mono, just call 'CreateDynMethod'
			//
			var m_CreateDynMethod = method.GetType().GetMethod("CreateDynMethod", nonPublicInstance);
			if (m_CreateDynMethod != null)
			{
				var h_CreateDynMethod = MethodInvoker.GetHandler(m_CreateDynMethod);
				h_CreateDynMethod(method, new object[0]);
				return;
			}

			// on all .NET Core versions, call 'RuntimeHelpers._CompileMethod' but with a different parameter:
			//
			var m__CompileMethod = typeof(RuntimeHelpers).GetMethod("_CompileMethod", nonPublicStatic);
			var h__CompileMethod = MethodInvoker.GetHandler(m__CompileMethod);

			var m_GetMethodDescriptor = method.GetType().GetMethod("GetMethodDescriptor", nonPublicInstance);
			var h_GetMethodDescriptor = MethodInvoker.GetHandler(m_GetMethodDescriptor);
			var handle = (RuntimeMethodHandle)h_GetMethodDescriptor(method, new object[0]);

			// 1) RuntimeHelpers._CompileMethod(handle.GetMethodInfo())
			//
			object runtimeMethodInfo = null;
			var f_m_value = handle.GetType().GetField("m_value", nonPublicInstance);
			if (f_m_value != null)
				runtimeMethodInfo = f_m_value.GetValue(handle);
			else
			{
				var m_GetMethodInfo = handle.GetType().GetMethod("GetMethodInfo", nonPublicInstance);
				if (m_GetMethodInfo != null)
				{
					var h_GetMethodInfo = MethodInvoker.GetHandler(m_GetMethodInfo);
					runtimeMethodInfo = h_GetMethodInfo(handle, new object[0]);
				}
			}
			if (runtimeMethodInfo != null)
			{
				try
				{
					// this can throw BadImageFormatException "An attempt was made to load a program with an incorrect format"
					h__CompileMethod(null, new object[] { runtimeMethodInfo });
					return;
				}
				catch (Exception)
				{
				}
			}

			// 2) RuntimeHelpers._CompileMethod(handle.Value)
			//
			if (m__CompileMethod.GetParameters()[0].ParameterType.IsAssignableFrom(handle.Value.GetType()))
			{
				h__CompileMethod(null, new object[] { handle.Value });
				return;
			}

			// 3) RuntimeHelpers._CompileMethod(handle)
			//
			if (m__CompileMethod.GetParameters()[0].ParameterType.IsAssignableFrom(handle.GetType()))
			{
				h__CompileMethod(null, new object[] { handle });
				return;
			}
		}
	}
}