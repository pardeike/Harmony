# Injected values

<div id="patching"></div>
<div id="common-injected-values"></div>

Prefixes, postfixes, and finalizers can receive the original's arguments, instance, and result. Declare only the parameters you need.

## Patch-time checks in Harmony 3

Harmony rejects a binding during patch installation when it can establish that the declaration is incompatible with the supplied value or storage. Errors identify the original method, callback, parameter, resolved source, supplied and requested types, and the reason for rejection. Infix errors also identify the selected operation and scope.

These checks catch definite mistakes. Acceptance does not prove that every runtime value will work. For example:

| Binding | Outcome |
| --- | --- |
| A receiver requested as an unrelated class | Rejected, even if the callback would ignore it |
| A `string` argument read as `object` | Accepted |
| An `object` argument read as `string` | Accepted; the actual value must be suitable |
| A `string` state local accessed through `ref object __state` | Accepted under the existing state contract; writing an incompatible object is still the patch's responsibility |
| `ref MethodBase __originalMethod` or `ref object[] __args` | Rejected; these injections supply values, not addresses |

Checks follow the conversions the emitter actually performs. Arguments can be boxed, which wraps a value type as an object. Ordinary field and state injections do not add boxing. Wider reference bindings do not cause Harmony to insert runtime casts or checks.

Uncertain interface, array, generic variance and proxy relationships remain accepted. This feature adds no numeric, enum, pointer, nullable or struct-layout conversion policy. Existing result, state, and Infix restrictions still apply. It also checks delegate receivers and invocation signatures, finalizer returns, and passthrough results for definite incompatibility.

A rejected addition leaves that method's installed replacement and published patch registrations unchanged. Removing an invalid old registration remains possible because Harmony validates the surviving registrations. This is a per-method installation guarantee; prepare callbacks, factories, and transpilers may already have run. The checks add no work to patched invocations.

## __instance

**`__instance`** is the original method's `this`. For a static method, a by-value reference parameter receives `null`; value-type and `ref` receiver requests fail.

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

The array excludes the receiver. `ref object[]` is invalid, as are arguments the emitter cannot represent in the array, such as pointers and byref-like values. Use typed argument injections for those methods. Broader by-value declarations such as `object __args` are accepted for ordinary patches.

## method arguments

To read an original argument, declare a matching patch parameter; add `ref` to change it:

- Use the original argument type or a type assignable from it, such as `object`. Harmony 3 also accepts plausible runtime-dependent reference bindings as described above.
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
