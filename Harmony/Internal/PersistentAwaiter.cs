using Mono.Cecil;
using MonoMod.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace HarmonyLib
{
	// Adapt only the public completion interfaces. The original builder and state-machine type,
	// their storage, their Task/ValueTask, and the awaiter's GetResult remain unchanged.
	internal static class PersistentAwaiter
	{
		const string notifyName = "System.Runtime.CompilerServices.INotifyCompletion";
		const string criticalName = "System.Runtime.CompilerServices.ICriticalNotifyCompletion";
		static readonly Dictionary<Type, Type> proxies = [];
		delegate void Register<T>(ref T awaiter, Action continuation);

		sealed class Registration<T>
		{
			public T Value;
			readonly object[] frame;
			static readonly Register<T> normal = CreateRegistration<T>(notifyName, "OnCompleted");
			static readonly Register<T> critical = CreateRegistration<T>(criticalName, "UnsafeOnCompleted");
			public Registration(T value, object[] frame) { Value = value; this.frame = frame; }
			public void OnCompleted(Action continuation) => normal(ref Value, PersistentState.Resume(frame, continuation));
			public void UnsafeOnCompleted(Action continuation) => critical(ref Value, PersistentState.Resume(frame, continuation));
		}

		static Register<T> CreateRegistration<T>(string interfaceName, string name)
		{
			var contract = typeof(T).GetInterfaces().FirstOrDefault(type => type.FullName == interfaceName);
			if (contract is null && typeof(T).FullName == interfaceName) contract = typeof(T);
			if (contract is null) return null;
			var method = new DynamicMethod("HarmonyPersistent" + name, typeof(void), [typeof(T).MakeByRefType(), typeof(Action)], typeof(PersistentAwaiter), true);
			var il = method.GetILGenerator();
			il.Emit(OpCodes.Ldarg_0);
			il.Emit(OpCodes.Ldarg_1);
			il.Emit(OpCodes.Constrained, typeof(T));
			il.Emit(OpCodes.Callvirt, contract.GetMethod(name));
			il.Emit(OpCodes.Ret);
			return (Register<T>)method.CreateDelegate(typeof(Register<T>));
		}

		internal static bool IsRegistration(MethodInfo method, Type stateMachine)
			=> method.Name is "AwaitOnCompleted" or "AwaitUnsafeOnCompleted" && !method.IsStatic && method.IsGenericMethod
			&& method.ReturnType == typeof(void) && method.GetGenericArguments().Length == 2
			&& method.GetGenericArguments()[1] == stateMachine && method.GetParameters().Length == 2
			&& method.GetParameters()[0].ParameterType == method.GetGenericArguments()[0].MakeByRefType()
			&& method.GetParameters()[1].ParameterType == stateMachine.MakeByRefType();

		internal static MethodInfo Wrap(MethodInfo method, OpCode opcode)
		{
			var arguments = method.GetGenericArguments();
			var awaiter = arguments[0];
			var contractName = method.Name == "AwaitUnsafeOnCompleted" ? criticalName : notifyName;
			var contract = awaiter.GetInterfaces().Concat([awaiter]).FirstOrDefault(type => type.FullName == contractName)
				?? throw new NotSupportedException($"Persistent Infix state needs the public {contractName} contract on {awaiter}");
			Type proxy;
			lock (proxies)
				if (!proxies.TryGetValue(contract, out proxy)) proxies[contract] = proxy = CreateProxy(contract);
			MethodInfo adapted;
			try { adapted = method.GetGenericMethodDefinition().MakeGenericMethod(proxy, arguments[1]); }
			catch (ArgumentException error)
			{
				throw new NotSupportedException($"The async builder {method.DeclaringType} requires more than the public awaiter contract; persistent Infix state cannot adapt it", error);
			}
			var builder = method.DeclaringType;
			var signature = new[] { builder.IsValueType ? builder.MakeByRefType() : builder, awaiter.MakeByRefType(), arguments[1].MakeByRefType(), typeof(object[]) };
			var wrapper = new DynamicMethod("HarmonyPersistentAwait", typeof(void), signature, typeof(PersistentAwaiter), true);
			var il = wrapper.GetILGenerator();
			var registration = typeof(Registration<>).MakeGenericType(awaiter);
			var holder = il.DeclareLocal(registration);
			var wrapped = il.DeclareLocal(proxy);
			il.Emit(OpCodes.Ldarg_1);
			il.Emit(OpCodes.Ldobj, awaiter);
			il.Emit(OpCodes.Ldarg_3);
			il.Emit(OpCodes.Newobj, registration.GetConstructor([awaiter, typeof(object[])]));
			il.Emit(OpCodes.Stloc, holder);
			il.Emit(OpCodes.Ldloca, wrapped);
			il.Emit(OpCodes.Initobj, proxy);
			foreach (var name in new[] { "OnCompleted", "UnsafeOnCompleted" })
			{
				var field = proxy.GetField(name + "Callback");
				if (field is null) continue;
				il.Emit(OpCodes.Ldloca, wrapped);
				il.Emit(OpCodes.Ldloc, holder);
				il.Emit(OpCodes.Ldftn, registration.GetMethod(name));
				il.Emit(OpCodes.Newobj, typeof(Action<Action>).GetConstructor([typeof(object), typeof(IntPtr)]));
				il.Emit(OpCodes.Stfld, field);
			}
			il.BeginExceptionBlock();
			il.Emit(OpCodes.Ldarg_0);
			il.Emit(OpCodes.Ldloca, wrapped);
			il.Emit(OpCodes.Ldarg_2);
			il.Emit(opcode, adapted);
			il.Emit(OpCodes.Ldarg_3);
			il.Emit(OpCodes.Call, AccessTools.Method(typeof(PersistentState), nameof(PersistentState.Suspended)));
			il.BeginFinallyBlock();
			il.Emit(OpCodes.Ldarg_1);
			il.Emit(OpCodes.Ldloc, holder);
			il.Emit(OpCodes.Ldfld, registration.GetField(nameof(Registration<int>.Value)));
			il.Emit(OpCodes.Stobj, awaiter);
			il.EndExceptionBlock();
			il.Emit(OpCodes.Ret);
			return wrapper;
		}

		static Type CreateProxy(Type contract)
		{
			// Emitting the small interface adapter also lets a net35 Harmony build work with async
			// code when loaded on a newer runtime, without a compile-time async dependency.
			using var module = ModuleDefinition.CreateModule("HarmonyPersistentAwaiter_" + Guid.NewGuid().ToString("N"), new ModuleParameters
			{
				Kind = ModuleKind.Dll,
				ReflectionImporterProvider = MMReflectionImporter.ProviderNoDefault
			});
			var type = new TypeDefinition("", "Awaiter", Mono.Cecil.TypeAttributes.Public | Mono.Cecil.TypeAttributes.Sealed
				| Mono.Cecil.TypeAttributes.SequentialLayout, module.ImportReference(typeof(ValueType)));
			module.Types.Add(type);
			type.Interfaces.Add(new InterfaceImplementation(module.ImportReference(contract)));
			foreach (var name in contract.FullName == criticalName ? new[] { "OnCompleted", "UnsafeOnCompleted" } : new[] { "OnCompleted" })
			{
				var field = new FieldDefinition(name + "Callback", Mono.Cecil.FieldAttributes.Public, module.ImportReference(typeof(Action<Action>)));
				type.Fields.Add(field);
				var method = new MethodDefinition(name, Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.Virtual
					| Mono.Cecil.MethodAttributes.Final | Mono.Cecil.MethodAttributes.NewSlot | Mono.Cecil.MethodAttributes.HideBySig, module.TypeSystem.Void);
				method.Parameters.Add(new ParameterDefinition(module.ImportReference(typeof(Action))));
				type.Methods.Add(method);
				var il = method.Body.GetILProcessor();
				il.Emit(Mono.Cecil.Cil.OpCodes.Ldarg_0);
				il.Emit(Mono.Cecil.Cil.OpCodes.Ldfld, field);
				il.Emit(Mono.Cecil.Cil.OpCodes.Ldarg_1);
				il.Emit(Mono.Cecil.Cil.OpCodes.Callvirt, module.ImportReference(typeof(Action<Action>).GetMethod("Invoke")));
				il.Emit(Mono.Cecil.Cil.OpCodes.Ret);
			}
			return GeneratedAssemblyLoader.Load(module).GetType(type.FullName);
		}
	}
}
