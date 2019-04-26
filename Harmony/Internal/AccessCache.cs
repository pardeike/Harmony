using System;
using System.Collections.Generic;
using System.Reflection;

namespace HarmonyLib
{
	internal class AccessCache
	{
		readonly Dictionary<Type, Dictionary<string, FieldInfo>> fields = new Dictionary<Type, Dictionary<string, FieldInfo>>();
		readonly Dictionary<Type, Dictionary<string, PropertyInfo>> properties = new Dictionary<Type, Dictionary<string, PropertyInfo>>();
		readonly Dictionary<Type, Dictionary<string, Dictionary<int, MethodBase>>> methods = new Dictionary<Type, Dictionary<string, Dictionary<int, MethodBase>>>();

		internal FieldInfo GetFieldInfo(Type type, string name)
		{
			if (fields.TryGetValue(type, out var fieldsByType) == false)
			{
				fieldsByType = new Dictionary<string, FieldInfo>();
				fields.Add(type, fieldsByType);
			}
			if (fieldsByType.TryGetValue(name, out var field) == false)
			{
				field = AccessTools.DeclaredField(type, name);
				fieldsByType.Add(name, field);
			}
			return field;
		}

		internal PropertyInfo GetPropertyInfo(Type type, string name)
		{
			if (properties.TryGetValue(type, out var propertiesByType) == false)
			{
				propertiesByType = new Dictionary<string, PropertyInfo>();
				properties.Add(type, propertiesByType);
			}
			if (propertiesByType.TryGetValue(name, out var property) == false)
			{
				property = AccessTools.DeclaredProperty(type, name);
				propertiesByType.Add(name, property);
			}
			return property;
		}

		static int CombinedHashCode(IEnumerable<object> objects)
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

		internal MethodBase GetMethodInfo(Type type, string name, Type[] arguments)
		{
			methods.TryGetValue(type, out var methodsByName);
			if (methodsByName == null)
			{
				methodsByName = new Dictionary<string, Dictionary<int, MethodBase>>();
				methods[type] = methodsByName;
			}
			methodsByName.TryGetValue(name, out var methodsByArguments);
			if (methodsByArguments == null)
			{
				methodsByArguments = new Dictionary<int, MethodBase>();
				methodsByName[name] = methodsByArguments;
			}
			var argumentsHash = CombinedHashCode(arguments);
			if (methodsByArguments.TryGetValue(argumentsHash, out var method) == false)
			{
				method = AccessTools.Method(type, name, arguments);
				methodsByArguments.Add(argumentsHash, method);
			}
			return method;
		}
	}
}