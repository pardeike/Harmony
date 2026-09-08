# Choose a patch type

<div id="patching"></div>

Start with the smallest change that expresses your intent. A postfix is often enough to adjust a return value; an Infix can target one operation without rewriting the surrounding instructions.

| I want to… | Start with | Scope |
| --- | --- | --- |
| Change arguments or skip a method | [Prefix](patching-prefix.md) | One method invocation |
| Read or change a result | [Postfix](patching-postfix.md) | After completion or a skip |
| Clean up on failure or handle an exception | [Finalizer](patching-finalizer.md) | The patched method and its patches |
| Change a selected call, field access, or other operation | [Infix](patching-infix.md) | Selected operations in an outer method |
| Rewrite instructions directly | [Transpiler](patching-transpiler.md) | The replacement body, during generation |
| Call a copy of an original implementation | [Reverse patch](reverse-patching.md) | A stub method you control |

## Runtime flow

[!include[Patch execution](../includes/patch-flow.md)]

## Concept

To provide your own code to Harmony, define patch methods. The patch type determines when your code runs and what it can change.

#### Types of patches

**Prefixes** run before the original; **postfixes** run after it completes or is skipped.

**Transpilers** change the original's IL instructions when Harmony builds the replacement method, not on each call.

**Finalizers** handle exceptions from prefixes, the original, or postfixes. They can observe, replace, or suppress an exception. Without one, an exception skips the remaining patches and reaches the caller.

An [Infix](patching-infix.md) applies ordinary prefixes, postfixes, or finalizers to selected operations inside an outer method. It can target method/property calls, field reads or writes, construction, and literal loads. Other callers and unselected operations remain unchanged.

A **reverse patch** copies the original into a stub method you can call from your own code. You can also transpile that copy.

#### Patches need to be static

Harmony stores references to static patch methods so it can reapply everyone's patches whenever registrations change. It does not create or store patch-class instances.

Use `__state` for values that belong to one patched invocation and need to pass between patches in the same class. Static fields are appropriate for deliberately shared state, not independent per-call values.

#### Commonly unsupported use cases

Harmony patches only within the current AppDomain.

Currently, support for generic types and methods is experimental and can give unexpected results. See [Edge Cases](patching-edgecases.md#generics) for more information.

Calls already inlined into another method can bypass your patch. See [Inlining](patching-edgecases.md#inlining).

## Patch Class

Manual patching lets you supply methods from any class. Annotation patching groups them in a patch class.

**Layout**
The class can be public or private, static or not. Mark it with `[HarmonyPatch]` and describe the target using annotations. Its static patch methods use recognized names such as `Prefix`, or attributes such as `[HarmonyPrefix]`. Helper methods and fields are fine too.

**Combining attributes**
Harmony combines the target annotations on the class with those on each patch method. For example, specify the declaring type on the class and the method name on a patch method. See [combining annotations](annotations.md#combining-annotations).

## Patch methods

Harmony recognizes patch and helper methods **by name**, or by their attributes:

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

For manual patching, wrap each patch's `MethodInfo` in a `HarmonyMethod` and pass it to `Patch()`. Method names do not matter.

![note] Patch methods _must_ be static but you can define them public or private. They cannot be dynamic methods but you can write static patch factory methods that return dynamic methods.

```csharp
[HarmonyPatch(...)]
class Patch
{
	// the return type of factory methods can be either MethodInfo or DynamicMethod
	[HarmonyPrefix]
	static MethodInfo PrefixFactory(MethodBase originalMethod)
	{
		// return an instance of MethodInfo or an instance of DynamicMethod
	}

	[HarmonyPostfix]
	static MethodInfo PostfixFactory(MethodBase originalMethod)
	{
		// return an instance of MethodInfo or an instance of DynamicMethod
	}
}
```

### Method names

The patch names are **Prefix**, **Postfix**, **Transpiler**, and **Finalizer**. Annotation patching also recognizes the [helpers](patching-auxiliary.md) **Prepare**, **TargetMethod**, **TargetMethods**, and **Cleanup**. Each name has a corresponding attribute, such as `[HarmonyPrepare]`.

## Patch method types

Prefixes, postfixes, and finalizers share the [injected values](patching-injections.md) available as parameters.

### Prefix

A prefix is a method that is executed before the original method. It is commonly used to:

- access and edit the arguments of the original method
- set the result of the original method
- skip the original method
- set custom state that can be recalled in the postfix
- run code before the original

### Postfix

A postfix runs after the original completes or is skipped, but not after an exception. It is commonly used to:

- read or change the result of the original method
- access the arguments of the original method
- read custom state from the prefix

### Transpiler

A transpiler edits the original method's IL instructions.

### Finalizer

A finalizer is a method that executes after all postfixes. It wraps the original method, all prefixes, and postfixes in try/catch logic and is called either with `null` (no exception) or with an exception if one occurred. It is commonly used to:

- run cleanup on success or failure
- handle exceptions and suppress them
- handle exceptions and alter them

[note]: ../images/note.png
