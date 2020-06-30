using HarmonyLib;

namespace HarmonyLibTests.Assets
{
#pragma warning disable CS0169, CS0414, IDE0051, IDE0052
	public class AccessToolsClass
	{
		class Inner
		{
		}

		private string field1 = "field1orig";
		public readonly string field2 = "field2orig";
		public static string field3 = "field3orig";
		// Note: static readonly fields cannot be set by reflection since .NET Core 3+:
		// https://docs.microsoft.com/en-us/dotnet/core/compatibility/corefx#fieldinfosetvalue-throws-exception-for-static-init-only-fields
		// As of .NET Core 3.1, the FieldRef delegates can change static readonly fields, so all resetting happens in the unit tests themselves.
		private static readonly string field4 = "field4orig";
		public int field5 = -111;
		private readonly int field6 = -999;

		int _property;

		int Property
		{
			get => _property;
			set => _property = value;
		}
		int Property2
		{
			get => _property;
			set => _property = value;
		}

		// Workaround for structs incapable of having a default constructor:
		// use a dummy non-default constructor for all involved asset types.
		// Class instance fields already have inlined defaults above.
		public AccessToolsClass(object _) { }

		public string Method1()
		{
			return field1;
		}

		public string Method2()
		{
			return field2;
		}

		public void SetField(string val)
		{
			field1 = val;
		}

		public string Method3()
		{
			return field3;
		}

		public string Method4()
		{
			return field4;
		}
	}

	public class AccessToolsSubClass : AccessToolsClass
	{
		public AccessToolsSubClass(object _) : base(_) { }
	}

	public struct AccessToolsStruct
	{
		public string structField1;
		private readonly int structField2;
		private static int structField3 = -123;
		public static readonly string structField4 = "structField4orig";

		// Structs don't allow default constructor, but we need to assign some values to instance fields
		// that aren't simply the default value for their types (so that ref value can be checked against orig value).
		public AccessToolsStruct(object _)
		{
			structField1 = "structField1orig";
			structField2 = -666;
		}
	}
#pragma warning restore CS0169, CS0414, IDE0051, IDE0052

	public static class AccessToolsMethodDelegate
	{
		public interface IInterface
		{
			string Test(int n, ref float f);
		}

		public class Base : IInterface
		{
			public int x;

			public virtual string Test(int n, ref float f)
			{
				return $"base test {n} {++f} {++x}";
			}
		}

		public class Derived : Base
		{
			public override string Test(int n, ref float f)
			{
				return $"derived test {n} {++f} {++x}";
			}
		}

		public struct Struct : IInterface
		{
			public int x;

			public string Test(int n, ref float f)
			{
				return $"struct result {n} {++f} {++x}";
			}
		}

		public static int x;

		public static string Test(int n, ref float f)
		{
			return $"static test {n} {++f} {++x}";
		}
	}

	public static class AccessToolsHarmonyDelegate
	{
		public class Foo
		{
			private string SomeMethod(string s)
			{
				return $"[{s}]";
			}
		}

		[HarmonyDelegate(typeof(Foo), "SomeMethod")]
		public delegate string FooSomeMethod(Foo foo, string s);
	}
}