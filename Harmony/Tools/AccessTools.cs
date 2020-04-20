using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Serialization;
using MonoMod.Utils;

namespace HarmonyLib
{
	/// <summary>A helper class for reflection related functions</summary>
	/// 
	public static class AccessTools
	{
		/// <summary>Shortcut for <see cref="BindingFlags"/> to simplify the use of reflections and make it work for any access level</summary>
		/// 
		public static BindingFlags all = BindingFlags.Public
			| BindingFlags.NonPublic
			| BindingFlags.Instance
			| BindingFlags.Static
			| BindingFlags.GetField
			| BindingFlags.SetField
			| BindingFlags.GetProperty
			| BindingFlags.SetProperty;

		/// <summary>Shortcut for <see cref="BindingFlags"/> to simplify the use of reflections and make it work for any access level but only within the current type</summary>
		/// 
		public static BindingFlags allDeclared = all | BindingFlags.DeclaredOnly;

		/// <summary>Gets a type by name. Prefers a full name with namespace but falls back to the first type matching the name otherwise</summary>
		/// <param name="name">The name</param>
		/// <returns>A type or null if not found</returns>
		///
		public static Type TypeByName(string name)
		{
			var type = Type.GetType(name, false);
			var assemblies = AppDomain.CurrentDomain.GetAssemblies().Where(a => a.FullName.StartsWith("Microsoft.VisualStudio") == false);
			if (type == null)
				type = assemblies
					.SelectMany(a => GetTypesFromAssembly(a))
					.FirstOrDefault(t => t.FullName == name);
			if (type == null)
				type = assemblies
					.SelectMany(a => GetTypesFromAssembly(a))
					.FirstOrDefault(t => t.Name == name);
			if (type == null && Harmony.DEBUG)
				FileLog.Log($"AccessTools.TypeByName: Could not find type named {name}");
			return type;
		}

		/// <summary>Gets all type by name from a given assembly. This is a wrapper that respects different .NET versions</summary>
		/// <param name="assembly">The assembly</param>
		/// <returns>An array of types</returns>
		/// 
		public static Type[] GetTypesFromAssembly(Assembly assembly)
		{
#if NETCOREAPP3_0 || NETCOREAPP3_1
			return assembly.DefinedTypes.ToArray();
#else
			return assembly.GetTypes();
#endif
		}

		/// <summary>Applies a function going up the type hierarchy and stops at the first non null result</summary>
		/// <typeparam name="T">Result type of func()</typeparam>
		/// <param name="type">The class/type to start with</param>
		/// <param name="func">The evaluation function returning T</param>
		/// <returns>Returns the first non null result or <c>default(T)</c> when reaching the top level type object</returns>
		///
		public static T FindIncludingBaseTypes<T>(Type type, Func<Type, T> func) where T : class
		{
			while (true)
			{
				var result = func(type);
#pragma warning disable RECS0017
				if (result != null) return result;
#pragma warning restore RECS0017
				if (type == typeof(object)) return default;
				type = type.BaseType;
			}
		}

		/// <summary>Applies a function going into inner types and stops at the first non null result</summary>
		/// <typeparam name="T">Generic type parameter</typeparam>
		/// <param name="type">The class/type to start with</param>
		/// <param name="func">The evaluation function returning T</param>
		/// <returns>Returns the first non null result or null with no match</returns>
		///
		public static T FindIncludingInnerTypes<T>(Type type, Func<Type, T> func) where T : class
		{
			var result = func(type);
#pragma warning disable RECS0017
			if (result != null) return result;
#pragma warning restore RECS0017
			foreach (var subType in type.GetNestedTypes(all))
			{
				result = FindIncludingInnerTypes(subType, func);
#pragma warning disable RECS0017
				if (result != null)
					break;
#pragma warning restore RECS0017
			}
			return result;
		}

		/// <summary>Gets the reflection information for a directly declared field</summary>
		/// <param name="type">The class/type where the field is defined</param>
		/// <param name="name">The name of the field</param>
		/// <returns>A field or null when type/name is null or when the field cannot be found</returns>
		///
		public static FieldInfo DeclaredField(Type type, string name)
		{
			if (type == null)
			{
				if (Harmony.DEBUG)
					FileLog.Log("AccessTools.DeclaredField: type is null");
				return null;
			}
			if (name == null)
			{
				if (Harmony.DEBUG)
					FileLog.Log("AccessTools.DeclaredField: name is null");
				return null;
			}
			var field = type.GetField(name, allDeclared);
			if (field == null && Harmony.DEBUG)
				FileLog.Log($"AccessTools.DeclaredField: Could not find field for type {type} and name {name}");
			return field;
		}

		/// <summary>Gets the reflection information for a field by searching the type and all its super types</summary>
		/// <param name="type">The class/type where the field is defined</param>
		/// <param name="name">The name of the field (case sensitive)</param>
		/// <returns>A field or null when type/name is null or when the field cannot be found</returns>
		///
		public static FieldInfo Field(Type type, string name)
		{
			if (type == null)
			{
				if (Harmony.DEBUG)
					FileLog.Log("AccessTools.Field: type is null");
				return null;
			}
			if (name == null)
			{
				if (Harmony.DEBUG)
					FileLog.Log("AccessTools.Field: name is null");
				return null;
			}
			var field = FindIncludingBaseTypes(type, t => t.GetField(name, all));
			if (field == null && Harmony.DEBUG)
				FileLog.Log($"AccessTools.Field: Could not find field for type {type} and name {name}");
			return field;
		}

		/// <summary>Gets the reflection information for a field</summary>
		/// <param name="type">The class/type where the field is declared</param>
		/// <param name="idx">The zero-based index of the field inside the class definition</param>
		/// <returns>A field or null when type is null or when the field cannot be found</returns>
		///
		public static FieldInfo DeclaredField(Type type, int idx)
		{
			if (type == null)
			{
				if (Harmony.DEBUG)
					FileLog.Log("AccessTools.DeclaredField: type is null");
				return null;
			}
			var field = GetDeclaredFields(type).ElementAtOrDefault(idx);
			if (field == null && Harmony.DEBUG)
				FileLog.Log($"AccessTools.DeclaredField: Could not find field for type {type} and idx {idx}");
			return field;
		}

		/// <summary>Gets the reflection information for a directly declared property</summary>
		/// <param name="type">The class/type where the property is declared</param>
		/// <param name="name">The name of the property (case sensitive)</param>
		/// <returns>A property or null when type/name is null or when the property cannot be found</returns>
		///
		public static PropertyInfo DeclaredProperty(Type type, string name)
		{
			if (type == null)
			{
				if (Harmony.DEBUG)
					FileLog.Log("AccessTools.DeclaredProperty: type is null");
				return null;
			}
			if (name == null)
			{
				if (Harmony.DEBUG)
					FileLog.Log("AccessTools.DeclaredProperty: name is null");
				return null;
			}
			var property = type.GetProperty(name, allDeclared);
			if (property == null && Harmony.DEBUG)
				FileLog.Log($"AccessTools.DeclaredProperty: Could not find property for type {type} and name {name}");
			return property;
		}

		/// <summary>Gets the reflection information for the getter method of a directly declared property</summary>
		/// <param name="type">The class/type where the property is declared</param>
		/// <param name="name">The name of the property (case sensitive)</param>
		/// <returns>A method or null when type/name is null or when the property cannot be found</returns>
		///
		public static MethodInfo DeclaredPropertyGetter(Type type, string name)
		{
			return DeclaredProperty(type, name)?.GetGetMethod(true);
		}

		/// <summary>Gets the reflection information for the setter method of a directly declared property</summary>
		/// <param name="type">The class/type where the property is declared</param>
		/// <param name="name">The name of the property (case sensitive)</param>
		/// <returns>A method or null when type/name is null or when the property cannot be found</returns>
		///
		public static MethodInfo DeclaredPropertySetter(Type type, string name)
		{
			return DeclaredProperty(type, name)?.GetSetMethod(true);
		}

		/// <summary>Gets the reflection information for a property by searching the type and all its super types</summary>
		/// <param name="type">The class/type</param>
		/// <param name="name">The name</param>
		/// <returns>A property or null when type/name is null or when the property cannot be found</returns>
		///
		public static PropertyInfo Property(Type type, string name)
		{
			if (type == null)
			{
				if (Harmony.DEBUG)
					FileLog.Log("AccessTools.Property: type is null");
				return null;
			}
			if (name == null)
			{
				if (Harmony.DEBUG)
					FileLog.Log("AccessTools.Property: name is null");
				return null;
			}
			var property = FindIncludingBaseTypes(type, t => t.GetProperty(name, all));
			if (property == null && Harmony.DEBUG)
				FileLog.Log($"AccessTools.Property: Could not find property for type {type} and name {name}");
			return property;
		}

		/// <summary>Gets the reflection information for the getter method of a property by searching the type and all its super types</summary>
		/// <param name="type">The class/type</param>
		/// <param name="name">The name</param>
		/// <returns>A method or null when type/name is null or when the property cannot be found</returns>
		///
		public static MethodInfo PropertyGetter(Type type, string name)
		{
			return Property(type, name)?.GetGetMethod(true);
		}

		/// <summary>Gets the reflection information for the setter method of a property by searching the type and all its super types</summary>
		/// <param name="type">The class/type</param>
		/// <param name="name">The name</param>
		/// <returns>A method or null when type/name is null or when the property cannot be found</returns>
		///
		public static MethodInfo PropertySetter(Type type, string name)
		{
			return Property(type, name)?.GetSetMethod(true);
		}

		/// <summary>Gets the reflection information for a directly declared method</summary>
		/// <param name="type">The class/type where the method is declared</param>
		/// <param name="name">The name of the method (case sensitive)</param>
		/// <param name="parameters">Optional parameters to target a specific overload of the method</param>
		/// <param name="generics">Optional list of types that define the generic version of the method</param>
		/// <returns>A method or null when type/name is null or when the method cannot be found</returns>
		///
		public static MethodInfo DeclaredMethod(Type type, string name, Type[] parameters = null, Type[] generics = null)
		{
			if (type == null)
			{
				if (Harmony.DEBUG)
					FileLog.Log("AccessTools.DeclaredMethod: type is null");
				return null;
			}
			if (name == null)
			{
				if (Harmony.DEBUG)
					FileLog.Log("AccessTools.DeclaredMethod: name is null");
				return null;
			}
			MethodInfo result;
			var modifiers = new ParameterModifier[] { };

			if (parameters == null)
				result = type.GetMethod(name, allDeclared);
			else
				result = type.GetMethod(name, allDeclared, null, parameters, modifiers);

			if (result == null)
			{
				if (Harmony.DEBUG)
					FileLog.Log($"AccessTools.DeclaredMethod: Could not find method for type {type} and name {name} and parameters {parameters?.Description()}");
				return null;
			}

			if (generics != null) result = result.MakeGenericMethod(generics);
			return result;
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
			if (type == null)
			{
				if (Harmony.DEBUG)
					FileLog.Log("AccessTools.Method: type is null");
				return null;
			}
			if (name == null)
			{
				if (Harmony.DEBUG)
					FileLog.Log("AccessTools.Method: name is null");
				return null;
			}
			MethodInfo result;
			var modifiers = new ParameterModifier[] { };
			if (parameters == null)
			{
				try
				{
					result = FindIncludingBaseTypes(type, t => t.GetMethod(name, all));
				}
				catch (AmbiguousMatchException ex)
				{
					result = FindIncludingBaseTypes(type, t => t.GetMethod(name, all, null, new Type[0], modifiers));

					if (result == null)
					{
						throw new AmbiguousMatchException($"Ambiguous match in Harmony patch for {type}:{name}." + ex);
					}
				}
			}
			else
			{
				result = FindIncludingBaseTypes(type, t => t.GetMethod(name, all, null, parameters, modifiers));
			}

			if (result == null)
			{
				if (Harmony.DEBUG)
					FileLog.Log($"AccessTools.Method: Could not find method for type {type} and name {name} and parameters {parameters?.Description()}");
				return null;
			}

			if (generics != null) result = result.MakeGenericMethod(generics);
			return result;
		}

		/// <summary>Gets the reflection information for a method by searching the type and all its super types</summary>
		/// <param name="typeColonMethodname">The full name like <c>Namespace.Type1.Type2:MethodName</c> of the type where the method is declared</param>
		/// <param name="parameters">Optional parameters to target a specific overload of the method</param>
		/// <param name="generics">Optional list of types that define the generic version of the method</param>
		/// <returns>A method or null when type/name is null or when the method cannot be found</returns>
		///
		public static MethodInfo Method(string typeColonMethodname, Type[] parameters = null, Type[] generics = null)
		{
			if (typeColonMethodname == null)
			{
				if (Harmony.DEBUG)
					FileLog.Log("AccessTools.Method: typeColonMethodname is null");
				return null;
			}
			var parts = typeColonMethodname.Split(':');
			if (parts.Length != 2)
				throw new ArgumentException("Method must be specified as 'Namespace.Type1.Type2:MethodName", nameof(typeColonMethodname));

			var type = TypeByName(parts[0]);
			return DeclaredMethod(type, parts[1], parameters, generics);
		}

		/// <summary>Gets the names of all method that are declared in a type</summary>
		/// <param name="type">The declaring class/type</param>
		/// <returns>A list of method names</returns>
		///
		public static List<string> GetMethodNames(Type type)
		{
			if (type == null)
			{
				if (Harmony.DEBUG)
					FileLog.Log("AccessTools.GetMethodNames: type is null");
				return new List<string>();
			}
			return GetDeclaredMethods(type).Select(m => m.Name).ToList();
		}

		/// <summary>Gets the names of all method that are declared in the type of the instance</summary>
		/// <param name="instance">An instance of the type to search in</param>
		/// <returns>A list of method names</returns>
		///
		public static List<string> GetMethodNames(object instance)
		{
			if (instance == null)
			{
				if (Harmony.DEBUG)
					FileLog.Log("AccessTools.GetMethodNames: instance is null");
				return new List<string>();
			}
			return GetMethodNames(instance.GetType());
		}

		/// <summary>Gets the names of all fields that are declared in a type</summary>
		/// <param name="type">The declaring class/type</param>
		/// <returns>A list of field names</returns>
		///
		public static List<string> GetFieldNames(Type type)
		{
			if (type == null)
			{
				if (Harmony.DEBUG)
					FileLog.Log("AccessTools.GetFieldNames: type is null");
				return new List<string>();
			}
			return GetDeclaredFields(type).Select(f => f.Name).ToList();
		}

		/// <summary>Gets the names of all fields that are declared in the type of the instance</summary>
		/// <param name="instance">An instance of the type to search in</param>
		/// <returns>A list of field names</returns>
		///
		public static List<string> GetFieldNames(object instance)
		{
			if (instance == null)
			{
				if (Harmony.DEBUG)
					FileLog.Log("AccessTools.GetFieldNames: instance is null");
				return new List<string>();
			}
			return GetFieldNames(instance.GetType());
		}

		/// <summary>Gets the names of all properties that are declared in a type</summary>
		/// <param name="type">The declaring class/type</param>
		/// <returns>A list of property names</returns>
		///
		public static List<string> GetPropertyNames(Type type)
		{
			if (type == null)
			{
				if (Harmony.DEBUG)
					FileLog.Log("AccessTools.GetPropertyNames: type is null");
				return new List<string>();
			}
			return GetDeclaredProperties(type).Select(f => f.Name).ToList();
		}

		/// <summary>Gets the names of all properties that are declared in the type of the instance</summary>
		/// <param name="instance">An instance of the type to search in</param>
		/// <returns>A list of property names</returns>
		///
		public static List<string> GetPropertyNames(object instance)
		{
			if (instance == null)
			{
				if (Harmony.DEBUG)
					FileLog.Log("AccessTools.GetPropertyNames: instance is null");
				return new List<string>();
			}
			return GetPropertyNames(instance.GetType());
		}

		/// <summary>Gets the type of any class member of</summary>
		/// <param name="member">A member</param>
		/// <returns>The class/type of this member</returns>
		///
		public static Type GetUnderlyingType(this MemberInfo member)
		{
			switch (member.MemberType)
			{
				case MemberTypes.Event:
					return ((EventInfo)member).EventHandlerType;
				case MemberTypes.Field:
					return ((FieldInfo)member).FieldType;
				case MemberTypes.Method:
					return ((MethodInfo)member).ReturnType;
				case MemberTypes.Property:
					return ((PropertyInfo)member).PropertyType;
				default:
					throw new ArgumentException("Member must be of type EventInfo, FieldInfo, MethodInfo, or PropertyInfo");
			}
		}

		/// <summary>Test if a class member is actually an concrete implementation</summary>
		/// <param name="member">A member</param>
		/// <returns>True if the member is a declared</returns>
		///
		public static bool IsDeclaredMember<T>(this T member) where T : MemberInfo
		{
			return member.DeclaringType == member.ReflectedType;
		}

		/// <summary>Gets the real implementation of a class member</summary>
		/// <param name="member">A member</param>
		/// <returns>The member itself if its declared. Otherwise the member that is actually implemented in some base type</returns>
		///
		public static T GetDeclaredMember<T>(this T member) where T : MemberInfo
		{
			if (member.DeclaringType == null || member.IsDeclaredMember())
				return member;

			var metaToken = member.MetadataToken;
			foreach (var other in member.DeclaringType.GetMembers(all))
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
			if (type == null)
			{
				if (Harmony.DEBUG)
					FileLog.Log("AccessTools.DeclaredConstructor: type is null");
				return null;
			}
			if (parameters == null) parameters = new Type[0];
			var flags = searchForStatic ? allDeclared & ~BindingFlags.Instance : allDeclared & ~BindingFlags.Static;
			return type.GetConstructor(flags, null, parameters, new ParameterModifier[] { });
		}

		/// <summary>Gets the reflection information for a constructor by searching the type and all its super types</summary>
		/// <param name="type">The class/type where the constructor is declared</param>
		/// <param name="parameters">Optional parameters to target a specific overload of the method</param>
		/// <param name="searchForStatic">Optional parameters to only consider static constructors</param>
		/// <returns>A constructor info or null when type is null or when the method cannot be found</returns>
		///
		public static ConstructorInfo Constructor(Type type, Type[] parameters = null, bool searchForStatic = false)
		{
			if (type == null)
			{
				if (Harmony.DEBUG)
					FileLog.Log("AccessTools.ConstructorInfo: type is null");
				return null;
			}
			if (parameters == null) parameters = new Type[0];
			var flags = searchForStatic ? all & ~BindingFlags.Instance : all & ~BindingFlags.Static;
			return FindIncludingBaseTypes(type, t => t.GetConstructor(flags, null, parameters, new ParameterModifier[] { }));
		}

		/// <summary>Gets reflection information for all declared constructors</summary>
		/// <param name="type">The class/type where the constructors are declared</param>
		/// <param name="searchForStatic">Optional parameters to only consider static constructors</param>
		/// <returns>A list of constructor infos</returns>
		///
		public static List<ConstructorInfo> GetDeclaredConstructors(Type type, bool? searchForStatic = null)
		{
			if (type == null)
			{
				if (Harmony.DEBUG)
					FileLog.Log("AccessTools.GetDeclaredConstructors: type is null");
				return null;
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
			if (type == null)
			{
				if (Harmony.DEBUG)
					FileLog.Log("AccessTools.GetDeclaredMethods: type is null");
				return null;
			}
			return type.GetMethods(allDeclared).ToList();
		}

		/// <summary>Gets reflection information for all declared properties</summary>
		/// <param name="type">The class/type where the properties are declared</param>
		/// <returns>A list of properties</returns>
		///
		public static List<PropertyInfo> GetDeclaredProperties(Type type)
		{
			if (type == null)
			{
				if (Harmony.DEBUG)
					FileLog.Log("AccessTools.GetDeclaredProperties: type is null");
				return null;
			}
			return type.GetProperties(allDeclared).ToList();
		}

		/// <summary>Gets reflection information for all declared fields</summary>
		/// <param name="type">The class/type where the fields are declared</param>
		/// <returns>A list of fields</returns>
		///
		public static List<FieldInfo> GetDeclaredFields(Type type)
		{
			if (type == null)
			{
				if (Harmony.DEBUG)
					FileLog.Log("AccessTools.GetDeclaredFields: type is null");
				return null;
			}
			return type.GetFields(allDeclared).ToList();
		}

		/// <summary>Gets the return type of a method or constructor</summary>
		/// <param name="methodOrConstructor">The method/constructor</param>
		/// <returns>The return type</returns>
		///
		public static Type GetReturnedType(MethodBase methodOrConstructor)
		{
			if (methodOrConstructor == null)
			{
				if (Harmony.DEBUG)
					FileLog.Log("AccessTools.GetReturnedType: methodOrConstructor is null");
				return null;
			}
			var constructor = methodOrConstructor as ConstructorInfo;
			if (constructor != null) return typeof(void);
			return ((MethodInfo)methodOrConstructor).ReturnType;
		}

		/// <summary>Given a type, returns the first inner type matching a recursive search by name</summary>
		/// <param name="type">The class/type to start searching at</param>
		/// <param name="name">The name of the inner type (case sensitive)</param>
		/// <returns>The inner type or null if type/name is null or if a type with that name cannot be found</returns>
		///
		public static Type Inner(Type type, string name)
		{
			if (type == null)
			{
				if (Harmony.DEBUG)
					FileLog.Log("AccessTools.Inner: type is null");
				return null;
			}
			if (name == null)
			{
				if (Harmony.DEBUG)
					FileLog.Log("AccessTools.Inner: name is null");
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
			if (type == null)
			{
				if (Harmony.DEBUG)
					FileLog.Log("AccessTools.FirstInner: type is null");
				return null;
			}
			if (predicate == null)
			{
				if (Harmony.DEBUG)
					FileLog.Log("AccessTools.FirstInner: predicate is null");
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
			if (type == null)
			{
				if (Harmony.DEBUG)
					FileLog.Log("AccessTools.FirstMethod: type is null");
				return null;
			}
			if (predicate == null)
			{
				if (Harmony.DEBUG)
					FileLog.Log("AccessTools.FirstMethod: predicate is null");
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
			if (type == null)
			{
				if (Harmony.DEBUG)
					FileLog.Log("AccessTools.FirstConstructor: type is null");
				return null;
			}
			if (predicate == null)
			{
				if (Harmony.DEBUG)
					FileLog.Log("AccessTools.FirstConstructor: predicate is null");
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
			if (type == null)
			{
				if (Harmony.DEBUG)
					FileLog.Log("AccessTools.FirstProperty: type is null");
				return null;
			}
			if (predicate == null)
			{
				if (Harmony.DEBUG)
					FileLog.Log("AccessTools.FirstProperty: predicate is null");
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
			if (parameters == null) return new Type[0];
			return parameters.Select(p => p == null ? typeof(object) : p.GetType()).ToArray();
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
				var index = inputTypes.FindIndex(inType => inType != null && pType.IsAssignableFrom(inType));
				if (index >= 0)
					return inputs[index];
				return GetDefaultValue(pType);
			}).ToArray();
		}

		/// <summary>A read/writable reference to an instance field</summary>
		/// <typeparam name="T">The class the field is defined in or "object" if type cannot be accessed at compile time</typeparam>
		/// <typeparam name="F">The type of the field</typeparam>
		/// <param name="obj">The runtime instance to access the field (leave empty for static fields)</param>
		/// <returns>An readable/assignable object representing the field</returns>
		///
		public delegate ref F FieldRef<T, F>(T obj = default);

		/// <summary>Creates an instance field reference</summary>
		/// <typeparam name="T">The class the field is defined in</typeparam>
		/// <typeparam name="F">The type of the field</typeparam>
		/// <param name="fieldName">The name of the field</param>
		/// <returns>A read and writable field reference delegate</returns>
		///
		public static FieldRef<T, F> FieldRefAccess<T, F>(string fieldName)
		{
			const BindingFlags bf = BindingFlags.NonPublic |
											BindingFlags.Instance |
											BindingFlags.DeclaredOnly;

			try
			{
				var fi = typeof(T).GetField(fieldName, bf);
				return FieldRefAccess<T, F>(fi);
			}
			catch (Exception ex)
			{
				throw new ArgumentException($"FieldRefAccess<{typeof(T)}, {typeof(F)}> for {fieldName} caused an exception", ex);
			}
		}

		/// <summary>Creates an instance field reference for a specific instance</summary>
		/// <typeparam name="T">The class the field is defined in</typeparam>
		/// <typeparam name="F">The type of the field</typeparam>
		/// <param name="instance">The instance</param>
		/// <param name="fieldName">The name of the field</param>
		/// <returns>An readable/assignable object representing the field</returns>
		///
		public static ref F FieldRefAccess<T, F>(T instance, string fieldName)
		{
			return ref FieldRefAccess<T, F>(fieldName)(instance);
		}

		/// <summary>Creates an instance field reference delegate for a private type</summary>
		/// <typeparam name="F">The type of the field</typeparam>
		/// <param name="type">The class/type</param>
		/// <param name="fieldName">The name of the field</param>
		/// <returns>A read and writable <see cref="FieldRef{T,F}"/> delegate</returns>
		///
		public static FieldRef<object, F> FieldRefAccess<F>(Type type, string fieldName)
		{
			return FieldRefAccess<object, F>(Field(type, fieldName));
		}

		/// <summary>Creates an instance field reference delegate for a fieldinfo</summary>
		/// <typeparam name="T">The class the field is defined in or "object" if type cannot be accessed at compile time</typeparam>
		/// <typeparam name="F">The type of the field</typeparam>
		/// <param name="fieldInfo">The field of the field</param>
		/// <returns>A read and writable <see cref="FieldRef{T,F}"/> delegate</returns>
		///
		public static FieldRef<T, F> FieldRefAccess<T, F>(FieldInfo fieldInfo)
		{
			if (fieldInfo == null)
				throw new ArgumentNullException(nameof(fieldInfo));
			if (!typeof(F).IsAssignableFrom(fieldInfo.FieldType))
				throw new ArgumentException("FieldInfo type does not match FieldRefAccess return type.");
			if (typeof(T) != typeof(object))
				if (fieldInfo.DeclaringType == null || !fieldInfo.DeclaringType.IsAssignableFrom(typeof(T)))
					throw new MissingFieldException(typeof(T).Name, fieldInfo.Name);

			var s_name = $"__refget_{typeof(T).Name}_fi_{fieldInfo.Name}";

			var dm = new DynamicMethodDefinition(s_name, typeof(F).MakeByRefType(), new[] { typeof(T) });

			var il = dm.GetILGenerator();
			il.Emit(OpCodes.Ldarg_0);
			il.Emit(OpCodes.Ldflda, fieldInfo);
			il.Emit(OpCodes.Ret);

			return (FieldRef<T, F>)dm.Generate().CreateDelegate(typeof(FieldRef<T, F>));
		}

		/// <summary>Creates a static field reference</summary>
		/// <typeparam name="T">The class the field is defined in or "object" if type cannot be accessed at compile time</typeparam>
		/// <typeparam name="F">The type of the field</typeparam>
		/// <param name="fieldName">The name of the field</param>
		/// <returns>An readable/assignable object representing the static field</returns>
		///
		public static ref F StaticFieldRefAccess<T, F>(string fieldName)
		{
			return ref StaticFieldRefAccess<F>(typeof(T), fieldName);
		}

		/// <summary>Creates a static field reference</summary>
		/// <typeparam name="F">The type of the field</typeparam>
		/// <param name="type">The class/type</param>
		/// <param name="fieldName">The name of the field</param>
		/// <returns>An readable/assignable object representing the static field</returns>
		///
		public static ref F StaticFieldRefAccess<F>(Type type, string fieldName)
		{
			const BindingFlags bf = BindingFlags.NonPublic |
											BindingFlags.Static |
											BindingFlags.DeclaredOnly;
			try
			{
				var fi = type.GetField(fieldName, bf);
				return ref StaticFieldRefAccess<F>(fi)();
			}
			catch (Exception ex)
			{
				throw new ArgumentException($"StaticFieldRefAccess<{typeof(F)}> for {type}, {fieldName} caused an exception", ex);
				throw;
			}
		}

		/// <summary>Creates a static field reference</summary>
		/// <typeparam name="T">The class the field is defined in or "object" if type cannot be accessed at compile time</typeparam>
		/// <typeparam name="F">The type of the field</typeparam>
		/// <param name="fieldInfo">The field</param>
		/// <returns>An readable/assignable object representing the static field</returns>
		///
		public static ref F StaticFieldRefAccess<T, F>(FieldInfo fieldInfo)
		{
			try
			{
				return ref StaticFieldRefAccess<F>(fieldInfo)();
			}
			catch (Exception ex)
			{
				throw new ArgumentException($"StaticFieldRefAccess<{typeof(T)}, {typeof(F)}> for {fieldInfo} caused an exception", ex);
				throw;
			}
		}

		/// <summary>A read/writable reference delegate to a static field</summary>
		/// <typeparam name="F">The type of the field</typeparam>
		/// <returns>An readable/assignable object representing the static field</returns>
		///
		public delegate ref F FieldRef<F>();

		/// <summary>Creates a static field reference delegate</summary>
		/// <typeparam name="F">The type of the field</typeparam>
		/// <param name="fieldInfo">The field</param>
		/// <returns>A read and writable <see cref="FieldRef{F}"/> delegate</returns>
		///
		public static FieldRef<F> StaticFieldRefAccess<F>(FieldInfo fieldInfo)
		{
			if (fieldInfo == null)
				throw new ArgumentNullException(nameof(fieldInfo));
			var type = fieldInfo.DeclaringType;

			var s_name = $"__refget_{type?.Name ?? "null"}_static_fi_{fieldInfo.Name}";

			var dm = new DynamicMethodDefinition(s_name, typeof(F).MakeByRefType(), new Type[0]);

			var il = dm.GetILGenerator();
			il.Emit(OpCodes.Ldsflda, fieldInfo);
			il.Emit(OpCodes.Ret);

			return (FieldRef<F>)dm.Generate().CreateDelegate(typeof(FieldRef<F>));
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
			_ = PrepForRemoting(exception, new object[0]);
#else
			System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception).Throw();
#endif
			// For the sake of any static code analyzer, always throw exception, even if ExceptionDispatchInfo.Throw above was called.
			throw exception;
		}

		/// <summary>Tells you if the current runtime is based on Mono</summary>
		/// <returns>True if we are running under Mono, false otherwise (.NET)</returns>
		///
		public static bool IsMonoRuntime { get; } = Type.GetType("Mono.Runtime") != null;

		/// <summary>Throws a missing member runtime exception</summary>
		/// <param name="type">The type that is involved</param>
		/// <param name="names">A list of names</param>
		///
		public static void ThrowMissingMemberException(Type type, params string[] names)
		{
			var fields = string.Join(",", GetFieldNames(type).ToArray());
			var properties = string.Join(",", GetPropertyNames(type).ToArray());
			throw new MissingMemberException($"{string.Join(",", names)}; available fields: {fields}; available properties: {properties}");
		}

		/// <summary>Gets default value for a specific type</summary>
		/// <param name="type">The class/type</param>
		/// <returns>The default value</returns>
		///
		public static object GetDefaultValue(Type type)
		{
			if (type == null)
			{
				if (Harmony.DEBUG)
					FileLog.Log("AccessTools.GetDefaultValue: type is null");
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
			if (type == null)
				throw new ArgumentNullException(nameof(type));
			var ctor = type.GetConstructor(BindingFlags.Public | BindingFlags.Instance, null, CallingConventions.Any, new Type[0], null);
			if (ctor != null)
				return Activator.CreateInstance(type);
			return FormatterServices.GetUninitializedObject(type);
		}

		/// <summary>Makes a deep copy of any object</summary>
		/// <typeparam name="T">The type of the instance that should be created</typeparam>
		/// <param name="source">The original object</param>
		/// <returns>A copy of the original object but of type T</returns>
		///
		public static T MakeDeepCopy<T>(object source) where T : class
		{
			return MakeDeepCopy(source, typeof(T)) as T;
		}

		/// <summary>Makes a deep copy of any object</summary>
		/// <typeparam name="T">The type of the instance that should be created</typeparam>
		/// <param name="source">The original object</param>
		/// <param name="result">[out] The copy of the original object</param>
		/// <param name="processor">Optional value transformation function (taking a field name and src/dst <see cref="Traverse"/> instances)</param>
		/// <param name="pathRoot">The optional path root to start with</param>
		///
		public static void MakeDeepCopy<T>(object source, out T result, Func<string, Traverse, Traverse, object> processor = null, string pathRoot = "")
		{
			result = (T)MakeDeepCopy(source, typeof(T), processor, pathRoot);
		}

		/// <summary>Makes a deep copy of any object</summary>
		/// <param name="source">The original object</param>
		/// <param name="resultType">The type of the instance that should be created</param>
		/// <param name="processor">Optional value transformation function (taking a field name and src/dst <see cref="Traverse"/> instances)</param>
		/// <param name="pathRoot">The optional path root to start with</param>
		/// <returns>The copy of the original object</returns>
		///
		public static object MakeDeepCopy(object source, Type resultType, Func<string, Traverse, Traverse, object> processor = null, string pathRoot = "")
		{
			if (source == null || resultType == null)
				return null;

			resultType = Nullable.GetUnderlyingType(resultType) ?? resultType;
			var type = source.GetType();

			if (type.IsPrimitive)
				return source;

			if (type.IsEnum)
				return Enum.ToObject(resultType, (int)source);

			if (type.IsGenericType && resultType.IsGenericType)
			{
				var addOperation = FirstMethod(resultType, m => m.Name == "Add" && m.GetParameters().Count() == 1);
				if (addOperation != null)
				{
					var addableResult = Activator.CreateInstance(resultType);
					var addInvoker = MethodInvoker.GetHandler(addOperation);
					var newElementType = resultType.GetGenericArguments()[0];
					var i = 0;
					foreach (var element in source as IEnumerable)
					{
						var iStr = (i++).ToString();
						var path = pathRoot.Length > 0 ? pathRoot + "." + iStr : iStr;
						var newElement = MakeDeepCopy(element, newElementType, processor, path);
						_ = addInvoker(addableResult, new object[] { newElement });
					}
					return addableResult;
				}

				// TODO: add dictionaries support
				// maybe use methods in Dictionary<KeyValuePair<TKey,TVal>>
			}

			if (type.IsArray && resultType.IsArray)
			{
				var elementType = resultType.GetElementType();
				var length = ((Array)source).Length;
				var arrayResult = Activator.CreateInstance(resultType, new object[] { length }) as object[];
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
				var value = processor != null ? processor(path, src, dst) : src.GetValue();
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
			return type.IsValueType && !IsValue(type) && !IsVoid(type);
		}

		/// <summary>Tests if a type is a class</summary>
		/// <param name="type">The type</param>
		/// <returns>True if the type is a class</returns>
		///
		public static bool IsClass(Type type)
		{
			return !type.IsValueType;
		}

		/// <summary>Tests if a type is a value type</summary>
		/// <param name="type">The type</param>
		/// <returns>True if the type is a value type</returns>
		///
		public static bool IsValue(Type type)
		{
			return type.IsPrimitive || type.IsEnum;
		}

		/// <summary>Tests if a type is an integer type</summary>
		/// <param name="type">The type</param>
		/// <returns>True if the type represents some integer</returns>
		/// 
		public static bool IsInteger(Type type)
		{
			switch (Type.GetTypeCode(type))
			{
				case TypeCode.Byte:
				case TypeCode.SByte:
				case TypeCode.UInt16:
				case TypeCode.UInt32:
				case TypeCode.UInt64:
				case TypeCode.Int16:
				case TypeCode.Int32:
				case TypeCode.Int64:
					return true;
				default:
					return false;
			}
		}

		/// <summary>Tests if a type is a floating point type</summary>
		/// <param name="type">The type</param>
		/// <returns>True if the type represents some floating point</returns>
		/// 
		public static bool IsFloatingPoint(Type type)
		{
			switch (Type.GetTypeCode(type))
			{
				case TypeCode.Decimal:
				case TypeCode.Double:
				case TypeCode.Single:
					return true;
				default:
					return false;
			}
		}

		/// <summary>Tests if a type is a numerical type</summary>
		/// <param name="type">The type</param>
		/// <returns>True if the type represents some number</returns>
		///
		public static bool IsNumber(Type type)
		{
			return IsInteger(type) || IsFloatingPoint(type);
		}

		/// <summary>Tests if a type is void</summary>
		/// <param name="type">The type</param>
		/// <returns>True if the type is void</returns>
		///
		public static bool IsVoid(Type type)
		{
			return type == typeof(void);
		}

		/// <summary>Test whether an instance is of a nullable type</summary>
		/// <typeparam name="T">Type of instance</typeparam>
		/// <param name="instance">An instance to test</param>
		/// <returns>True if instance is of nullable type, false if not</returns>
		///
#pragma warning disable IDE0060
		public static bool IsOfNullableType<T>(T instance)
#pragma warning restore IDE0060
		{
			return Nullable.GetUnderlyingType(typeof(T)) != null;
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