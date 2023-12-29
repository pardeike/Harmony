using System;
using System.Collections.Generic;
using System.Reflection;

namespace HarmonyLib
{
	internal class AccessCache
	{
		internal enum MemberType
		{
			Any,
			Static,
			Instance
		}

		const BindingFlags BasicFlags = BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.GetField | BindingFlags.SetField | BindingFlags.GetProperty | BindingFlags.SetProperty;
		static readonly Dictionary<MemberType, BindingFlags> declaredOnlyBindingFlags = new()
		{
			{ MemberType.Any, BasicFlags | BindingFlags.Instance | BindingFlags.Static },
			{ MemberType.Instance, BasicFlags | BindingFlags.Instance },
			{ MemberType.Static, BasicFlags | BindingFlags.Static }
		};

		readonly Dictionary<Type, Dictionary<string, FieldInfo>> declaredFields = [];
		readonly Dictionary<Type, Dictionary<string, PropertyInfo>> declaredProperties = [];
		readonly Dictionary<Type, Dictionary<string, Dictionary<int, MethodBase>>> declaredMethods = [];

		readonly Dictionary<Type, Dictionary<string, FieldInfo>> inheritedFields = [];
		readonly Dictionary<Type, Dictionary<string, PropertyInfo>> inheritedProperties = [];
		readonly Dictionary<Type, Dictionary<string, Dictionary<int, MethodBase>>> inheritedMethods = [];

		static T Get<T>(Dictionary<Type, Dictionary<string, T>> dict, Type type, string name, Func<T> fetcher)
		{
			lock (dict)
			{
				if (dict.TryGetValue(type, out var valuesByName) is false)
				{
					valuesByName = [];
					dict[type] = valuesByName;
				}
				if (valuesByName.TryGetValue(name, out var value) is false)
				{
					value = fetcher();
					valuesByName[name] = value;
				}
				return value;
			}
		}

		static T Get<T>(Dictionary<Type, Dictionary<string, Dictionary<int, T>>> dict, Type type, string name, Type[] arguments, Func<T> fetcher)
		{
			lock (dict)
			{
				if (dict.TryGetValue(type, out var valuesByName) is false)
				{
					valuesByName = [];
					dict[type] = valuesByName;
				}
				if (valuesByName.TryGetValue(name, out var valuesByArgument) is false)
				{
					valuesByArgument = [];
					valuesByName[name] = valuesByArgument;
				}
				var argumentsHash = AccessTools.CombinedHashCode(arguments);
				if (valuesByArgument.TryGetValue(argumentsHash, out var value) is false)
				{
					value = fetcher();
					valuesByArgument[argumentsHash] = value;
				}
				return value;
			}
		}

		internal FieldInfo GetFieldInfo(Type type, string name, MemberType memberType = MemberType.Any, bool declaredOnly = false)
		{
			var value = Get(declaredFields, type, name, () => type.GetField(name, declaredOnlyBindingFlags[memberType]));
			if (value is null && declaredOnly is false)
				value = Get(inheritedFields, type, name, () => AccessTools.FindIncludingBaseTypes(type, t => t.GetField(name, AccessTools.all)));
			return value;
		}

		internal PropertyInfo GetPropertyInfo(Type type, string name, MemberType memberType = MemberType.Any, bool declaredOnly = false)
		{
			var value = Get(declaredProperties, type, name, () => type.GetProperty(name, declaredOnlyBindingFlags[memberType]));
			if (value is null && declaredOnly is false)
				value = Get(inheritedProperties, type, name, () => AccessTools.FindIncludingBaseTypes(type, t => t.GetProperty(name, AccessTools.all)));
			return value;
		}

		internal MethodBase GetMethodInfo(Type type, string name, Type[] arguments, MemberType memberType = MemberType.Any, bool declaredOnly = false)
		{
			var value = Get(declaredMethods, type, name, arguments, () => type.GetMethod(name, declaredOnlyBindingFlags[memberType], null, arguments, null));
			if (value is null && declaredOnly is false)
				value = Get(inheritedMethods, type, name, arguments, () => AccessTools.Method(type, name, arguments));
			return value;
		}
	}
}
