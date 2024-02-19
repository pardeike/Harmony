using HarmonyLib;
using System;
using System.Collections.Generic;

namespace HarmonyLibTests.Assets
{
	public interface IAccessToolsType
	{
		int Property1 { get; }

		string this[string key] { get; }

		string Method1();
	}

	public interface IInner { }

#pragma warning disable CS0169, CS0414, IDE0044, IDE0051, IDE0052
	public class AccessToolsClass : IAccessToolsType
	{
		private class Inner : IInner
		{
			public int x;

			public override string ToString() => x.ToString();
		}

		public static IInner NewInner(int x) => new Inner { x = x };

		private struct InnerStruct : IInner
		{
			public int x;

			public override string ToString() => x.ToString();
		}

		public static IInner NewInnerStruct(int x) => new InnerStruct { x = x };

		protected string field1 = "field1orig";
		public readonly float field2 = 2.71828f;
		public static long field3 = 271828L;
		// Note: static readonly fields cannot be set by reflection since .NET Core 3+:
		// https://docs.microsoft.com/en-us/dotnet/core/compatibility/corefx#fieldinfosetvalue-throws-exception-for-static-init-only-fields
		// As of .NET Core 3.1, the FieldRef delegates can change static readonly fields, so all resetting happens in the unit tests themselves.
		private static readonly string field4 = "field4orig";
		private Inner field5 = new() { x = 999 };
		private Inner[] field6 = [new Inner { x = 11 }, new Inner { x = 22 }];
		private InnerStruct field7 = new() { x = 999 };
		private List<InnerStruct> field8 = [new InnerStruct { x = 11 }, new InnerStruct { x = 22 }];
		internal DayOfWeek field9 = DayOfWeek.Saturday;

		private int _property = 314159;

		public int Property1
		{
			get => _property;
			set => _property = value;
		}

		private int Property1b
		{
			get => _property;
			set => _property = value;
		}

		protected string Property2 { get; } = "3.14159";

		public static string Property3 { get; set; } = "2.71828";

		private static double Property4 { get; } = 2.71828;

		public string this[string key]
		{
			get => key;
			set { }
		}

		public string Method1() => field1;

		public double Method2() => field2;

		public void SetField(string val) => field1 = val;

		public static long Method3() => field3;

		public static string Method4() => field4;
	}

	public class AccessToolsSubClass : AccessToolsClass
	{
		private string subclassField1 = "subclassField1orig";
		internal static int subclassField2 = -321;
	}

#pragma warning disable CS9113
	public struct AccessToolsStruct(object obj) : IAccessToolsType
	{
		private enum InnerEnum : byte
		{
			A = 1,
			B = 2,
			C = 4,
		}

		public static Enum NewInnerEnum(byte b) => (InnerEnum)b;

		public string structField1 = "structField1orig";
		private readonly int structField2 = -666;
		private static int structField3 = -123;
		public static readonly string structField4 = "structField4orig";
		private InnerEnum structField5 = InnerEnum.B;

		public int Property1 { get; set; } = 161803;

		private string Property2 { get; } = "1.61803";

		public static int Property3 { get; set; } = 299792458;

		private static string Property4 { get; } = "299,792,458";

		public string this[string key] => key;

		public string Method1() => structField1;
	}
#pragma warning restore CS0169, CS0414, CS9113, IDE0044, IDE0051, IDE0052

	public static class AccessToolsCreateInstance
	{
		// Has default public parameterless constructor.
		public class NoConstructor
		{
			public bool constructorCalled = true;
		}

		// Does NOT have a default public parameterless constructor (or any parameterless constructor for that matter).
		public class OnlyNonParameterlessConstructor(int n)
		{
			public bool constructorCalled = n != 0xbeef; // true
		}

		public class PublicParameterlessConstructor
		{
			public bool constructorCalled = true;

			public PublicParameterlessConstructor()
			{
			}
		}

		public class InternalParameterlessConstructor
		{
			public bool constructorCalled = true;

			internal InternalParameterlessConstructor()
			{
			}
		}
	}

	public static class AccessToolsMethodDelegate
	{
		public interface IInterface
		{
			string Test(int n, ref float f);
		}

		public class Base : IInterface
		{
			public int x;

			public virtual string Test(int n, ref float f) => $"base test {n} {++f} {++x}";
		}

		public class Derived : Base
		{
			public override string Test(int n, ref float f) => $"derived test {n} {++f} {++x}";
		}

		public struct Struct : IInterface
		{
			public int x;

			public string Test(int n, ref float f) => $"struct result {n} {++f} {++x}";
		}

		public static int x;

		public static string Test(int n, ref float f) => $"static test {n} {++f} {++x}";
	}

	public static class AccessToolsHarmonyDelegate
	{
		public class Foo
		{
			private string SomeMethod(string s) => $"[{s}]";
		}

		[HarmonyDelegate(typeof(Foo), "SomeMethod")]
		public delegate string FooSomeMethod(Foo foo, string s);
	}
}
