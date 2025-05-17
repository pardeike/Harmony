# ABOUT

A draft of how to patch inner methods with so called infixes

# CONCEPT

Pseudo code to demonstrate how infixes logically work:

```cs
var test = new Test();

Console.WriteLine($"--> OriginalMethod={test.OriginalMethod(0, "foobartest")}");
Console.WriteLine("");
Console.WriteLine($"--> OriginalMethod={test.OriginalMethod(123, "foobartest")}");
Console.WriteLine("");
	
// patching would happen here

Console.WriteLine($"--> OriginalMethod_Patched_By_Harmony={test.OriginalMethod_Patched_By_Harmony(0, "foobartest")}");
Console.WriteLine("");
Console.WriteLine($"--> OriginalMethod_Patched_By_Harmony={test.OriginalMethod_Patched_By_Harmony(123, "foobartest")}");
Console.WriteLine("");

// patch code section

[HarmonyPatch(typeof(Test), nameof(Test.OriginalMethod))]
public static class Patches
{
	// infixes have the following possible attributes:
	// - HarmonyInfixPatch(Type type, string methodName) ... and all the other argument variations on HarmonyPatch()
	// - HarmonyInnerPrefix
	// - HarmonyInnerPostfix
	//
	// the goal here is to keep the methodinfo from HarmonyPath for the outer method
	// and the methodinfo (and optional index) from HarmonyInfixPatch to find the patch position
	// inside the outer method
	//
	// in order to sort infixes, we need to identify them and that is done by a tuple of (MethodInfo, int)
	// which uniquely identifies an infix by method and index (-1 means all occurances).
	// it is also planned to get (MethodInfo, int) by calling a defined delegate method that takes the original methodinfo
	// and its codeinstructions just like TargetMethod does for normal patches
	//
	// injected arguments are basically the same as in ordinary patches but have some changes:
	// - anything referring to the original has an EXTRA prefix of 'o_' (so __instance becomes o___instance)
	// - anything referring to the patched method inside the original will have the same name as normally
	//   - so to rewrite an argument 'foo' in the called method one would simply use 'string foo'
	//   - if the same argument is from the outer original method, it would be 'string o_foo'
	//   - local variables are injected by index '__var_N' (stable because original var index is preserved because it append only)
	//   - same for instance, specials and result
	
	[HarmonyInfixPatch(typeof(Helper), nameof(Helper.Decide))]
	public static bool InnerPrefix(int o_idx, ref string str, ref bool __result, ref int __var_counter)
	{
		if (o_idx == 0)
		{
			__result = false;
			return false;
		}

		str = str + ".";
		__var_counter = __var_counter + 1;
		return true;
	}

	[HarmonyPriority(Priority.High)]
	[HarmonyInnerPostfix]
	[HarmonyInfixPatch(typeof(Helper), nameof(Helper.Decide))]
	public static void LogSomething(Test o___instance, Helper __instance, bool __result, int __var_counter, string __var_0)
	{
		Console.WriteLine($"### {o___instance}/{__instance}, Decide = {__result} count={__var_counter} secret={__var_0}");
	}
}

// original code section

public class Helper
{
	public bool Decide(string str)
	{
		Console.WriteLine($"Decide {str}");
		return str.Contains("test");
	}

	public override string ToString() => this.GetHashCode().ToString();
}

public class Test
{
	// before patching (original)
	public string OriginalMethod(int idx, string input)
	{
		var secret = "secret";
		var helper = new Helper();
		while (input.Length > 0)
		{
			if (!helper.Decide(input)) // we want to patch Decide and run code before and after
				break;
			input = input.Substring(1);
		}
		return input + $" {secret}";
	}

	// still original but restructured for better understanding
	public string OriginalMethod_Destructured_Like_IL(int idx, string input)
	{
		var secret = "secret";
		var helper = new Helper();
		while (input.Length > 0)
		{
			// Original method contains this base form of the call
			// somehow (instead of 'res' the stack is used). It's how IL works but
			// C# makes it hard to see. So this row is what we patch with an inner prefix/postfix
			var res = helper.Decide(input);

			if (!res)
				break;
			input = input.Substring(1);
		}
		return input + $" {secret}";
	}

	// after patching
	public string OriginalMethod_Patched_By_Harmony(int idx, string input)
	{
		var secret = "secret"; // first defined variable (var index 0)
		int counter = 0; // from injection of __var_counter
		
		var helper = new Helper();
		while (input.Length > 0)
		{
			// just like with normal prefixes/postfixes, Harmony creates some local state variables
			// then each inner prefix is called before the (inner) original and then the original is (conditionally)
			// called and finally each inner postfix is called
			//
			bool __result = false; // result of infixed method
			bool __runOriginal = false; // should we skip infixed method?
			//
			// all injected arguments of the infixed method and the patches are stored in local vars
			int var1 = idx;
			string var2 = input;

			// { start of loop thru all inner prefixes
			__runOriginal = Patches.InnerPrefix(var1, ref var2, ref __result, ref counter);
			// } end of loop

			if (__runOriginal)
			{
				// NOTE: this is still the original code unchanged
				__result = helper.Decide(var2);
				// end original call code
			}

			// { start of loop thru all inner postfixes
			Patches.LogSomething(this, helper, __result, counter, secret);
			// } end of loop

			// write our local vars back
			idx = var1;
			input = var2;
			var result = __result;
			
			 // back to normal

			if (!result)
				break;
			input = input.Substring(1);
		}
		return input + $" {secret}";
	}

	public override string ToString() => this.GetHashCode().ToString();
}

// fake API for testing

public class HarmonyInfixPatchAttribute : HarmonyAttribute
{
	public Type type;
	public string method;
	public int index;

	public HarmonyInfixPatchAttribute(Type type, string method, int index = -1)
	{
		this.type = type;
		this.method = method;
		this.index = index;
	}
}

public class HarmonyInnerPostfix : HarmonyAttribute
{
	public Type type;
	public string method;
	public int index;

	public HarmonyInnerPostfix()
	{
	}

	public HarmonyInnerPostfix(Type type, string method, int index = -1)
	{
		this.type = type;
		this.method = method;
		this.index = index;
	}
}
```

This prints to console:

```
Decide foobartest
Decide oobartest
Decide obartest
Decide bartest
Decide artest
Decide rtest
Decide test
Decide est
--> OriginalMethod=est secret

Decide foobartest
Decide oobartest
Decide obartest
Decide bartest
Decide artest
Decide rtest
Decide test
Decide est
--> OriginalMethod=est secret

### 58870012/60068066, Decide = False count=0 secret=secret
--> OriginalMethod_Patched_By_Harmony=foobartest secret

Decide foobartest.
### 58870012/3741682, Decide = True count=1 secret=secret
Decide oobartest..
### 58870012/3741682, Decide = True count=2 secret=secret
Decide obartest...
### 58870012/3741682, Decide = True count=3 secret=secret
Decide bartest....
### 58870012/3741682, Decide = True count=4 secret=secret
Decide artest.....
### 58870012/3741682, Decide = True count=5 secret=secret
Decide rtest......
### 58870012/3741682, Decide = True count=6 secret=secret
Decide test.......
### 58870012/3741682, Decide = True count=7 secret=secret
Decide est........
### 58870012/3741682, Decide = False count=8 secret=secret
--> OriginalMethod_Patched_By_Harmony=est........ secret
```

Some notes in no particular order:
- For convenience, it would seem nice to have a params int[]? indices = null for InfixPatch / InnerPrefix / InnerPostfix, to be able to specify multiple locations to patch - at least if that's something intended to be supported. I'm not sure how that should influence something like __var_counter, but the easiest would certainly be to just treat each location as a new thing, although there might be an interesting case for more shared variables?
- I like the name InfixPatch - but at the same time having InfixPatch and InnerPrefix / InnerPostfix seems inconsistent. I would favor changing it to InnerPatch to keep it consistent and not have the mouthfuls of InfixPrefix and InfixPostfix.
- Would the extra _ in something like o___instance be necessary? o_originalArgument and o__instance seem nicer, imo.
- I'd also consider to make the o more "obvious", maybe extend it to "outer"? It would be a bit longer, but would make it nicer to read imo and make it more obvious what it refers to.

# IMPLEMENTATION HINTS

The classes MethodCreator, MethodCreatorConfig and MethodCreatorTools are the core of Harmony. They generate a replacement method dynamically that will replace the original method when patching it. Ignoring transpilers for now, the code will insert all prefixes and postfixes at the beginning and end of the original methods IL. It does so by analyzing each patches parameters, the original methods parameters and what glue IL to create to insert a call to each patch. It also deals with other injections like instance or state (see InjectedParameter class).

The goal with infixes is that they are not inserted at the same locations as pre/postfixes but instead around method calls inside the IL of the original method. This works a lot like pre/postfixes but as you can see in the pseudo code, increases the number of injectable things. An infix can refer to the originals parameters or to the wrapped calls parameters. Therefore the magic names need to be adapted but a lot of logic in MethodCreatorTools.EmitCallParameter() can be shared.

This means that MethodCreator needs to somehow transfer all necessary information to each infixes Apply() method. Maybe via the existing context or an extension to it. Alternatively, the abstact Infix.Apply() is not the best way to structure this.

Overall MethodCreator.CreateReplacement() should orchestrate the logic or delegate it to other methods, just like it does with AddPrefixes() but for infixes.