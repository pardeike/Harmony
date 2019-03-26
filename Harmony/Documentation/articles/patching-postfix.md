# Patching

## Postfix

A postfix is a method that is executed after the original method. It is commonly used to:
 
- read or change the result of the original method
- access the arguments of the original method
- make sure your code is always executed
- read custom state from the prefix

### Reading or changing the result

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

### Pass through postfixes

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

### Reading original arguments

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

### Postfixes always run

Harmony will not skip any postfix regardless of what any prefix or the original method do. It is good style to use postfixes as much as possible since they lead to more compatible code.

### Passing state between prefix and postfix

See prefix