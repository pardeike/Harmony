using System;
using System.Collections.Generic;
using System.Linq;

namespace Harmony
{
	public static class CollectionTools
	{
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