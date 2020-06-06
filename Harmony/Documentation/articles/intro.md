# Introduction

_Harmony - a library for patching, replacing and decorating .NET methods during runtime._

## Prerequisites

Harmony works with all languages that compile to [CIL](https://wikipedia.org/wiki/Common_Intermediate_Language), Microsofts intermediate byte code language. This is foremost the [.NET Framework](https://wikipedia.org/wiki/Portal:.NET_Framework) and of course [Mono](<https://wikipedia.org/wiki/Mono_(software)>) - used by the game engine Unity.

The exception is probably [Unity .NET Standard profile](https://docs.unity3d.com/2019.1/Documentation/Manual/dotnetProfileSupport.html), which does not provide the functionality to fully create methods on the fly at runtime.

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

In general, if you want to change how an exising C# application like a game works and you don't have the source code for that application, you have basically two principles to do that:

1. Alter dll files on disk
2. Re-point method implementations (hooking)

Depending on the needs and situation, altering dll files is not always a desirable solution. For example

- it has legal implications
- it might be blocked by an anti-cheat system
- it does not coordinate nicely with multiple concurrent changes
- it has to be done before and outside the original application

Harmony uses a variation of hooking and focuces only on runtime changes that don't affect files on disk:

- less conflicts with multiple mods
- supports existing mod loaders
- changes can be made dynamically/conditionally
- the patch order can be flexible
- other mods can be patched too
- less legal issues

## How Harmony works

Where other patch libraries simply allow you to replace the original method, Harmony goes one step further and gives you:

- A way to keep the original method intact
- Execute your code before and/or after the original method
- Modify the original with IL code processors
- Multiple Harmony patches co-exist and don't conflict with each other

![](https://raw.githubusercontent.com/pardeike/Harmony/master/Harmony/Documentation/images/patch-logic.svg?sanitize=true)

## Limits of runtime patching

![note] Harmony can't do everything. Make sure you understand the following:

- With Harmony, you only manipulate **methods**. This includes constructors and getters/setters.

- You can only work with methods that have an actual IL code body, which means that they appear in a dissassembler like [dnSpy](https://github.com/0xd4d/dnSpy).

- Methods that are too small might get [inlined](https://wikipedia.org/wiki/Inline_expansion) and your patches will not run.

- You cannot add fields to classes and you cannot extend enums (they get compiled into ints).

- Patching generic methods or methods in generic classes is tricky and might not work as expected.

## Hello World Example

Original game code:

[!code-csharp[example](../examples/intro_somegame.cs?name=example)]

Patching with Harmony annotations:

[!code-csharp[example](../examples/intro_annotations.cs?name=example)]

Alternatively, manual patching with reflection:

[!code-csharp[example](../examples/intro_manual.cs?name=example)]

[note]: https://raw.githubusercontent.com/pardeike/Harmony/master/Harmony/Documentation/images/note.png
