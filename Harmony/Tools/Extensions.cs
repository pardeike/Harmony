using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Harmony
{
	public static class GeneralExtensions
	{
		public static string Join<T>(this IEnumerable<T> enumeration, Func<T, string> converter = null, string delimiter = ", ")
		{
			if (converter == null) converter = t => t.ToString();
			return enumeration.Aggregate("", (prev, curr) => prev + (prev != "" ? delimiter : "") + converter(curr));
		}

		public static string Description(this Type[] parameters)
		{
			if (parameters == null) return "NULL";
			var pattern = @", \w+, Version=[0-9.]+, Culture=neutral, PublicKeyToken=[0-9a-f]+";
			return "(" + parameters.Join(p => p?.FullName == null ? "null" : Regex.Replace(p.FullName, pattern, "")) + ")";
		}

		public static string FullDescription(this MethodBase method)
		{
			var parameters = method.GetParameters().Select(p => p.ParameterType).ToArray();
			return method.DeclaringType.FullName + "." + method.Name + parameters.Description();
		}

		public static Type[] Types(this ParameterInfo[] pinfo)
		{
			return pinfo.Select(pi => pi.ParameterType).ToArray();
		}

		public static T GetValueSafe<S, T>(this Dictionary<S, T> dictionary, S key)
		{
			T result;
			if (dictionary.TryGetValue(key, out result))
				return result;
			return default(T);
		}

		public static T GetTypedValue<T>(this Dictionary<string, object> dictionary, string key)
		{
			object result;
			if (dictionary.TryGetValue(key, out result))
				if (result is T)
					return (T)result;
			return default(T);
		}
	}

	public static class CollectionExtensions
	{
		public static void Do<T>(this IEnumerable<T> sequence, Action<T> action)
		{
			if (sequence == null) return;
			var enumerator = sequence.GetEnumerator();
			while (enumerator.MoveNext()) action(enumerator.Current);
		}

		public static void DoIf<T>(this IEnumerable<T> sequence, Func<T, bool> condition, Action<T> action)
		{
			sequence.Where(condition).Do(action);
		}

		public static IEnumerable<T> Add<T>(this IEnumerable<T> sequence, T item)
		{
			return (sequence ?? Enumerable.Empty<T>()).Concat(new[] { item });
		}

		public static T[] AddRangeToArray<T>(this T[] sequence, T[] items)
		{
			return (sequence ?? Enumerable.Empty<T>()).Concat(items).ToArray();
		}

		public static T[] AddToArray<T>(this T[] sequence, T item)
		{
			return Add(sequence, item).ToArray();
		}
	}
}