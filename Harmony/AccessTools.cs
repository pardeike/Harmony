using System;
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
}