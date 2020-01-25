using HarmonyLib;
using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace HarmonyLibTests
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
	public class GenericsPatches
	{
		[Test]
		public void Test_GenericStructReturnTypes()
		{
			var originalClass = typeof(MyList<>).MakeGenericType(typeof(int));
			Assert.IsNotNull(originalClass);
			var originalMethod = originalClass.GetMethod("GetEnumerator");
			Assert.IsNotNull(originalMethod);

			var patchClass = typeof(TestGenericStructReturnTypes_Patch);
			var postfix = patchClass.GetMethod("Postfix");
			Assert.IsNotNull(postfix);

			var instance = new Harmony("test");
			Assert.IsNotNull(instance);

			var patcher = instance.CreateProcessor(originalMethod);
			Assert.IsNotNull(patcher);
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
	}
}