using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Utils;
using System;
using System.Reflection;
using FieldAttributes = Mono.Cecil.FieldAttributes;
using MethodAttributes = Mono.Cecil.MethodAttributes;
using MethodImplAttributes = Mono.Cecil.MethodImplAttributes;
using TypeAttributes = Mono.Cecil.TypeAttributes;

namespace HarmonyLib
{
	internal static class DynamicMethodProxy
	{
		// DynamicMethods and methods in emitted assemblies cannot be bound through assembly metadata.
		// Forward through an exact-signature delegate:
		// no boxing, copied ref arguments, native pointers, or reflection-wrapped exceptions.
		internal static MethodInfo Create(MethodInfo target)
		{
			using var module = ModuleDefinition.CreateModule("HarmonyDynamicMethod_" + Guid.NewGuid().ToString("N"), new ModuleParameters
			{
				Kind = ModuleKind.Dll,
				ReflectionImporterProvider = MMReflectionImporter.ProviderNoDefault
			});
			var callback = new TypeDefinition("", "Callback", TypeAttributes.Public | TypeAttributes.Sealed, module.ImportReference(typeof(MulticastDelegate)));
			module.Types.Add(callback);
			var constructor = new MethodDefinition(".ctor", MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName, module.TypeSystem.Void)
			{
				ImplAttributes = MethodImplAttributes.Runtime
			};
			constructor.Parameters.Add(new ParameterDefinition(module.TypeSystem.Object));
			constructor.Parameters.Add(new ParameterDefinition(module.TypeSystem.IntPtr));
			callback.Methods.Add(constructor);
			var invoke = new MethodDefinition("Invoke", MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Virtual | MethodAttributes.NewSlot, module.ImportReference(target.ReturnType))
			{
				ImplAttributes = MethodImplAttributes.Runtime
			};
			callback.Methods.Add(invoke);
			var holder = new TypeDefinition("", "Proxy", TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed, module.TypeSystem.Object);
			module.Types.Add(holder);
			var field = new FieldDefinition("Target", FieldAttributes.Private | FieldAttributes.Static, callback);
			holder.Fields.Add(field);
			var proxy = new MethodDefinition("Invoke", MethodAttributes.Public | MethodAttributes.Static, invoke.ReturnType);
			holder.Methods.Add(proxy);
			foreach (var parameter in target.GetParameters())
			{
				var type = module.ImportReference(parameter.ParameterType);
				invoke.Parameters.Add(new ParameterDefinition(type));
				proxy.Parameters.Add(new ParameterDefinition(type));
			}
			var il = proxy.Body.GetILProcessor();
			il.Emit(OpCodes.Ldsfld, field);
			foreach (var parameter in proxy.Parameters) il.Emit(OpCodes.Ldarg, parameter);
			il.Emit(OpCodes.Callvirt, invoke);
			il.Emit(OpCodes.Ret);
			// Load through the same resolver as other Cecil-generated methods. A unique assembly identity prevents
			// separate Harmony copies from binding each other's callbacks. The field retains the target's lifetime.
			var assembly = GeneratedAssemblyLoader.Load(module);
			if (AccessTools.IsMonoRuntime) assembly.SetMonoCorlibInternal(true);
			var typeProxy = assembly.GetType(holder.FullName);
			typeProxy.GetField(field.Name, BindingFlags.NonPublic | BindingFlags.Static)
				.SetValue(null, target.CreateDelegate(assembly.GetType(callback.FullName)));
			return typeProxy.GetMethod(proxy.Name);
		}
	}
}
