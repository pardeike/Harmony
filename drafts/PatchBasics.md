# ABOUT

A draft of how prefixes and postfixes can be applied.

# CONCEPT

Pseudo code to demonstrate how patches logically work:

```cs
public static void Main(String[] args)
{
	var harmony = new Harmony("test");
	harmony.PatchAll();
	
	Test1.Run();
	Test2.Run();
}

[HarmonyPatch(typeof(Test1), "Foo")]
public class SimplePatches
{
	// basic patch
	[HarmonyPrefix]
	[HarmonyPriority(0)]
	static void BasicPrefix()
	{
		Console.WriteLine("- BasicPrefix");
	}

	// injecting argument
	[HarmonyPrefix]
	[HarmonyPriority(-1)]
	static void InjectingArgument(string str)
	{
		Console.WriteLine($"- InjectingArgument {str}");
	}

	// injecting a private type argument
	[HarmonyPrefix]
	[HarmonyPriority(-2)]
	static void InjectingArgumentWithPrivateType(object p)
	{
		Console.WriteLine($"- InjectingArgumentWithPrivateType {p}");
	}

	// injecting instance
	[HarmonyPrefix]
	[HarmonyPriority(-3)]
	static void InjectingInstance(Test1 __instance)
	{
		Console.WriteLine($"- InjectingInstance {__instance}");
	}

	// injecting instance if its type is private
	[HarmonyPrefix]
	[HarmonyPriority(-4)]
	static void InjectingInstanceWithPrivateType(object __instance)
	{
		Console.WriteLine($"- InjectingInstanceWithPrivateType {__instance}");
	}

	// changing the value of an argument
	[HarmonyPrefix]
	[HarmonyPriority(-5)]
	static void ChangeArgument(ref string str)
	{
		var old = str;
		str = "changed";
		Console.WriteLine($"- ChangeArgument {old} {str}");
	}

	// injecting argument
	[HarmonyPrefix]
	[HarmonyPriority(-6)]
	static void InjectingNamedArgument([HarmonyArgument("str")] string myName)
	{
		Console.WriteLine($"- InjectingNamedArgument {myName}");
	}

	// injecting field
	[HarmonyPrefix]
	[HarmonyPriority(-7)]
	static void InjectingField(string ___field1)
	{
		Console.WriteLine($"- InjectingField {___field1}");
	}

	// changing field
	[HarmonyPrefix]
	[HarmonyPriority(-8)]
	static void ChangeField(ref string ___field1)
	{
		___field1 = "field1+";
		Console.WriteLine($"- ChangeField {___field1}");
	}

	// injecting private field
	[HarmonyPrefix]
	[HarmonyPriority(-9)]
	static void InjectingPrivateField(object ___priv)
	{
		Console.WriteLine($"- InjectingPrivateField {___priv}");
	}

	// calling private method on instance
	static readonly MethodInfo m_Method = AccessTools.Method(typeof(Test1), "Method");
	static readonly FastInvokeHandler methodDelegate = MethodInvoker.GetHandler(m_Method);
	//
	[HarmonyPrefix]
	[HarmonyPriority(-10)]
	static void CallingPrivateMethodOnInstance(Test1 __instance)
	{
		var s = methodDelegate(__instance);
		Console.WriteLine($"- CallingPrivateMethodOnInstance {s}");
	}

	// calling base method on instance
	[HarmonyReversePatch]
	[HarmonyPatch(typeof(BaseTest), "Method")]
	[MethodImpl(MethodImplOptions.NoInlining)]
	static string BaseMethodDummy(Test1 instance) { return null; }
	//
	[HarmonyPrefix]
	[HarmonyPriority(-11)]
	static void CallingBaseMethodOnInstance(Test1 __instance)
	{
		var s = BaseMethodDummy(__instance);
		Console.WriteLine($"- CallingBaseMethodOnInstance {s}");
	}
}

// patching overloads
[HarmonyPatch]
public class MorePatches1
{
	// patching overload
	[HarmonyPatch(typeof(Test2), nameof(Test2.Bar), new[] { typeof(string) })]
	static void PrefixForOverload()
	{
		Console.WriteLine("- PrefixForOverload");
	}

	// patching overload with ref argument
	[HarmonyPatch(typeof(Test2), nameof(Test2.Bar), new[] { typeof(string), typeof(int) }, new[] { ArgumentType.Normal, ArgumentType.Ref })]
	static void PrefixForOverloadWithRefArg()
	{
		Console.WriteLine("- PrefixForOverloadWithRefArg");
	}
}

// patching overload with private declaring type or argument type
[HarmonyPatch]
public class MorePatches2
{
	static MethodBase TargetMethod()
	{
		var privateType = AccessTools.Inner(typeof(Test2), "Private");
		return AccessTools.Method(typeof(Test2), "Bar", new[] { privateType });
	}
	
	[HarmonyPrefix]
	static void PrefixForOverloadWithPrivateArg()
	{
		Console.WriteLine("- PrefixForOverloadWithPrivateArg");
	}
}

// methods

class BaseTest
{
	internal virtual string Method()
	{
		return "Method[Base]()";
	}
}

class Test1 : BaseTest
{
	private string field1 = "field1";
	private Private priv = new Private("private2");

	private class Private
	{
		string val;
		public Private(string val) { this.val = val; }
		public override string ToString() { return val; }
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	void Foo(string str, Private p)
	{
		Console.WriteLine($"Foo({str}, {p})");
	}

	internal override string Method()
	{
		return "Method[Overwritten]()";
	}

	public static void Run()
	{
		Console.WriteLine("# Test1");
		var test = new Test1();
		test.Foo("orginal", new Private("private1"));
	}
}

class Test2
{
	private class Private
	{
		string val;
		public Private(string val) { this.val = val; }
		public override string ToString() { return val; }
	}

	public static void Bar()
	{
		Console.WriteLine("Bar()");
	}

	public static void Bar(string s)
	{
		Console.WriteLine($"Bar({s})");
	}

	public static void Bar(string s, ref int n)
	{
		Console.WriteLine($"Bar({s}, {n})");
	}

	static void Bar(Private p)
	{
		Console.WriteLine($"Bar({p})");
	}

	public static void Run()
	{
		Console.WriteLine();
		Console.WriteLine("# Test2");
		Test2.Bar("something");
		var n = 123;
		Test2.Bar("something", ref n);
		Bar(new Private("private3"));
	}
}
```
