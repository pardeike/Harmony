# What's New

Harmony 2.0 has come a long way since the last release 1.2.0.1. Here are all the changes:

#### New

CI/CD on Azure, Travis and AppVeyor  
Switched to `MonoMod.Common` for shared low level patching with MonoMod project  
Works with more .NET versions  
Inline prevention for Mono  
4th patch type: `Finalizer` - for handling and manipulating exceptions  
Reverse Patching (original onto one of your stub methods)  
Convenience extension methods for `CodeInstruction`  
Selective debug log with `[HarmonyDebug]` annotation - works even with future changes of the method  
`Prepare`/`Cleanup` will be called even with exceptions during patching  
Cleanup can now receive and return the current Exception during patching  
Better exception reporting with `HarmonyException`  
Automatic documentation generated to `https://harmony.pardeike.net`  
AccessTools has methods for declared members  
`FastAccess` now deals with generics  
`Manipulator` transpiler helper  
Get IL code from a method  
Support for IL InlineSignature (patching methods with CALLI)

#### Fixed

Priority field spelling  
`Traverse` can handle static members  
Methods returning struct types are now patchable  
Main API is now properly divided into static/instance methods  
`HarmonyMethod` and other high level API throws on null input  
Patch sorting  
DeepCopy works with nullable types  
Patch annotations API cleaned up  
`FieldRef` covers more cases and is simplified  
`__result` assignability checks  
Handling `__state` without Prefix  
Debug log writes out full type names  
Documentation now uses compiled code snippets for correctness  
`Traverse` works with inherited fields, properties and methods

#### Changes

Removed Self-patching  
Renamed `Add()` extension on `IEnumerable<T>` and `T[]` to `AddItem()` to avoid conflicts  
`HarmonyInstance` is now called `Harmony` and `Harmony` namespace is now called `HarmonyLib`
