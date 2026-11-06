using MonoMod.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace HarmonyLib
{
	// Preserve one definition context through existing construction, metadata export and callable snapshots.
	internal static class GenericMethodContext
	{
		internal static DynamicMethodDefinition Create(MethodBase original, string name, Type returnType, Type[] parameters)
		{
			var method = new DynamicMethodDefinition(name, typeof(void), []);
			var definition = method.Definition;
			var module = definition.Module;
			var names = new HashSet<string>(Arguments(original).Select(parameter => parameter.Name));
			// CecilILGenerator imports operands without a context. Supply the source definition's context at that boundary.
			var source = module.ImportReference(original);
			AccessTools.Field(typeof(Mono.Cecil.ModuleDefinition), "reflection_importer").SetValue(module, new GenericImporter(module, source, original));
			CopyParameters(original.DeclaringType?.GetGenericArguments() ?? [], definition.DeclaringType);
			CopyParameters(original.IsGenericMethod ? original.GetGenericArguments() : [], definition);
			definition.ReturnType = module.ImportReference(returnType, source);
			foreach (var type in parameters) definition.Parameters.Add(new Mono.Cecil.ParameterDefinition(module.ImportReference(type, source)));
			return method;

			void CopyParameters(Type[] parameters, Mono.Cecil.IGenericParameterProvider owner)
			{
				foreach (var parameter in parameters)
				{
					var name = parameter.Name;
					if (owner == definition)
					{
						// MonoMod relinks by name before owner/position. Keep synthetic method names distinct from all source names.
						name = "__HarmonyMethod" + owner.GenericParameters.Count;
						while (!names.Add(name)) name += "_";
					}
					owner.GenericParameters.Add(new Mono.Cecil.GenericParameter(name, owner)
					{ Attributes = (Mono.Cecil.GenericParameterAttributes)parameter.GenericParameterAttributes & ~Mono.Cecil.GenericParameterAttributes.VarianceMask });
				}
				for (var i = 0; i < parameters.Length; i++)
					foreach (var constraint in parameters[i].GetGenericParameterConstraints())
#if NET35
						owner.GenericParameters[i].Constraints.Add(module.ImportReference(constraint, source));
#else
						owner.GenericParameters[i].Constraints.Add(new Mono.Cecil.GenericParameterConstraint(module.ImportReference(constraint, source)));
#endif
			}
		}

		internal static MethodInfo BindCallback(MethodBase original, MethodInfo callback)
		{
			if (original is null || !callback.IsGenericMethodDefinition) return callback;
			// A generic callback definition takes the target's type arguments: a definition's own parameters or a closed instantiation's arguments.
			var closed = (original.IsGenericMethod || original.DeclaringType?.IsGenericType == true) && !original.ContainsGenericParameters;
			if (!closed && !MethodBodyPatching.IsDefinitionWide(original)) return callback;
			var arguments = Arguments(original);
			if (callback.GetGenericArguments().Length != arguments.Length)
				throw new ArgumentException($"Generic callback {callback.FullDescription()} requires {arguments.Length} type parameters: declaring-type parameters, then method parameters.");
			return callback.MakeGenericMethod(arguments);
		}

		internal static void ValidateRegistration(MethodBase original, MethodInfo callback)
		{
			if (original is not null && (MethodBodyPatching.IsDefinitionWide(original) || MethodBodyPatching.FamilyOwner(original) is not null)
				&& (callback.IsGenericMethod && !callback.IsGenericMethodDefinition
					|| callback.DeclaringType?.IsGenericType == true && !callback.DeclaringType.IsGenericTypeDefinition))
				throw new NotSupportedException($"Register the generic callback definition, or use a factory to preserve a fixed instantiation: {callback.FullDescription()}");
		}

		internal static Type[] Arguments(MethodBase method) => [.. method.DeclaringType?.GetGenericArguments() ?? [], .. method.IsGenericMethod ? method.GetGenericArguments() : []];
		internal static bool IsDefinition(MethodBase method) => method.ContainsGenericParameters && method == method.Module.ResolveMethod(method.MetadataToken);
		internal static bool SameDefinition(MethodBase left, MethodBase right) => left != right
			&& (right.IsGenericMethod || right.DeclaringType?.IsGenericType == true) && left.Module == right.Module && left.MetadataToken == right.MetadataToken;

		internal static MethodInfo Construct(MethodInfo definition, Type[] arguments)
		{
			var count = definition.DeclaringType?.GetGenericArguments().Length ?? 0;
			if (count != 0)
				definition = (MethodInfo)MethodBase.GetMethodFromHandle(definition.MethodHandle, definition.DeclaringType.MakeGenericType(arguments.Take(count).ToArray()).TypeHandle);
			return definition.IsGenericMethodDefinition ? definition.MakeGenericMethod(arguments.Skip(count).ToArray()) : definition;
		}

		sealed class GenericImporter(Mono.Cecil.ModuleDefinition module, Mono.Cecil.MethodReference source, MethodBase original) : Mono.Cecil.IReflectionImporter
		{
			readonly MMReflectionImporter importer = new(module) { UseDefault = false };
			// Cecil's interface imports assemblies by name. MonoMod's name overload is obsolete and forwards to this default importer anyway.
			readonly Mono.Cecil.DefaultReflectionImporter names = new(module);
			readonly Type[] arguments = Arguments(original);
			internal readonly Dictionary<Mono.Cecil.MemberReference, object> members = [];
			public Mono.Cecil.AssemblyNameReference ImportReference(AssemblyName name) => names.ImportReference(name);
			public Mono.Cecil.TypeReference ImportReference(Type type, Mono.Cecil.IGenericParameterProvider context)
			{
				Check(type);
				return Remember(importer.ImportReference(type, context ?? source), type);
			}
			public Mono.Cecil.FieldReference ImportReference(FieldInfo field, Mono.Cecil.IGenericParameterProvider context)
			{
				Check(field.DeclaringType, true);
				return Remember(importer.ImportReference(field, context ?? source), field);
			}
			public Mono.Cecil.MethodReference ImportReference(MethodBase method, Mono.Cecil.IGenericParameterProvider context)
			{
				Check(method.DeclaringType, true);
				if (method.IsGenericMethod && !method.IsGenericMethodDefinition)
					foreach (var argument in method.GetGenericArguments()) Check(argument);
				var reference = importer.ImportReference(method, context ?? source);
				if (method.DeclaringType?.IsGenericTypeDefinition is true)
				{
					var element = reference is Mono.Cecil.GenericInstanceMethod instance ? instance.ElementMethod : reference;
					element.DeclaringType = importer.ImportReference(method.DeclaringType, context ?? source);
				}
				return Remember(reference, method);
			}
			T Remember<T>(T reference, MemberInfo member) where T : Mono.Cecil.MemberReference
			{
				members[reference] = reference is Mono.Cecil.GenericInstanceType && member is Type { IsGenericTypeDefinition: true } type
					? new KeyValuePair<Type, bool>(type, true)
					: member is MethodBase or FieldInfo && (reference.DeclaringType is Mono.Cecil.GenericInstanceType
						&& member.DeclaringType?.IsGenericTypeDefinition is true || reference is Mono.Cecil.GenericInstanceMethod && member is MethodInfo { IsGenericMethodDefinition: true })
						? new KeyValuePair<MemberInfo, bool>(member, true) : member;
				return reference;
			}
			void Check(Type type, bool memberDeclaringType = false)
			{
				if (type is null) return;
				if (type.IsGenericParameter && !arguments.Contains(type)) throw new ArgumentException("The generic parameter belongs to another definition: " + type);
				if (type.HasElementType) Check(type.GetElementType());
				else if (type.IsGenericType && (!type.IsGenericTypeDefinition || memberDeclaringType))
					foreach (var argument in type.GetGenericArguments()) Check(argument);
			}
			internal Mono.Cecil.TypeReference TypeToken(Type type, bool instantiate)
			{
				return Remember(importer.ImportReference(type, instantiate ? source : null), type);
			}
		}

		internal static Mono.Cecil.TypeReference TypeToken(Mono.Cecil.MethodDefinition method, Type type, bool instantiate)
			=> (AccessTools.Field(typeof(Mono.Cecil.ModuleDefinition), "reflection_importer").GetValue(method.Module) as GenericImporter)?.TypeToken(type, instantiate);

		internal static Dictionary<Mono.Cecil.MemberReference, object> RuntimeMembers(DynamicMethodDefinition method)
			=> method.Definition.HasGenericParameters || method.Definition.DeclaringType.HasGenericParameters
				? (AccessTools.Field(typeof(Mono.Cecil.ModuleDefinition), "reflection_importer").GetValue(method.Definition.Module) as GenericImporter)?.members : null;

		internal static void CopyParameters(Mono.Cecil.IGenericParameterProvider source, Mono.Cecil.IGenericParameterProvider target)
		{
			foreach (var parameter in source.GenericParameters)
				target.GenericParameters.Add(new Mono.Cecil.GenericParameter(parameter.Name, target) { Attributes = parameter.Attributes });
			for (var i = 0; i < source.GenericParameters.Count; i++)
				foreach (var constraint in source.GenericParameters[i].Constraints)
#if NET35
					target.GenericParameters[i].Constraints.Add(constraint);
#else
					target.GenericParameters[i].Constraints.Add(new Mono.Cecil.GenericParameterConstraint(constraint.ConstraintType));
#endif
		}

	}
}
