# Injected values

<div id="patching"></div>
<div id="common-injected-values"></div>

Prefixes, postfixes, and finalizers can receive the original's arguments, instance, and result. Declare only the parameters you need.

## __instance

**`__instance`** is the original method's `this`. It is available for instance methods, not static methods.

## __result

**`__result`** holds the returned value. Its type must match the original return type or be assignable from it. It starts with that type's default value before prefixes run. To change it, use `ref`, for example `ref string __result`.

## __resultRef

**`__resultRef`** changes the reference itself for a **ref return**. Declare it as `ref RefResult<T> __resultRef`, where `T` is the returned element type; for `ref string`, use `ref RefResult<string>`.

## __state

**`__state`** passes a per-call value from prefix to postfix or finalizer. Set it in the prefix using `ref` or `out`. It can be any type, but the patches sharing it must be in the same class.

## ___fields

**Three** underscores select a field: `___someField` reads `someField`, including private fields. Use `ref` to write it, for example `ref string ___name`.

## __args

**`object[] __args`** contains all arguments in declaration order. Editing its elements updates the corresponding arguments; `ref` is not needed. It has more overhead than typed argument injection.

## method arguments

To read an original argument, declare a matching patch parameter; add `ref` to change it:

- The type of an injected argument must be assignable from the original argument (or just use `object`)
- Use its original name or **`__n`**, where `n` is its zero-based argument index. Argument annotations can map custom names.

If an original argument name conflicts with a Harmony injection name or naming convention, use `ArgumentMode.Original` to match its exact, case-sensitive name without interpreting it:

```csharp
static void Prefix([HarmonyArgument("__result", ArgumentMode.Original)] ref bool result)
```

## __originalMethod

**`MethodBase __originalMethod`** identifies the method being patched, useful when one patch targets several methods.

![note] Calling it invokes the **patched** method, not the unmodified original. Use a [reverse patch](reverse-patching.md) for a callable copy of the original.

## __runOriginal

**`bool __runOriginal`** is read-only. In a prefix it tells you whether the original is still scheduled to run; a later prefix can still skip it. In a postfix it tells you whether the original ran.

## Transpilers

In transpilers, arguments are only matched by their type so you can choose any argument name you like.

- **`IEnumerable<CodeInstruction>`** is required and receives the instructions to edit.
- **`ILGenerator`** is optional and receives the current IL generator.
- **`MethodBase`** is optional and receives the original method being patched.

[note]: ../images/note.png
