# Injected values

<div id="patching"></div>
<div id="common-injected-values"></div>

Prefixes, postfixes, and finalizers can receive the original's arguments, instance, and result. Declare only the parameters you need.

## Patch-time checks in Harmony 3

Harmony rejects a binding during patch installation when it can establish that the declaration is incompatible with the supplied value or storage. Errors identify the original method, callback, parameter, resolved source, supplied and requested types, and the reason for rejection. Infix errors also identify the selected operation and scope.

These checks catch definite mistakes. Acceptance does not prove that every runtime value will work. For example:

| Binding | Outcome |
| --- | --- |
| A receiver requested as an unrelated class | Rejected by default, even if the callback would ignore it |
| A `string` argument read as `object` | Accepted |
| An `object` argument read as `string` | Accepted; the actual value must be suitable |
| A `string` state local accessed through `ref object __state` | Accepted under the existing state contract; writing an incompatible object is still the patch's responsibility |
| `ref MethodBase __originalMethod` or `ref object[] __args` | Rejected; these injections supply values, not addresses |

Checks follow the conversions the emitter actually performs. Arguments can be boxed, which wraps a value type as an object. Ordinary field and state injections do not add boxing. Wider reference bindings do not cause Harmony to insert runtime casts or checks.

Uncertain interface, array, generic variance and proxy relationships remain accepted. This feature adds no numeric, enum, pointer or struct-layout conversion policy. A nullable value type boxes like any other value type: a boxed `int?` is a boxed `int` or `null`, so it can be read as `object` or `IComparable`, while a reference argument declared as `int?` is rejected because the emitter has no unboxing conversion. Existing result, state, and Infix restrictions still apply. It also checks delegate receivers and invocation signatures, finalizer returns, and passthrough results for definite incompatibility.

The reference checks apply to the registrations being added. Surviving registrations were accepted when they were added, by this or an older engine, and are rebuilt with their accepted bindings; only structural requirements such as missing storage still apply to them. A pair of passthrough postfixes is rechecked when either of them is being added. `__exception` is checked against `Exception` even before any finalizer exists, so a later finalizer cannot invalidate an installed prefix.

A rejected addition leaves that method's installed replacement and published patch registrations unchanged, and other owners can still add, remove and rebuild beside an older registration that would fail today's checks. This is a per-method installation guarantee; prepare callbacks, factories, and transpilers may already have run. The checks add no work to patched invocations.

### Unchecked reference binding

Use `[HarmonyUncheckedReferenceBinding]` on an individual patch method when its reference bindings deliberately depend on knowledge outside the declared types. Here, **reference** means an object reference: the option applies to both ordinary parameters and `ref` parameters.

For example, one callback can patch `First(A first, Other second)` and `Second(Other first, B second)` and use only the matching argument:

```csharp
[HarmonyPrefix]
[HarmonyUncheckedReferenceBinding]
static void Prefix(A first, B second, MethodBase __originalMethod)
{
    if (__originalMethod.Name == nameof(Targets.First))
        first.Use();
    else
        second.Use();
}
```

For manual registration, set the equivalent nullable `HarmonyMethod` field:

```csharp
harmony.Patch(original, prefix: new HarmonyMethod(patchMethod)
{
    uncheckedReferenceBinding = true
});
```

The option skips the new compatibility checks between reference types. It also covers object references produced by boxing that the emitter already performs, such as an unused `int` argument supplied to an unrelated reference parameter. It applies to ordinary prefixes, postfixes and finalizers, including injected delegates and reference passthrough returns. A sibling-typed `ref` passthrough postfix can therefore opt in. Infix callbacks carry the same option, but retain their existing stricter contracts.

The option does **not** add casts, boxing, unboxing or other conversions. It retains these requirements:

- Named arguments and fields must exist, and bindings that require storage or an address must have it. `__result` still requires a result, and `ref __instance` requires a receiver. Value-only injections such as `__originalMethod` and `__args` still cannot be requested by `ref`.
- Value/reference bindings must use a conversion the emitter already supports. An `object` argument requested as `Guid` still fails.
- Existing `__result`, exact `RefResult<T> __resultRef`, state, Infix storage/scope and passthrough contracts still apply. Delegate arity and value/address shape checks remain.

This is permission to emit the existing binding, not a declaration that the CLR considers it safe. Harmony does not inspect whether a callback touches an incompatible argument. Its body, callers and the runtime must tolerate the emitted code; successful installation does not prove that later execution is safe.

The default is checked. The method-only attribute affects that callback's registrations; the manual field affects the individual registration. An explicit `false` overrides an imported attribute. The stored flag is a snapshot: changing the input `HarmonyMethod` later does not change an installed patch. For a factory, put the option on the registered factory or its `HarmonyMethod`; it follows the resolved callback through each rebuild. Another registration remains checked even if it resolves to the same callback. A transpiler registration rejects the option, since transpiler instruction validation is outside it, and transpiler records never affect the patch-state version.

Generic policy is unchanged. The binder uses runtime type relationships for closed types and leaves open or uncertain relationships accepted, without recursively expanding generic constraints. Self-referential types such as `Node<T> where T : Node<T>` do not require special traversal. Acceptance here does not expand support for patching open generic methods.

**Mixed Harmony versions:** methods with an opted-out registration use shared patch-state version 5. A second capable engine preserves the flag when rebuilding. Older engines reject that method's state before reading or updating its registrations, so they cannot silently discard the option. Remove the opted-out registrations using a capable engine to restore the format required by the remaining patches. Unmarked methods retain their previous format. This protects published state; hosts must still serialize cross-engine updates, since an older engine could already hold an unpublished candidate created before the opted-out registration.

## __instance

**`__instance`** is the original method's `this`. For a static method, a by-value reference parameter receives `null`; value-type and `ref` receiver requests fail.

For a value-type original, including primitives such as `int` or `double`, `T __instance` receives a copy, `ref T __instance` addresses the caller's storage, `object __instance` receives a boxed copy, and `ref object __instance` copies the boxed value back after the patch returns.

## __result

**`__result`** holds the returned value. Its type must match the original return type or be assignable from it. It starts with that type's default value before prefixes run. To change it, use `ref`, for example `ref string __result`.

## __resultRef

**`__resultRef`** changes the reference itself for a **ref return**. Declare it as `ref RefResult<T> __resultRef`, where `T` is the returned element type; for `ref string`, use `ref RefResult<string>`.

## __state

**`__state`** passes a per-call value from prefix to postfix or finalizer. Set it in the prefix using `ref` or `out`. It can be any type, but the patches sharing it must be in the same class.

## ___fields

**Three** underscores select a field: `___someField` reads `someField`, including private fields. Use `ref` to write it, for example `ref string ___name`.

For a static original, an instance field or an injected instance delegate binds to the first argument when that argument can hold the instance: a reference argument declared as the declaring type, a related type or `object`, or a value-type argument of exactly the declaring type, by value or by `ref`. Otherwise the registration is rejected. Harmony 2 used the first argument without checking it.

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
