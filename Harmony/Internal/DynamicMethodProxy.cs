using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Utils;
using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using FieldAttributes = Mono.Cecil.FieldAttributes;
using MethodAttributes = Mono.Cecil.MethodAttributes;
using MethodImplAttributes = Mono.Cecil.MethodImplAttributes;
using TypeAttributes = Mono.Cecil.TypeAttributes;

namespace HarmonyLib
{
	internal static class DynamicMethodProxy
	{
#if !NET35 && !NETSTANDARD2_0
		sealed class Origin(MethodBase original, string role)
		{
			internal readonly MethodBase Original = original;
			internal readonly string Role = role;
		}

		// Scope mutable helpers to their original and helper site. Equal signatures alone cannot distinguish two live callbacks.
		sealed class StandInKey : IEquatable<StandInKey>
		{
			readonly object[] parts;
			internal StandInKey(MethodInfo method)
			{
				var generated = origins.TryGetValue(method, out var origin);
				var owner = generated ? origin.Original : method;
				parts = owner is System.Reflection.Emit.DynamicMethod ? [owner] :
					[owner.Module, owner.MetadataToken, owner.DeclaringType, .. owner.IsGenericMethod ? owner.GetGenericArguments() : Type.EmptyTypes];
				if (generated) parts = [.. parts, origin.Role, method.ReturnType, .. method.GetParameters().Select(parameter => parameter.ParameterType)];
			}
			public bool Equals(StandInKey other) => other is not null && parts.SequenceEqual(other.parts);
			public override bool Equals(object other) => other is StandInKey key && Equals(key);
			public override int GetHashCode()
			{
				var hash = 17;
				foreach (var part in parts) hash = unchecked(hash * 31 + (part?.GetHashCode() ?? 0));
				return hash;
			}
		}

		static readonly ConditionalWeakTable<MethodInfo, Origin> origins = new();
		static readonly Dictionary<StandInKey, MethodInfo> permanent = [];

		internal static void Remember(MethodInfo method, MethodBase original, string role)
		{
			if (original is not null) origins.Add(method, new Origin(original, role));
		}

		internal static MethodInfo Permanent(MethodInfo target, List<Action> publish)
		{
			var key = new StandInKey(target);
			lock (permanent)
			{
				if (!permanent.TryGetValue(key, out var proxy))
				{
					proxy = Create(target, collectible: false);
					permanent.Add(key, proxy);
					return proxy;
				}
				var field = proxy.DeclaringType.GetField("Target", BindingFlags.NonPublic | BindingFlags.Static);
				var callback = target.CreateDelegate(field.FieldType);
				void Update() => field.SetValue(null, callback);
				if (publish is null) Update();
				else publish.Add(Update);
				return proxy;
			}
		}
#endif

		// A one-dimensional zero-based array. Before .NET 8, an array of function pointers is a distinct type
		// that compares unequal to its element's MakeArrayType(), so ask the runtime where it answers.
		static readonly PropertyInfo isSZArray = typeof(Type).GetProperty("IsSZArray");
		internal static bool IsVector(Type type)
			=> isSZArray?.GetValue(type, null) is bool vector ? vector : type == type.GetElementType().MakeArrayType();

		// Keep function pointer signatures exact so delegate binding agrees with the callback.
		static TypeReference ImportType(ModuleDefinition module, Type type)
		{
			if (type.IsByRef) return new ByReferenceType(ImportType(module, type.GetElementType()));
			if (type.IsPointer) return new PointerType(ImportType(module, type.GetElementType()));
			if (type.IsArray) return IsVector(type)
				? new ArrayType(ImportType(module, type.GetElementType())) : new ArrayType(ImportType(module, type.GetElementType()), type.GetArrayRank());
			if (typeof(Type).GetProperty("IsFunctionPointer")?.GetValue(type, null) is true)
			{
				var pointer = new FunctionPointerType
				{
					ReturnType = ImportType(module, (Type)typeof(Type).GetMethod("GetFunctionPointerReturnType").Invoke(type, null))
				};
				foreach (var parameter in (Type[])typeof(Type).GetMethod("GetFunctionPointerParameterTypes").Invoke(type, null))
					pointer.Parameters.Add(new ParameterDefinition(ImportType(module, parameter)));
				foreach (var convention in (Type[])typeof(Type).GetMethod("GetFunctionPointerCallingConventions").Invoke(type, null))
					pointer.CallingConvention = convention.Name switch
					{
						"CallConvCdecl" => MethodCallingConvention.C,
						"CallConvStdcall" => MethodCallingConvention.StdCall,
						"CallConvThiscall" => MethodCallingConvention.ThisCall,
						"CallConvFastcall" => MethodCallingConvention.FastCall,
						_ => throw new NotSupportedException("Unsupported function pointer calling convention: " + convention)
					};
				if (pointer.CallingConvention == MethodCallingConvention.Default
					&& typeof(Type).GetProperty("IsUnmanagedFunctionPointer")?.GetValue(type, null) is true)
					pointer.CallingConvention = (MethodCallingConvention)9; // IMAGE_CEE_CS_CALLCONV_UNMANAGED
				return pointer;
			}
			return module.ImportReference(type);
		}

		// DynamicMethods and methods in emitted assemblies cannot be bound through assembly metadata.
		// Forward through an exact-signature delegate:
		// no boxing, copied ref arguments, native pointers, or reflection-wrapped exceptions.
		// A collectible stand-in can go away with its callback. A module that cannot bind collectible assemblies needs one that lives for the process.
		internal static MethodInfo Create(MethodInfo target, bool collectible = true)
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
			var invoke = new MethodDefinition("Invoke", MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Virtual | MethodAttributes.NewSlot, ImportType(module, target.ReturnType))
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
				var type = ImportType(module, parameter.ParameterType);
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
			var assembly = GeneratedAssemblyLoader.Load(module, collectible);
			if (AccessTools.IsMonoRuntime) assembly.SetMonoCorlibInternal(true);
			var typeProxy = assembly.GetType(holder.FullName);
			typeProxy.GetField(field.Name, BindingFlags.NonPublic | BindingFlags.Static)
				.SetValue(null, target.CreateDelegate(assembly.GetType(callback.FullName)));
			return typeProxy.GetMethod(proxy.Name);
		}
	}
}
