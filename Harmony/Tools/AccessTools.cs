using MonoMod.Core.Platforms;
using MonoMod.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
#if NET45_OR_GREATER || NETSTANDARD || NETCOREAPP
using System.Runtime.CompilerServices;
#endif

namespace HarmonyLib
{
	// ---------------------------------------------------------------------------------------------------------------------
	// NOTE:
	// When adding or updating methods that have a first argument of 'Type type, please create/update a corresponding method
	// and xml documentation in AccessToolsExtensions!
	// ---------------------------------------------------------------------------------------------------------------------

	/// <summary>A helper class for reflection related functions</summary>
	///
	public static class AccessTools
	{
		/// <summary>Shortcut for <see cref="BindingFlags"/> to simplify the use of reflections and make it work for any access level</summary>
		///
		public static readonly BindingFlags all = BindingFlags.Public // This should a be const, but changing from static (readonly) to const breaks binary compatibility.
			| BindingFlags.NonPublic
			| BindingFlags.Instance
			| BindingFlags.Static
			| BindingFlags.GetField
			| BindingFlags.SetField
			| BindingFlags.GetProperty
			| BindingFlags.SetProperty;

		/// <summary>Shortcut for <see cref="BindingFlags"/> to simplify the use of reflections and make it work for any access level but only within the current type</summary>
		///
		public static readonly BindingFlags allDeclared = all | BindingFlags.DeclaredOnly; // This should a be const, but changing from static (readonly) to const breaks binary compatibility.

		/// <summary>Enumerates all assemblies in the current app domain, excluding visual studio assemblies</summary>
		/// <returns>An enumeration of <see cref="Assembly"/></returns>
		///
		public static IEnumerable<Assembly> AllAssemblies() => AppDomain.CurrentDomain.GetAssemblies().Where(a => a.FullName.StartsWith("Microsoft.VisualStudio") is false);

		/// <summary>Gets a type by name. Prefers a full name with namespace but falls back to the first type matching the name otherwise</summary>
		/// <param name="name">The name</param>
		/// <returns>A type or null if not found</returns>
		///
		public static Type TypeByName(string name)
		{
			var type = Type.GetType(name, false);
			type ??= AllTypes().FirstOrDefault(t => t.FullName == name);
			type ??= AllTypes().FirstOrDefault(t => t.Name == name);
			if (type is null) FileLog.Debug($"AccessTools.TypeByName: Could not find type named {name}");
			return type;
		}

		/// <summary>Gets all successfully loaded types from a given assembly</summary>
		/// <param name="assembly">The assembly</param>
		/// <returns>An array of types</returns>
		/// <remarks>
		/// This calls and returns <see cref="Assembly.GetTypes"/>, while catching any thrown <see cref="ReflectionTypeLoadException"/>.
		/// If such an exception is thrown, returns the successfully loaded types (<see cref="ReflectionTypeLoadException.Types"/>,
		/// filtered for non-null values).
		/// </remarks>
		///
		public static Type[] GetTypesFromAssembly(Assembly assembly)
		{
			try
			{
				return assembly.GetTypes();
			}
			catch (ReflectionTypeLoadException ex)
			{
				FileLog.Debug($"AccessTools.GetTypesFromAssembly: assembly {assembly} => {ex}");
				return ex.Types.Where(type => type is not null).ToArray();
			}
		}

		/// <summary>Enumerates all successfully loaded types in the current app domain, excluding visual studio assemblies</summary>
		/// <returns>An enumeration of all <see cref="Type"/> in all assemblies, excluding visual studio assemblies</returns>
		///
		public static IEnumerable<Type> AllTypes() => AllAssemblies().SelectMany(GetTypesFromAssembly);

		/// <summary>Enumerates all inner types (non-recursive) of a given type</summary>
		/// <param name="type">The class/type to start with</param>
		/// <returns>An enumeration of all inner <see cref="Type"/></returns>
		///
		public static IEnumerable<Type> InnerTypes(Type type) => type.GetNestedTypes(all);

		/// <summary>Applies a function going up the type hierarchy and stops at the first non-<c>null</c> result</summary>
		/// <typeparam name="T">Result type of func()</typeparam>
		/// <param name="type">The class/type to start with</param>
		/// <param name="func">The evaluation function returning T</param>
		/// <returns>The first non-<c>null</c> result, or <c>null</c> if no match</returns>
		/// <remarks>
		/// The type hierarchy of a class or value type (including struct) does NOT include implemented interfaces,
		/// and the type hierarchy of an interface is only itself (regardless of whether that interface implements other interfaces).
		/// The top-most type in the type hierarchy of all non-interface types (including value types) is <see cref="object"/>.
		/// </remarks>
		///
		public static T FindIncludingBaseTypes<T>(Type type, Func<Type, T> func) where T : class
		{
			while (true)
			{
				var result = func(type);
				if (result is object) return result;
				type = type.BaseType;
				if (type is null) return null;
			}
		}

		/// <summary>Applies a function going into inner types and stops at the first non-<c>null</c> result</summary>
		/// <typeparam name="T">Generic type parameter</typeparam>
		/// <param name="type">The class/type to start with</param>
		/// <param name="func">The evaluation function returning T</param>
		/// <returns>The first non-<c>null</c> result, or <c>null</c> if no match</returns>
		///
		public static T FindIncludingInnerTypes<T>(Type type, Func<Type, T> func) where T : class
		{
			var result = func(type);
			if (result is object) return result;
			foreach (var subType in type.GetNestedTypes(all))
			{
				result = FindIncludingInnerTypes(subType, func);
				if (result is object)
					break;
			}
			return result;
		}

		/// <summary>Creates an identifiable version of a method</summary>
		/// <param name="method">The method</param>
		/// <returns></returns>
		public static MethodInfo Identifiable(this MethodInfo method) => PlatformTriple.Current.GetIdentifiable(method) as MethodInfo ?? method;

		/// <summary>Gets the reflection information for a directly declared field</summary>
		/// <param name="type">The class/type where the field is defined</param>
		/// <param name="name">The name of the field</param>
		/// <returns>A field or null when type/name is null or when the field cannot be found</returns>
		///
		public static FieldInfo DeclaredField(Type type, string name)
		{
			if (type is null)
			{
				FileLog.Debug("AccessTools.DeclaredField: type is null");
				return null;
			}
			if (string.IsNullOrEmpty(name))
			{
				FileLog.Debug("AccessTools.DeclaredField: name is null/empty");
				return null;
			}
			var fieldInfo = type.GetField(name, allDeclared);
			if (fieldInfo is null) FileLog.Debug($"AccessTools.DeclaredField: Could not find field for type {type} and name {name}");
			return fieldInfo;
		}

		/// <summary>Gets the reflection information for a directly declared field</summary>
		/// <param name="typeColonName">The member in the form <c>TypeFullName:MemberName</c>, where TypeFullName matches the form recognized by <a href="https://docs.microsoft.com/en-us/dotnet/api/system.type.gettype">Type.GetType</a> like <c>Some.Namespace.Type</c>.</param>
		/// <returns>A field or null when the field cannot be found</returns>
		///
		public static FieldInfo DeclaredField(string typeColonName)
		{
			var info = Tools.TypColonName(typeColonName);
			var fieldInfo = info.type.GetField(info.name, allDeclared);
			if (fieldInfo is null) FileLog.Debug($"AccessTools.DeclaredField: Could not find field for type {info.type} and name {info.name}");
			return fieldInfo;
		}

		/// <summary>Gets the reflection information for a field by searching the type and all its super types</summary>
		/// <param name="type">The class/type where the field is defined</param>
		/// <param name="name">The name of the field (case sensitive)</param>
		/// <returns>A field or null when type/name is null or when the field cannot be found</returns>
		///
		public static FieldInfo Field(Type type, string name)
		{
			if (type is null)
			{
				FileLog.Debug("AccessTools.Field: type is null");
				return null;
			}
			if (string.IsNullOrEmpty(name))
			{
				FileLog.Debug("AccessTools.Field: name is null/empty");
				return null;
			}
			var fieldInfo = FindIncludingBaseTypes(type, t => t.GetField(name, all));
			if (fieldInfo is null) FileLog.Debug($"AccessTools.Field: Could not find field for type {type} and name {name}");
			return fieldInfo;
		}

		/// <summary>Gets the reflection information for a field by searching the type and all its super types</summary>
		/// <param name="typeColonName">The member in the form <c>TypeFullName:MemberName</c>, where TypeFullName matches the form recognized by <a href="https://docs.microsoft.com/en-us/dotnet/api/system.type.gettype">Type.GetType</a> like <c>Some.Namespace.Type</c>.</param>
		/// <returns>A field or null when the field cannot be found</returns>
		///
		public static FieldInfo Field(string typeColonName)
		{
			var info = Tools.TypColonName(typeColonName);
			var fieldInfo = FindIncludingBaseTypes(info.type, t => t.GetField(info.name, all));
			if (fieldInfo is null) FileLog.Debug($"AccessTools.Field: Could not find field for type {info.type} and name {info.name}");
			return fieldInfo;
		}

		/// <summary>Gets the reflection information for a field</summary>
		/// <param name="type">The class/type where the field is declared</param>
		/// <param name="idx">The zero-based index of the field inside the class definition</param>
		/// <returns>A field or null when type is null or when the field cannot be found</returns>
		///
		public static FieldInfo DeclaredField(Type type, int idx)
		{
			if (type is null)
			{
				FileLog.Debug("AccessTools.DeclaredField: type is null");
				return null;
			}
			var fieldInfo = GetDeclaredFields(type).ElementAtOrDefault(idx);
			if (fieldInfo is null) FileLog.Debug($"AccessTools.DeclaredField: Could not find field for type {type} and idx {idx}");
			return fieldInfo;
		}

		/// <summary>Gets the reflection information for a directly declared property</summary>
		/// <param name="type">The class/type where the property is declared</param>
		/// <param name="name">The name of the property (case sensitive)</param>
		/// <returns>A property or null when type/name is null or when the property cannot be found</returns>
		///
		public static PropertyInfo DeclaredProperty(Type type, string name)
		{
			if (type is null)
			{
				FileLog.Debug("AccessTools.DeclaredProperty: type is null");
				return null;
			}
			if (string.IsNullOrEmpty(name))
			{
				FileLog.Debug("AccessTools.DeclaredProperty: name is null/empty");
				return null;
			}
			var property = type.GetProperty(name, allDeclared);
			if (property is null) FileLog.Debug($"AccessTools.DeclaredProperty: Could not find property for type {type} and name {name}");
			return property;
		}

		/// <summary>Gets the reflection information for a directly declared property</summary>
		/// <param name="typeColonName">The member in the form <c>TypeFullName:MemberName</c>, where TypeFullName matches the form recognized by <a href="https://docs.microsoft.com/en-us/dotnet/api/system.type.gettype">Type.GetType</a> like <c>Some.Namespace.Type</c>.</param>
		/// <returns>A property or null when the property cannot be found</returns>
		///
		public static PropertyInfo DeclaredProperty(string typeColonName)
		{
			var info = Tools.TypColonName(typeColonName);
			var property = info.type.GetProperty(info.name, allDeclared);
			if (property is null) FileLog.Debug($"AccessTools.DeclaredProperty: Could not find property for type {info.type} and name {info.name}");
			return property;
		}

		/// <summary>Gets the reflection information for a directly declared indexer property</summary>
		/// <param name="type">The class/type where the indexer property is declared</param>
		/// <param name="parameters">Optional parameters to target a specific overload of multiple indexers</param>
		/// <returns>An indexer property or null when type is null or when it cannot be found</returns>
		///
		public static PropertyInfo DeclaredIndexer(Type type, Type[] parameters = null)
		{
			if (type is null)
			{
				FileLog.Debug("AccessTools.DeclaredIndexer: type is null");
				return null;
			}

			try
			{
				// Can find multiple indexers without specified parameters, but only one with specified ones
				var indexer = parameters is null ?
					type.GetProperties(allDeclared).SingleOrDefault(property => property.GetIndexParameters().Length > 0)
					: type.GetProperties(allDeclared).FirstOrDefault(property => property.GetIndexParameters().Select(param => param.ParameterType).SequenceEqual(parameters));

				if (indexer is null) FileLog.Debug($"AccessTools.DeclaredIndexer: Could not find indexer for type {type} and parameters {parameters?.Description()}");

				return indexer;
			}
			catch (InvalidOperationException ex)
			{
				throw new AmbiguousMatchException("Multiple possible indexers were found.", ex);
			}
		}

		/// <summary>Gets the reflection information for the getter method of a directly declared property</summary>
		/// <param name="type">The class/type where the property is declared</param>
		/// <param name="name">The name of the property (case sensitive)</param>
		/// <returns>A method or null when type/name is null or when the property cannot be found</returns>
		///
		public static MethodInfo DeclaredPropertyGetter(Type type, string name) => DeclaredProperty(type, name)?.GetGetMethod(true);

		/// <summary>Gets the reflection information for the getter method of a directly declared property</summary>
		/// <param name="typeColonName">The member in the form <c>TypeFullName:MemberName</c>, where TypeFullName matches the form recognized by <a href="https://docs.microsoft.com/en-us/dotnet/api/system.type.gettype">Type.GetType</a> like <c>Some.Namespace.Type</c>.</param>
		/// <returns>A method or null when the property cannot be found</returns>
		///
		public static MethodInfo DeclaredPropertyGetter(string typeColonName) => DeclaredProperty(typeColonName)?.GetGetMethod(true);

		/// <summary>Gets the reflection information for the getter method of a directly declared indexer property</summary>
		/// <param name="type">The class/type where the indexer property is declared</param>
		/// <param name="parameters">Optional parameters to target a specific overload of multiple indexers</param>
		/// <returns>A method or null when type is null or when indexer property cannot be found</returns>
		///
		public static MethodInfo DeclaredIndexerGetter(Type type, Type[] parameters = null) => DeclaredIndexer(type, parameters)?.GetGetMethod(true);

		/// <summary>Gets the reflection information for the setter method of a directly declared property</summary>
		/// <param name="type">The class/type where the property is declared</param>
		/// <param name="name">The name of the property (case sensitive)</param>
		/// <returns>A method or null when type/name is null or when the property cannot be found</returns>
		///
		public static MethodInfo DeclaredPropertySetter(Type type, string name) => DeclaredProperty(type, name)?.GetSetMethod(true);

		/// <summary>Gets the reflection information for the Setter method of a directly declared property</summary>
		/// <param name="typeColonName">The member in the form <c>TypeFullName:MemberName</c>, where TypeFullName matches the form recognized by <a href="https://docs.microsoft.com/en-us/dotnet/api/system.type.gettype">Type.GetType</a> like <c>Some.Namespace.Type</c>.</param>
		/// <returns>A method or null when the property cannot be found</returns>
		///
		public static MethodInfo DeclaredPropertySetter(string typeColonName) => DeclaredProperty(typeColonName)?.GetSetMethod(true);

		/// <summary>Gets the reflection information for the setter method of a directly declared indexer property</summary>
		/// <param name="type">The class/type where the indexer property is declared</param>
		/// <param name="parameters">Optional parameters to target a specific overload of multiple indexers</param>
		/// <returns>A method or null when type is null or when indexer property cannot be found</returns>
		///
		public static MethodInfo DeclaredIndexerSetter(Type type, Type[] parameters) => DeclaredIndexer(type, parameters)?.GetSetMethod(true);

		/// <summary>Gets the reflection information for a property by searching the type and all its super types</summary>
		/// <param name="type">The class/type</param>
		/// <param name="name">The name</param>
		/// <returns>A property or null when type/name is null or when the property cannot be found</returns>
		///
		public static PropertyInfo Property(Type type, string name)
		{
			if (type is null)
			{
				FileLog.Debug("AccessTools.Property: type is null");
				return null;
			}
			if (string.IsNullOrEmpty(name))
			{
				FileLog.Debug("AccessTools.Property: name is null/empty");
				return null;
			}
			var property = FindIncludingBaseTypes(type, t => t.GetProperty(name, all));
			if (property is null) FileLog.Debug($"AccessTools.Property: Could not find property for type {type} and name {name}");
			return property;
		}

		/// <summary>Gets the reflection information for a property by searching the type and all its super types</summary>
		/// <param name="typeColonName">The member in the form <c>TypeFullName:MemberName</c>, where TypeFullName matches the form recognized by <a href="https://docs.microsoft.com/en-us/dotnet/api/system.type.gettype">Type.GetType</a> like <c>Some.Namespace.Type</c>.</param>
		/// <returns>A property or null when the property cannot be found</returns>
		///
		public static PropertyInfo Property(string typeColonName)
		{
			var info = Tools.TypColonName(typeColonName);
			var property = FindIncludingBaseTypes(info.type, t => t.GetProperty(info.name, all));
			if (property is null) FileLog.Debug($"AccessTools.Property: Could not find property for type {info.type} and name {info.name}");
			return property;
		}

		/// <summary>Gets the reflection information for an indexer property by searching the type and all its super types</summary>
		/// <param name="type">The class/type</param>
		/// <param name="parameters">Optional parameters to target a specific overload of multiple indexers</param>
		/// <returns>An indexer property or null when type is null or when it cannot be found</returns>
		///
		public static PropertyInfo Indexer(Type type, Type[] parameters = null)
		{
			if (type is null)
			{
				FileLog.Debug("AccessTools.Indexer: type is null");
				return null;
			}

			// Can find multiple indexers without specified parameters, but only one with specified ones
			Func<Type, PropertyInfo> func = parameters is null ?
				t => t.GetProperties(all).SingleOrDefault(property => property.GetIndexParameters().Length > 0)
				: t => t.GetProperties(all).FirstOrDefault(property => property.GetIndexParameters().Select(param => param.ParameterType).SequenceEqual(parameters));

			try
			{
				var indexer = FindIncludingBaseTypes(type, func);

				if (indexer is null) FileLog.Debug($"AccessTools.Indexer: Could not find indexer for type {type} and parameters {parameters?.Description()}");

				return indexer;
			}
			catch (InvalidOperationException ex)
			{
				throw new AmbiguousMatchException("Multiple possible indexers were found.", ex);
			}
		}

		/// <summary>Gets the reflection information for the getter method of a property by searching the type and all its super types</summary>
		/// <param name="type">The class/type</param>
		/// <param name="name">The name</param>
		/// <returns>A method or null when type/name is null or when the property cannot be found</returns>
		///
		public static MethodInfo PropertyGetter(Type type, string name) => Property(type, name)?.GetGetMethod(true);

		/// <summary>Gets the reflection information for the getter method of a property by searching the type and all its super types</summary>
		/// <param name="typeColonName">The member in the form <c>TypeFullName:MemberName</c>, where TypeFullName matches the form recognized by <a href="https://docs.microsoft.com/en-us/dotnet/api/system.type.gettype">Type.GetType</a> like <c>Some.Namespace.Type</c>.</param>
		/// <returns>A method or null when type/name is null or when the property cannot be found</returns>
		///
		public static MethodInfo PropertyGetter(string typeColonName) => Property(typeColonName)?.GetGetMethod(true);

		/// <summary>Gets the reflection information for the getter method of an indexer property by searching the type and all its super types</summary>
		/// <param name="type">The class/type</param>
		/// <param name="parameters">Optional parameters to target a specific overload of multiple indexers</param>
		/// <returns>A method or null when type is null or when the indexer property cannot be found</returns>
		///
		public static MethodInfo IndexerGetter(Type type, Type[] parameters = null) => Indexer(type, parameters)?.GetGetMethod(true);

		/// <summary>Gets the reflection information for the setter method of a property by searching the type and all its super types</summary>
		/// <param name="type">The class/type</param>
		/// <param name="name">The name</param>
		/// <returns>A method or null when type/name is null or when the property cannot be found</returns>
		///
		public static MethodInfo PropertySetter(Type type, string name) => Property(type, name)?.GetSetMethod(true);

		/// <summary>Gets the reflection information for the setter method of a property by searching the type and all its super types</summary>
		/// <param name="typeColonName">The member in the form <c>TypeFullName:MemberName</c>, where TypeFullName matches the form recognized by <a href="https://docs.microsoft.com/en-us/dotnet/api/system.type.gettype">Type.GetType</a> like <c>Some.Namespace.Type</c>.</param>
		/// <returns>A method or null when type/name is null or when the property cannot be found</returns>
		///
		public static MethodInfo PropertySetter(string typeColonName) => Property(typeColonName)?.GetSetMethod(true);

		/// <summary>Gets the reflection information for the setter method of an indexer property by searching the type and all its super types</summary>
		/// <param name="type">The class/type</param>
		/// <param name="parameters">Optional parameters to target a specific overload of multiple indexers</param>
		/// <returns>A method or null when type is null or when the indexer property cannot be found</returns>
		///
		public static MethodInfo IndexerSetter(Type type, Type[] parameters = null) => Indexer(type, parameters)?.GetSetMethod(true);

		/// <summary>Gets the reflection information for a directly declared method</summary>
		/// <param name="type">The class/type where the method is declared</param>
		/// <param name="name">The name of the method (case sensitive)</param>
		/// <param name="parameters">Optional parameters to target a specific overload of the method</param>
		/// <param name="generics">Optional list of types that define the generic version of the method</param>
		/// <returns>A method or null when type/name is null or when the method cannot be found</returns>
		///
		public static MethodInfo DeclaredMethod(Type type, string name, Type[] parameters = null, Type[] generics = null)
		{
			if (type is null)
			{
				FileLog.Debug("AccessTools.DeclaredMethod: type is null");
				return null;
			}
			if (string.IsNullOrEmpty(name))
			{
				FileLog.Debug("AccessTools.DeclaredMethod: name is null/empty");
				return null;
			}
			MethodInfo result;
			var modifiers = new ParameterModifier[] { };

			if (parameters is null)
				result = type.GetMethod(name, allDeclared);
			else
				result = type.GetMethod(name, allDeclared, null, parameters, modifiers);

			if (result is null)
			{
				FileLog.Debug($"AccessTools.DeclaredMethod: Could not find method for type {type} and name {name} and parameters {parameters?.Description()}");
				return null;
			}

			if (generics is not null) result = result.MakeGenericMethod(generics);
			return result;
		}

		/// <summary>Gets the reflection information for a directly declared method</summary>
		/// <param name="typeColonName">The member in the form <c>TypeFullName:MemberName</c>, where TypeFullName matches the form recognized by <a href="https://docs.microsoft.com/en-us/dotnet/api/system.type.gettype">Type.GetType</a> like <c>Some.Namespace.Type</c>.</param>
		/// <param name="parameters">Optional parameters to target a specific overload of the method</param>
		/// <param name="generics">Optional list of types that define the generic version of the method</param>
		/// <returns>A method or null when the method cannot be found</returns>
		///
		public static MethodInfo DeclaredMethod(string typeColonName, Type[] parameters = null, Type[] generics = null)
		{
			var info = Tools.TypColonName(typeColonName);
			return DeclaredMethod(info.type, info.name, parameters, generics);
		}

		/// <summary>Gets the reflection information for a method by searching the type and all its super types</summary>
		/// <param name="type">The class/type where the method is declared</param>
		/// <param name="name">The name of the method (case sensitive)</param>
		/// <param name="parameters">Optional parameters to target a specific overload of the method</param>
		/// <param name="generics">Optional list of types that define the generic version of the method</param>
		/// <returns>A method or null when type/name is null or when the method cannot be found</returns>
		///
		public static MethodInfo Method(Type type, string name, Type[] parameters = null, Type[] generics = null)
		{
			if (type is null)
			{
				FileLog.Debug("AccessTools.Method: type is null");
				return null;
			}
			if (string.IsNullOrEmpty(name))
			{
				FileLog.Debug("AccessTools.Method: name is null/empty");
				return null;
			}
			MethodInfo result;
			var modifiers = new ParameterModifier[] { };
			if (parameters is null)
			{
				try
				{
					result = FindIncludingBaseTypes(type, t => t.GetMethod(name, all));
				}
				catch (AmbiguousMatchException ex)
				{
					result = FindIncludingBaseTypes(type, t => t.GetMethod(name, all, null, [], modifiers));
					if (result is null)
					{
						throw new AmbiguousMatchException($"Ambiguous match in Harmony patch for {type}:{name}", ex);
					}
				}
			}
			else
			{
				result = FindIncludingBaseTypes(type, t => t.GetMethod(name, all, null, parameters, modifiers));
			}

			if (result is null)
			{
				FileLog.Debug($"AccessTools.Method: Could not find method for type {type} and name {name} and parameters {parameters?.Description()}");
				return null;
			}

			if (generics is not null) result = result.MakeGenericMethod(generics);
			return result;
		}

		/// <summary>Gets the reflection information for a method by searching the type and all its super types</summary>
		/// <param name="typeColonName">The member in the form <c>TypeFullName:MemberName</c>, where TypeFullName matches the form recognized by <a href="https://docs.microsoft.com/en-us/dotnet/api/system.type.gettype">Type.GetType</a> like <c>Some.Namespace.Type</c>.</param>
		/// <param name="parameters">Optional parameters to target a specific overload of the method</param>
		/// <param name="generics">Optional list of types that define the generic version of the method</param>
		/// <returns>A method or null when the method cannot be found</returns>
		///
		public static MethodInfo Method(string typeColonName, Type[] parameters = null, Type[] generics = null)
		{
			var info = Tools.TypColonName(typeColonName);
			return Method(info.type, info.name, parameters, generics);
		}

		/// <summary>Gets the <see cref="IEnumerator.MoveNext"/> method of an enumerator method</summary>
		/// <param name="method">Enumerator method that creates the enumerator <see cref="IEnumerator" /></param>
		/// <returns>The internal <see cref="IEnumerator.MoveNext"/> method of the enumerator or <b>null</b> if no valid enumerator is detected</returns>
		///
		public static MethodInfo EnumeratorMoveNext(MethodBase method)
		{
			if (method is null)
			{
				FileLog.Debug("AccessTools.EnumeratorMoveNext: method is null");
				return null;
			}

			var codes = PatchProcessor.ReadMethodBody(method).Where(pair => pair.Key == OpCodes.Newobj);
			if (codes.Count() != 1)
			{
				FileLog.Debug($"AccessTools.EnumeratorMoveNext: {method.FullDescription()} contains no Newobj opcode");
				return null;
			}
			var ctor = codes.First().Value as ConstructorInfo;
			if (ctor == null)
			{
				FileLog.Debug($"AccessTools.EnumeratorMoveNext: {method.FullDescription()} contains no constructor");
				return null;
			}
			var type = ctor.DeclaringType;
			if (type == null)
			{
				FileLog.Debug($"AccessTools.EnumeratorMoveNext: {method.FullDescription()} refers to a global type");
				return null;
			}
			return Method(type, nameof(IEnumerator.MoveNext));
		}

#if NET45_OR_GREATER || NETSTANDARD || NETCOREAPP
		/// <summary>Gets the <see cref="IAsyncStateMachine.MoveNext"/> method of an async method's state machine</summary>
		/// <param name="method">Async method that creates the state machine internally</param>
		/// <returns>The internal <see cref="IAsyncStateMachine.MoveNext"/> method of the async state machine or <b>null</b> if no valid async method is detected</returns>
		///
		public static MethodInfo AsyncMoveNext(MethodBase method)
		{
			if (method is null)
			{
				FileLog.Debug("AccessTools.AsyncMoveNext: method is null");
				return null;
			}

			var asyncAttribute = method.GetCustomAttribute<AsyncStateMachineAttribute>();
			if (asyncAttribute is null)
			{
				FileLog.Debug($"AccessTools.AsyncMoveNext: Could not find AsyncStateMachine for {method.FullDescription()}");
				return null;
			}

			var asyncStateMachineType = asyncAttribute.StateMachineType;
			var asyncMethodBody = DeclaredMethod(asyncStateMachineType, nameof(IAsyncStateMachine.MoveNext));
			if (asyncMethodBody is null)
			{
				FileLog.Debug($"AccessTools.AsyncMoveNext: Could not find async method body for {method.FullDescription()}");
				return null;
			}

			return asyncMethodBody;
		}
#endif

		/// <summary>Gets the names of all method that are declared in a type</summary>
		/// <param name="type">The declaring class/type</param>
		/// <returns>A list of method names</returns>
		///
		public static List<string> GetMethodNames(Type type)
		{
			if (type is null)
			{
				FileLog.Debug("AccessTools.GetMethodNames: type is null");
				return [];
			}
			return GetDeclaredMethods(type).Select(m => m.Name).ToList();
		}

		/// <summary>Gets the names of all method that are declared in the type of the instance</summary>
		/// <param name="instance">An instance of the type to search in</param>
		/// <returns>A list of method names</returns>
		///
		public static List<string> GetMethodNames(object instance)
		{
			if (instance is null)
			{
				FileLog.Debug("AccessTools.GetMethodNames: instance is null");
				return [];
			}
			return GetMethodNames(instance.GetType());
		}

		/// <summary>Gets the names of all fields that are declared in a type</summary>
		/// <param name="type">The declaring class/type</param>
		/// <returns>A list of field names</returns>
		///
		public static List<string> GetFieldNames(Type type)
		{
			if (type is null)
			{
				FileLog.Debug("AccessTools.GetFieldNames: type is null");
				return [];
			}
			return GetDeclaredFields(type).Select(f => f.Name).ToList();
		}

		/// <summary>Gets the names of all fields that are declared in the type of the instance</summary>
		/// <param name="instance">An instance of the type to search in</param>
		/// <returns>A list of field names</returns>
		///
		public static List<string> GetFieldNames(object instance)
		{
			if (instance is null)
			{
				FileLog.Debug("AccessTools.GetFieldNames: instance is null");
				return [];
			}
			return GetFieldNames(instance.GetType());
		}

		/// <summary>Gets the names of all properties that are declared in a type</summary>
		/// <param name="type">The declaring class/type</param>
		/// <returns>A list of property names</returns>
		///
		public static List<string> GetPropertyNames(Type type)
		{
			if (type is null)
			{
				FileLog.Debug("AccessTools.GetPropertyNames: type is null");
				return [];
			}
			return GetDeclaredProperties(type).Select(f => f.Name).ToList();
		}

		/// <summary>Gets the names of all properties that are declared in the type of the instance</summary>
		/// <param name="instance">An instance of the type to search in</param>
		/// <returns>A list of property names</returns>
		///
		public static List<string> GetPropertyNames(object instance)
		{
			if (instance is null)
			{
				FileLog.Debug("AccessTools.GetPropertyNames: instance is null");
				return [];
			}
			return GetPropertyNames(instance.GetType());
		}

		/// <summary>Gets the type of any class member of</summary>
		/// <param name="member">A member</param>
		/// <returns>The class/type of this member</returns>
		///
		public static Type GetUnderlyingType(this MemberInfo member)
		{
			return member.MemberType switch
			{
				MemberTypes.Event => ((EventInfo)member).EventHandlerType,
				MemberTypes.Field => ((FieldInfo)member).FieldType,
				MemberTypes.Method => ((MethodInfo)member).ReturnType,
				MemberTypes.Property => ((PropertyInfo)member).PropertyType,
				_ => throw new ArgumentException("Member must be of type EventInfo, FieldInfo, MethodInfo, or PropertyInfo"),
			};
		}

		/// <summary>Test if a class member is actually an concrete implementation</summary>
		/// <param name="member">A member</param>
		/// <returns>True if the member is a declared</returns>
		///
		public static bool IsDeclaredMember<T>(this T member) where T : MemberInfo => member.DeclaringType == member.ReflectedType;

		/// <summary>Gets the real implementation of a class member</summary>
		/// <param name="member">A member</param>
		/// <returns>The member itself if its declared. Otherwise the member that is actually implemented in some base type</returns>
		///
		public static T GetDeclaredMember<T>(this T member) where T : MemberInfo
		{
			if (member.DeclaringType is null || member.IsDeclaredMember())
				return member;

			var metaToken = member.MetadataToken;
			var members = member.DeclaringType?.GetMembers(all) ?? [];
			foreach (var other in members)
				if (other.MetadataToken == metaToken)
					return (T)other;

			return member;
		}

		/// <summary>Gets the reflection information for a directly declared constructor</summary>
		/// <param name="type">The class/type where the constructor is declared</param>
		/// <param name="parameters">Optional parameters to target a specific overload of the constructor</param>
		/// <param name="searchForStatic">Optional parameters to only consider static constructors</param>
		/// <returns>A constructor info or null when type is null or when the constructor cannot be found</returns>
		///
		public static ConstructorInfo DeclaredConstructor(Type type, Type[] parameters = null, bool searchForStatic = false)
		{
			if (type is null)
			{
				FileLog.Debug("AccessTools.DeclaredConstructor: type is null");
				return null;
			}
			parameters ??= [];
			var flags = searchForStatic ? allDeclared & ~BindingFlags.Instance : allDeclared & ~BindingFlags.Static;
			return type.GetConstructor(flags, null, parameters, []);
		}

		/// <summary>Gets the reflection information for a constructor by searching the type and all its super types</summary>
		/// <param name="type">The class/type where the constructor is declared</param>
		/// <param name="parameters">Optional parameters to target a specific overload of the method</param>
		/// <param name="searchForStatic">Optional parameters to only consider static constructors</param>
		/// <returns>A constructor info or null when type is null or when the method cannot be found</returns>
		///
		public static ConstructorInfo Constructor(Type type, Type[] parameters = null, bool searchForStatic = false)
		{
			if (type is null)
			{
				FileLog.Debug("AccessTools.ConstructorInfo: type is null");
				return null;
			}
			parameters ??= [];
			var flags = searchForStatic ? all & ~BindingFlags.Instance : all & ~BindingFlags.Static;
			return FindIncludingBaseTypes(type, t => t.GetConstructor(flags, null, parameters, []));
		}

		/// <summary>Gets reflection information for all declared constructors</summary>
		/// <param name="type">The class/type where the constructors are declared</param>
		/// <param name="searchForStatic">Optional parameters to only consider static constructors</param>
		/// <returns>A list of constructor infos</returns>
		///
		public static List<ConstructorInfo> GetDeclaredConstructors(Type type, bool? searchForStatic = null)
		{
			if (type is null)
			{
				FileLog.Debug("AccessTools.GetDeclaredConstructors: type is null");
				return [];
			}
			var flags = allDeclared;
			if (searchForStatic.HasValue)
				flags = searchForStatic.Value ? flags & ~BindingFlags.Instance : flags & ~BindingFlags.Static;
			return type.GetConstructors(flags).Where(method => method.DeclaringType == type).ToList();
		}

		/// <summary>Gets reflection information for all declared methods</summary>
		/// <param name="type">The class/type where the methods are declared</param>
		/// <returns>A list of methods</returns>
		///
		public static List<MethodInfo> GetDeclaredMethods(Type type)
		{
			if (type is null)
			{
				FileLog.Debug("AccessTools.GetDeclaredMethods: type is null");
				return [];
			}
			return [.. type.GetMethods(allDeclared)];
		}

		/// <summary>Gets reflection information for all declared properties</summary>
		/// <param name="type">The class/type where the properties are declared</param>
		/// <returns>A list of properties</returns>
		///
		public static List<PropertyInfo> GetDeclaredProperties(Type type)
		{
			if (type is null)
			{
				FileLog.Debug("AccessTools.GetDeclaredProperties: type is null");
				return [];
			}
			return [.. type.GetProperties(allDeclared)];
		}

		/// <summary>Gets reflection information for all declared fields</summary>
		/// <param name="type">The class/type where the fields are declared</param>
		/// <returns>A list of fields</returns>
		///
		public static List<FieldInfo> GetDeclaredFields(Type type)
		{
			if (type is null)
			{
				FileLog.Debug("AccessTools.GetDeclaredFields: type is null");
				return [];
			}
			return [.. type.GetFields(allDeclared)];
		}

		/// <summary>Gets the return type of a method or constructor</summary>
		/// <param name="methodOrConstructor">The method/constructor</param>
		/// <returns>The return type</returns>
		///
		public static Type GetReturnedType(MethodBase methodOrConstructor)
		{
			if (methodOrConstructor is null)
			{
				FileLog.Debug("AccessTools.GetReturnedType: methodOrConstructor is null");
				return null;
			}
			var constructor = methodOrConstructor as ConstructorInfo;
			if (constructor is not null) return typeof(void);
			return ((MethodInfo)methodOrConstructor).ReturnType;
		}

		/// <summary>Given a type, returns the first inner type matching a recursive search by name</summary>
		/// <param name="type">The class/type to start searching at</param>
		/// <param name="name">The name of the inner type (case sensitive)</param>
		/// <returns>The inner type or null if type/name is null or if a type with that name cannot be found</returns>
		///
		public static Type Inner(Type type, string name)
		{
			if (type is null)
			{
				FileLog.Debug("AccessTools.Inner: type is null");
				return null;
			}
			if (string.IsNullOrEmpty(name))
			{
				FileLog.Debug("AccessTools.Inner: name is null/empty");
				return null;
			}
			return FindIncludingBaseTypes(type, t => t.GetNestedType(name, all));
		}

		/// <summary>Given a type, returns the first inner type matching a recursive search with a predicate</summary>
		/// <param name="type">The class/type to start searching at</param>
		/// <param name="predicate">The predicate to search with</param>
		/// <returns>The inner type or null if type/predicate is null or if a type with that name cannot be found</returns>
		///
		public static Type FirstInner(Type type, Func<Type, bool> predicate)
		{
			if (type is null)
			{
				FileLog.Debug("AccessTools.FirstInner: type is null");
				return null;
			}
			if (predicate is null)
			{
				FileLog.Debug("AccessTools.FirstInner: predicate is null");
				return null;
			}
			return type.GetNestedTypes(all).FirstOrDefault(subType => predicate(subType));
		}

		/// <summary>Given a type, returns the first method matching a predicate</summary>
		/// <param name="type">The class/type to start searching at</param>
		/// <param name="predicate">The predicate to search with</param>
		/// <returns>The method or null if type/predicate is null or if a type with that name cannot be found</returns>
		///
		public static MethodInfo FirstMethod(Type type, Func<MethodInfo, bool> predicate)
		{
			if (type is null)
			{
				FileLog.Debug("AccessTools.FirstMethod: type is null");
				return null;
			}
			if (predicate is null)
			{
				FileLog.Debug("AccessTools.FirstMethod: predicate is null");
				return null;
			}
			return type.GetMethods(allDeclared).FirstOrDefault(method => predicate(method));
		}

		/// <summary>Given a type, returns the first constructor matching a predicate</summary>
		/// <param name="type">The class/type to start searching at</param>
		/// <param name="predicate">The predicate to search with</param>
		/// <returns>The constructor info or null if type/predicate is null or if a type with that name cannot be found</returns>
		///
		public static ConstructorInfo FirstConstructor(Type type, Func<ConstructorInfo, bool> predicate)
		{
			if (type is null)
			{
				FileLog.Debug("AccessTools.FirstConstructor: type is null");
				return null;
			}
			if (predicate is null)
			{
				FileLog.Debug("AccessTools.FirstConstructor: predicate is null");
				return null;
			}
			return type.GetConstructors(allDeclared).FirstOrDefault(constructor => predicate(constructor));
		}

		/// <summary>Given a type, returns the first property matching a predicate</summary>
		/// <param name="type">The class/type to start searching at</param>
		/// <param name="predicate">The predicate to search with</param>
		/// <returns>The property or null if type/predicate is null or if a type with that name cannot be found</returns>
		///
		public static PropertyInfo FirstProperty(Type type, Func<PropertyInfo, bool> predicate)
		{
			if (type is null)
			{
				FileLog.Debug("AccessTools.FirstProperty: type is null");
				return null;
			}
			if (predicate is null)
			{
				FileLog.Debug("AccessTools.FirstProperty: predicate is null");
				return null;
			}
			return type.GetProperties(allDeclared).FirstOrDefault(property => predicate(property));
		}

		/// <summary>Returns an array containing the type of each object in the given array</summary>
		/// <param name="parameters">An array of objects</param>
		/// <returns>An array of types or an empty array if parameters is null (if an object is null, the type for it will be object)</returns>
		///
		public static Type[] GetTypes(object[] parameters)
		{
			if (parameters is null) return [];
			return parameters.Select(p => p is null ? typeof(object) : p.GetType()).ToArray();
		}

		/// <summary>Creates an array of input parameters for a given method and a given set of potential inputs</summary>
		/// <param name="method">The method/constructor you are planing to call</param>
		/// <param name="inputs"> The possible input parameters in any order</param>
		/// <returns>An object array matching the method signature</returns>
		///
		public static object[] ActualParameters(MethodBase method, object[] inputs)
		{
			var inputTypes = inputs.Select(obj => obj?.GetType()).ToList();
			return method.GetParameters().Select(p => p.ParameterType).Select(pType =>
			{
				var index = inputTypes.FindIndex(inType => inType is not null && pType.IsAssignableFrom(inType));
				if (index >= 0)
					return inputs[index];
				return GetDefaultValue(pType);
			}).ToArray();
		}

		/// <summary>A readable/assignable reference delegate to an instance field of a class or static field (NOT an instance field of a struct)</summary>
		/// <typeparam name="T">
		/// An arbitrary type if the field is static; otherwise the class that defines the field, or a parent class (including <see cref="object"/>),
		/// implemented interface, or derived class of this type
		/// </typeparam>
		/// <typeparam name="F">
		/// The type of the field; or if the field's type is a reference type (a class or interface, NOT a struct or other value type),
		/// a type that <see cref="Type.IsAssignableFrom(Type)">is assignable from</see> that type; or if the field's type is an enum type,
		/// either that type or the underlying integral type of that enum type
		/// </typeparam>
		/// <param name="instance">The runtime instance to access the field (ignored and can be omitted for static fields)</param>
		/// <returns>A readable/assignable reference to the field</returns>
		/// <exception cref="NullReferenceException">Null instance passed to a non-static field ref delegate</exception>
		/// <exception cref="InvalidCastException">
		/// Instance of invalid type passed to a non-static field ref delegate
		/// (this can happen if <typeparamref name="T"/> is a parent class or interface of the field's declaring type)
		/// </exception>
		/// <remarks>
		/// <para>
		/// This delegate cannot be used for instance fields of structs, since a struct instance passed to the delegate would be passed by
		/// value and thus would be a copy that only exists within the delegate's invocation. This is fine for a readonly reference,
		/// but makes assignment futile. Use <see cref="StructFieldRef{T, F}"/> instead.
		/// </para>
		/// <para>
		/// Note that <typeparamref name="T"/> is not required to be the field's declaring type. It can be a parent class (including <see cref="object"/>),
		/// implemented interface, or a derived class of the field's declaring type ("<c>instanceOfT is FieldDeclaringType</c>" must be possible).
		/// Specifically, <typeparamref name="F"/> must be <see cref="Type.IsAssignableFrom(Type)">assignable from</see> OR to the field's declaring type.
		/// Technically, this allows <c>Nullable</c>, although <c>Nullable</c> is only relevant for structs, and since only static fields of structs
		/// are allowed for this delegate, and the instance passed to such a delegate is ignored, this hardly matters.
		/// </para>
		/// <para>
		/// Similarly, <typeparamref name="F"/> is not required to be the field's field type, unless that type is a non-enum value type.
		/// It can be a parent class (including <c>object</c>) or implemented interface of the field's field type. It cannot be a derived class.
		/// This variance is not allowed for value types, since that would require boxing/unboxing, which is not allowed for ref values.
		/// Special case for enum types: <typeparamref name="F"/> can also be the underlying integral type of the enum type.
		/// Specifically, for reference types, <typeparamref name="F"/> must be <see cref="Type.IsAssignableFrom(Type)">assignable from</see>
		/// the field's field type; for non-enum value types, <typeparamref name="F"/> must be exactly the field's field type; for enum types,
		/// <typeparamref name="F"/> must be either the field's field type or the underyling integral type of that field type.
		/// </para>
		/// <para>
		/// This delegate supports static fields, even those defined in structs, for legacy reasons.
		/// For such static fields, <typeparamref name="T"/> is effectively ignored.
		/// Consider using <see cref="FieldRef{F}"/> (and <c>StaticFieldRefAccess</c> methods that return it) instead for static fields.
		/// </para>
		/// </remarks>
		///
		public delegate ref F FieldRef<in T, F>(T instance = default);

		/// <summary>Creates a field reference delegate for an instance field of a class</summary>
		/// <typeparam name="T">The class that defines the instance field, or derived class of this type</typeparam>
		/// <typeparam name="F">
		/// The type of the field; or if the field's type is a reference type (a class or interface, NOT a struct or other value type),
		/// a type that <see cref="Type.IsAssignableFrom(Type)">is assignable from</see> that type; or if the field's type is an enum type,
		/// either that type or the underlying integral type of that enum type
		/// </typeparam>
		/// <param name="fieldName">The name of the field</param>
		/// <returns>A readable/assignable <see cref="FieldRef{T,F}"/> delegate</returns>
		/// <remarks>
		/// <para>
		/// For backwards compatibility, there is no class constraint on <typeparamref name="T"/>.
		/// Instead, the non-value-type check is done at runtime within the method.
		/// </para>
		/// </remarks>
		///
		public static FieldRef<T, F> FieldRefAccess<T, F>(string fieldName)
		{
			if (fieldName is null)
				throw new ArgumentNullException(nameof(fieldName));
			try
			{
				var delegateInstanceType = typeof(T);
				if (delegateInstanceType.IsValueType)
					throw new ArgumentException("T (FieldRefAccess instance type) must not be a value type");
				return Tools.FieldRefAccess<T, F>(Tools.GetInstanceField(delegateInstanceType, fieldName), needCastclass: false);
			}
			catch (Exception ex)
			{
				throw new ArgumentException($"FieldRefAccess<{typeof(T)}, {typeof(F)}> for {fieldName} caused an exception", ex);
			}
		}

		/// <summary>Creates an instance field reference for a specific instance of a class</summary>
		/// <typeparam name="T">The class that defines the instance field, or derived class of this type</typeparam>
		/// <typeparam name="F">
		/// The type of the field; or if the field's type is a reference type (a class or interface, NOT a struct or other value type),
		/// a type that <see cref="Type.IsAssignableFrom(Type)">is assignable from</see> that type; or if the field's type is an enum type,
		/// either that type or the underlying integral type of that enum type
		/// </typeparam>
		/// <param name="instance">The instance</param>
		/// <param name="fieldName">The name of the field</param>
		/// <returns>A readable/assignable reference to the field</returns>
		/// <remarks>
		/// <para>
		/// This method is meant for one-off access to a field's value for a single instance.
		/// If you need to access a field's value for potentially multiple instances, use <see cref="FieldRefAccess{T, F}(string)"/> instead.
		/// <c>FieldRefAccess&lt;T, F&gt;(instance, fieldName)</c> is functionally equivalent to <c>FieldRefAccess&lt;T, F&gt;(fieldName)(instance)</c>.
		/// </para>
		/// <para>
		/// For backwards compatibility, there is no class constraint on <typeparamref name="T"/>.
		/// Instead, the non-value-type check is done at runtime within the method.
		/// </para>
		/// </remarks>
		///
		public static ref F FieldRefAccess<T, F>(T instance, string fieldName)
		{
			if (instance is null)
				throw new ArgumentNullException(nameof(instance));
			if (fieldName is null)
				throw new ArgumentNullException(nameof(fieldName));
			try
			{
				var delegateInstanceType = typeof(T);
				if (delegateInstanceType.IsValueType)
					throw new ArgumentException("T (FieldRefAccess instance type) must not be a value type");
				return ref Tools.FieldRefAccess<T, F>(Tools.GetInstanceField(delegateInstanceType, fieldName), needCastclass: false)(instance);
			}
			catch (Exception ex)
			{
				throw new ArgumentException($"FieldRefAccess<{typeof(T)}, {typeof(F)}> for {instance}, {fieldName} caused an exception", ex);
			}
		}

		/// <summary>Creates a field reference delegate for an instance field of a class or static field (NOT an instance field of a struct)</summary>
		/// <typeparam name="F">
		/// The type of the field; or if the field's type is a reference type (a class or interface, NOT a struct or other value type),
		/// a type that <see cref="Type.IsAssignableFrom(Type)">is assignable from</see> that type; or if the field's type is an enum type,
		/// either that type or the underlying integral type of that enum type
		/// </typeparam>
		/// <param name="type">
		/// The type that defines the field, or derived class of this type; must not be a struct type unless the field is static
		/// </param>
		/// <param name="fieldName">The name of the field</param>
		/// <returns>
		/// A readable/assignable <see cref="FieldRef{T,F}"/> delegate with <c>T=object</c>
		/// (for static fields, the <c>instance</c> delegate parameter is ignored)
		/// </returns>
		/// <remarks>
		/// <para>
		/// This method is meant for cases where the given type is only known at runtime and thus can't be used as a type parameter <c>T</c>
		/// in e.g. <see cref="FieldRefAccess{T, F}(string)"/>.
		/// </para>
		/// <para>
		/// This method supports static fields, even those defined in structs, for legacy reasons.
		/// Consider using <see cref="StaticFieldRefAccess{F}(Type, string)"/> (and other overloads) instead for static fields.
		/// </para>
		/// </remarks>
		///
		public static FieldRef<object, F> FieldRefAccess<F>(Type type, string fieldName)
		{
			if (type is null)
				throw new ArgumentNullException(nameof(type));
			if (fieldName is null)
				throw new ArgumentNullException(nameof(fieldName));
			try
			{
				var fieldInfo = Field(type, fieldName);
				if (fieldInfo is null)
					throw new MissingFieldException(type.Name, fieldName);
				// Backwards compatibility: This supports static fields, even those defined in structs. For static fields, T is effectively ignored.
				if (fieldInfo.IsStatic is false && fieldInfo.DeclaringType is Type declaringType)
				{
					// When fieldInfo is passed to FieldRefAccess methods, the T generic class constraint is insufficient to ensure that
					// the field is not a struct instance field, since T could be object, ValueType, or an interface that the struct implements.
					if (declaringType.IsValueType)
						throw new ArgumentException("Either FieldDeclaringType must be a class or field must be static");
				}
				// Field's declaring type cannot be object, since object has no fields, so always need a castclass for T=object.
				return Tools.FieldRefAccess<object, F>(fieldInfo, needCastclass: true);
			}
			catch (Exception ex)
			{
				throw new ArgumentException($"FieldRefAccess<{typeof(F)}> for {type}, {fieldName} caused an exception", ex);
			}
		}

		/// <summary>Creates a field reference delegate for an instance field of a class or static field (NOT an instance field of a struct)</summary>
		/// <typeparam name="F"> type of the field</typeparam>
		/// <param name="typeColonName">The member in the form <c>TypeFullName:MemberName</c>, where TypeFullName matches the form recognized by <a href="https://docs.microsoft.com/en-us/dotnet/api/system.type.gettype">Type.GetType</a> like <c>Some.Namespace.Type</c>.</param>
		/// <returns>A readable/assignable <see cref="FieldRef{T,F}"/> delegate with <c>T=object</c></returns>
		///
		public static FieldRef<object, F> FieldRefAccess<F>(string typeColonName)
		{
			var info = Tools.TypColonName(typeColonName);
			return FieldRefAccess<F>(info.type, info.name);
		}

		/// <summary>Creates a field reference delegate for an instance field of a class or static field (NOT an instance field of a struct)</summary>
		/// <typeparam name="T">
		/// An arbitrary type if the field is static; otherwise the class that defines the field, or a parent class (including <see cref="object"/>),
		/// implemented interface, or derived class of this type ("<c>instanceOfT is FieldDeclaringType</c>" must be possible)
		/// </typeparam>
		/// <typeparam name="F">
		/// The type of the field; or if the field's type is a reference type (a class or interface, NOT a struct or other value type),
		/// a type that <see cref="Type.IsAssignableFrom(Type)">is assignable from</see> that type; or if the field's type is an enum type,
		/// either that type or the underlying integral type of that enum type
		/// </typeparam>
		/// <param name="fieldInfo">The field</param>
		/// <returns>A readable/assignable <see cref="FieldRef{T,F}"/> delegate</returns>
		/// <remarks>
		/// <para>
		/// This method is meant for cases where the field has already been obtained, avoiding the field searching cost in
		/// e.g. <see cref="FieldRefAccess{T, F}(string)"/>.
		/// </para>
		/// <para>
		/// This method supports static fields, even those defined in structs, for legacy reasons.
		/// For such static fields, <typeparamref name="T"/> is effectively ignored.
		/// Consider using <see cref="StaticFieldRefAccess{T, F}(FieldInfo)"/> (and other overloads) instead for static fields.
		/// </para>
		/// <para>
		/// For backwards compatibility, there is no class constraint on <typeparamref name="T"/>.
		/// Instead, the non-value-type check is done at runtime within the method.
		/// </para>
		/// </remarks>
		///
		public static FieldRef<T, F> FieldRefAccess<T, F>(FieldInfo fieldInfo)
		{
			if (fieldInfo is null)
				throw new ArgumentNullException(nameof(fieldInfo));
			try
			{
				var delegateInstanceType = typeof(T);
				if (delegateInstanceType.IsValueType)
					throw new ArgumentException("T (FieldRefAccess instance type) must not be a value type");
				var needCastclass = false;
				// Backwards compatibility: FieldRefAccess<F>(Type type, string fieldName) used to delegate to this method,
				// and thus this method must support the same cases - namely, static fields. For static fields, T is effectively ignored.
				if (fieldInfo.IsStatic is false && fieldInfo.DeclaringType is Type declaringType)
				{
					// When fieldInfo is passed to FieldRefAccess methods, the T generic class constraint is insufficient to ensure that
					// the field is not a struct instance field, since T could be object, ValueType, or an interface that the struct implements.
					if (declaringType.IsValueType)
						throw new ArgumentException("Either FieldDeclaringType must be a class or field must be static");
					needCastclass = Tools.FieldRefNeedsClasscast(delegateInstanceType, declaringType);
				}
				return Tools.FieldRefAccess<T, F>(fieldInfo, needCastclass);
			}
			catch (Exception ex)
			{
				throw new ArgumentException($"FieldRefAccess<{typeof(T)}, {typeof(F)}> for {fieldInfo} caused an exception", ex);
			}
		}

		/// <summary>Creates a field reference for an instance field of a class</summary>
		/// <typeparam name="T">
		/// The type that defines the field; or a parent class (including <see cref="object"/>), implemented interface, or derived class of this type
		/// ("<c>instanceOfT is FieldDeclaringType</c>" must be possible)
		/// </typeparam>
		/// <typeparam name="F">
		/// The type of the field; or if the field's type is a reference type (a class or interface, NOT a struct or other value type),
		/// a type that <see cref="Type.IsAssignableFrom(Type)">is assignable from</see> that type; or if the field's type is an enum type,
		/// either that type or the underlying integral type of that enum type
		/// </typeparam>
		/// <param name="instance">The instance</param>
		/// <param name="fieldInfo">The field</param>
		/// <returns>A readable/assignable reference to the field</returns>
		/// <remarks>
		/// <para>
		/// This method is meant for one-off access to a field's value for a single instance and where the field has already been obtained.
		/// If you need to access a field's value for potentially multiple instances, use <see cref="FieldRefAccess{T, F}(FieldInfo)"/> instead.
		/// <c>FieldRefAccess&lt;T, F&gt;(instance, fieldInfo)</c> is functionally equivalent to <c>FieldRefAccess&lt;T, F&gt;(fieldInfo)(instance)</c>.
		/// </para>
		/// <para>
		/// For backwards compatibility, there is no class constraint on <typeparamref name="T"/>.
		/// Instead, the non-value-type check is done at runtime within the method.
		/// </para>
		/// </remarks>
		///
		public static ref F FieldRefAccess<T, F>(T instance, FieldInfo fieldInfo)
		{
			if (instance is null)
				throw new ArgumentNullException(nameof(instance));
			if (fieldInfo is null)
				throw new ArgumentNullException(nameof(fieldInfo));
			try
			{
				var delegateInstanceType = typeof(T);
				if (delegateInstanceType.IsValueType)
					throw new ArgumentException("T (FieldRefAccess instance type) must not be a value type");
				if (fieldInfo.IsStatic)
					throw new ArgumentException("Field must not be static");
				var needCastclass = false;
				if (fieldInfo.DeclaringType is Type declaringType)
				{
					// When fieldInfo is passed to FieldRefAccess methods, the T generic class constraint is insufficient to ensure that
					// the field is not a struct instance field, since T could be object, ValueType, or an interface that the struct implements.
					if (declaringType.IsValueType)
						throw new ArgumentException("FieldDeclaringType must be a class");
					needCastclass = Tools.FieldRefNeedsClasscast(delegateInstanceType, declaringType);
				}
				return ref Tools.FieldRefAccess<T, F>(fieldInfo, needCastclass)(instance);
			}
			catch (Exception ex)
			{
				throw new ArgumentException($"FieldRefAccess<{typeof(T)}, {typeof(F)}> for {instance}, {fieldInfo} caused an exception", ex);
			}
		}

		/// <summary>A readable/assignable reference delegate to an instance field of a struct</summary>
		/// <typeparam name="T">The struct that defines the instance field</typeparam>
		/// <typeparam name="F">
		/// The type of the field; or if the field's type is a reference type (a class or interface, NOT a struct or other value type),
		/// a type that <see cref="Type.IsAssignableFrom(Type)">is assignable from</see> that type; or if the field's type is an enum type,
		/// either that type or the underlying integral type of that enum type
		/// </typeparam>
		/// <param name="instance">A reference to the runtime instance to access the field</param>
		/// <returns>A readable/assignable reference to the field</returns>
		///
		public delegate ref F StructFieldRef<T, F>(ref T instance) where T : struct;

		/// <summary>Creates a field reference delegate for an instance field of a struct</summary>
		/// <typeparam name="T">The struct that defines the instance field</typeparam>
		/// <typeparam name="F">
		/// The type of the field; or if the field's type is a reference type (a class or interface, NOT a struct or other value type),
		/// a type that <see cref="Type.IsAssignableFrom(Type)">is assignable from</see> that type; or if the field's type is an enum type,
		/// either that type or the underlying integral type of that enum type
		/// </typeparam>
		/// <param name="fieldName">The name of the field</param>
		/// <returns>A readable/assignable <see cref="StructFieldRef{T,F}"/> delegate</returns>
		///
		public static StructFieldRef<T, F> StructFieldRefAccess<T, F>(string fieldName) where T : struct
		{
			if (fieldName is null)
				throw new ArgumentNullException(nameof(fieldName));
			try
			{
				return Tools.StructFieldRefAccess<T, F>(Tools.GetInstanceField(typeof(T), fieldName));
			}
			catch (Exception ex)
			{
				throw new ArgumentException($"StructFieldRefAccess<{typeof(T)}, {typeof(F)}> for {fieldName} caused an exception", ex);
			}
		}

		/// <summary>Creates an instance field reference for a specific instance of a struct</summary>
		/// <typeparam name="T">The struct that defines the instance field</typeparam>
		/// <typeparam name="F">
		/// The type of the field; or if the field's type is a reference type (a class or interface, NOT a struct or other value type),
		/// a type that <see cref="Type.IsAssignableFrom(Type)">is assignable from</see> that type; or if the field's type is an enum type,
		/// either that type or the underlying integral type of that enum type
		/// </typeparam>
		/// <param name="instance">The instance</param>
		/// <param name="fieldName">The name of the field</param>
		/// <returns>A readable/assignable reference to the field</returns>
		/// <remarks>
		/// <para>
		/// This method is meant for one-off access to a field's value for a single instance.
		/// If you need to access a field's value for potentially multiple instances, use <see cref="StructFieldRefAccess{T, F}(string)"/> instead.
		/// <c>StructFieldRefAccess&lt;T, F&gt;(ref instance, fieldName)</c> is functionally equivalent to <c>StructFieldRefAccess&lt;T, F&gt;(fieldName)(ref instance)</c>.
		/// </para>
		/// </remarks>
		///
		public static ref F StructFieldRefAccess<T, F>(ref T instance, string fieldName) where T : struct
		{
			if (fieldName is null)
				throw new ArgumentNullException(nameof(fieldName));
			try
			{
				return ref Tools.StructFieldRefAccess<T, F>(Tools.GetInstanceField(typeof(T), fieldName))(ref instance);
			}
			catch (Exception ex)
			{
				throw new ArgumentException($"StructFieldRefAccess<{typeof(T)}, {typeof(F)}> for {instance}, {fieldName} caused an exception", ex);
			}
		}

		/// <summary>Creates a field reference delegate for an instance field of a struct</summary>
		/// <typeparam name="T">The struct that defines the instance field</typeparam>
		/// <typeparam name="F">
		/// The type of the field; or if the field's type is a reference type (a class or interface, NOT a struct or other value type),
		/// a type that <see cref="Type.IsAssignableFrom(Type)">is assignable from</see> that type; or if the field's type is an enum type,
		/// either that type or the underlying integral type of that enum type
		/// </typeparam>
		/// <param name="fieldInfo">The field</param>
		/// <returns>A readable/assignable <see cref="StructFieldRef{T,F}"/> delegate</returns>
		/// <remarks>
		/// <para>
		/// This method is meant for cases where the field has already been obtained, avoiding the field searching cost in
		/// e.g. <see cref="StructFieldRefAccess{T, F}(string)"/>.
		/// </para>
		/// </remarks>
		///
		public static StructFieldRef<T, F> StructFieldRefAccess<T, F>(FieldInfo fieldInfo) where T : struct
		{
			if (fieldInfo is null)
				throw new ArgumentNullException(nameof(fieldInfo));
			try
			{
				Tools.ValidateStructField<T, F>(fieldInfo);
				return Tools.StructFieldRefAccess<T, F>(fieldInfo);
			}
			catch (Exception ex)
			{
				throw new ArgumentException($"StructFieldRefAccess<{typeof(T)}, {typeof(F)}> for {fieldInfo} caused an exception", ex);
			}
		}

		/// <summary>Creates a field reference for an instance field of a struct</summary>
		/// <typeparam name="T">The struct that defines the instance field</typeparam>
		/// <typeparam name="F">
		/// The type of the field; or if the field's type is a reference type (a class or interface, NOT a struct or other value type),
		/// a type that <see cref="Type.IsAssignableFrom(Type)">is assignable from</see> that type; or if the field's type is an enum type,
		/// either that type or the underlying integral type of that enum type
		/// </typeparam>
		/// <param name="instance">The instance</param>
		/// <param name="fieldInfo">The field</param>
		/// <returns>A readable/assignable reference to the field</returns>
		/// <remarks>
		/// <para>
		/// This method is meant for one-off access to a field's value for a single instance and where the field has already been obtained.
		/// If you need to access a field's value for potentially multiple instances, use <see cref="StructFieldRefAccess{T, F}(FieldInfo)"/> instead.
		/// <c>StructFieldRefAccess&lt;T, F&gt;(ref instance, fieldInfo)</c> is functionally equivalent to <c>StructFieldRefAccess&lt;T, F&gt;(fieldInfo)(ref instance)</c>.
		/// </para>
		/// </remarks>
		///
		public static ref F StructFieldRefAccess<T, F>(ref T instance, FieldInfo fieldInfo) where T : struct
		{
			if (fieldInfo is null)
				throw new ArgumentNullException(nameof(fieldInfo));
			try
			{
				Tools.ValidateStructField<T, F>(fieldInfo);
				return ref Tools.StructFieldRefAccess<T, F>(fieldInfo)(ref instance);
			}
			catch (Exception ex)
			{
				throw new ArgumentException($"StructFieldRefAccess<{typeof(T)}, {typeof(F)}> for {instance}, {fieldInfo} caused an exception", ex);
			}
		}

		/// <summary>A readable/assignable reference delegate to a static field</summary>
		/// <typeparam name="F">
		/// The type of the field; or if the field's type is a reference type (a class or interface, NOT a struct or other value type),
		/// a type that <see cref="Type.IsAssignableFrom(Type)">is assignable from</see> that type; or if the field's type is an enum type,
		/// either that type or the underlying integral type of that enum type
		/// </typeparam>
		/// <returns>A readable/assignable reference to the field</returns>
		///
		public delegate ref F FieldRef<F>();

		/// <summary>Creates a static field reference</summary>
		/// <typeparam name="T">The type (can be class or struct) the field is defined in</typeparam>
		/// <typeparam name="F">
		/// The type of the field; or if the field's type is a reference type (a class or interface, NOT a struct or other value type),
		/// a type that <see cref="Type.IsAssignableFrom(Type)">is assignable from</see> that type; or if the field's type is an enum type,
		/// either that type or the underlying integral type of that enum type
		/// </typeparam>
		/// <param name="fieldName">The name of the field</param>
		/// <returns>A readable/assignable reference to the field</returns>
		///
		public static ref F StaticFieldRefAccess<T, F>(string fieldName) => ref StaticFieldRefAccess<F>(typeof(T), fieldName);

		/// <summary>Creates a static field reference</summary>
		/// <typeparam name="F">
		/// The type of the field; or if the field's type is a reference type (a class or interface, NOT a struct or other value type),
		/// a type that <see cref="Type.IsAssignableFrom(Type)">is assignable from</see> that type; or if the field's type is an enum type,
		/// either that type or the underlying integral type of that enum type
		/// </typeparam>
		/// <param name="type">The type (can be class or struct) the field is defined in</param>
		/// <param name="fieldName">The name of the field</param>
		/// <returns>A readable/assignable reference to the field</returns>
		///
		public static ref F StaticFieldRefAccess<F>(Type type, string fieldName)
		{
			try
			{
				var fieldInfo = Field(type, fieldName);
				if (fieldInfo is null)
					throw new MissingFieldException(type.Name, fieldName);
				return ref Tools.StaticFieldRefAccess<F>(fieldInfo)();
			}
			catch (Exception ex)
			{
				throw new ArgumentException($"StaticFieldRefAccess<{typeof(F)}> for {type}, {fieldName} caused an exception", ex);
			}
		}

		/// <summary>Creates a static field reference</summary>
		/// <typeparam name="F">The type of the field</typeparam>
		/// <param name="typeColonName">The member in the form <c>TypeFullName:MemberName</c>, where TypeFullName matches the form recognized by <a href="https://docs.microsoft.com/en-us/dotnet/api/system.type.gettype">Type.GetType</a> like <c>Some.Namespace.Type</c>.</param>
		/// <returns>A readable/assignable reference to the field</returns>
		///
		public static ref F StaticFieldRefAccess<F>(string typeColonName)
		{
			var info = Tools.TypColonName(typeColonName);
			return ref StaticFieldRefAccess<F>(info.type, info.name);
		}

		/// <summary>Creates a static field reference</summary>
		/// <typeparam name="T">An arbitrary type (by convention, the type the field is defined in)</typeparam>
		/// <typeparam name="F">
		/// The type of the field; or if the field's type is a reference type (a class or interface, NOT a struct or other value type),
		/// a type that <see cref="Type.IsAssignableFrom(Type)">is assignable from</see> that type; or if the field's type is an enum type,
		/// either that type or the underlying integral type of that enum type
		/// </typeparam>
		/// <param name="fieldInfo">The field</param>
		/// <returns>A readable/assignable reference to the field</returns>
		/// <remarks>
		/// The type parameter <typeparamref name="T"/> is only used in exception messaging and to distinguish between this method overload
		/// and the <see cref="StaticFieldRefAccess{F}(FieldInfo)"/> overload (which returns a <see cref="FieldRef{F}"/> rather than a reference).
		/// </remarks>
		///
		public static ref F StaticFieldRefAccess<T, F>(FieldInfo fieldInfo)
		{
			if (fieldInfo is null)
				throw new ArgumentNullException(nameof(fieldInfo));
			try
			{
				return ref Tools.StaticFieldRefAccess<F>(fieldInfo)();
			}
			catch (Exception ex)
			{
				throw new ArgumentException($"StaticFieldRefAccess<{typeof(T)}, {typeof(F)}> for {fieldInfo} caused an exception", ex);
			}
		}

		/// <summary>Creates a static field reference delegate</summary>
		/// <typeparam name="F">
		/// The type of the field; or if the field's type is a reference type (a class or interface, NOT a struct or other value type),
		/// a type that <see cref="Type.IsAssignableFrom(Type)">is assignable from</see> that type; or if the field's type is an enum type,
		/// either that type or the underlying integral type of that enum type
		/// </typeparam>
		/// <param name="fieldInfo">The field</param>
		/// <returns>A readable/assignable <see cref="FieldRef{F}"/> delegate</returns>
		///
		public static FieldRef<F> StaticFieldRefAccess<F>(FieldInfo fieldInfo)
		{
			if (fieldInfo is null)
				throw new ArgumentNullException(nameof(fieldInfo));
			try
			{
				return Tools.StaticFieldRefAccess<F>(fieldInfo);
			}
			catch (Exception ex)
			{
				throw new ArgumentException($"StaticFieldRefAccess<{typeof(F)}> for {fieldInfo} caused an exception", ex);
			}
		}

		/// <summary>Creates a delegate to a given method</summary>
		/// <typeparam name="DelegateType">The delegate Type</typeparam>
		/// <param name="method">The method to create a delegate from.</param>
		/// <param name="instance">
		/// Only applies for instance methods. If <c>null</c> (default), returned delegate is an open (a.k.a. unbound) instance delegate
		/// where an instance is supplied as the first argument to the delegate invocation; else, delegate is a closed (a.k.a. bound)
		/// instance delegate where the delegate invocation always applies to the given <paramref name="instance"/>.
		/// </param>
		/// <param name="virtualCall">
		/// Only applies for instance methods. If <c>true</c> (default) and <paramref name="method"/> is virtual, invocation of the delegate
		/// calls the instance method virtually (the instance type's most-derived/overriden implementation of the method is called);
		/// else, invocation of the delegate calls the exact specified <paramref name="method"/> (this is useful for calling base class methods)
		/// Note: if <c>false</c> and <paramref name="method"/> is an interface method, an ArgumentException is thrown.
		/// </param>
		/// <param name="delegateArgs">
		/// Only applies for instance methods, and if argument <paramref name="instance"/> is null.
		/// This argument only matters if the target <paramref name="method"/> signature contains a value type (such as struct or primitive types),
		/// and your <typeparamref name="DelegateType"/> argument is replaced by a non-value type
		/// (usually <c>object</c>) instead of using said value type.
		/// Use this if the generic arguments of <typeparamref name="DelegateType"/> doesn't represent the delegate's
		/// arguments, and calling this function fails
		/// <returns>A delegate of given <typeparamref name="DelegateType"/> to given <paramref name="method"/></returns>
		/// </param>
		/// <remarks>
		/// <param>
		/// Delegate invocation is more performant and more convenient to use than <see cref="MethodBase.Invoke(object, object[])"/>
		/// at a one-time setup cost.
		/// </param>
		/// <param>
		/// Works for both type of static and instance methods, both open and closed (a.k.a. unbound and bound) instance methods,
		/// and both class and struct methods.
		/// </param>
		/// </remarks>
		///
		public static DelegateType MethodDelegate<DelegateType>(MethodInfo method, object instance = null, bool virtualCall = true, Type[] delegateArgs = null) where DelegateType : Delegate
		{
			if (method is null)
				throw new ArgumentNullException(nameof(method));

			var delegateType = typeof(DelegateType);

			// Static method delegate
			if (method.IsStatic)
			{
				return (DelegateType)Delegate.CreateDelegate(delegateType, method);
			}

			var declaringType = method.DeclaringType;
			if (declaringType != null && declaringType.IsInterface && !virtualCall)
			{
				throw new ArgumentException("Interface methods must be called virtually");
			}

			// Open instance method delegate ...
			if (instance is null)
			{
				var delegateParameters = delegateType.GetMethod("Invoke").GetParameters();
				if (delegateParameters.Length == 0)
				{
					// Following should throw an ArgumentException with the proper message string.
					_ = Delegate.CreateDelegate(typeof(DelegateType), method);
					// But in case it doesn't...
					throw new ArgumentException("Invalid delegate type");
				}
				var delegateInstanceType = delegateParameters[0].ParameterType;
				// Exceptional case: delegate struct instance type cannot be created from an interface method.
				// This case is handled in the "non-virtual call" case, using the struct method and the matching delegate instance type.
				if (declaringType != null && declaringType.IsInterface && delegateInstanceType.IsValueType)
				{
					var interfaceMapping = delegateInstanceType.GetInterfaceMap(declaringType);
					method = interfaceMapping.TargetMethods[Array.IndexOf(interfaceMapping.InterfaceMethods, method)];
					declaringType = delegateInstanceType;
				}

				// ... that virtually calls ...
				if (declaringType != null && virtualCall)
				{
					// ... an interface method
					// If method is already an interface method, just create a delegate from it directly.
					if (declaringType.IsInterface)
					{
						return (DelegateType)Delegate.CreateDelegate(delegateType, method);
					}
					// delegate interface instance type requires interface method.
					if (delegateInstanceType.IsInterface)
					{
						var interfaceMapping = declaringType.GetInterfaceMap(delegateInstanceType);
						var interfaceMethod = interfaceMapping.InterfaceMethods[Array.IndexOf(interfaceMapping.TargetMethods, method)];
						return (DelegateType)Delegate.CreateDelegate(delegateType, interfaceMethod);
					}

					// ... a class instance method
					// Exceptional case: struct instance methods actually have their internal instance parameter passed by ref,
					// and thus are incompatible with typical non-ref-instance delegates
					// (delegate type must be: delegate <return type> MyStructDelegate(ref MyStruct instance, ...)),
					// so for struct instance methods, instead always use DynamicMethodDefinition approach,
					// so that typical non-ref-instance delegates work.
					if (!declaringType.IsValueType)
					{
						return (DelegateType)Delegate.CreateDelegate(delegateType, method.GetBaseDefinition());
					}
				}

				// ... that non-virtually calls
				var parameters = method.GetParameters();
				var numParameters = parameters.Length;
				var parameterTypes = new Type[numParameters + 1];
				parameterTypes[0] = declaringType;
				for (var i = 0; i < numParameters; i++)
					parameterTypes[i + 1] = parameters[i].ParameterType;
				var delegateArgsResolved = delegateArgs ?? delegateType.GetGenericArguments();
				var dynMethodReturn = delegateArgsResolved.Length < parameterTypes.Length
					? parameterTypes
					: delegateArgsResolved;
				var dmd = new DynamicMethodDefinition(
					"OpenInstanceDelegate_" + method.Name,
					method.ReturnType,
					dynMethodReturn)
				{
					// OwnerType = declaringType
				};
				var ilGen = dmd.GetILGenerator();
				if (declaringType != null && declaringType.IsValueType && delegateArgsResolved.Length > 0 &&
					!delegateArgsResolved[0].IsByRef)
				{
					ilGen.Emit(OpCodes.Ldarga_S, 0);
				}
				else
					ilGen.Emit(OpCodes.Ldarg_0);
				for (var i = 1; i < parameterTypes.Length; i++)
				{
					ilGen.Emit(OpCodes.Ldarg, i);
					// unbox to make il code valid
					if (parameterTypes[i].IsValueType && i < delegateArgsResolved.Length &&
						!delegateArgsResolved[i].IsValueType)
					{
						ilGen.Emit(OpCodes.Unbox_Any, parameterTypes[i]);
					}
				}

				ilGen.Emit(OpCodes.Call, method);
				ilGen.Emit(OpCodes.Ret);
				return (DelegateType)dmd.Generate().CreateDelegate(delegateType);
			}

			// Closed instance method delegate that virtually calls
			if (virtualCall)
			{
				return (DelegateType)Delegate.CreateDelegate(delegateType, instance, method.GetBaseDefinition());
			}

			// Closed instance method delegate that non-virtually calls
			// It's possible to create a delegate to a derived class method bound to a base class object,
			// but this has undefined behavior, so disallow it.
			if (declaringType != null && !declaringType.IsInstanceOfType(instance))
			{
				// Following should throw an ArgumentException with the proper message string.
				_ = Delegate.CreateDelegate(typeof(DelegateType), instance, method);
				// But in case it doesn't...
				throw new ArgumentException("Invalid delegate type");
			}
			// Mono had a bug where it internally uses the equivalent of ldvirtftn when calling delegate constructor on a method pointer,
			// so as a workaround, manually create a dynamic method to create the delegate using ldftn rather than ldvirtftn.
			// See https://github.com/mono/mono/issues/19964
			if (IsMonoRuntime)
			{
				var dmd = new DynamicMethodDefinition(
					"LdftnDelegate_" + method.Name,
					delegateType,
					[typeof(object)])
				{
					// OwnerType = delegateType
				};
				var ilGen = dmd.GetILGenerator();
				ilGen.Emit(OpCodes.Ldarg_0);
				ilGen.Emit(OpCodes.Ldftn, method);
				ilGen.Emit(OpCodes.Newobj, delegateType.GetConstructor([typeof(object), typeof(IntPtr)]));
				ilGen.Emit(OpCodes.Ret);
				return (DelegateType)dmd.Generate().Invoke(null, [instance]);
			}
			return (DelegateType)Activator.CreateInstance(delegateType, instance, method.MethodHandle.GetFunctionPointer());
		}

		/// <summary>Creates a delegate to a given method</summary>
		/// <typeparam name="DelegateType">The delegate Type</typeparam>
		/// <param name="typeColonName">The method in the form <c>TypeFullName:MemberName</c>, where TypeFullName matches the form recognized by <a href="https://docs.microsoft.com/en-us/dotnet/api/system.type.gettype">Type.GetType</a> like <c>Some.Namespace.Type</c>.</param>
		/// <param name="instance">
		/// Only applies for instance methods. If <c>null</c> (default), returned delegate is an open (a.k.a. unbound) instance delegate
		/// where an instance is supplied as the first argument to the delegate invocation; else, delegate is a closed (a.k.a. bound)
		/// instance delegate where the delegate invocation always applies to the given <paramref name="instance"/>.
		/// </param>
		/// <param name="virtualCall">
		/// Only applies for instance methods. If <c>true</c> (default) and <paramref name="typeColonName"/> is virtual, invocation of the delegate
		/// calls the instance method virtually (the instance type's most-derived/overriden implementation of the method is called);
		/// else, invocation of the delegate calls the exact specified <paramref name="typeColonName"/> (this is useful for calling base class methods)
		/// Note: if <c>false</c> and <paramref name="typeColonName"/> is an interface method, an ArgumentException is thrown.
		/// </param>
		/// <returns>A delegate of given <typeparamref name="DelegateType"/> to given <paramref name="typeColonName"/></returns>
		/// <remarks>
		/// <para>
		/// Delegate invocation is more performant and more convenient to use than <see cref="MethodBase.Invoke(object, object[])"/>
		/// at a one-time setup cost.
		/// </para>
		/// <para>
		/// Works for both type of static and instance methods, both open and closed (a.k.a. unbound and bound) instance methods,
		/// and both class and struct methods.
		/// </para>
		/// </remarks>
		///
		public static DelegateType MethodDelegate<DelegateType>(string typeColonName, object instance = null, bool virtualCall = true) where DelegateType : Delegate
		{
			var method = DeclaredMethod(typeColonName);
			return MethodDelegate<DelegateType>(method, instance, virtualCall);
		}

		/// <summary>Creates a delegate for a given delegate definition, attributed with [<see cref="HarmonyLib.HarmonyDelegate"/>]</summary>
		/// <typeparam name="DelegateType">The delegate Type, attributed with [<see cref="HarmonyLib.HarmonyDelegate"/>]</typeparam>
		/// <param name="instance">
		/// Only applies for instance methods. If <c>null</c> (default), returned delegate is an open (a.k.a. unbound) instance delegate
		/// where an instance is supplied as the first argument to the delegate invocation; else, delegate is a closed (a.k.a. bound)
		/// instance delegate where the delegate invocation always applies to the given <paramref name="instance"/>.
		/// </param>
		/// <returns>A delegate of given <typeparamref name="DelegateType"/> to the method specified via [<see cref="HarmonyLib.HarmonyDelegate"/>]
		/// attributes on <typeparamref name="DelegateType"/></returns>
		/// <remarks>
		/// This calls <see cref="MethodDelegate{DelegateType}(MethodInfo, object, bool, Type[])"/> with the <c>method</c> and <c>virtualCall</c> arguments
		/// determined from the [<see cref="HarmonyLib.HarmonyDelegate"/>] attributes on <typeparamref name="DelegateType"/>,
		/// and the given <paramref name="instance"/> (for closed instance delegates).
		/// </remarks>
		///
		public static DelegateType HarmonyDelegate<DelegateType>(object instance = null) where DelegateType : Delegate
		{
			var harmonyMethod = HarmonyMethodExtensions.GetMergedFromType(typeof(DelegateType));
			harmonyMethod.methodType ??= MethodType.Normal;
			var method = harmonyMethod.GetOriginalMethod() as MethodInfo;
			if (method is null)
				throw new NullReferenceException($"Delegate {typeof(DelegateType)} has no defined original method");
			return MethodDelegate<DelegateType>(method, instance, harmonyMethod.nonVirtualDelegate is false);
		}

		/// <summary>Returns who called the current method</summary>
		/// <returns>The calling method/constructor (excluding the caller)</returns>
		///
		public static MethodBase GetOutsideCaller()
		{
			var trace = new StackTrace(true);
			foreach (var frame in trace.GetFrames())
			{
				var method = frame.GetMethod();
				if (method.DeclaringType?.Namespace != typeof(Harmony).Namespace)
					return method;
			}
			throw new Exception("Unexpected end of stack trace");
		}

#if NET35
		static readonly MethodInfo m_PrepForRemoting = Method(typeof(Exception), "PrepForRemoting") // MS .NET
			?? Method(typeof(Exception), "FixRemotingException"); // mono .NET
		static readonly FastInvokeHandler PrepForRemoting = MethodInvoker.GetHandler(m_PrepForRemoting);
#endif

		/// <summary>Rethrows an exception while preserving its stack trace (throw statement typically clobbers existing stack traces)</summary>
		/// <param name="exception">The exception to rethrow</param>
		///
		public static void RethrowException(Exception exception)
		{
#if NET35
			_ = PrepForRemoting(exception);
#else
			System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception).Throw();
#endif
			// For the sake of any static code analyzer, always throw exception, even if ExceptionDispatchInfo.Throw above was called.
			throw exception;
		}

		/// <summary>True if the current runtime is based on Mono, false otherwise (.NET)</summary>
		///
		public static bool IsMonoRuntime { get; } = Type.GetType("Mono.Runtime") is not null;

		/// <summary>True if the current runtime is .NET Framework, false otherwise (.NET Core or Mono, although latter isn't guaranteed)</summary>
		///
		public static bool IsNetFrameworkRuntime { get; } =
			Type.GetType("System.Runtime.InteropServices.RuntimeInformation", false)?.GetProperty("FrameworkDescription")
			.GetValue(null, null).ToString().StartsWith(".NET Framework") ?? IsMonoRuntime is false;

		/// <summary>True if the current runtime is .NET Core, false otherwise (Mono or .NET Framework)</summary>
		///
		public static bool IsNetCoreRuntime { get; } =
			Type.GetType("System.Runtime.InteropServices.RuntimeInformation", false)?.GetProperty("FrameworkDescription")
			.GetValue(null, null).ToString().StartsWith(".NET Core") ?? false;

		/// <summary>Throws a missing member runtime exception</summary>
		/// <param name="type">The type that is involved</param>
		/// <param name="names">A list of names</param>
		///
		public static void ThrowMissingMemberException(Type type, params string[] names)
		{
			var fields = string.Join(",", [.. GetFieldNames(type)]);
			var properties = string.Join(",", [.. GetPropertyNames(type)]);
			throw new MissingMemberException($"{string.Join(",", names)}; available fields: {fields}; available properties: {properties}");
		}

		/// <summary>Gets default value for a specific type</summary>
		/// <param name="type">The class/type</param>
		/// <returns>The default value</returns>
		///
		public static object GetDefaultValue(Type type)
		{
			if (type is null)
			{
				FileLog.Debug("AccessTools.GetDefaultValue: type is null");
				return null;
			}
			if (type == typeof(void)) return null;
			if (type.IsValueType)
				return Activator.CreateInstance(type);
			return null;
		}

		/// <summary>Creates an (possibly uninitialized) instance of a given type</summary>
		/// <param name="type">The class/type</param>
		/// <returns>The new instance</returns>
		///
		public static object CreateInstance(Type type)
		{
			if (type is null)
				throw new ArgumentNullException(nameof(type));
			var ctor = type.GetConstructor(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, binder: null,
				CallingConventions.Any, [], modifiers: null);
			if (ctor is not null)
				return ctor.Invoke(null);
#if NET5_0_OR_GREATER
			return System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(type);
#else
			return System.Runtime.Serialization.FormatterServices.GetUninitializedObject(type);
#endif
		}

		/// <summary>Creates an (possibly uninitialized) instance of a given type</summary>
		/// <typeparam name="T">The class/type</typeparam>
		/// <returns>The new instance</returns>
		///
		public static T CreateInstance<T>()
		{
			var instance = CreateInstance(typeof(T));
			// Not using `as` operator since it only works with reference types.
			if (instance is T typedInstance)
				return typedInstance;
			return default;
		}

		/// <summary>
		/// A cache for the <see cref="ICollection{T}.Add"/> or similar Add methods for different types.
		/// </summary>
		///
		static readonly Dictionary<Type, FastInvokeHandler> addHandlerCache = [];

#if NET35
		static readonly ReaderWriterLock addHandlerCacheLock = new();
#else
		static readonly ReaderWriterLockSlim addHandlerCacheLock = new(LockRecursionPolicy.SupportsRecursion);
#endif

		/// <summary>Makes a deep copy of any object</summary>
		/// <typeparam name="T">The type of the instance that should be created; for legacy reasons, this must be a class or interface</typeparam>
		/// <param name="source">The original object</param>
		/// <returns>A copy of the original object but of type T</returns>
		///
		public static T MakeDeepCopy<T>(object source) where T : class => MakeDeepCopy(source, typeof(T)) as T;

		/// <summary>Makes a deep copy of any object</summary>
		/// <typeparam name="T">The type of the instance that should be created</typeparam>
		/// <param name="source">The original object</param>
		/// <param name="result">[out] The copy of the original object</param>
		/// <param name="processor">Optional value transformation function (taking a field name and src/dst <see cref="Traverse"/> instances)</param>
		/// <param name="pathRoot">The optional path root to start with</param>
		///
		public static void MakeDeepCopy<T>(object source, out T result, Func<string, Traverse, Traverse, object> processor = null, string pathRoot = "") => result = (T)MakeDeepCopy(source, typeof(T), processor, pathRoot);

		/// <summary>Makes a deep copy of any object</summary>
		/// <param name="source">The original object</param>
		/// <param name="resultType">The type of the instance that should be created</param>
		/// <param name="processor">Optional value transformation function (taking a field name and src/dst <see cref="Traverse"/> instances)</param>
		/// <param name="pathRoot">The optional path root to start with</param>
		/// <returns>The copy of the original object</returns>
		///
		public static object MakeDeepCopy(object source, Type resultType, Func<string, Traverse, Traverse, object> processor = null, string pathRoot = "")
		{
			if (source is null || resultType is null)
				return null;

			resultType = Nullable.GetUnderlyingType(resultType) ?? resultType;
			var type = source.GetType();

			if (type.IsPrimitive)
				return source;

			if (type.IsEnum)
				return Enum.ToObject(resultType, (int)source);

			if (type.IsGenericType && resultType.IsGenericType)
			{
#if NET35
				addHandlerCacheLock.AcquireReaderLock(200);
#else
				addHandlerCacheLock.EnterUpgradeableReadLock();
#endif
				try
				{
					if (!addHandlerCache.TryGetValue(resultType, out var addInvoker))
					{
						var addOperation = FirstMethod(resultType, m => m.Name == "Add" && m.GetParameters().Length == 1);
						if (addOperation is not null)
						{
							addInvoker = MethodInvoker.GetHandler(addOperation);
						}
#if NET35
						addHandlerCacheLock.UpgradeToWriterLock(200);
						addHandlerCacheLock.AcquireWriterLock(200);
#else
						addHandlerCacheLock.EnterWriteLock();
#endif
						try
						{
							addHandlerCache[resultType] = addInvoker;
						}
						finally
						{
#if NET35
							addHandlerCacheLock.ReleaseWriterLock();
#else
							addHandlerCacheLock.ExitWriteLock();
#endif
						}
					}
					if (addInvoker != null)
					{
						var addableResult = Activator.CreateInstance(resultType);
						var newElementType = resultType.GetGenericArguments()[0];
						var i = 0;
						foreach (var element in source as IEnumerable)
						{
							var iStr = (i++).ToString();
							var path = pathRoot.Length > 0 ? pathRoot + "." + iStr : iStr;
							var newElement = MakeDeepCopy(element, newElementType, processor, path);
							_ = addInvoker(addableResult, [newElement]);
						}
						return addableResult;
					}
				}
				finally
				{
#if NET35
					addHandlerCacheLock.ReleaseReaderLock();
#else
					addHandlerCacheLock.ExitUpgradeableReadLock();
#endif
				}
			}

			if (type.IsArray && resultType.IsArray)
			{
				var elementType = resultType.GetElementType();
				var length = ((Array)source).Length;
				var arrayResult = Activator.CreateInstance(resultType, [length]) as object[];
				var originalArray = source as object[];
				for (var i = 0; i < length; i++)
				{
					var iStr = i.ToString();
					var path = pathRoot.Length > 0 ? pathRoot + "." + iStr : iStr;
					arrayResult[i] = MakeDeepCopy(originalArray[i], elementType, processor, path);
				}
				return arrayResult;
			}

			var ns = type.Namespace;
			if (ns == "System" || (ns?.StartsWith("System.") ?? false))
				return source;

			var result = CreateInstance(resultType == typeof(object) ? type : resultType);
			Traverse.IterateFields(source, result, (name, src, dst) =>
			{
				var path = pathRoot.Length > 0 ? pathRoot + "." + name : name;
				var value = processor is not null ? processor(path, src, dst) : src.GetValue();
				if (dst.IsWriteable)
					_ = dst.SetValue(MakeDeepCopy(value, dst.GetValueType(), processor, path));
			});
			return result;
		}

		/// <summary>Tests if a type is a struct</summary>
		/// <param name="type">The type</param>
		/// <returns>True if the type is a struct</returns>
		///
		public static bool IsStruct(Type type)
		{
			if (type == null)
				return false;
			return type.IsValueType && !IsValue(type) && !IsVoid(type);
		}

		/// <summary>Tests if a type is a class</summary>
		/// <param name="type">The type</param>
		/// <returns>True if the type is a class</returns>
		///
		public static bool IsClass(Type type)
		{
			if (type == null)
				return false;
			return !type.IsValueType;
		}

		/// <summary>Tests if a type is a value type</summary>
		/// <param name="type">The type</param>
		/// <returns>True if the type is a value type</returns>
		///
		public static bool IsValue(Type type)
		{
			if (type == null)
				return false;
			return type.IsPrimitive || type.IsEnum;
		}

		/// <summary>Tests if a type is an integer type</summary>
		/// <param name="type">The type</param>
		/// <returns>True if the type represents some integer</returns>
		///
		public static bool IsInteger(Type type)
		{
			if (type == null)
				return false;
			return Type.GetTypeCode(type) switch
			{
				TypeCode.Byte or TypeCode.SByte or TypeCode.UInt16 or TypeCode.UInt32 or TypeCode.UInt64 or TypeCode.Int16 or TypeCode.Int32 or TypeCode.Int64 => true,
				_ => false,
			};
		}

		/// <summary>Tests if a type is a floating point type</summary>
		/// <param name="type">The type</param>
		/// <returns>True if the type represents some floating point</returns>
		///
		public static bool IsFloatingPoint(Type type)
		{
			if (type == null)
				return false;
			return Type.GetTypeCode(type) switch
			{
				TypeCode.Decimal or TypeCode.Double or TypeCode.Single => true,
				_ => false,
			};
		}

		/// <summary>Tests if a type is a numerical type</summary>
		/// <param name="type">The type</param>
		/// <returns>True if the type represents some number</returns>
		///
		public static bool IsNumber(Type type) => IsInteger(type) || IsFloatingPoint(type);

		/// <summary>Tests if a type is void</summary>
		/// <param name="type">The type</param>
		/// <returns>True if the type is void</returns>
		///
		public static bool IsVoid(Type type) => type == typeof(void);

		/// <summary>Test whether an instance is of a nullable type</summary>
		/// <typeparam name="T">Type of instance</typeparam>
		/// <param name="instance">An instance to test</param>
		/// <returns>True if instance is of nullable type, false if not</returns>
		///
#pragma warning disable IDE0060
		public static bool IsOfNullableType<T>(T instance) => Nullable.GetUnderlyingType(typeof(T)) is not null;

		/// <summary>Tests whether a type or member is static, as defined in C#</summary>
		/// <param name="member">The type or member</param>
		/// <returns>True if the type or member is static</returns>
		///
		public static bool IsStatic(MemberInfo member)
		{
			if (member is null)
				throw new ArgumentNullException(nameof(member));
			return member.MemberType switch
			{
				MemberTypes.Constructor or MemberTypes.Method => ((MethodBase)member).IsStatic,
				MemberTypes.Event => IsStatic((EventInfo)member),
				MemberTypes.Field => ((FieldInfo)member).IsStatic,
				MemberTypes.Property => IsStatic((PropertyInfo)member),
				MemberTypes.TypeInfo or MemberTypes.NestedType => IsStatic((Type)member),
				_ => throw new ArgumentException($"Unknown member type: {member.MemberType}"),
			};
		}

		/// <summary>Tests whether a type is static, as defined in C#</summary>
		/// <param name="type">The type</param>
		/// <returns>True if the type is static</returns>
		///
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static bool IsStatic(Type type)
		{
			if (type is null)
				return false;
			return type.IsAbstract && type.IsSealed;
		}

		/// <summary>Tests whether a property is static, as defined in C#</summary>
		/// <param name="propertyInfo">The property</param>
		/// <returns>True if the property is static</returns>
		///
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static bool IsStatic(PropertyInfo propertyInfo)
		{
			if (propertyInfo is null)
				throw new ArgumentNullException(nameof(propertyInfo));
			return propertyInfo.GetAccessors(true)[0].IsStatic;
		}

		/// <summary>Tests whether an event is static, as defined in C#</summary>
		/// <param name="eventInfo">The event</param>
		/// <returns>True if the event is static</returns>
		///
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static bool IsStatic(EventInfo eventInfo)
		{
			if (eventInfo is null)
				throw new ArgumentNullException(nameof(eventInfo));
			return eventInfo.GetAddMethod(true).IsStatic;
		}

		/// <summary>Calculates a combined hash code for an enumeration of objects</summary>
		/// <param name="objects">The objects</param>
		/// <returns>The hash code</returns>
		///
		public static int CombinedHashCode(IEnumerable<object> objects)
		{
			var hash1 = (5381 << 16) + 5381;
			var hash2 = hash1;
			var i = 0;
			foreach (var obj in objects)
			{
				if (i % 2 == 0)
					hash1 = ((hash1 << 5) + hash1 + (hash1 >> 27)) ^ obj.GetHashCode();
				else
					hash2 = ((hash2 << 5) + hash2 + (hash2 >> 27)) ^ obj.GetHashCode();
				++i;
			}
			return hash1 + (hash2 * 1566083941);
		}
	}
}
