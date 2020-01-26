# Patching

## Reverse Patch

A reverse patch is a stub method in your own code that "becomes" the original or part of the original method so you can use that yourself. Typical use cases are:

- easily call private methods
- no reflection, no delegate, native performance
- you can cherry-pick part of the original method by using a transpiler
- can give you the unmodified original
- will freeze the implementation to the time you reverse patch

![note] Reverse patching does not replace delegates. If you want to use a foreign method and want it to be patched by later patches, you would simply create a delegate or call it via reflections. That way you always get the latest version. Reverse patches give you a more private version of a foreign original method.

### Defining a reverse patch

Creating a reverse patch is easy. You add the `[HarmonyReversePatch]` attribute to a patch method. As you can see in the example below, you still need to point out the original method in futher annotations.

The method signature must match the original. This includes static/non static but in the example, we use the fact that an instance method gets the instance in an extra argument at position 0.

![note] While it is tempting to define a stub as an instance method, one has to be very careful with that. The IL code that is copied from the original expects `this` to be pointing to the original class type. This obviously won't work if your instance stub is in a class that is not the original.

```csharp
private class OriginalCode
{
	private void Test(int counter, string name)
	{
		// ...
	}
}

[HarmonyPatch]
public class Patch
{
	[HarmonyReversePatch]
	[HarmonyPatch(typeof(OriginalCode), "Test")]
	public static void MyTest(object instance, int counter, ref string name)
	{
		// its a stub so it has no initial content
		throw new NotImplementedException("It's a stub");
	}
}

class Main
{
	void Test()
	{
		// here we call OriginalCode.Test()
		var originalInstance = ...
		Patch.MyTest(originalInstance, 100, "hello");
	}
}
```

### Types of reverse patches

xxx

### Changing the content of the original

The real power of reverse patches comes to play when you use a **reverse patch transpiler**. It will allow you to change the original IL that is copied onto your stub method. As a result, you can extract parts of an foreign original method to a specific stub that you can easily call.

**Example**
Lets say the original method is long and has some part where a checksum is calculated. Instead of copying that source code from a disassembler into a method of your own, you could reverse patch the method that contains it into a method you call `Checksum(...)`.

Doing this requires knowledge of how CIL works and how you extract part of IL so its stack usage (the elements put onto it and the elements left on it). This will define the signature of your stub. So if your checksum part takes a string from the IL stack and leaves an int on it after being run, your signature would be `static int Checksum(string txt)`.

To define a reverse patch transpiler, you simply put a transpiler **into** your stub:

```csharp
private class OriginalClass
{
	private string SpecialCalculation(string original, int n)
	{
		var parts = original.Split('-').Reverse().ToArray();
		var str = string.Join("", parts) + n;
		return str + "Prolog";
	}
}

[HarmonyPatch]
public class Patch
{
	// When reverse patched, StringOperation will contain all the
	// code from the original including the Join() but not the +n
	//
	// Basically
	// var parts = original.Split('-').Reverse().ToArray();
	// return string.Join("", parts)
	//
	[HarmonyReversePatch]
	[HarmonyPatch(typeof(OriginalClass), "SpecialCalculation")]
	public static string StringOperation(string original)
	{
		// This inner transpiler will be applied to the original and
		// the result will replace this method
		//
		// That will allow this method to have a different signature
		// than the original and it must match the transpiled result
		//
		IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			var list = Transpilers.Manipulator(instructions,
				item => item.opcode == OpCodes.Ldarg_1,
				item => item.opcode = OpCodes.Ldarg_0
			).ToList();
			var mJoin = SymbolExtensions.GetMethodInfo(() => string.Join(null, null));
			var idx = list.FindIndex(item => item.opcode == OpCodes.Call && item.operand as MethodInfo == mJoin);
			list.RemoveRange(idx + 1, list.Count - (idx + 1));
			return list.AsEnumerable();
		}

		// make compiler happy
		_ = Transpiler(null);
		return original;
	}
}
```

[note]: https://raw.githubusercontent.com/pardeike/Harmony/master/Harmony/Documentation/images/note.png
