using System;

namespace HarmonyTests.Tools.Assets
{
	// we keep this code pretty simple so the IL does not change
	// between runtim versions

	public class CodeMatcherClass
	{
		public static int StaticField;
		public static int OtherStaticField;
		public int InstanceField;

		public static int StaticProperty { get; set; }
		public int InstanceProperty { get; set; }
		public virtual int VirtualProperty { get; set; }
		public int GetOnlyProperty => InstanceField;
		public int SetOnlyProperty { set => InstanceField = value; }

		public static void Foo() { }
		public static void Bar(string s) { }

		public static void Method()
		{
			Foo();
			Bar("A");
			Bar("B");
			Bar("C");
			Foo();
			Bar("D");
			Foo();
			Bar("E");
			Bar("F");
			Bar("G");
			Bar("H");
			Foo();
		}
	}

	public class CodeMatcherDerivedClass : CodeMatcherClass
	{
		public override int VirtualProperty { get; set; }

		public void UseProperties()
		{
			InstanceProperty = VirtualProperty;
			VirtualProperty = InstanceProperty;
		}
	}
}
