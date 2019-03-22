# Patching

### Patch methods

Inside the class Harmony searches for methods with the specific names `TargetMethod()`, `Prepare()`, `Prefix()`, `Postfix()` or `Transpiler()`. Instead of relying on those names, you can also use the method annoations `[HarmonyTargetMethod]`, `[HarmonyPrepare]`, `[HarmonyPrefix]`, `[HarmonyPostfix]` or `[HarmonyTranspiler]`.

**TargetMethod** (Optional)

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

**Prepare** (Optional)

Before the patching, Harmony gives you a chance to prepare your state. For this, Harmony searches for a method called

```csharp
static bool Prepare()
static bool Prepare(HarmonyInstance instance)
// or
[HarmonyPrepare]
static bool MyInitializer(...)
```
	
That method, if it exists, is expected to return a boolean that controls if patching will happen. You can optionally receive the harmony instance if you want to run other Harmony methods inside your code.

**Prefix** (Optional)

```csharp
static bool Prefix(...)
// or
[HarmonyPrefix]
static bool MyPrefix(...)
```

This method defines the code that is executed before the original method. Execution will be skipped if an earlier prefix indicates it wants to skip the original method. It follows the guidelines defined in [[Patching|Patching]].

**Postfix** (Optional)

```csharp
static void Postfix(...)
// or
[HarmonyPostfix]
static void MyPostfix(...)
```

This method defines the code that is executed after the original method. This is a good place to execute code that always needs execution. It follows the guidelines defined in [[Patching|Patching]].

**Transpiler** (Optional)

```csharp
static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instr, ...)
// or
[HarmonyTranspiler]
static IEnumerable<CodeInstruction> MyTranspiler(IEnumerable<CodeInstruction> instr, ...)
```

This method defines the transpiler that modifies the code of the original method. Use this in the advanced case where you want to modify the original methods IL codes. It follows the guidelines defined in [[Patching|Patching]].

### Patch parameters

Each prefix and postfix can get all the parameters of the original method as well as the instance (if original method is not static) and the return value. In order to patch a method your patches need to follow the following principles when defining them:

* A patch must be a **static** method
* A prefix patch has a return type of **void** or **bool**
* A postfix patch has a return type of **void** or the return signature must match the type of the **first** parameter (passthrough mode)
* Patches can use a parameter named **__instance** to access the instance value if original method is not static
* Patches can use a parameter named **__result** to access the returned value (prefixes get default value)
* Patches can use a parameter named **__state** to store information in the prefix method that can be accessed again in the postfix method. Think of it as a local variable. It can be any type and you are responsible to initialize its value in the prefix
* Parameter names starting with three underscores, for example **___someField**, can be used to read and write (with 'ref') private fields on the instance that has the same name (minus the underscores)
* Patches can define only those parameters they want to access (no need to define all)
* Patch parameters must use the **exact** same name and type as the original method (*object* is ok too)
* Patches can either get parameters normally or by declaring any parameter **ref** (for manipulation)
* To allow patch reusing, one can inject the original method by using a parameter named **__originalMethod**

Transpilers have some other optional parameters:

* A parameter of type `ILGenerator` that will be set to the current IL code generator
* A parameter of type `MethodBase` that will be set to the current original method being patched
* They must contain one parameter of type `IEnumerable<CodeInstruction>` that will be used to pass the IL codes to it

Example:
    
```csharp
// original method in class Customer
private List<string> getNames(int count, out Error error)

// prefix
// - wants instance, result and count
// - wants to change count
// - returns a boolean that controls if original is executed (true) or not (false)
static bool Prefix(Customer __instance, List<string> __result, ref int count)

// postfix
// - wants result and error
// - does not change any of those
static void Postfix(List<string> __result, Error error)

// transpiler
// - wants to use original method
static IEnumerable<CodeInstruction> Transpiler(MethodBase original, IEnumerable<CodeInstruction> instructions)
```