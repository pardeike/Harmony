using HarmonyLib;
using HarmonyLibTests.Assets;
using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace HarmonyLibTests.Patching
{
	public class MyList<T> : IEnumerable<T>, IEnumerable
	{
		public struct MyEnumerator : IEnumerator<T>, IEnumerator
		{
			public List<T> list;
			private int index;
			public T Current { get; private set; }

			object IEnumerator.Current
			{
				get
				{
					if (index == 0 || index == list.Count + 1)
						throw new IndexOutOfRangeException();
					return Current;
				}
			}

			internal void SetList(List<T> list)
			{
				this.list = list;
			}

			internal MyEnumerator(List<T> list)
			{
				this.list = list;
				index = 0;
				Current = default;
			}

			public void Dispose()
			{
			}

			public bool MoveNext()
			{
				if ((uint)index < (uint)list.Count)
				{
					Current = list[index];
					index++;
					return true;
				}
				return MoveNextRare();
			}

			private bool MoveNextRare()
			{
				index = list.Count + 1;
				Current = default;
				return false;
			}

			void IEnumerator.Reset()
			{
				index = 0;
				Current = default;
			}
		}

		public List<T> list = new List<T>();

		[MethodImpl(MethodImplOptions.NoInlining)]
		public MyEnumerator GetEnumerator()
		{
			return new MyEnumerator(list);
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		IEnumerator<T> IEnumerable<T>.GetEnumerator()
		{
			return new MyEnumerator(list);
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		IEnumerator IEnumerable.GetEnumerator()
		{
			return new MyEnumerator(list);
		}
	}

	public class TestGenericStructReturnTypes_Patch
	{
		public static MyList<int>.MyEnumerator Postfix(MyList<int>.MyEnumerator input)
		{
			input.SetList(new List<int>() { 100, 200, 300 });
			return input;
		}
	}

	[TestFixture]
	public class GenericsPatches : TestLogger
	{
		[Test]
		public void Test_GenericStructReturnTypes()
		{
			var originalClass = typeof(MyList<>).MakeGenericType(typeof(int));
			Assert.NotNull(originalClass);
			var originalMethod = originalClass.GetMethod("GetEnumerator");
			Assert.NotNull(originalMethod);

			var patchClass = typeof(TestGenericStructReturnTypes_Patch);
			var postfix = patchClass.GetMethod("Postfix");
			Assert.NotNull(postfix);

			var instance = new Harmony("test");
			Assert.NotNull(instance);

			var patcher = instance.CreateProcessor(originalMethod);
			Assert.NotNull(patcher);
			_ = patcher.AddPostfix(postfix);
			_ = patcher.Patch();

			var list = new MyList<int> { list = new List<int>() { 1, 2, 3 } };

			var enumerator = list.GetEnumerator();
			var result = new List<int>();
			while (enumerator.MoveNext())
				result.Add(enumerator.Current);

			Assert.AreEqual(3, result.Count);
			Assert.AreEqual(result[0], 100);
			Assert.AreEqual(result[1], 200);
			Assert.AreEqual(result[2], 300);
		}

		[Test]
		public void Test_GenericsOriginalMethod()
		{
			var originalMethod = typeof(Class13<int>).GetMethod(nameof(Class13<int>.Add));
			Assert.NotNull(originalMethod);

			var patchClass = typeof(Class13Patch);
			var prefix = patchClass.GetMethod("Prefix");
			Assert.NotNull(prefix);

			var list1 = new Class13<int> { 1, 2, 3 };
			list1.Add(1000);
			var e1 = list1.GetEnumerator();
			_ = e1.MoveNext();
			_ = e1.MoveNext();
			_ = e1.MoveNext();
			_ = e1.MoveNext();
			Assert.AreEqual(1000, e1.Current);

			var instance = new Harmony("test");
			Assert.NotNull(instance);

			var patcher = instance.CreateProcessor(originalMethod);
			Assert.NotNull(patcher);
			_ = patcher.AddPrefix(prefix);
			_ = patcher.Patch();

			Class13Patch.method = null;
			Class13Patch.result = 0;

			var list2 = new Class13<int> { 1, 2, 3 };
			list2.Add(1000);
			var e2 = list2.GetEnumerator();
			_ = e2.MoveNext();
			_ = e2.MoveNext();
			_ = e2.MoveNext();
			_ = e2.MoveNext();
			Assert.AreEqual(999, e2.Current);

			Assert.AreEqual(1000, Class13Patch.result);
			Assert.AreEqual(originalMethod, Class13Patch.method);
		}
	}
}
