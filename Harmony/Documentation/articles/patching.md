# Patching

## Concept

In order to provide your own code to Harmony you need to define methods that run in the context of the original method. Harmony provides three types of methods that each offer different possibilities.

#### Main types of patches

Two of them, **Prefix** and **Postfix** are high level and you can write them as simple static methods. The third, called **Transpiler**, is not a method that is not executed together with the original but called in an earlier stage where the instructions of the original are fed into the transpiler so it can process and change them, to finally output the instructions that will build the new original.

#### Patches need to be static

Patch methods need to be static because Harmony works with multiple users in different assemblies in mind. In order to guarantee the correct patching order, patches are always re-applied as soon as someone wants to change the original. Since it is hard to serialize data in a generic way across assemblies in .NET, Harmony only stores a method pointer to your patch methods so it can use and apply them at a later point again.

If you need custom state in your patches, it is recommended to use a static variable and store all your patch state in there. Keep in mind that Transpilers are only executed to generate the method so they don't "run" when the original is executed.

## Patch Class

With manual patching, you can put your patches anywhere you like. Patching by annotations simplifies patching by assuming you create one class for each patched original and define your patch methods inside it. The name of the class can be arbitrary but a common way to name them is `OriginalClass_OriginalMethodName_Patch`.

The class can be static or not, public or private, it doesn't matter. However, in order to make Harmony find it, it must have at least one `[HarmonyPatch]` attribute. Inside the class you can define as many methods as you want and some of them should be (static) patch methods.

The attributes of the methods in the class inherit the attributes of the class.

## Patch methods

Harmony identifies your patch methods and their helper methods **by name**. If you prefer to name your methods differently, you can use attributes to tell Harmony what your methods are.

```csharp
[HarmonyPatch(...)]
class Patch
{
    static void Prefix()
    {
        // this method uses the name "Prefix", no annotation necessary
    }

    [HarmonyPostfix]
    static void MyOwnName()
    {
        // this method is a Postfix as defined by the attribute
    }
}
```

If you prefer manual patching, you can use any method name or class structure you want. You are responsible to retrieve the MethodInfo for the different patch methods and supply them to the Patch() method by wrapping them into HarmonyMethod objects.

![note] Patch methods *must* be static but you can define them public or private. They cannot be dynamic methods but you can write static patch factory methods that return dynamic methods.

### Method names

Manual patching knows only three main patch types: **Prefix**, **Postfix** and **Transpiler**. If you use attributes for patching, you can also use the helper methods: **Prepare**, **TargetMethod**, **TargetMethods** and **Cleanup** as explained below.

Each of those names has a corresponding attribute starting with [Harmony...]. So instead of calling one of your methods "Prepare", you can call it anything and decorate it with a `[HarmonyPrepare]` attribute.

## Patch method types

Both, prefix and postfix have specific semantics that are unique to them. They do however share the ability to use a range of injected values as arguments. Those are listed after the discussion of prefix and postfix.

### Prefix

A prefix is a method that is excuted before the original method. It commonly use to:

- access and edit the arguments of the original method  
- set the result of the original method  
- skip the original method  
- set custom state that can be recalled in the postfix

![note] The first prefix that skips the original will skip all remaining prefixes too. Postfixes are not affected.

#### Reading and changing arguments

```csharp
public class OriginalCode
{
    public void Test(int counter, string name)
    {
        // ...
    }
}

[HarmonyPatch(typeof(OriginalCode), "Test")]
class Patch
{
    static void Prefix(int counter, ref string name)
    {  
        FileLog.Log("counter = " + counter); // read
        name = "test"; // write with ref keyword
    }
}
```

#### Changing the result and skipping the original

To change the result of the original, use `__result` as a argument of your prefix. It must match the return type or be assignable from it. Changing the result of the original does not make sense if you let the original run so skipping the original is necessary too.

To skip the original, let the prefix return a `bool` and return `true` to let the original run after all prefixes or `false` to stop executing prefixes and skip the original. Postfixes will always be executed.

![note] It is not recommended to skip the original unless you want to completely change the way it works. If you only want a small change or a side effect, using a postfix or a transpiler is always preferred since it allows for multiple users changing the original without each implementation fighting over how the original should behave.

```csharp
public class OriginalCode
{
    public string GetName()
    {
        // ...
    }
}

[HarmonyPatch(typeof(OriginalCode), "GetName")]
class Patch
{
    static bool Prefix(ref string __result)
    {  
        __result = "test";
        return true; // make sure you only skip if really necessary
    }
}
```

#### Passing state between prefix and postfix

If you want to share state between your prefix and the corresponding postfix, you can use `__state` (with the `ref` or `out` keyword). If you need more than one value you can create your own type and pass it instead.

```cs
public class OriginalCode
{
    public void Test(int counter, string name)
    {
        // ...
    }
}

[HarmonyPatch(typeof(OriginalCode), "Test")]
class Patch
{
    // this example choose to use a Stopwatch type to measure
    // and share state between prefix and postfix

    static void Prefix(out Stopwatch __state)
    {  
        __state = new Stopwatch(); // assign your own state
        __state.Start();
    }

    static void Postfix(Stopwatch __state)
    {  
        __state.Stop();
        FileLog.Log(__state.Elapsed);
    }
}
```

### Postfix

A postfix is a method that is excuted after the original method. It commonly use to:
 
- read or change the result of the original method  
- access the arguments of the original method 
- make sure your code is always executed  
- read custom state from the prefix

#### Reading or changing the result

Since the postfix has access to the result of the original (or a prefix that has skipped the original), it can read or alter the result by using the argument `__result`. It must match the return type of the original or be assignable from it.

```csharp
public class OriginalCode
{
    public string GetName()
    {
        // ...
    }
}

[HarmonyPatch(typeof(OriginalCode), "GetName")]
class Patch
{
    static void Postfix(ref string __result)
    {  
        if (__result == "foo")
            __result = "bar";
    }
}
```

#### Pass through postfixes

An alternative way to change the result of an original method is to use a **pass through** postfix. A pass through postfix has a non-void return type that matches the type of the first argument.

Harmony will call the postfix with the result of the original and will use the result of the postfix to continue. Since this works for all types, it is especially useful for types like `IEnumerable<T>` that cannot be combined with `ref`. This allows for changing the result with `yield` operations.

```csharp
public class OriginalCode
{
    public string GetName()
    {
        return "David";
    }

    public IEnumerable<int> GetNumbers()
    {
        yield return 1;
        yield return 2;
    }
}

[HarmonyPatch(typeof(OriginalCode), "GetName")]
class Patch1
{
    static string Postfix(string name)
    {  
        return "Hello " + name;
    }
}

[HarmonyPatch(typeof(OriginalCode), "GetNumbers")]
class Patch2
{
    static IEnumerable<int> Postfix(IEnumerable<int> values)
    {  
        yield return 0;
        foreach (var value in values)
            yield return value;
        yield return 99;
    }
}
```

#### Reading original arguments

```csharp
public class OriginalCode
{
    public void Test(int counter)
    {
        // ...
    }
}

[HarmonyPatch(typeof(OriginalCode), "Test")]
class Patch
{
    static void Prefix(int counter)
    {  
        FileLog.Log("counter = " + counter);
    }
}
```

#### Postfixes always run

Harmony will not skip any postfix regardless of what any prefix or the original method do. It is good style to use postfixes as much as possible since they lead to more compatible code.

#### Passing state between prefix and postfix

See prefix

### Common injected values

Each prefix and postfix can get all the arguments of the original method as well as the instance (if original method is not static) and the return value. Patches can define only those parameters they want to access.

#### Instance

Patches can use an argument named `__instance` to access the instance value if original method is not static.

#### Result

Patches can use an argument named `__result` to access the returned value (prefixes get default value).

#### State

Patches can use an argument named `__state` to store information in the prefix method that can be accessed again in the postfix method. Think of it as a local variable. It can be any type and you are responsible to initialize its value in the prefix.

#### Fields

Argument names starting with three underscores, for example `___someField`, can be used to read and (with `ref`) write private fields on the instance that has the corresponding name (minus the underscores).

#### Argument types

Arguments from the original must use the exact same name and type as the original method but using `object` is ok too.

#### The original

To allow patch reusing, one can inject the original method by using an argument named `__originalMethod`.

#### Special arguments

In transpilers, arguments are only matched by their type so you can choose any argument name you like.

An argument of type `IEnumerable<CodeInstruction>` is required and will be used to pass the IL codes to the transpiler
An argument of type `ILGenerator` will be set to the current IL code generator
An argument of type `MethodBase` will be set to the current original method being patched

### Transpiler

This method defines the transpiler that modifies the code of the original method. Use this in the advanced case where you want to modify the original methods IL codes.

```csharp
static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instr, ...)
// or
[HarmonyTranspiler]
static IEnumerable<CodeInstruction> MyTranspiler(IEnumerable<CodeInstruction> instr, ...)
```

+++

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

[note]: https://raw.githubusercontent.com/pardeike/Harmony/master/Harmony/Documentation/images/note.png
