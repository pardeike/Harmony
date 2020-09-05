# Patching

## Concept

In order to provide your own code to Harmony, you need to define methods that run in the context of the original method. Harmony provides three types of methods that each offer different possibilities.

#### Types of patches

Two of them, the **Prefix** patch and the **Postfix** patch are easy to understand and you can write them as simple static methods.

**Transpiler** patches are not methods that are executed together with the original but instead are called in an earlier stage where the instructions of the original are fed into the transpiler so it can process and change them, to finally output the instructions that will build the new original.

A **Finalizer** patch is a static method that handles exceptions and can change them. It is the only patch type that is immune to exceptions thrown by the original method or by any applied patches. The other patch types are considered part of the original and may not get executed when an exception occurs.

Finally, there is the **Reverse Patch**. It is different from the previous types in that it patches your methods instead of foreign original methods. To use it, you define a stub that looks like the original in some way and patch the original onto your stub which you can easily call from your own code. You can even transpile the result during the process.

#### Patches need to be static

Patch methods need to be static because Harmony works with multiple users in different assemblies in mind. In order to guarantee the correct patching order, patches are always re-applied as soon as someone wants to change the original. Since it is hard to serialize data in a generic way across assemblies in .NET, Harmony only stores a method pointer to your patch methods so it can use and apply them at a later point again.

If you need custom state in your patches, it is recommended to use a static variable and store all your patch state in there. Keep in mind that Transpilers are only executed to generate the method so they don't "run" when the original is executed.

#### Commonly unsupported use cases

Harmony works only in the current AppDomain. Accessing other app domains requires xpc and serialization which is not supported.

Currently, support for generic types and methods is experimental and can give unexpected results. See [Edge Cases](patching-edgecases.md#generics) for more information.

When a method is inlined and the code that tries to mark in for not inlining does not work, your patches are not called because there is no method to patch.

## Patch Class

With manual patching, you can put your patches anywhere you like since you will refer to them yourself. Patching by annotations simplifies patching by assuming that you set up annotated classes and define your patch methods inside them.

**Layout**
The class can be static or not, public or private, it doesn't matter. However, in order to make Harmony find it, it must have at least one `[HarmonyPatch]` attribute. Inside the class you define patches as static methods that either have special names like Prefix or Transpiler or use attributes to define their type. Usually they also include annotations that define their target (the original method you want to patch). It also common to have fields and other helper methods in the class.

**Attribute Inheritance**
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

![note] Patch methods _must_ be static but you can define them public or private. They cannot be dynamic methods but you can write static patch factory methods that return dynamic methods.

### Method names

Manual patching knows four main patch types: **Prefix**, **Postfix**, **Transpiler** and **Finalizer**. If you use attributes for patching, you can also use the helper methods: **Prepare**, **TargetMethod**, **TargetMethods** and **Cleanup** as explained below.

Each of those names has a corresponding attribute starting with [Harmony...]. So instead of calling one of your methods "Prepare", you can call it anything and decorate it with a `[HarmonyPrepare]` attribute.

## Patch method types

Both prefix and postfix have specific semantics that are unique to them. They do however share the ability to use a range of injected values as arguments.

### Prefix

A prefix is a method that is executed before the original method. It is commonly used to:

- access and edit the arguments of the original method
- set the result of the original method
- skip the original method
- set custom state that can be recalled in the postfix
- run a piece of code at the beginning that is guaranteed to be executed

### Postfix

A postfix is a method that is executed after the original method. It is commonly used to:

- read or change the result of the original method
- access the arguments of the original method
- read custom state from the prefix

### Transpiler

This method defines the transpiler that modifies the code of the original method. Use this in the advanced case where you want to modify the original methods IL codes.

### Finalizer

A finalizer is a method that is executed after all postfixes. It will wrap the original, all prefixes and postfixes in a try/catch logic and is either called with `null` (no exception) or with an exception if one occured. It is commonly used to:

- run a piece of code at the end that is guaranteed to be executed
- handle exceptions and suppress them
- handle exceptions and alter them

[note]: https://raw.githubusercontent.com/pardeike/Harmony/master/Harmony/Documentation/images/note.png
