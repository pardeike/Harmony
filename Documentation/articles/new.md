# What's new in v3

<div id="whats-new"></div>

Harmony 3 is in preview development. These guides describe the new APIs on the `v3` branch; the [2.x documentation](https://harmony.pardeike.net/v2/) remains the reference for the current stable version.

## Patch inside a method with Infix

Use prefixes, postfixes, and finalizers around selected operations in an outer method. Select calls, property access, field reads and writes, construction, or literal loads. Other callers are unaffected by that Infix.

[!include[Infix operation scope](../includes/infix-scope.md)]

Start with the [working example](patching-infix.md#a-working-example), then explore [authoring recipes](patching-infix-authoring.md) and [limits](patching-infix-limits.md).

## Work with generated bodies

`InfixOuterBody.Auto` resolves supported iterator and async methods to their generated execution body. `ArgumentMode.Captured` accesses live captured variables; `ArgumentMode.Persistent` keeps patch-owned values across suspensions within one execution.

Read about [generated bodies](patching-infix.md#generated-bodies-and-captured-variables) and [persistent state](patching-infix.md#keep-patch-owned-values-across-await-and-yield), including the supported compiler protocols and cleanup rules.

## Earlier: Harmony 2

The following is the original overview of changes from 1.2.0.1 to Harmony 2, retained for reference.

#### New

CI/CD with GitHub Actions
Switched to `MonoMod.Core` for shared low level patching with MonoMod project
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
