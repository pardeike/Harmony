using System;
using System.Reflection;
using System.Reflection.Emit;

namespace HarmonyLib
{
	/// <summary>A factory to create delegate types</summary>
	public class DelegateTypeFactory
	{
		readonly ModuleBuilder module;

		static int counter;

		/// <summary>Default constructor</summary>
		public DelegateTypeFactory()
		{
			counter++;
			var name = $"HarmonyDTFAssembly{counter}";
			var assembly = PatchTools.DefineDynamicAssembly(name);
			module = assembly.DefineDynamicModule($"HarmonyDTFModule{counter}");
		}

		/// <summary>Creates a delegate type for a method</summary>
		/// <param name="method">The method</param>
		/// <returns>The new delegate type</returns>
		///
		public Type CreateDelegateType(MethodInfo method)
		{
			var attr = TypeAttributes.Sealed | TypeAttributes.Public;
			var typeBuilder = module.DefineType($"HarmonyDTFType{counter}", attr, typeof(MulticastDelegate));

			var constructor = typeBuilder.DefineConstructor(
				MethodAttributes.RTSpecialName | MethodAttributes.HideBySig | MethodAttributes.Public,
				CallingConventions.Standard, new[] { typeof(object), typeof(IntPtr) });
			constructor.SetImplementationFlags(MethodImplAttributes.CodeTypeMask);

			var parameters = method.GetParameters();

			var invokeMethod = typeBuilder.DefineMethod(
				"Invoke", MethodAttributes.HideBySig | MethodAttributes.Virtual | MethodAttributes.Public,
				method.ReturnType, parameters.Types());
			invokeMethod.SetImplementationFlags(MethodImplAttributes.CodeTypeMask);

			for (var i = 0; i < parameters.Length; i++)
				invokeMethod.DefineParameter(i + 1, ParameterAttributes.None, parameters[i].Name);

#if NETSTANDARD2_0
			return typeBuilder.CreateTypeInfo().AsType();
#else
			return typeBuilder.CreateType();
#endif
		}
	}
}
