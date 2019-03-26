# Introduction

*Harmony - a library for patching, replacing and decorating .NET methods during runtime.*

## Prerequisites

Harmony works with all languages that compile to [CIL](https://wikipedia.org/wiki/Common_Intermediate_Language), Microsofts intermediate byte code language. This is foremost the [.NET Framework](https://wikipedia.org/wiki/Portal:.NET_Framework) and of course [Mono](https://wikipedia.org/wiki/Mono_(software)) - used by the game engine Unity.

The exception is [.NET Core](https://wikipedia.org/wiki/.NET_Core), which does not provide the functionality to fully create methods on the fly at runtime. Chances are that .NET Core v3 might include everything to support Harmony [[See this Issue](https://github.com/dotnet/corefx/issues/29715)]

### Bootstrapping and Injection

Harmony does not provide you with a way to run your own code within an application that is not designed to execute foreign code. You need a way to inject at least the few lines that start the Harmony patching and this is usually done with a loader. Here are some common examples of loaders (incomplete):

- [Unity Doorstep](https://github.com/NeighTools/UnityDoorstop)
- [BepInEx](https://github.com/BepInEx/BepInEx)
- [UnityAssemblyInjector](https://github.com/avail/UnityAssemblyInjector)
- [MonoJunkie](https://github.com/wledfor2/MonoJunkie)
- [MInjector](https://github.com/EquiFox/MInjector)
- and more...

You need to find your own injection method or choose a game that supports user dll loading (usually called Mods) like for example RimWorld ([Wiki](https://rimworldwiki.com/wiki/Modding_Tutorials/)).

### Dependencies

It has no other dependencies and will most likely work in other environments too. Harmony was tested on PC, Mac and Linux and support 32- and 64-bit. For a typical Unity target, simply set your project to .Net 3.5 or Mono 2.x and include the Harmony dll.

## Altering functionality (Patching)

If you want to change how an exising C# application like a game works and you don't have the source code for that application, you have basically two options to do that:

1) Alter dll files on disk  
2) Re-point method implementations (hooking)

Depending on the needs and situation, altering dll files is not always a desirable solution. For example

- it has legal implications
- it might be blocked by an anti-cheat system
- it does not coordinate nicely with multiple concurrent changes
- it has to be done before and outside the original application

Harmony focuces only on runtime changes that don't affect files on disk:

- less conflicts with multiple mods  
- supports existing mod loaders  
- changes can be made dynamically/conditionally  
- the patch order can be flexible  
- other mods can be patched too  
- less legal issues  

## How Harmony works

Where other patch libraries simply allow you to replace the original method, Harmony goes one step further and gives you:

* A way to keep the original method intact
* Execute your code before and/or after the original method
* Modify the original with IL code processors
* Multiple Harmony patches co-exist and don't conflict with each other

![](https://raw.githubusercontent.com/pardeike/Harmony/master/Harmony/Documentation/images/patch-logic.svg?sanitize=true)

## Limits of runtime patching

![note] Harmony can't do everything. Make sure you understand the following:

- With Harmony, you only manipulate **methods**. This includes constructors and getters/setters.

- You can only work with methods that have an actual IL code body, which means that they appear in a dissassembler like [dnSpy](https://github.com/0xd4d/dnSpy).

- Methods that are too small might get [inlined](https://wikipedia.org/wiki/Inline_expansion) and your patches will not run.

- You cannot add fields to classes and you cannot extend enums (they get compiled into ints).

## Hello World Example

Original game code:

```cs
public class SomeGameClass
{
	private bool isRunning;
	private int counter;

	private int DoSomething()
	{
		if (isRunning)
		{
			counter++;
			return counter * 10;
		}
	}
}
```

Patching with Harmony annotations:

```cs
// your code, most likely in your own dll

using SomeGame;
using Harmony;

public class MyPatcher
{
	// make sure DoPatching() is called at start either by
	// the mod loader or by your injector
	
	public static void DoPatching()
	{
		var harmony = HarmonyInstance.Create("com.example.patch");
		harmony.PatchAll();
	}
}

[HarmonyPatch(typeof(SomeGameClass))]
[HarmonyPatch("DoSomething")]
class Patch01
{
	static FieldRef<SomeGameClass,bool> isRunningRef = 
		AccessTools.FieldRefAccess<SomeGameClass, bool>("isRunning");

	static bool Prefix(SomeGameClass __instance, ref int ___counter)
	{
		isRunningRef(__instance) = true;
		if (___counter > 100)
			return false;
		___counter = 0;
		return true;
	}

	static void Postfix(ref int __result)
	{
		__result *= 2;
	}
}
```

Alternatively, manual patching with reflection:

```cs
// your code, most likely in your own dll

using SomeGame;
using Harmony;

public class MyPatcher
{
	// make sure DoPatching() is called at start either by
	// the mod loader or by your injector
	
	public static void DoPatching()
	{
		var harmony = HarmonyInstance.Create("com.example.patch");
		
		var mOriginal = typeof(SomeGameClass).GetMethod("DoSomething", BindingFlags.Instance | BindingFlags.NonPublic);
		var mPrefix = typeof(MyPatcher).GetMethod("MyPrefix", BindingFlags.Static | BindingFlags.Public);
		var mPostfix = typeof(MyPatcher).GetMethod("MyPostfix", BindingFlags.Static | BindingFlags.Public);
		// add null checks here
		
		harmony.Patch(mOriginal, new HarmonyMethod(mPrefix), new HarmonyMethod(mPostfix));
	}
	
	public static void MyPrefix()
	{
		// ...
	}
	
	public static void MyPostfix()
	{
		// ...
	}
}
```

[note]: https://raw.githubusercontent.com/pardeike/Harmony/master/Harmony/Documentation/images/note.png