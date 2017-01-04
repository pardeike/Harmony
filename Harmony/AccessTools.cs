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

		public static MethodInfo Method(Type type, string name, Type[] arguments = null)
		{
			if (type == null || name == null) return null;
			if (arguments == null) return type.GetMethod(name, all);
			var result = type.GetMethod(name, all, null, arguments, null);
			return result;
		}

		public static Type Inner(Type type, string name)
		{
			if (type == null || name == null) return null;
			return type.GetNestedType(name, all);
		}

		public static Type[] GetTypes(object[] arguments)
		{
			if (arguments == null) return new Type[0];
			return arguments.Select(a => a == null ? typeof(object) : a.GetType()).ToArray();
		}

		public static object GetDefaultValue(Type type)
		{
			if (type == null) return null;
			if (type == typeof(void)) return null;
			if (type.IsValueType)
				return Activator.CreateInstance(type);
			return null;
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

	public class AccessCache
	{
		internal Dictionary<Type, Dictionary<string, FieldInfo>> fields = new Dictionary<Type, Dictionary<string, FieldInfo>>();
		internal Dictionary<Type, Dictionary<string, PropertyInfo>> properties = new Dictionary<Type, Dictionary<string, PropertyInfo>>();
		internal Dictionary<Type, Dictionary<string, Dictionary<Type[], MethodInfo>>> methods = new Dictionary<Type, Dictionary<string, Dictionary<Type[], MethodInfo>>>();

		internal FieldInfo GetFieldInfo(Type type, string name)
		{
			Dictionary<string, FieldInfo> fieldsByType = null;
			fields.TryGetValue(type, out fieldsByType);
			if (fieldsByType == null)
			{
				fieldsByType = new Dictionary<string, FieldInfo>();
				fields.Add(type, fieldsByType);
			}

			FieldInfo field = null;
			fieldsByType.TryGetValue(name, out field);
			if (field == null)
			{
				field = AccessTools.Field(type, name);
				fieldsByType.Add(name, field);
			}
			return field;
		}

		internal PropertyInfo GetPropertyInfo(Type type, string name)
		{
			Dictionary<string, PropertyInfo> propertiesByType = null;
			properties.TryGetValue(type, out propertiesByType);
			if (propertiesByType == null)
			{
				propertiesByType = new Dictionary<string, PropertyInfo>();
				properties.Add(type, propertiesByType);
			}

			PropertyInfo property = null;
			propertiesByType.TryGetValue(name, out property);
			if (property == null)
			{
				property = AccessTools.Property(type, name);
				propertiesByType.Add(name, property);
			}
			return property;
		}

		internal MethodInfo GetMethodInfo(Type type, string name, Type[] arguments)
		{
			Dictionary<string, Dictionary<Type[], MethodInfo>> methodsByName = null;
			methods.TryGetValue(type, out methodsByName);
			if (methodsByName == null)
			{
				methodsByName = new Dictionary<string, Dictionary<Type[], MethodInfo>>();
				methods.Add(type, methodsByName);
			}

			Dictionary<Type[], MethodInfo> methodsByArguments = null;
			methodsByName.TryGetValue(name, out methodsByArguments);
			if (methodsByArguments == null)
			{
				methodsByArguments = new Dictionary<Type[], MethodInfo>();
				methodsByName.Add(name, methodsByArguments);
			}

			MethodInfo method = null;
			methodsByArguments.TryGetValue(arguments, out method);
			if (method == null)
			{
				method = AccessTools.Method(type, name, arguments);
				methodsByArguments.Add(arguments, method);
			}

			return method;
		}
	}
}