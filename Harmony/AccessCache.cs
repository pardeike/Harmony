using System;
using System.Collections.Generic;
using System.Reflection;

namespace Harmony
{
	public class AccessCache
	{
		internal Dictionary<Type, Dictionary<string, FieldInfo>> fields = new Dictionary<Type, Dictionary<string, FieldInfo>>();
		internal Dictionary<Type, Dictionary<string, PropertyInfo>> properties = new Dictionary<Type, Dictionary<string, PropertyInfo>>();
		internal Dictionary<Type, Dictionary<string, Dictionary<Type[], MethodInfo>>> methods = new Dictionary<Type, Dictionary<string, Dictionary<Type[], MethodInfo>>>();

		public FieldInfo GetFieldInfo(Type type, string name)
		{
			Dictionary<string, FieldInfo> fieldsByType = null;
			fields.TryGetValue(type, out fieldsByType);
			if (fieldsByType == null)
			{
				fieldsByType = new Dictionary<string, FieldInfo>();
				Debug.Log("Cache - adding field type " + type.FullName);
				fields.Add(type, fieldsByType);
			}

			FieldInfo field = null;
			fieldsByType.TryGetValue(name, out field);
			if (field == null)
			{
				field = AccessTools.Field(type, name);
				Debug.Log("Cache - adding fieldinfo " + field);
				fieldsByType.Add(name, field);
			}
			return field;
		}

		public PropertyInfo GetPropertyInfo(Type type, string name)
		{
			Dictionary<string, PropertyInfo> propertiesByType = null;
			properties.TryGetValue(type, out propertiesByType);
			if (propertiesByType == null)
			{
				propertiesByType = new Dictionary<string, PropertyInfo>();
				Debug.Log("Cache - adding property type " + type.FullName);
				properties.Add(type, propertiesByType);
			}

			PropertyInfo property = null;
			propertiesByType.TryGetValue(name, out property);
			if (property == null)
			{
				property = AccessTools.Property(type, name);
				Debug.Log("Cache - adding propertyinfo " + property);
				propertiesByType.Add(name, property);
			}
			return property;
		}

		public MethodInfo GetMethodInfo(Type type, string name, Type[] arguments)
		{
			Dictionary<string, Dictionary<Type[], MethodInfo>> methodsByName = null;
			methods.TryGetValue(type, out methodsByName);
			if (methodsByName == null)
			{
				methodsByName = new Dictionary<string, Dictionary<Type[], MethodInfo>>();
				Debug.Log("Cache - adding method type " + type.FullName);
				methods.Add(type, methodsByName);
			}

			Dictionary<Type[], MethodInfo> methodsByArguments = null;
			methodsByName.TryGetValue(name, out methodsByArguments);
			if (methodsByArguments == null)
			{
				methodsByArguments = new Dictionary<Type[], MethodInfo>();
				Debug.Log("Cache - adding method arguments " + name);
				methodsByName.Add(name, methodsByArguments);
			}

			MethodInfo method = null;
			methodsByArguments.TryGetValue(arguments, out method);
			if (method == null)
			{
				method = AccessTools.Method(type, name, arguments);
				Debug.Log("Cache - adding methodinfo " + method);
				methodsByArguments.Add(arguments, method);
			}

			return method;
		}
	}
}