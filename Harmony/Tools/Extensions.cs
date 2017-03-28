using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Harmony
{
	public static class GeneralExtensions
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

	public static class EnumerableExtensions
	{
		public static IEnumerable<T> Do<T>(this IEnumerable<T> sequence, Action<T> action)
		{
			if (sequence == null) return null;
			foreach (var item in sequence)
			{
				action(item);
			}
			return sequence;
		}

		public static IEnumerable<T> DoIf<T>(this IEnumerable<T> sequence, Func<T, bool> condition, Action<T> action)
		{
			return sequence.Where(condition).Do(action);
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
		public static IEnumerable<T> ReplaceMatchingSequence<T>(this IEnumerable<T> source, IEnumerable<T> elements, Func<IEnumerable<T>, IEnumerable<T>> replacer)
		{
			return ReplaceMatchingSequence(source, elements.Select(x => (Func<T, bool>)(y => y.Equals(x))), replacer);
		}

		public static IEnumerable<T> ReplaceMatchingSequence<T>(this IEnumerable<T> source, IEnumerable<Func<T, bool>> predicates, Func<IEnumerable<T>, IEnumerable<T>> replacer)
		{
			if (source == null)
				throw new ArgumentNullException(nameof(source));

			if (predicates == null)
				throw new ArgumentNullException(nameof(predicates));

			var p = predicates.ToArray();

			if (p.Length == 0)
				throw new ArgumentException("Count of predicates is zero.");

			if (replacer == null)
				throw new ArgumentNullException(nameof(replacer));

			return ReplaceMatchingSequenceInner(source, p, replacer);
		}

		private static IEnumerable<T> ReplaceMatchingSequenceInner<T>(this IEnumerable<T> source, Func<T, bool>[] predicates, Func<IEnumerable<T>, IEnumerable<T>> replacer)
		{
			var matchingIndex = -1;

			var buffer = new List<T>();

			foreach (var e in source)
			{
				// matched next predicate
				if (predicates[matchingIndex + 1](e))
				{
					matchingIndex++;

					// add item to buffer
					buffer.Add(e);

					// all predicates matched
					if (matchingIndex + 1 == predicates.Length)
					{
						matchingIndex = -1;
						foreach (var r in replacer(buffer))
						{
							yield return r;
						}
						buffer.Clear();
					}
				}
				else
				{
					// no match, so clear buffer if any and reset index
					foreach (var i in buffer)
						yield return i;
					buffer.Clear();
					matchingIndex = -1;

					// then return current item
					yield return e;
				}
			}

			// finished going through source, send out the buffer if any
			foreach (var l in buffer)
				yield return l;
		}
	}
}