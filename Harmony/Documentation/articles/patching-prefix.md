# Patching

## Prefix

A prefix is a method that is executed before the original method. It is commonly used to:

- access and edit the arguments of the original method
- set the result of the original method
- skip the original method
- set custom state that can be recalled in the postfix

![note] The first prefix that skips the original will skip all remaining prefixes too. Postfixes are not affected.

### Reading and changing arguments

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

### Changing the result and skipping the original

To change the result of the original, use `__result` as an argument of your prefix. It must match the return type or be assignable from it. Changing the result of the original does not make sense if you let the original run so skipping the original is necessary too.

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

Some users have trouble understanding the disconnect between altering what the original method returns and what the Prefix returns. The following example is meant to illustrate that the boolean return value of the Prefix only determines if the original gets run or not.

```csharp
public class OriginalCode
{
    public bool IsFullAfterTakingIn(int i)
    {
        // do expensive calculations
    }
}

[HarmonyPatch(typeof(OriginalCode), "IsFullAfterTakingIn")]
class Patch
{
    static bool Prefix(ref bool __result, int i)
    {  
        if (i > 5)
        {
            __result = true; // any call to IsFullAfterTakingIn(i) where i > 5 now immediately returns true
           return false; // skips the original and its expensive calculations
        }
        return true; // make sure you only skip if really necessary
    }
}
```

### Passing state between prefix and postfix

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
    // this example uses a Stopwatch type to measure
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

[note]: https://raw.githubusercontent.com/pardeike/Harmony/master/Harmony/Documentation/images/note.png