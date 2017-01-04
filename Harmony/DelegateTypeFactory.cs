using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace Harmony
{
	class DelegateTypeFactory
	{
		readonly ModuleBuilder module;

		public DelegateTypeFactory()
		{
			var name = new AssemblyName("DelegateTypeFactory");
			var assembly = AppDomain.CurrentDomain.DefineDynamicAssembly(name, AssemblyBuilderAccess.Run);
			module = assembly.DefineDynamicModule("DelegateTypeFactory");
		}

		public Type CreateDelegateType(MethodInfo method)
		{
			string nameBase = string.Format("{0}{1}", method.DeclaringType.Name, method.Name);
			string name = GetUniqueName(nameBase);

			var attr = TypeAttributes.Sealed | TypeAttributes.Public;
			var typeBuilder = module.DefineType(name, attr, typeof(MulticastDelegate));

			var constructor = typeBuilder.DefineConstructor(
				 MethodAttributes.RTSpecialName | MethodAttributes.HideBySig | MethodAttributes.Public,
				 CallingConventions.Standard, new[] { typeof(object), typeof(IntPtr) });
			constructor.SetImplementationFlags(MethodImplAttributes.CodeTypeMask);

			var parameters = method.GetParameters();

			var invokeMethod = typeBuilder.DefineMethod(
				 "Invoke", MethodAttributes.HideBySig | MethodAttributes.Virtual | MethodAttributes.Public,
				 method.ReturnType, parameters.Select(p => p.ParameterType).ToArray());
			invokeMethod.SetImplementationFlags(MethodImplAttributes.CodeTypeMask);

			for (int i = 0; i < parameters.Length; i++)
			{
				var parameter = parameters[i];
				invokeMethod.DefineParameter(i + 1, ParameterAttributes.None, parameter.Name);
			}

			return typeBuilder.CreateType();
		}

		string GetUniqueName(string nameBase)
		{
			int number = 2;
			string name = nameBase;
			while (module.GetType(name) != null)
				name = nameBase + number++;
			return name;
		}
	}
}
