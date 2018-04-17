using System;
using System.Reflection;
using System.Reflection.Emit;

namespace Harmony
{
	public class DelegateTypeFactory
	{
		readonly ModuleBuilder module;

		static int counter;
		public DelegateTypeFactory()
		{
			counter++;
			var name = new AssemblyName("HarmonyDTFAssembly" + counter);
			var assembly = AppDomain.CurrentDomain.DefineDynamicAssembly(name, AssemblyBuilderAccess.Run);
			module = assembly.DefineDynamicModule("HarmonyDTFModule" + counter);
		}

		public Type CreateDelegateType(MethodInfo method)
		{
			var attr = TypeAttributes.Sealed | TypeAttributes.Public;
			var typeBuilder = module.DefineType("HarmonyDTFType" + counter, attr, typeof(MulticastDelegate));

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

			return typeBuilder.CreateType();
		}
	}
}
