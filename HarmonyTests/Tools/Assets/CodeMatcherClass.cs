using System;

namespace HarmonyTests.Tools.Assets
{
	// we keep this code pretty simple so the IL does not change
	// between runtim versions

	public class CodeMatcherClass
	{
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
}