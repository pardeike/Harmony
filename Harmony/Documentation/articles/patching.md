# Patching

## Concept

In order to provide your own code to Harmony, you need to define methods that run in the context of the original method. Harmony provides three types of methods that each offer different possibilities.

#### Main types of patches

Two of them, **Prefix** and **Postfix** are high level and you can write them as simple static methods. The third, called **Transpiler**, is not a method that is executed together with the original but called in an earlier stage where the instructions of the original are fed into the transpiler so it can process and change them, to finally output the instructions that will build the new original.

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

Both prefix and postfix have specific semantics that are unique to them. They do however share the ability to use a range of injected values as arguments.

### Prefix

A prefix is a method that is executed before the original method. It is commonly used to:

- access and edit the arguments of the original method
- set the result of the original method
- skip the original method
- set custom state that can be recalled in the postfix

### Postfix

A postfix is a method that is executed after the original method. It is commonly used to:
 
- read or change the result of the original method
- access the arguments of the original method
- make sure your code is always executed
- read custom state from the prefix

### Transpiler

This method defines the transpiler that modifies the code of the original method. Use this in the advanced case where you want to modify the original methods IL codes.

[note]: https://raw.githubusercontent.com/pardeike/Harmony/master/Harmony/Documentation/images/note.png