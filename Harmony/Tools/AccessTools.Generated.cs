using MonoMod.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace HarmonyLib
{
	public static partial class AccessTools
	{
		const string CompilerGeneratedAttributeName = "System.Runtime.CompilerServices.CompilerGeneratedAttribute";
		const string AsyncStateMachineInterfaceName = "System.Runtime.CompilerServices.IAsyncStateMachine";
		const string EnumeratorInterfaceName = "System.Collections.IEnumerator";
		static readonly string[] stateMachineAttributes =
		[
			"System.Runtime.CompilerServices.IteratorStateMachineAttribute",
			"System.Runtime.CompilerServices.AsyncStateMachineAttribute",
			"System.Runtime.CompilerServices.AsyncIteratorStateMachineAttribute"
		];

		/// <summary>Resolves the execution body of a compiler-generated iterator, async method, or async iterator</summary>
		/// <param name="method">The declared method, or its already resolved execution body</param>
		/// <returns>The generated MoveNext method, the supplied method if it is already that body, or null for an ordinary method</returns>
		/// <exception cref="ArgumentException">Recognized state-machine metadata is malformed or cannot be closed from the supplied method</exception>
		/// <remarks>Async iterators resolve to IAsyncStateMachine.MoveNext, not MoveNextAsync. Open generic methods must be closed first.</remarks>
		public static MethodInfo StateMachineMoveNext(MethodBase method)
		{
			if (method is null) throw new ArgumentNullException(nameof(method));
			if (method is MethodInfo dynamicMethod && dynamicMethod.IsDynamicMethod()) return null;
			if (method is MethodInfo candidate && HasCompilerGeneratedAttribute(method.DeclaringType)
				&& method.DeclaringType.GetInterfaces().Any(type => type.FullName == AsyncStateMachineInterfaceName || type.FullName == EnumeratorInterfaceName))
			{
				var body = StateMachineBody(method.DeclaringType, method);
				if (body == candidate) return candidate;
			}

			var attributes = CustomAttributeData.GetCustomAttributes(method)
				.Where(attribute => stateMachineAttributes.Contains(attribute.Constructor.DeclaringType.FullName)).ToArray();
			if (attributes.Length > 1)
				throw GeneratedResolutionError(method, "Several state-machine attributes describe this method");
			if (attributes.Length == 1)
			{
				var attribute = attributes[0];
				if (attribute.ConstructorArguments.Count != 1 || attribute.ConstructorArguments[0].Value is not Type stateType)
					throw GeneratedResolutionError(method, "The state-machine attribute has no valid generated type");
				stateType = CloseStateMachineType(stateType, method);
				var async = attribute.Constructor.DeclaringType.FullName != stateMachineAttributes[0];
				return StateMachineBody(stateType, method, async ? AsyncStateMachineInterfaceName : EnumeratorInterfaceName);
			}

			// Older iterator compilers omit the attribute. A matching generated type and IEnumerator
			// implementation are required; one arbitrary newobj in an ordinary factory is not enough.
			if (method is not MethodInfo info || !IsIteratorReturnType(info.ReturnType) || method.GetMethodBody() is null) return null;
			var stateTypes = PatchProcessor.ReadMethodBody(method)
				.Where(instruction => instruction.Key == OpCodes.Newobj)
				.Select(instruction => (instruction.Value as ConstructorInfo)?.DeclaringType)
				.Where(type => IsGeneratedStateMachineType(type) && type.Name.StartsWith("<" + method.Name + ">", StringComparison.Ordinal)
					&& SameTypeDefinition(type.DeclaringType, method.DeclaringType)).Distinct().ToArray();
			if (stateTypes.Length == 0) return null;
			if (stateTypes.Length != 1) throw GeneratedResolutionError(method, "Several generated iterator types are constructed by this method");
			return StateMachineBody(CloseStateMachineType(stateTypes[0], method), method, EnumeratorInterfaceName);
		}

		/// <summary>Finds a compiler-generated local function directly referenced by an exact containing method</summary>
		/// <param name="containingMethod">The source method, or a resolved local function for nested-local lookup</param>
		/// <param name="name">The local function's source name</param>
		/// <param name="parameters">Optional source parameter types, excluding compiler-supplied closure arguments</param>
		/// <returns>The selected generated method</returns>
		/// <exception cref="ArgumentException">The local function was not preserved as a referenced generated method</exception>
		/// <exception cref="AmbiguousMatchException">Several referenced local functions match</exception>
		public static MethodInfo LocalFunction(MethodBase containingMethod, string name, Type[] parameters = null)
		{
			if (containingMethod is null) throw new ArgumentNullException(nameof(containingMethod));
			if (string.IsNullOrEmpty(name)) throw new ArgumentException("A local function name is required", nameof(name));
			var matches = GeneratedReferences(containingMethod).Where(method => LocalFunctionName(method.Name) == name)
				.Where(method => parameters is null || method.GetParameters().Where(parameter => !IsGeneratedClosureType(ElementTypeForGenerated(parameter.ParameterType)))
					.Select(parameter => parameter.ParameterType).SequenceEqual(parameters)).ToArray();
			if (matches.Length == 0) throw GeneratedResolutionError(containingMethod, $"No referenced local function named '{name}' matches. Optimized-away functions cannot be resolved");
			if (matches.Length != 1) throw new AmbiguousMatchException($"Local function '{name}' in {containingMethod.FullDescription()} is ambiguous: {string.Join(", ", matches.Select(method => method.FullDescription()).ToArray())}");
			return matches[0];
		}

		/// <summary>Finds lambda implementation methods referenced directly by an exact containing method</summary>
		/// <param name="containingMethod">The source method, or a resolved generated method</param>
		/// <returns>Distinct lambda methods in deterministic metadata order</returns>
		/// <remarks>The order can change after recompilation. Compiler-generated state-machine bodies are inspected for iterator and async methods.</remarks>
		public static IEnumerable<MethodInfo> Lambdas(MethodBase containingMethod)
		{
			if (containingMethod is null) throw new ArgumentNullException(nameof(containingMethod));
			return GeneratedReferences(containingMethod).Where(method => method.Name.Contains(">b__")).ToArray();
		}

		static IEnumerable<MethodInfo> GeneratedReferences(MethodBase containingMethod)
		{
			if (containingMethod is MethodInfo dynamicMethod && dynamicMethod.IsDynamicMethod()) return [];
			var body = (MethodBase)StateMachineMoveNext(containingMethod) ?? containingMethod;
			if (body.GetMethodBody() is null) return [];
			var sourceName = IsGeneratedStateMachineType(body.DeclaringType) ? GeneratedSourceName(body.DeclaringType.Name) : GeneratedSourceName(body.Name);
			return PatchProcessor.ReadMethodBody(body)
				.Where(instruction => instruction.Key == OpCodes.Call || instruction.Key == OpCodes.Callvirt
					|| instruction.Key == OpCodes.Ldftn || instruction.Key == OpCodes.Ldvirtftn)
				.Select(instruction => instruction.Value as MethodInfo)
				.Where(method => method != null && (method.Name.Contains(">g__") || method.Name.Contains(">b__"))
					&& (HasCompilerGeneratedAttribute(method) || HasCompilerGeneratedAttribute(method.DeclaringType))
					&& SameGeneratedOwner(body.DeclaringType, method.DeclaringType) && GeneratedSourceName(method.Name) == sourceName)
				.Distinct().OrderBy(method => method.MetadataToken).ThenBy(method => method.FullDescription(), StringComparer.Ordinal).ToArray();
		}

		static string GeneratedSourceName(string name)
		{
			while (name.StartsWith("<", StringComparison.Ordinal))
			{
				var end = Math.Max(name.LastIndexOf(">g__", StringComparison.Ordinal), Math.Max(name.LastIndexOf(">b__", StringComparison.Ordinal), name.LastIndexOf(">d__", StringComparison.Ordinal)));
				if (end < 0) break;
				name = name.Substring(1, end - 1);
			}
			return name;
		}

		static string LocalFunctionName(string name)
		{
			var start = name.LastIndexOf(">g__", StringComparison.Ordinal);
			if (start < 0) return null;
			start += 4;
			var end = name.IndexOf('|', start);
			return end < 0 ? null : name.Substring(start, end - start);
		}

		static Type CloseStateMachineType(Type type, MethodBase method)
		{
			if (!type.ContainsGenericParameters) return type;
			var arguments = (method.DeclaringType?.GetGenericArguments() ?? Type.EmptyTypes)
				.Concat(method.IsGenericMethod ? method.GetGenericArguments() : Type.EmptyTypes).ToArray();
			if (!type.IsGenericType || type.GetGenericArguments().Length != arguments.Length || arguments.Any(argument => argument.ContainsGenericParameters))
				throw GeneratedResolutionError(method, $"Cannot close state-machine type {type.FullDescription()}; supply a closed declaring type and method");
			try { return type.GetGenericTypeDefinition().MakeGenericType(arguments); }
			catch (ArgumentException exception) { throw new ArgumentException($"Cannot close state-machine type for {method.FullDescription()}", exception); }
		}

		static MethodInfo StateMachineBody(Type type, MethodBase requested, string interfaceName = null)
		{
			if (type.ContainsGenericParameters) throw GeneratedResolutionError(requested, "The state-machine type contains unresolved generic parameters");
			var interfaces = type.GetInterfaces();
			interfaceName ??= interfaces.Any(item => item.FullName == AsyncStateMachineInterfaceName) ? AsyncStateMachineInterfaceName : EnumeratorInterfaceName;
			var contracts = interfaces.Where(item => item.FullName == interfaceName).ToArray();
			if (contracts.Length != 1) throw GeneratedResolutionError(requested, $"State-machine type {type.FullDescription()} does not implement {interfaceName}");
			var map = type.GetInterfaceMap(contracts[0]);
			var methods = Enumerable.Range(0, map.InterfaceMethods.Length)
				.Where(index => map.InterfaceMethods[index].Name == "MoveNext" && map.InterfaceMethods[index].GetParameters().Length == 0)
				.Select(index => map.TargetMethods[index]).Distinct().ToArray();
			if (methods.Length != 1 || methods[0].IsAbstract || methods[0].IsStatic || methods[0].GetMethodBody() is null)
				throw GeneratedResolutionError(requested, $"State-machine type {type.FullDescription()} has no unique executable MoveNext implementation");
			return methods[0];
		}

		static bool IsIteratorReturnType(Type type) => new[] { type }.Concat(type.GetInterfaces())
			.Any(item => item.FullName == "System.Collections.IEnumerable" || item.FullName == EnumeratorInterfaceName
				|| item.IsGenericType && (item.GetGenericTypeDefinition().FullName == "System.Collections.Generic.IEnumerable`1"
					|| item.GetGenericTypeDefinition().FullName == "System.Collections.Generic.IEnumerator`1"));

		internal static bool HasCompilerGeneratedAttribute(MemberInfo member) => member != null && CustomAttributeData.GetCustomAttributes(member)
			.Any(attribute => attribute.Constructor.DeclaringType.FullName == CompilerGeneratedAttributeName);

		internal static bool IsGeneratedClosureType(Type type) => type != null && HasCompilerGeneratedAttribute(type)
			&& type.Name.StartsWith("<>c__DisplayClass", StringComparison.Ordinal);

		internal static bool IsGeneratedStateMachineType(Type type) => type != null && HasCompilerGeneratedAttribute(type)
			&& type.Name.StartsWith("<", StringComparison.Ordinal) && type.Name.Contains(">d__");

		static Type ElementTypeForGenerated(Type type) => type.IsByRef ? type.GetElementType() : type;

		static bool SameTypeDefinition(Type first, Type second) => first != null && second != null
			&& (first.IsGenericType ? first.GetGenericTypeDefinition() : first) == (second.IsGenericType ? second.GetGenericTypeDefinition() : second);

		static bool SameGeneratedOwner(Type first, Type second)
		{
			while (first != null && HasCompilerGeneratedAttribute(first)) first = first.DeclaringType;
			while (second != null && HasCompilerGeneratedAttribute(second)) second = second.DeclaringType;
			return SameTypeDefinition(first, second);
		}

		static ArgumentException GeneratedResolutionError(MethodBase method, string reason) => new($"{reason}: {method.FullDescription()}");
	}
}
