using HarmonyLib;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace HarmonyLibTests.Assets
{
	[HarmonyPatch]
	public class NoAttributesClass
	{
		[HarmonyPrepare]
		public void Method1() { }
	}

	[HarmonyPatch(typeof(string))]
	[HarmonyPatch("foobar")]
	[HarmonyPriority(Priority.HigherThanNormal)]
	[HarmonyPatch([typeof(float), typeof(string)])]
	public class AllAttributesClass
	{
		[HarmonyPrepare]
		public static bool Method1() => true;

		[HarmonyPrefix]
		[HarmonyPriority(Priority.High)]
		public static void Method2() { }

		[HarmonyPostfix]
		[HarmonyBefore("foo", "bar")]
		[HarmonyAfter("test")]
		public static void Method3() { }

		[HarmonyFinalizer]
		[HarmonyPriority(Priority.Low)]
		public static void Method4() { }
	}

	public class AllAttributesClassMethodsInstance
	{
		public static void Test()
		{
		}
	}

	[HarmonyPatch(typeof(AllAttributesClassMethodsInstance), "Test")]
	[HarmonyPriority(Priority.HigherThanNormal)]
	public class AllAttributesClassMethods
	{
		[HarmonyPrepare]
		public static bool Method1() => true;

		[HarmonyCleanup]
		public static void Method2() { }

		[HarmonyPrefix]
		[HarmonyPriority(Priority.Low)]
		public static void Method3Low() { }

		[HarmonyPrefix]
		[HarmonyPriority(Priority.High)]
		public static void Method3High() { }

		[HarmonyPostfix]
		[HarmonyBefore("xfoo", "xbar")]
		[HarmonyAfter("xtest")]
		[HarmonyPriority(Priority.High)]
		public static void Method4High() { }

		[HarmonyPostfix]
		[HarmonyBefore("xfoo", "xbar")]
		[HarmonyAfter("xtest")]
		[HarmonyPriority(Priority.Low)]
		public static void Method4Low() { }

		[HarmonyFinalizer]
		[HarmonyPriority(Priority.Low)]
		public static void Method5Low() { }

		[HarmonyFinalizer]
		[HarmonyPriority(Priority.High)]
		public static void Method5High() { }
	}

	public class NoAnnotationsClass
	{
		[HarmonyPatch(typeof(List<string>), "TestMethod")]
		[HarmonyPatch([typeof(string), typeof(string), typeof(string)], [ArgumentType.Normal, ArgumentType.Ref, ArgumentType.Normal])]
		static void Patch() { }
	}

	public class MainClass
	{
		[MethodImpl(MethodImplOptions.NoInlining)]
		public void SomeMethod()
		{
		}
	}

	public class SubClass : MainClass
	{
	}

	[HarmonyPatch(typeof(MainClass), "SomeMethod")]
	public class MainClassPatch
	{
		public static void Prefix()
		{
		}
	}

	[HarmonyPatch(typeof(SubClass), "SomeMethod")]
	public class SubClassPatch
	{
		public static void Prefix()
		{
		}
	}
}
