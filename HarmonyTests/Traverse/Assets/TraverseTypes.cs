using System;
using System.Collections.Generic;
using System.Linq;

namespace HarmonyLibTests.Assets
{
	public class TraverseTypes<T> where T : new()
	{
#pragma warning disable IDE0052
#pragma warning disable CS0414
		private readonly int IntField;
		private readonly string StringField;
		private readonly Type TypeField;
		private readonly IEnumerable<bool> ListOfBoolField;
		private readonly Dictionary<T, List<string>> MixedField;
#pragma warning restore CS0414
#pragma warning restore IDE0052

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
		class InnerClass1
		{
			class InnerClass2
			{
#pragma warning disable IDE0052
#pragma warning disable CS0414
				private readonly string field;
#pragma warning restore CS0414
#pragma warning restore IDE0052

				public InnerClass2()
				{
					field = "helloInstance";
				}
			}

#pragma warning disable IDE0052
			readonly InnerClass2 inner2;
#pragma warning restore IDE0052

			public InnerClass1()
			{
				inner2 = new InnerClass2();
			}
		}

		class InnerStaticFieldClass1
		{
			class InnerStaticFieldClass2
			{
#pragma warning disable IDE0051
#pragma warning disable CS0414
				static readonly string field = "helloStatic";
#pragma warning restore CS0414
#pragma warning restore IDE0051
			}

#pragma warning disable IDE0052
			static InnerStaticFieldClass2 inner2;
#pragma warning restore IDE0052

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

#pragma warning disable IDE0052
		readonly InnerClass1 innerInstance;
		static InnerStaticFieldClass1 innerStatic;
#pragma warning restore IDE0052

		public TraverseNestedTypes(string staticValue)
		{
			innerInstance = new InnerClass1();
			innerStatic = new InnerStaticFieldClass1();
			InnerStaticClass1.InnerStaticClass2.field = staticValue;
		}
	}

}
