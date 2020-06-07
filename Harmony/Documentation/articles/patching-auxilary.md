# Patching

## Auxilary patch methods

If you use manual patching, you are in full control of your state and the execution and handling of extra resources and logic. For annotation patching, Harmony offers you a number of methods you can implement on a patch class that allow you to execute code before and after patching on that class as well as methods that combine annotations with manually defining which methods that should be patched.

Each of those methods can take up to three optional arguments that are injected by type so you can call them anything you like:

- `MethodBase original` - the current original being patched
- `Harmony harmony` - the current Harmony instance
- `Exception ex` - only valid in `Cleanup` and receives a possible exception

### Prepare

Before the patching, Harmony gives you a chance to prepare your state. For this, Harmony searches for a method called

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

By returning `false` it can skip the patching in this class. It is recommended to inject `original` into Prepare because **Harmony calls it at least twice**: once before patching in the class starts (original is `null`) and once for each method being patched (original indicates which method is currently patched).

### TargetMethod

Most of the times, you will use a combination of `HarmonyPatch()` annotations on the class to define the method you want to patch. Sometimes though, it is necessary to calculate the method with code. For this, Harmony searches for a method called

```csharp
static MethodBase TargetMethod(...)
// or
[HarmonyTargetMethod]
static MethodBase CalculateMethod(...)
```

That method, if it exists, is expected to return a `MethodBase` of the method to be patched.

### TargetMethods

If you want to patch multiple methods with the same patch, you can use `TargetMethods`. It has the same behaviour as `TargetMethod` except that it returns an enumeration of `MethodBase` instead of a single `MethodBase`:

```csharp
static IEnumerable<MethodBase> TargetMethods(...)
// or
[HarmonyTargetMethod]
static IEnumerable<MethodBase> CalculateMethods(...)
```

A typical implementation would `yield` the results like this:

[!code-csharp[example](../examples/patching-auxilary.cs?name=yield)]

### Cleanup

After patching, Harmony gives you a chance to clean up your state. For this, Harmony searches for a method called

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

Similar to `Prepare()` this method is called with `original` set to the method that just has been patched and then finally one more time before ending the overall patching (original will be `null`).

Additionally, you can intercept exceptions that are thrown while patching. Use the injection of `exception` to learn what happened and check if you can cast it to `HarmonyException` to get more information. Finally, you can return `Exception` to replace the exception or `null` to suppress it.