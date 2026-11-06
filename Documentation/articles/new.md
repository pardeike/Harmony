# What's new in v3

<div id="whats-new"></div>

These guides describe Harmony 3 and its new APIs on the `v3` branch; the [2.x documentation](https://harmony.pardeike.net/v2/) covers Harmony 2.

## Patch inside a method with Infix

Use prefixes, postfixes, and finalizers around selected operations in an outer method. Select calls, property access, field reads and writes, construction, or literal loads. Other callers are unaffected by that Infix.

[!include[Infix operation scope](../includes/infix-scope.md)]

Start with the [working example](patching-infix.md#a-working-example), then explore [recipes and instruction edits](patching-infix-authoring.md) and [limits](patching-infix-limits.md).

## Work with generated bodies

`InfixOuterBody.Auto` resolves supported iterator and async methods to their generated execution body. `ArgumentMode.Captured` accesses live captured variables; `ArgumentMode.Persistent` keeps patch-owned values across suspensions within one execution.

Read about [generated bodies](patching-infix.md#generated-bodies-and-captured-variables) and [persistent state](patching-infix.md#keep-patch-owned-values-across-await-and-yield), including the supported compiler protocols and cleanup rules.

## .NET 11 support

Harmony 3 adds .NET 11 support, verified with RC3. [.NET 11 runtime async](patching-edgecases.md#runtime-async-in-net-11) remains unsupported and throws `NotSupportedException` when patched or reverse-patched. Ordinary `Task`-returning methods and compiler-generated async state machines remain supported, but patches on them do not reach callers compiled with runtime-async, see the [edge cases](patching-edgecases.md#runtime-async-in-net-11).

## Host Harmony in a runtime

A runtime that can replace method bodies can install Harmony's patches itself. `MethodBodyPatching` registers an eligibility check, a baseline reader, and an installer. An accepted generic definition gets one body for all instantiations. Declined targets keep the detour. Read [how to host Harmony](hosting.md).

The host contract includes these rules:

- Register all parts before Harmony pins a host-owned target. Late non-null registration throws. A failed host installation retains its selection without publishing candidate patch records.
- Installers must not call patch, unpatch, reverse-patch, or rebuild operations. Harmony rejects re-entry into the same target or generic family.
- Definition-wide bindings require a guaranteed assignment from the open source. Closed bindings and passthrough results validate closed types. Foreign generic parameters in member operands fail before installation.
- Directly constructed callbacks are rejected for targets installed through a definition. Register a generic callback definition, or use a factory to preserve fixed arguments.
- Within a generic family, equal priority and index put the definition's patch first, whichever member started the rebuild.
- Generic family bridges reject function-pointer signatures. Reverse patches into host-owned generic targets are unsupported. Constructors installed through a generic family return null from `Patch()`.
- Rebuilding a definition uses its members' records when it has no record of its own. Successful ordinary installation supersedes an earlier reverse recipe.
- The importer accepts `KeyValuePair<MemberInfo, bool>` with `true` for self-instantiated generic members. It must preserve MemberRef/TypeSpec and MethodSpec semantics. Refused identity operands such as `ldtoken` cannot use forwarding stand-ins.
- Generated closed helpers reuse permanent stand-ins by original, helper site, and signature. Refused open generic helpers can use permanent images, but those images cannot reference collectible dependencies.
- Body export is unavailable in Harmony's `net35` and `netstandard2.0` builds. Generic helper generation requires collectible `AssemblyLoadContext` support, so .NET Framework and Mono refuse Infix patches on a generic method definition before installation.

## Runtime detection

`AccessTools.IsNetCoreRuntime` is now true on .NET Core 3.x and on .NET 5 or later. It was false on all of them. `AccessTools.IsNetFrameworkRuntime` is no longer true on .NET Core 3.x.

## Match property access

`CodeMatch.GetsProperty` and `CodeMatch.SetsProperty` match calls to a property's getter or setter. They accept a `PropertyInfo` or a lambda such as `() => default(MyClass).MyProperty`. `SymbolExtensions.GetPropertyInfo` returns the property from such a lambda. See [CodeMatcher](patching-transpiler-matcher.md#match-fields-and-properties).

## Stack validation

Harmony rejects a patched method if the paths that reach an instruction have different stack depths. For runtimes that accept such IL, `Harmony.ValidateStack` and the environment variable `HARMONY_VALIDATE_STACK` turn this check off. See [stack depth](patching-transpiler-codes.md#stack-depth).

## Earlier: Harmony 2

The following is the original overview of changes from 1.2.0.1 to Harmony 2, retained for reference.

#### New

- CI/CD with GitHub Actions
- Switched to `MonoMod.Core` for shared low-level patching with the MonoMod project
- Works with more .NET versions
- Inline prevention for Mono
- 4th patch type: `Finalizer` - for handling and manipulating exceptions
- Reverse Patching (original onto one of your stub methods)
- Convenience extension methods for `CodeInstruction`
- Selective debug log with `[HarmonyDebug]` annotation - works even with future changes of the method
- `Prepare`/`Cleanup` will be called even with exceptions during patching
- Cleanup can now receive and return the current Exception during patching
- Better exception reporting with `HarmonyException`
- Automatic documentation generated to `https://harmony.pardeike.net`
- AccessTools has methods for declared members
- `FastAccess` now deals with generics
- `Manipulator` transpiler helper
- Get IL code from a method
- Support for IL InlineSignature (patching methods with CALLI)

#### Fixed

- Priority field spelling
- `Traverse` can handle static members
- Methods returning struct types are now patchable
- Main API is now properly divided into static/instance methods
- `HarmonyMethod` and other high-level APIs throw on null input
- Patch sorting
- DeepCopy works with nullable types
- Patch annotations API cleaned up
- `FieldRef` covers more cases and is simplified
- `__result` assignability checks
- Handling `__state` without Prefix
- Debug log writes out full type names
- Documentation now uses compiled code snippets for correctness
- `Traverse` works with inherited fields, properties, and methods

#### Changes

- Removed Self-patching
- Renamed `Add()` extension on `IEnumerable<T>` and `T[]` to `AddItem()` to avoid conflicts
- `HarmonyInstance` is now called `Harmony` and `Harmony` namespace is now called `HarmonyLib`
