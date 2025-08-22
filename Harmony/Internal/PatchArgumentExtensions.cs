using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace HarmonyLib
{
	internal static class PatchArgumentExtensions
	{
		static IEnumerable<HarmonyArgument> AllHarmonyArguments(object[] attributes)
		{
			return attributes.Select(attr =>
			{
				if (attr.GetType().Name != nameof(HarmonyArgument)) return null;
				return AccessTools.MakeDeepCopy<HarmonyArgument>(attr);
			})
			.OfType<HarmonyArgument>();
		}

		internal static HarmonyArgument GetArgumentAttribute(this ParameterInfo parameter)
		{
			try
			{
				var attributes = parameter.GetCustomAttributes(true);
				return AllHarmonyArguments(attributes).FirstOrDefault();
			}
			catch (NotSupportedException)
			{
				return null;
			}
		}

		internal static IEnumerable<HarmonyArgument> GetArgumentAttributes(this MethodInfo method)
		{
			try
			{
				var attributes = method.GetCustomAttributes(true);
				return AllHarmonyArguments(attributes);
			}
			catch (NotSupportedException)
			{
				return [];
			}
		}

		internal static IEnumerable<HarmonyArgument> GetArgumentAttributes(this Type type)
		{
			try
			{
				var attributes = type.GetCustomAttributes(true);
				return AllHarmonyArguments(attributes);
			}
			catch (NotSupportedException)
			{
				return [];
			}
		}

		internal static string GetRealName(this IEnumerable<HarmonyArgument> attributes, string name, string[] originalParameterNames)
		{
			var attribute = attributes.FirstOrDefault(p => p.OriginalName == name);
			if (attribute is null)
				return null;

			if (string.IsNullOrEmpty(attribute.NewName) is false)
				return attribute.NewName;

			if (originalParameterNames is not null && attribute.Index >= 0 && attribute.Index < originalParameterNames.Length)
				return originalParameterNames[attribute.Index];

			return null;
		}

		static string GetRealParameterName(this MethodInfo method, string[] originalParameterNames, string name)
		{
			if (method is null || method is DynamicMethod)
				return name;

			var argumentName = method.GetArgumentAttributes().GetRealName(name, originalParameterNames);
			if (argumentName is not null)
				return argumentName;

			var type = method.DeclaringType;
			if (type is not null)
			{
				argumentName = type.GetArgumentAttributes().GetRealName(name, originalParameterNames);
				if (argumentName is not null)
					return argumentName;
			}

			return name;
		}

		static string GetRealParameterName(this ParameterInfo parameter, string[] originalParameterNames)
		{
			var attribute = parameter.GetArgumentAttribute();
			if (attribute is null)
				return null;

			if (string.IsNullOrEmpty(attribute.OriginalName) is false)
				return attribute.OriginalName;

			if (attribute.Index >= 0 && attribute.Index < originalParameterNames.Length)
				return originalParameterNames[attribute.Index];

			return null;
		}

		internal static int GetArgumentIndex(this MethodInfo patch, string[] originalParameterNames, ParameterInfo patchParam)
		{
			if (patch is DynamicMethod)
				return Array.IndexOf(originalParameterNames, patchParam.Name);

			var originalName = patchParam.GetRealParameterName(originalParameterNames);
			if (originalName is not null)
				return Array.IndexOf(originalParameterNames, originalName);

			originalName = patch.GetRealParameterName(originalParameterNames, patchParam.Name);
			if (originalName is not null)
				return Array.IndexOf(originalParameterNames, originalName);

			return -1;
		}
	}
}
