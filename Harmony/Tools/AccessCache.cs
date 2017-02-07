using System;
using System.Collections.Generic;
using System.Reflection;

namespace Harmony
{
	public class AccessCache
	{
		Dictionary<Type, Dictionary<string, FieldInfo>> fields = new Dictionary<Type, Dictionary<string, FieldInfo>>();
		Dictionary<Type, Dictionary<string, PropertyInfo>> properties = new Dictionary<Type, Dictionary<string, PropertyInfo>>();
		readonly Dictionary<Type, Dictionary<string, Dictionary<Type[], MethodBase>>> methods = new Dictionary<Type, Dictionary<string, Dictionary<Type[], MethodBase>>>();

		public FieldInfo GetFieldInfo(Type type, string name)
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

		public PropertyInfo GetPropertyInfo(Type type, string name)
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

		public MethodBase GetMethodInfo(Type type, string name, Type[] arguments)
		{
			Dictionary<string, Dictionary<Type[], MethodBase>> methodsByName = null;
			methods.TryGetValue(type, out methodsByName);
			if (methodsByName == null)
			{
				methodsByName = new Dictionary<string, Dictionary<Type[], MethodBase>>();
				methods.Add(type, methodsByName);
			}

			Dictionary<Type[], MethodBase> methodsByArguments = null;
			methodsByName.TryGetValue(name, out methodsByArguments);
			if (methodsByArguments == null)
			{
				methodsByArguments = new Dictionary<Type[], MethodBase>();
				methodsByName.Add(name, methodsByArguments);
			}

			MethodBase method = null;
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