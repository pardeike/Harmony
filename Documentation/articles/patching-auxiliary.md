# Prepare, target, and clean up

<div id="auxiliary-patch-methods"></div>

<div id="patching"></div>

Annotation patch classes can define helpers to prepare and clean up patching, or choose target methods in code.

Helpers accept these optional arguments, matched by type rather than name:

- `MethodBase original` - the current original being patched
- `Harmony harmony` - the current Harmony instance
- `Exception ex` - only valid in `Cleanup` and receives a possible exception

Here is a simple example that patches a method inside a private type:

[!code-csharp[example](../examples/basics.cs?name=target_method)]

## Prepare

Harmony looks for a preparation method with one of these forms:

```csharp
static void Prepare(...)
static void Prepare(MethodBase original, ...)
static bool Prepare(MethodBase original, ...)
// or
[HarmonyPrepare]
static void MyInitializer(...)
static void MyInitializer(MethodBase original, ...)
static bool MyInitializer(MethodBase original, ...)
```

`Prepare` runs first with `original = null` for the whole class, then for each target method. Returning `false` skips the class or that target, respectively.

## TargetMethod

To choose the target in code instead of annotations, define:

```csharp
static MethodBase TargetMethod(...)
// or
[HarmonyTargetMethod]
static MethodBase CalculateMethod(...)
```

Return the target's `MethodBase`, never `null`. Use `Prepare()` to skip patching conditionally.

## TargetMethods

To apply the same patches to several targets, return an enumeration of `MethodBase`:

```csharp
static IEnumerable<MethodBase> TargetMethods(...)
// or
[HarmonyTargetMethods]
static IEnumerable<MethodBase> CalculateMethods(...)
```

A typical implementation would `yield` the results like this:

[!code-csharp[example](../examples/patching-auxiliary.cs?name=yield)]

Do not use an empty enumeration to skip patching; use `Prepare()` instead.

## Cleanup

For cleanup after patching, define:

```csharp
static void Cleanup(...)
static void Cleanup(MethodBase original, ...)
static Exception Cleanup(MethodBase original, ...)
// or
[HarmonyCleanup]
static void MyCleanup(...)
static void MyCleanup(MethodBase original, ...)
static Exception MyCleanup(MethodBase original, ...)
```

`Cleanup` runs after each target, then once for the whole class with `original = null`.

Inject `Exception` to inspect a patching failure; a `HarmonyException` may provide more details. Return an exception to replace it, or `null` to suppress it.
