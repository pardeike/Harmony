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

		public static IEnumerable<T> Do<T>(this IEnumerable<T> sequence, Action<T> action)
		{
			if (sequence == null) return null;
			IEnumerator<T> enumerator = sequence.GetEnumerator();
			while (enumerator.MoveNext()) action(enumerator.Current);
			return sequence;
		}

		public static IEnumerable<T> DoIf<T>(this IEnumerable<T> sequence, Func<T, bool> condition, Action<T> action)
		{
			return sequence.Where(condition).Do(action);
		}

		public static FieldInfo Field(Type type, string name)
		{
			if (type == null || name == null) return null;
			return type.GetField(name, all);
		}

		public static PropertyInfo Property(Type type, string name)
		{
			if (type == null || name == null) return null;
			return type.GetProperty(name, all);
		}

		public static MethodInfo Method(Type type, string name, Type[] parameters = null)
		{
			if (type == null || name == null) return null;
			if (parameters == null) return type.GetMethod(name, all);
			var result = type.GetMethod(name, all, null, parameters, null);
			return result;
		}

		public static Type Inner(Type type, string name)
		{
			if (type == null || name == null) return null;
			return type.GetNestedType(name, all);
		}

		public static Type[] GetTypes(object[] parameters)
		{
			if (parameters == null) return new Type[0];
			return parameters.Select(p => p == null ? typeof(object) : p.GetType()).ToArray();
		}

		public static List<string> GetFieldNames(Type type)
		{
			return type.GetFields(all).Select(f => f.Name).ToList();
		}

		public static List<string> GetFieldNames(object instance)
		{
			if (instance == null) return new List<string>();
			return GetFieldNames(instance.GetType());
		}

		public static List<string> GetPropertyNames(Type type)
		{
			return type.GetProperties(all).Select(f => f.Name).ToList();
		}

		public static List<string> GetPropertyNames(object instance)
		{
			if (instance == null) return new List<string>();
			return GetPropertyNames(instance.GetType());
		}

		public static object GetDefaultValue(Type type)
		{
			if (type == null) return null;
			if (type == typeof(void)) return null;
			if (type.IsValueType)
				return Activator.CreateInstance(type);
			return null;
		}

		public static bool isStruct(Type type)
		{
			return type.IsValueType && !isValue(type) && !isVoid(type);
		}

		public static bool isClass(Type type)
		{
			return !type.IsValueType;
		}

		public static bool isValue(Type type)
		{
			return type.IsPrimitive || type.IsEnum;
		}

		public static bool isVoid(Type type)
		{
			return type == typeof(void);
		}
	}

	public static class TypeExtensions
	{
		public static string Description(this Type[] parameters)
		{
			var types = parameters.Select(p => p == null ? "null" : p.FullName);
			return "(" + types.Aggregate("", (s, x) => s.Length == 0 ? x : s + ", " + x) + ")";
		}

		public static Type[] Types(this ParameterInfo[] pinfo)
		{
			return pinfo.Select(pi => pi.ParameterType).ToArray();
		}
	}
}