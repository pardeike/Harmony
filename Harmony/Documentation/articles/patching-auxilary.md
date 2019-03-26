# Patching

## Auxilary patch methods

### Prepare

Before the patching, Harmony gives you a chance to prepare your state. For this, Harmony searches for a method called

```csharp
static bool Prepare()
static bool Prepare(HarmonyInstance instance)
// or
[HarmonyPrepare]
static bool MyInitializer(...)
```
	
That method, if it exists, is expected to return a boolean that controls if patching will happen. You can optionally receive the harmony instance if you want to run other Harmony methods inside your code.

### TargetMethod

Most of the times, you will use a combination of `HarmonyPatch()` annotations on the class to define the method you want to patch. Sometimes though, it is necessary to calculate the method with code. For this, Harmony searches for a method called

```csharp
static MethodBase TargetMethod()
static MethodBase TargetMethod(HarmonyInstance instance)
// or
[HarmonyTargetMethod]
// NOTE: not passing harmony instance with attributes is broken in 1.2.0.1
static MethodBase CalculateMethod(HarmonyInstance instance)
```
	
That method, if it exists, is expected to return a `MethodInfo` of the method to be patched. You can optionally receive the harmony instance if you want to run other Harmony methods inside your code.

### TargetMethods

+++

### Cleanup

+++