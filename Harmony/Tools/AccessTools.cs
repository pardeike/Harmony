using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Harmony
{
	public static class AccessTools
	{
		public static BindingFlags all = BindingFlags.Public
			| BindingFlags.NonPublic
			| BindingFlags.Instance
			| BindingFlags.Static
			| BindingFlags.GetField
			| BindingFlags.SetField
			| BindingFlags.GetProperty
			| BindingFlags.SetProperty;

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
			return type;
		}

		public static T FindIncludingBaseTypes<T>(Type type, Func<Type, T> action)
		{
			while (true)
			{
				var result = action(type);
				if (result != null) return result;
				if (type == typeof(object)) return default(T);
				type = type.BaseType;
			}
		}

		public static T FindIncludingInnerTypes<T>(Type type, Func<Type, T> action)
		{
			var result = action(type);
			if (result != null) return result;
			foreach (var subType in type.GetNestedTypes(all))
			{
				result = FindIncludingInnerTypes(subType, action);
				if (result != null)
					break;
			}
			return result;
		}

		public static FieldInfo Field(Type type, string name)
		{
			if (type == null || name == null) return null;
			return FindIncludingBaseTypes(type, t => t.GetField(name, all));
		}

		public static PropertyInfo Property(Type type, string name)
		{
			if (type == null || name == null) return null;
			return FindIncludingBaseTypes(type, t => t.GetProperty(name, all));
		}

		public static MethodInfo Method(Type type, string name, Type[] parameters = null, Type[] generics = null)
		{
			if (type == null || name == null) return null;
			MethodInfo result;
			var modifiers = new ParameterModifier[] { };
			if (parameters == null)
			{
				try
				{
					result = FindIncludingBaseTypes(type, t => t.GetMethod(name, all));
				}
				catch (AmbiguousMatchException)
				{
					result = FindIncludingBaseTypes(type, t => t.GetMethod(name, all, null, new Type[0], modifiers));
				}
			}
			else
			{
				result = FindIncludingBaseTypes(type, t => t.GetMethod(name, all, null, parameters, modifiers));
			}
			if (result == null) return null;
			if (generics != null) result = result.MakeGenericMethod(generics);
			return result;
		}

		public static List<string> GetMethodNames(Type type)
		{
			if (type == null) return new List<string>();
			return type.GetMethods(all).Select(m => m.Name).ToList();
		}

		public static List<string> GetMethodNames(object instance)
		{
			if (instance == null) return new List<string>();
			return GetMethodNames(instance.GetType());
		}

		public static ConstructorInfo Constructor(Type type, Type[] parameters = null)
		{
			if (type == null) return null;
			if (parameters == null) parameters = new Type[0];
			return FindIncludingBaseTypes(type, t => t.GetConstructor(all, null, parameters, new ParameterModifier[] { }));
		}

		public static List<ConstructorInfo> GetDeclaredConstructors(Type type)
		{
			return type.GetConstructors(all).Where(method => method.DeclaringType == type).ToList();
		}

		public static List<MethodInfo> GetDeclaredMethods(Type type)
		{
			return type.GetMethods(all).Where(method => method.DeclaringType == type).ToList();
		}

		public static List<PropertyInfo> GetDeclaredProperties(Type type)
		{
			return type.GetProperties(all).Where(property => property.DeclaringType == type).ToList();
		}

		public static List<FieldInfo> GetDeclaredFields(Type type)
		{
			return type.GetFields(all).Where(field => field.DeclaringType == type).ToList();
		}

		public static Type GetReturnedType(MethodBase method)
		{
			var constructor = method as ConstructorInfo;
			if (constructor != null) return typeof(void);
			return ((MethodInfo)method).ReturnType;
		}

		public static Type Inner(Type type, string name)
		{
			if (type == null || name == null) return null;
			return FindIncludingBaseTypes(type, t => t.GetNestedType(name, all));
		}

		public static Type FirstInner(Type type, Func<Type, bool> predicate)
		{
			if (type == null || predicate == null) return null;
			return type.GetNestedTypes(all).FirstOrDefault(subType => predicate(subType));
		}

		public static MethodInfo FirstMethod(Type type, Func<MethodInfo, bool> predicate)
		{
			if (type == null || predicate == null) return null;
			return type.GetMethods(all).FirstOrDefault(method => predicate(method));
		}

		public static ConstructorInfo FirstConstructor(Type type, Func<ConstructorInfo, bool> predicate)
		{
			if (type == null || predicate == null) return null;
			return type.GetConstructors(all).FirstOrDefault(constructor => predicate(constructor));
		}

		public static PropertyInfo FirstProperty(Type type, Func<PropertyInfo, bool> predicate)
		{
			if (type == null || predicate == null) return null;
			return type.GetProperties(all).FirstOrDefault(property => predicate(property));
		}

		public static Type[] GetTypes(object[] parameters)
		{
			if (parameters == null) return new Type[0];
			return parameters.Select(p => p == null ? typeof(object) : p.GetType()).ToArray();
		}

		public static List<string> GetFieldNames(Type type)
		{
			if (type == null) return new List<string>();
			return type.GetFields(all).Select(f => f.Name).ToList();
		}

		public static List<string> GetFieldNames(object instance)
		{
			if (instance == null) return new List<string>();
			return GetFieldNames(instance.GetType());
		}

		public static List<string> GetPropertyNames(Type type)
		{
			if (type == null) return new List<string>();
			return type.GetProperties(all).Select(f => f.Name).ToList();
		}

		public static List<string> GetPropertyNames(object instance)
		{
			if (instance == null) return new List<string>();
			return GetPropertyNames(instance.GetType());
		}

		public static void ThrowMissingMemberException(Type type, params string[] names)
		{
			var fields = string.Join(",", GetFieldNames(type).ToArray());
			var properties = string.Join(",", GetPropertyNames(type).ToArray());
			throw new MissingMemberException(string.Join(",", names) + "; available fields: " + fields + "; available properties: " + properties);
		}

		public static object GetDefaultValue(Type type)
		{
			if (type == null) return null;
			if (type == typeof(void)) return null;
			if (type.IsValueType)
				return Activator.CreateInstance(type);
			return null;
		}

		public static bool IsStruct(Type type)
		{
			return type.IsValueType && !IsValue(type) && !IsVoid(type);
		}

		public static bool IsClass(Type type)
		{
			return !type.IsValueType;
		}

		public static bool IsValue(Type type)
		{
			return type.IsPrimitive || type.IsEnum;
		}

		public static bool IsVoid(Type type)
		{
			return type == typeof(void);
		}
	}
}