# Introduction to Harmony  
A library for patching, replacing and decorating .NET methods during runtime.
---

If you develop in C# and your code is loaded as a module/plugin into a host application, you can use Harmony to alter the functionality of all the available assemblies of that application. Where other patch libraries simply allow you to replace the original method, Harmony goes one step further and gives you:

* A way to keep the original method intact
* Execute your code before and/or after the original method
* Modify the original with IL code processors
* Multiple Harmony patches co-exist and don't conflict with each other

### Prerequisites

Harmony is designed to work with a minimum requirement of .NET 2.0 and is compatible with Mono which makes it a great way to develop extensions for [Unity](https://unity3d.com) games. It has no other dependencies and will most likely work in other environments too. Harmony was tested on PC, Mac and Linux and support 32- and 64-bit. For a typical Unity target, simply set your project to .Net 3.5 or Mono 2.x and include the Harmony dll.

Support for .NET Core in all its versions is upcoming and currently being tested. Stay tuned!

Add the Harmony dll to your project and merge it into your final dll with a tool like ILMerge. Alternatively, let your IDE copy the dll to your assembly folder and make sure it is loaded early (for that, the dll is already conveniently named 0Harmony.dll).

### Quick example

_The following is a specific example. Harmony works with any kind of application and code._

Here is a very short example on how to patch the method `WindowStack.Add(Window)` in a mod for the game [RimWorld](https://rimworldgame.com) so that it logs the window object to the games debug window:

```csharp
using System;
using Verse;
using Harmony;
using System.Reflection;

namespace HarmonyTest
{
	[StaticConstructorOnStartup]
	class Main
	{
		static Main()
		{
			var harmony = HarmonyInstance.Create("com.github.harmony.rimworld.mod.example");
			harmony.PatchAll(Assembly.GetExecutingAssembly());
		}
	}

	[HarmonyPatch(typeof(WindowStack))]
	[HarmonyPatch("Add")]
	[HarmonyPatch(new Type[] { typeof(Window) })]
	class Patch
	{
		static void Prefix(Window window)
		{
			Log.Warning("Window: " + window);
		}
	}
}
```

The important parts that are not RimWorld specific are the two lines inside the Main() method and the Patch class. The Prefix is always execute before Window.Add() everywhere that method is called and logs the window instance to the RimWorld debug window.
