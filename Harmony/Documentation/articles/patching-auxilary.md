# Patching

## Auxilary patch methods

### Prepare

Before the patching, Harmony gives you a chance to prepare your state. For this, Harmony searches for a method called

```csharp
static bool Prepare()
static bool Prepare(Harmony instance)
// or
[HarmonyPrepare]
static bool MyInitializer(...)
```
	
That method, if it exists, is expected to return a boolean that controls if patching will happen. You can optionally receive the harmony instance if you want to run other Harmony methods inside your code.

### TargetMethod

Most of the times, you will use a combination of `HarmonyPatch()` annotations on the class to define the method you want to patch. Sometimes though, it is necessary to calculate the method with code. For this, Harmony searches for a method called

```csharp
static MethodBase TargetMethod()
static MethodBase TargetMethod(Harmony instance)
// or
[HarmonyTargetMethod]
static MethodBase CalculateMethod()
static MethodBase CalculateMethod(Harmony instance)
```
	
That method, if it exists, is expected to return a `MethodBase` of the method to be patched. You can optionally receive the harmony instance if you want to run other Harmony methods inside your code.

### TargetMethods

If you want to patch multiple methods with the same patch, you can use `TargetMethods`. It has the same behaviour as `TargetMethod` except that it returns an enumeration of `MethodBase` instead of a single `MethodBase`:

```csharp
static IEnumerable<MethodBase> TargetMethods()
static IEnumerable<MethodBase> TargetMethods(Harmony instance)
// or
[HarmonyTargetMethod]
static IEnumerable<MethodBase> CalculateMethods()
static IEnumerable<MethodBase> CalculateMethods(Harmony instance)
```

A typical implementation would `yield` the results like this:

```csharp
static IEnumerable<MethodBase> TargetMethods()
{
	yield return AccessTools.Method(typeof(Foo), "Method1");
	yield return AccessTools.Method(typeof(Bar), "Method2");
	// you could also iterate using reflections over many methods
}
```

### Cleanup

After patching, Harmony gives you a chance to clean up your state. For this, Harmony searches for a method called

```csharp
static void Cleanup()
static void Cleanup(Harmony instance)
// or
[HarmonyCleanup]
static void MyCleanup(...)
```
	
You can optionally receive the harmony instance if you want to run other Harmony methods inside your code.