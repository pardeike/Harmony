using System;
using System.Collections.Generic;
using System.Linq;

namespace HarmonyTests.Assets
{
	public class TraverseTypes<T> where T : new()
	{
#pragma warning disable CS0414
		int IntField;
		string StringField;
#pragma warning restore CS0414
		Type TypeField;
		IEnumerable<bool> ListOfBoolField;
		Dictionary<T, List<string>> MixedField;

		public T key;

		public TraverseTypes()
		{
			IntField = 100;
			StringField = "hello";
			TypeField = typeof(Console);
			ListOfBoolField = (new bool[] { false, true }).Select(b => !b);

			var d = new Dictionary<T, List<string>>();
			var l = new List<string> { "world" };
			key = new T();
			d.Add(key, l);
			MixedField = d;
		}
	}

	public class TraverseNestedTypes
	{
		private class InnerClass1
		{
			private class InnerClass2
			{
#pragma warning disable CS0414
				private string field;
#pragma warning restore CS0414

				public InnerClass2()
				{
					field = "helloInstance";
				}
			}

			private InnerClass2 inner2;

			public InnerClass1()
			{
				inner2 = new InnerClass2();
			}
		}

		private class InnerStaticFieldClass1
		{
			private class InnerStaticFieldClass2
			{
#pragma warning disable CS0414
				private static string field = "helloStatic";
#pragma warning restore CS0414
			}

			private static InnerStaticFieldClass2 inner2;

			public InnerStaticFieldClass1()
			{
				inner2 = new InnerStaticFieldClass2();
			}
		}

		protected static class InnerStaticClass1
		{
			internal static class InnerStaticClass2
			{
				internal static string field;
			}
		}

		private InnerClass1 innerInstance;
		private static InnerStaticFieldClass1 innerStatic;

		public TraverseNestedTypes(string staticValue)
		{
			innerInstance = new InnerClass1();
			innerStatic = new InnerStaticFieldClass1();
			InnerStaticClass1.InnerStaticClass2.field = staticValue;
		}
	}

}