using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Serialization;

namespace Harmony
{
	/// <summary>A helper class for reflection related functions</summary>
	public static class AccessTools
	{
		/// <summary>Shortcut to simplify the use of reflections and make it work for any access level</summary>
		public static BindingFlags all = BindingFlags.Public
			| BindingFlags.NonPublic
			| BindingFlags.Instance
			| BindingFlags.Static
			| BindingFlags.GetField
			| BindingFlags.SetField
			| BindingFlags.GetProperty
			| BindingFlags.SetProperty;

		/// <summary>Shortcut to simplify the use of reflections and make it work for any access level but only within the current type</summary>
		public static BindingFlags allDeclared = all | BindingFlags.DeclaredOnly;

		/// <summary>Gets a type by name. Prefers a full name with namespace but falls back to the first type matching the name otherwise</summary>
		/// <param name="name">The name</param>
		/// <returns>A Type</returns>
		///
		public static Type TypeByName(string name)
		{
			var type = Type.GetType(name, false);
			if (type == null)
				type = AppDomain.CurrentDomain.GetAssemblies()
					.SelectMany(x => x.GetTypes())
					.FirstOrDefault(x => x.FullName == name);
			if (type == null)
				type = AppDomain.CurrentDomain.GetAssemblies()
					.SelectMany(x => x.GetTypes())
					.FirstOrDefault(x => x.Name == name);
			if (type == null && HarmonyInstance.DEBUG)
				FileLog.Log("AccessTools.TypeByName: Could not find type named " + name);
			return type;
		}

		/// <summary>Applies a function going up the type hierarchy and stops at the first non null result</summary>
		/// <typeparam name="T">Result type of func()</typeparam>
		/// <param name="type">The type to start with</param>
		/// <param name="func">The evaluation function returning T</param>
		/// <returns>Returns the first non null result or default(T) when reaching the top level type object</returns>
		///
		public static T FindIncludingBaseTypes<T>(Type type, Func<Type, T> func)
		{
			while (true)
			{
				var result = func(type);
				if (result != null) return result;
				if (type == typeof(object)) return default(T);
				type = type.BaseType;
			}
		}

		/// <summary>Applies a function going into inner types and stops at the first non null result</summary>
		/// <typeparam name="T">Generic type parameter</typeparam>
		/// <param name="type">The type to start with</param>
		/// <param name="func">The evaluation function returning T</param>
		/// <returns>Returns the first non null result or null with no match</returns>
		///
		public static T FindIncludingInnerTypes<T>(Type type, Func<Type, T> func)
		{
			var result = func(type);
			if (result != null) return result;
			foreach (var subType in type.GetNestedTypes(all))
			{
				result = FindIncludingInnerTypes(subType, func);
				if (result != null)
					break;
			}
			return result;
		}

		/// <summary>Gets the reflection information for a directly declared field</summary>
		/// <param name="type">The class where the field is defined</param>
		/// <param name="name">The name of the field</param>
		/// <returns>A FieldInfo or null when type/name is null or when the field cannot be found</returns>
		///
		public static FieldInfo DeclaredField(Type type, string name)
		{
			if (type == null)
			{
				if (HarmonyInstance.DEBUG)
					FileLog.Log("AccessTools.DeclaredField: type is null");
				return null;
			}
			if (name == null)
			{
				if (HarmonyInstance.DEBUG)
					FileLog.Log("AccessTools.DeclaredField: name is null");
				return null;
			}
			var field = type.GetField(name, allDeclared);
			if (field == null && HarmonyInstance.DEBUG)
				FileLog.Log("AccessTools.DeclaredField: Could not find field for type " + type + " and name " + name);
			return field;
		}

		/// <summary>Gets the reflection information for a field by searching the type and all its super types</summary>
		/// <param name="type">The class where the field is defined</param>
		/// <param name="name">The name of the field (case sensitive)</param>
		/// <returns>A FieldInfo or null when type/name is null or when the field cannot be found</returns>
		///
		public static FieldInfo Field(Type type, string name)
		{
			if (type == null)
			{
				if (HarmonyInstance.DEBUG)
					FileLog.Log("AccessTools.Field: type is null");
				return null;
			}
			if (name == null)
			{
				if (HarmonyInstance.DEBUG)
					FileLog.Log("AccessTools.Field: name is null");
				return null;
			}
			var field = FindIncludingBaseTypes(type, t => t.GetField(name, all));
			if (field == null && HarmonyInstance.DEBUG)
				FileLog.Log("AccessTools.Field: Could not find field for type " + type + " and name " + name);
			return field;
		}

		/// <summary>Gets the reflection information for a field</summary>
		/// <param name="type">The class where the field is declared</param>
		/// <param name="idx">The zero-based index of the field inside the class definition</param>
		/// <returns>A FieldInfo or null when type is null or when the field cannot be found</returns>
		///
		public static FieldInfo DeclaredField(Type type, int idx)
		{
			if (type == null)
			{
				if (HarmonyInstance.DEBUG)
					FileLog.Log("AccessTools.DeclaredField: type is null");
				return null;
			}
			var field = GetDeclaredFields(type).ElementAtOrDefault(idx);
			if (field == null && HarmonyInstance.DEBUG)
				FileLog.Log("AccessTools.DeclaredField: Could not find field for type " + type + " and idx " + idx);
			return field;
		}

		/// <summary>Gets the reflection information for a directly declared property</summary>
		/// <param name="type">The class where the property is declared</param>
		/// <param name="name">The name of the property (case sensitive)</param>
		/// <returns>A PropertyInfo or null when type/name is null or when the property cannot be found</returns>
		///
		public static PropertyInfo DeclaredProperty(Type type, string name)
		{
			if (type == null)
			{
				if (HarmonyInstance.DEBUG)
					FileLog.Log("AccessTools.DeclaredProperty: type is null");
				return null;
			}
			if (name == null)
			{
				if (HarmonyInstance.DEBUG)
					FileLog.Log("AccessTools.DeclaredProperty: name is null");
				return null;
			}
			var property = type.GetProperty(name, allDeclared);
			if (property == null && HarmonyInstance.DEBUG)
				FileLog.Log("AccessTools.DeclaredProperty: Could not find property for type " + type + " and name " + name);
			return property;
		}

		/// <summary>Gets the reflection information for the getter method of a directly declared property</summary>
		/// <param name="type">The class where the property is declared</param>
		/// <param name="name">The name of the property (case sensitive)</param>
		/// <returns>A MethodInfo or null when type/name is null or when the property cannot be found</returns>
		///
		public static MethodInfo DeclaredPropertyGetter(Type type, string name)
		{
			return DeclaredProperty(type, name)?.GetGetMethod(true);
		}

		/// <summary>Gets the reflection information for the setter method of a directly declared property</summary>
		/// <param name="type">The class where the property is declared</param>
		/// <param name="name">The name of the property (case sensitive)</param>
		/// <returns>A MethodInfo or null when type/name is null or when the property cannot be found</returns>
		///
		public static MethodInfo DeclaredPropertySetter(Type type, string name)
		{
			return DeclaredProperty(type, name)?.GetSetMethod(true);
		}

		/// <summary>Gets the reflection information for a property by searching the type and all its super types</summary>
		/// <param name="type">The type</param>
		/// <param name="name">The name</param>
		/// <returns>A PropertyInfo or null when type/name is null or when the property cannot be found</returns>
		///
		public static PropertyInfo Property(Type type, string name)
		{
			if (type == null)
			{
				if (HarmonyInstance.DEBUG)
					FileLog.Log("AccessTools.Property: type is null");
				return null;
			}
			if (name == null)
			{
				if (HarmonyInstance.DEBUG)
					FileLog.Log("AccessTools.Property: name is null");
				return null;
			}
			var property = FindIncludingBaseTypes(type, t => t.GetProperty(name, all));
			if (property == null && HarmonyInstance.DEBUG)
				FileLog.Log("AccessTools.Property: Could not find property for type " + type + " and name " + name);
			return property;
		}

		/// <summary>Gets the reflection information for the getter method of a property by searching the type and all its super types</summary>
		/// <param name="type">The type</param>
		/// <param name="name">The name</param>
		/// <returns>A MethodInfo or null when type/name is null or when the property cannot be found</returns>
		///
		public static MethodInfo PropertyGetter(Type type, string name)
		{
			return Property(type, name)?.GetGetMethod(true);
		}

		/// <summary>Gets the reflection information for the setter method of a property by searching the type and all its super types</summary>
		/// <param name="type">The type</param>
		/// <param name="name">The name</param>
		/// <returns>A MethodInfo or null when type/name is null or when the property cannot be found</returns>
		///
		public static MethodInfo PropertySetter(Type type, string name)
		{
			return Property(type, name)?.GetSetMethod(true);
		}

		/// <summary>Gets the reflection information for a directly declared method</summary>
		/// <param name="type">The class where the method is declared</param>
		/// <param name="name">The name of the method (case sensitive)</param>
		/// <param name="parameters">Optional parameters to target a specific overload of the method</param>
		/// <param name="generics">Optional list of types that define the generic version of the method</param>
		/// <returns>A MethodInfo or null when type/name is null or when the method cannot be found</returns>
		///
		public static MethodInfo DeclaredMethod(Type type, string name, Type[] parameters = null, Type[] generics = null)
		{
			if (type == null)
			{
				if (HarmonyInstance.DEBUG)
					FileLog.Log("AccessTools.DeclaredMethod: type is null");
				return null;
			}
			if (name == null)
			{
				if (HarmonyInstance.DEBUG)
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
				if (HarmonyInstance.DEBUG)
					FileLog.Log("AccessTools.DeclaredMethod: Could not find method for type " + type + " and name " + name + " and parameters " + parameters?.Description());
				return null;
			}

			if (generics != null) result = result.MakeGenericMethod(generics);
			return result;
		}

		/// <summary>Gets the reflection information for a method by searching the type and all its super types</summary>
		/// <param name="type">The class where the method is declared</param>
		/// <param name="name">The name of the method (case sensitive)</param>
		/// <param name="parameters">Optional parameters to target a specific overload of the method</param>
		/// <param name="generics">Optional list of types that define the generic version of the method</param>
		/// <returns>A MethodInfo or null when type/name is null or when the method cannot be found</returns>
		///
		public static MethodInfo Method(Type type, string name, Type[] parameters = null, Type[] generics = null)
		{
			if (type == null)
			{
				if (HarmonyInstance.DEBUG)
					FileLog.Log("AccessTools.Method: type is null");
				return null;
			}
			if (name == null)
			{
				if (HarmonyInstance.DEBUG)
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
						throw new AmbiguousMatchException($"Ambiguous match in Harmony patch for {type}:{name}." + ex);
				}
			}
			else
			{
				result = FindIncludingBaseTypes(type, t => t.GetMethod(name, all, null, parameters, modifiers));
			}

			if (result == null)
			{
				if (HarmonyInstance.DEBUG)
					FileLog.Log("AccessTools.Method: Could not find method for type " + type + " and name " + name + " and parameters " + parameters?.Description());
				return null;
			}

			if (generics != null) result = result.MakeGenericMethod(generics);
			return result;
		}

		/// <summary>Gets the reflection information for a method by searching the type and all its super types</summary>
		/// <param name="typeColonMethodname">The full name (Namespace.Type1.Type2:MethodName) of the type where the method is declared</param>
		/// <param name="parameters">Optional parameters to target a specific overload of the method</param>
		/// <param name="generics">Optional list of types that define the generic version of the method</param>
		/// <returns>A MethodInfo or null when type/name is null or when the method cannot be found</returns>
		///
		public static MethodInfo Method(string typeColonMethodname, Type[] parameters = null, Type[] generics = null)
		{
			if (typeColonMethodname == null)
			{
				if (HarmonyInstance.DEBUG)
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
		/// <param name="type">The declaring type</param>
		/// <returns>A list of method names</returns>
		///
		public static List<string> GetMethodNames(Type type)
		{
			if (type == null)
			{
				if (HarmonyInstance.DEBUG)
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
				if (HarmonyInstance.DEBUG)
					FileLog.Log("AccessTools.GetMethodNames: instance is null");
				return new List<string>();
			}
			return GetMethodNames(instance.GetType());
		}

		/// <summary>Gets the names of all fields that are declared in a type</summary>
		/// <param name="type">The declaring type</param>
		/// <returns>A list of field names</returns>
		///
		public static List<string> GetFieldNames(Type type)
		{
			if (type == null)
			{
				if (HarmonyInstance.DEBUG)
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
				if (HarmonyInstance.DEBUG)
					FileLog.Log("AccessTools.GetFieldNames: instance is null");
				return new List<string>();
			}
			return GetFieldNames(instance.GetType());
		}

		/// <summary>Gets the names of all properties that are declared in a type</summary>
		/// <param name="type">The declaring type</param>
		/// <returns>A list of property names</returns>
		///
		public static List<string> GetPropertyNames(Type type)
		{
			if (type == null)
			{
				if (HarmonyInstance.DEBUG)
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
				if (HarmonyInstance.DEBUG)
					FileLog.Log("AccessTools.GetPropertyNames: instance is null");
				return new List<string>();
			}
			return GetPropertyNames(instance.GetType());
		}

		/// <summary>Gets the type of any member of a class</summary>
		/// <param name="member">An EventInfo, FieldInfo, MethodInfo, or PropertyInfo</param>
		/// <returns>The type that represents the output of this member</returns>
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

		/// <summary>Gets the reflection information for a directly declared constructor</summary>
		/// <param name="type">The class where the constructor is declared</param>
		/// <param name="parameters">Optional parameters to target a specific overload of the constructor</param>
		/// <returns>A ConstructorInfo or null when type is null or when the constructor cannot be found</returns>
		///
		public static ConstructorInfo DeclaredConstructor(Type type, Type[] parameters = null)
		{
			if (type == null)
			{
				if (HarmonyInstance.DEBUG)
					FileLog.Log("AccessTools.DeclaredConstructor: type is null");
				return null;
			}
			if (parameters == null) parameters = new Type[0];
			return type.GetConstructor(allDeclared, null, parameters, new ParameterModifier[] { });
		}

		/// <summary>Gets the reflection information for a constructor by searching the type and all its super types</summary>
		/// <param name="type">The class where the constructor is declared</param>
		/// <param name="parameters">Optional parameters to target a specific overload of the method</param>
		/// <returns>A ConstructorInfo or null when type is null or when the method cannot be found</returns>
		///
		public static ConstructorInfo Constructor(Type type, Type[] parameters = null)
		{
			if (type == null)
			{
				if (HarmonyInstance.DEBUG)
					FileLog.Log("AccessTools.ConstructorInfo: type is null");
				return null;
			}
			if (parameters == null) parameters = new Type[0];
			return FindIncludingBaseTypes(type, t => t.GetConstructor(all, null, parameters, new ParameterModifier[] { }));
		}

		/// <summary>Gets reflection information for all declared constructors</summary>
		/// <param name="type">The class where the constructors are declared</param>
		/// <returns>A list of ConstructorInfo</returns>
		///
		public static List<ConstructorInfo> GetDeclaredConstructors(Type type)
		{
			if (type == null)
			{
				if (HarmonyInstance.DEBUG)
					FileLog.Log("AccessTools.GetDeclaredConstructors: type is null");
				return null;
			}
			return type.GetConstructors(allDeclared).Where(method => method.DeclaringType == type).ToList();
		}

		/// <summary>Gets reflection information for all declared methods</summary>
		/// <param name="type">The class where the methods are declared</param>
		/// <returns>A list of MethodInfo</returns>
		///
		public static List<MethodInfo> GetDeclaredMethods(Type type)
		{
			if (type == null)
			{
				if (HarmonyInstance.DEBUG)
					FileLog.Log("AccessTools.GetDeclaredMethods: type is null");
				return null;
			}
			return type.GetMethods(allDeclared).ToList();
		}

		/// <summary>Gets reflection information for all declared properties</summary>
		/// <param name="type">The class where the properties are declared</param>
		/// <returns>A list of PropertyInfo</returns>
		///
		public static List<PropertyInfo> GetDeclaredProperties(Type type)
		{
			if (type == null)
			{
				if (HarmonyInstance.DEBUG)
					FileLog.Log("AccessTools.GetDeclaredProperties: type is null");
				return null;
			}
			return type.GetProperties(allDeclared).ToList();
		}

		/// <summary>Gets reflection information for all declared fields</summary>
		/// <param name="type">The class where the fields are declared</param>
		/// <returns>A list of FieldInfo</returns>
		///
		public static List<FieldInfo> GetDeclaredFields(Type type)
		{
			if (type == null)
			{
				if (HarmonyInstance.DEBUG)
					FileLog.Log("AccessTools.GetDeclaredFields: type is null");
				return null;
			}
			return type.GetFields(allDeclared).ToList();
		}

		/// <summary>Gets the return type of a method or constructor</summary>
		/// <param name="methodOrConstructor">The method or constructor</param>
		/// <returns>The return type of the method</returns>
		///
		public static Type GetReturnedType(MethodBase methodOrConstructor)
		{
			if (methodOrConstructor == null)
			{
				if (HarmonyInstance.DEBUG)
					FileLog.Log("AccessTools.GetReturnedType: methodOrConstructor is null");
				return null;
			}
			var constructor = methodOrConstructor as ConstructorInfo;
			if (constructor != null) return typeof(void);
			return ((MethodInfo)methodOrConstructor).ReturnType;
		}

		/// <summary>Given a type, returns the first inner type matching a recursive search by name</summary>
		/// <param name="type">The type to start searching at</param>
		/// <param name="name">The name of the inner type (case sensitive)</param>
		/// <returns>The inner type or null if type/name is null or if a type with that name cannot be found</returns>
		///
		public static Type Inner(Type type, string name)
		{
			if (type == null)
			{
				if (HarmonyInstance.DEBUG)
					FileLog.Log("AccessTools.Inner: type is null");
				return null;
			}
			if (name == null)
			{
				if (HarmonyInstance.DEBUG)
					FileLog.Log("AccessTools.Inner: name is null");
				return null;
			}
			return FindIncludingBaseTypes(type, t => t.GetNestedType(name, all));
		}

		/// <summary>Given a type, returns the first inner type matching a recursive search with a predicate</summary>
		/// <param name="type">The type to start searching at</param>
		/// <param name="predicate">The predicate to search with</param>
		/// <returns>The inner type or null if type/predicate is null or if a type with that name cannot be found</returns>
		///
		public static Type FirstInner(Type type, Func<Type, bool> predicate)
		{
			if (type == null)
			{
				if (HarmonyInstance.DEBUG)
					FileLog.Log("AccessTools.FirstInner: type is null");
				return null;
			}
			if (predicate == null)
			{
				if (HarmonyInstance.DEBUG)
					FileLog.Log("AccessTools.FirstInner: predicate is null");
				return null;
			}
			return type.GetNestedTypes(all).FirstOrDefault(subType => predicate(subType));
		}

		/// <summary>Given a type, returns the first method matching a predicate</summary>
		/// <param name="type">The type to start searching at</param>
		/// <param name="predicate">The predicate to search with</param>
		/// <returns>The MethodInfo or null if type/predicate is null or if a type with that name cannot be found</returns>
		///
		public static MethodInfo FirstMethod(Type type, Func<MethodInfo, bool> predicate)
		{
			if (type == null)
			{
				if (HarmonyInstance.DEBUG)
					FileLog.Log("AccessTools.FirstMethod: type is null");
				return null;
			}
			if (predicate == null)
			{
				if (HarmonyInstance.DEBUG)
					FileLog.Log("AccessTools.FirstMethod: predicate is null");
				return null;
			}
			return type.GetMethods(allDeclared).FirstOrDefault(method => predicate(method));
		}

		/// <summary>Given a type, returns the first constructor matching a predicate</summary>
		/// <param name="type">The type to start searching at</param>
		/// <param name="predicate">The predicate to search with</param>
		/// <returns>The ConstructorInfo or null if type/predicate is null or if a type with that name cannot be found</returns>
		///
		public static ConstructorInfo FirstConstructor(Type type, Func<ConstructorInfo, bool> predicate)
		{
			if (type == null)
			{
				if (HarmonyInstance.DEBUG)
					FileLog.Log("AccessTools.FirstConstructor: type is null");
				return null;
			}
			if (predicate == null)
			{
				if (HarmonyInstance.DEBUG)
					FileLog.Log("AccessTools.FirstConstructor: predicate is null");
				return null;
			}
			return type.GetConstructors(allDeclared).FirstOrDefault(constructor => predicate(constructor));
		}

		/// <summary>Given a type, returns the first property matching a predicate</summary>
		/// <param name="type">The type to start searching at</param>
		/// <param name="predicate">The predicate to search with</param>
		/// <returns>The PropertyInfo or null if type/predicate is null or if a type with that name cannot be found</returns>
		///
		public static PropertyInfo FirstProperty(Type type, Func<PropertyInfo, bool> predicate)
		{
			if (type == null)
			{
				if (HarmonyInstance.DEBUG)
					FileLog.Log("AccessTools.FirstProperty: type is null");
				return null;
			}
			if (predicate == null)
			{
				if (HarmonyInstance.DEBUG)
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

		/// <summary>A read/writable reference to a field</summary>
		/// <typeparam name="T">The class the field is defined in</typeparam>
		/// <typeparam name="U">The type of the field</typeparam>
		/// <param name="obj">The runtime instance to access the field</param>
		/// <returns>The value of the field (or an assignable object)</returns>
		///
		public delegate ref U FieldRef<T, U>(T obj);

		/// <summary>Creates a field reference</summary>
		/// <typeparam name="T">The class the field is defined in</typeparam>
		/// <typeparam name="U">The type of the field</typeparam>
		/// <param name="fieldName">The name of the field</param>
		/// <returns>A read and writable field reference</returns>
		///
		public static FieldRef<T, U> FieldRefAccess<T, U>(string fieldName)
		{
			const BindingFlags bf = BindingFlags.NonPublic |
											BindingFlags.Instance |
											BindingFlags.DeclaredOnly;

			var fi = typeof(T).GetField(fieldName, bf);
			if (fi == null)
				throw new MissingFieldException(typeof(T).Name, fieldName);

			var s_name = "__refget_" + typeof(T).Name + "_fi_" + fi.Name;

			// workaround for using ref-return with DynamicMethod:
			// a.) initialize with dummy return value
			var dm = new DynamicMethod(s_name, typeof(U), new[] { typeof(T) }, typeof(T), true);

			// b.) replace with desired 'ByRef' return value
			var trv = Traverse.Create(dm);
			trv.Field("returnType").SetValue(typeof(U).MakeByRefType());
			trv.Field("m_returnType").SetValue(typeof(U).MakeByRefType());

			var il = dm.GetILGenerator();
			il.Emit(OpCodes.Ldarg_0);
			il.Emit(OpCodes.Ldflda, fi);
			il.Emit(OpCodes.Ret);
			return (FieldRef<T, U>)dm.CreateDelegate(typeof(FieldRef<T, U>));
		}

		/// <summary>Creates a field reference for a specific instance</summary>
		/// <typeparam name="T">The class the field is defined in</typeparam>
		/// <typeparam name="U">The type of the field</typeparam>
		/// <param name="instance">The instance</param>
		/// <param name="fieldName">The name of the field</param>
		/// <returns>A read and writable field reference</returns>
		///
		public static ref U FieldRefAccess<T, U>(T instance, string fieldName)
		{
			return ref FieldRefAccess<T, U>(fieldName)(instance);
		}

		/// <summary>Throws a missing member runtime exception</summary>
		/// <param name="type">The class that is involved</param>
		/// <param name="names">A list of names</param>
		///
		public static void ThrowMissingMemberException(Type type, params string[] names)
		{
			var fields = string.Join(",", GetFieldNames(type).ToArray());
			var properties = string.Join(",", GetPropertyNames(type).ToArray());
			throw new MissingMemberException(string.Join(",", names) + "; available fields: " + fields + "; available properties: " + properties);
		}

		/// <summary>Gets default value for a specific type</summary>
		/// <param name="type">The type</param>
		/// <returns>The default value</returns>
		///
		public static object GetDefaultValue(Type type)
		{
			if (type == null)
			{
				if (HarmonyInstance.DEBUG)
					FileLog.Log("AccessTools.GetDefaultValue: type is null");
				return null;
			}
			if (type == typeof(void)) return null;
			if (type.IsValueType)
				return Activator.CreateInstance(type);
			return null;
		}

		/// <summary>Creates an (possibly uninitialized) instance of a given type</summary>
		/// <param name="type">The type</param>
		/// <returns>The new instance</returns>
		///
		public static object CreateInstance(Type type)
		{
			if (type == null)
				throw new NullReferenceException("Cannot create instance for NULL type");
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
		/// <param name="processor">Optional value transformation function (taking a field name and src/dst traverse objects)</param>
		/// <param name="pathRoot">The optional path root to start with</param>
		///
		public static void MakeDeepCopy<T>(object source, out T result, Func<string, Traverse, Traverse, object> processor = null, string pathRoot = "")
		{
			result = (T)MakeDeepCopy(source, typeof(T), processor, pathRoot);
		}

		/// <summary>Makes a deep copy of any object</summary>
		/// <param name="source">The original object</param>
		/// <param name="resultType">The type of the instance that should be created</param>
		/// <param name="processor">Optional value transformation function (taking a field name and src/dst traverse objects)</param>
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
						addInvoker(addableResult, new object[] { newElement });
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

			var result = CreateInstance(resultType);
			Traverse.IterateFields(source, result, (name, src, dst) =>
			{
				var path = pathRoot.Length > 0 ? pathRoot + "." + name : name;
				var value = processor != null ? processor(path, src, dst) : src.GetValue();
				dst.SetValue(MakeDeepCopy(value, dst.GetValueType(), processor, path));
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
		public static bool IsOfNullableType<T>(T instance)
		{
			return Nullable.GetUnderlyingType(typeof(T)) != null;
		}
	}
}