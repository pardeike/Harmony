# Introduction

Harmony changes what .NET methods do while an application is running. Add code before or after a method, change its instructions, or patch a selected operation inside it. The original files stay unchanged.

<div class="guide-links">
  <a href="basics.md"><strong>Install and apply patches</strong><span>Set up the library and register your first patch class.</span></a>
  <a href="patching.md"><strong>Choose a patch type</strong><span>Match the change you want to the API that fits.</span></a>
</div>

## Prerequisites

Harmony patches [CIL](https://wikipedia.org/wiki/Common_Intermediate_Language), the intermediate language used by .NET and [Mono](<https://wikipedia.org/wiki/Mono_(software)>). Your source language does not have to be C#.

The runtime must allow Harmony to generate and execute replacement methods. In Unity, check the scripting backend and target platform. IL2CPP uses ahead-of-time compilation and does not support `System.Reflection.Emit`; the runtime patching described here requires a compatible managed runtime. See [Unity's scripting restrictions](https://docs.unity3d.com/6000.0/Documentation/Manual/scripting-restrictions.html).

### Bootstrapping and Injection

Harmony does not load your code into an application. You need mod support or a loader to run the few lines that apply your patches. Some loaders include:

- [Unity Doorstop](https://github.com/NeighTools/UnityDoorstop)
- [BepInEx](https://github.com/BepInEx/BepInEx)
- [UnityAssemblyInjector](https://github.com/avail/UnityAssemblyInjector)
- [MonoJunkie](https://github.com/wledfor2/MonoJunkie)
- [MInjector](https://github.com/EquiFox/MInjector)
- [net-core-injector](https://github.com/StackOverflowExcept1on/net-core-injector)
- and more...

Games with built-in mod support, such as [RimWorld](https://rimworldwiki.com/wiki/Modding_Tutorials/), can load your assembly themselves.

### Dependencies

The standard `Lib.Harmony` package merges its dependencies into one DLL. `Lib.Harmony.Thin` keeps them separate. Choose a target framework supported by your application; see [Getting Started](../index.md#getting-started).

## Altering functionality (Patching)

There are two common ways to change an application without its source code:

1. Alter DLL files on disk
2. Re-point method implementations (hooking)

Editing DLL files has drawbacks:

- it might be blocked by an anti-cheat system
- separate edits can overwrite each other
- it has to be done before and outside the original application

Harmony uses a variation of hooking and focuses only on runtime changes that don't affect files on disk:

- patches from multiple mods can share a method
- supports existing mod loaders
- changes can be made dynamically/conditionally
- the patch order can be flexible
- other mods can be patched too

## How Harmony works

Harmony lets you:

- Keep the original method's code
- Execute your code before and/or after it
- Modify its IL instructions
- Combine patches from multiple authors on the same method

See [how the patch types fit together](patching.md#runtime-flow) before choosing one.

## Limits of runtime patching

![note] Harmony can't do everything. Make sure you understand the following:

- Harmony rewrites **method bodies**, including constructors and getters/setters.

- Most patches need an IL body. [Native methods](patching-edgecases.md#native-external-methods) need a replacement implementation.

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

[note]: ../images/note.png
