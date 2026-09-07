# Infix odd cases and limits

An Infix selects compiled operations, not C# expressions. See the [Infix guide](patching-infix.md) for registration and signatures. These fragments need static, nongeneric patch classes, target attributes and imports.

## Adding another patch

Separate `Patch()` calls add registrations, even with the same owner. Both forms install targeted `HarmonyMethod` objects `a` and `b`:

```csharp
harmony.CreateProcessor(outer).AddInnerPrefix(a).Patch();
harmony.CreateProcessor(outer).AddInnerPrefix(b).Patch();
// Or accumulate both before installing:
harmony.CreateProcessor(outer).AddInnerPrefix(a).AddInnerPrefix(b).Patch();
```

All `AddInner...` methods append. Calling `Patch()` again installs the pending lists again. Ordinary `AddPrefix` has one slot, so adding twice before installation keeps only the second.

## A captured result can later be replaced

All void postfixes run before returning postfixes, which chain replacement results.

```csharp
static void Capture(StringBuilder __result,
    [HarmonyOuter] out StringBuilder __var_builder)
    => __var_builder = __result;

static StringBuilder Replace(StringBuilder result)
    => new StringBuilder("replacement");
```

For the same construction, the capture keeps the first builder while the outer code receives the replacement. It saves a reference, not a clone, so later object edits remain visible.

Even `Priority.Last` cannot move this capture past returning postfixes. Capture at a later operation or coordinate with the other patch author.

## A field expression may take an address

```csharp
class Counter
{
    public int count;
    public int Read() => count;
    public string Text() => count.ToString();
}
```

`Read` loads the value. `Text` may take its address. `FieldRead` selects `ldfld` and `ldsfld`, not `ldflda` or `ldsflda`.

Struct instance fields are unsupported because their receivers can be values or addresses. Struct static fields and supported method calls work. Property targets select accessor calls, not the fields inside them.

## Be careful with indices

`Positions = new[] { 2 }` selects the second match, not instruction number two. `-1` selects the last match. This applies to every operation kind.

Positions count after ordinary transpilers, before any Infix code. Other Infixes, inlined callbacks and finalizer helpers do not shift them.

```csharp
Total(); // Match 1
Total(); // Match 2: Positions = new[] { 2 } selects this call
Total(); // Match 3
```

The first Infix can skip its call or call `Total` again. The second still selects the original second call. Rebuilds start from the original method and rerun its transpilers.

Source or transpiler updates can change what a position means without causing an error. Review targets after updates. If surrounding instructions identify your match, use a transpiler with `CodeMatcher` and explicit checks.

## Literal positions count instructions

```csharp
for (var i = 0; i < 10; i++)
    Console.WriteLine("marker");
```

This is one load, repeated ten times. Position `1` selects every execution; position `2` is not the second iteration.

Literals match compiled values after transpilers. `2 * 3` can become one `6` load, and common values like `0` may occur elsewhere. Prefer distinctive values; the [position caveats](#be-careful-with-indices) still apply.

## Generic observation and replacement differ

`Container<int>.Use` selects that exact construction. The member on `Container<>` selects all constructions. Your patch signature must fit every match.

```csharp
static void Observe(object __result) => Console.WriteLine(__result);
static int Transform(int result) => result + 1;
```

The observer accepts integer and string results; the transformer requires integers. Returning `object` is not universal. A returning postfix's return type and first parameter must exactly match the result type. Unboxable values such as `Span<int>` need typed observations.

## Inner arguments are already evaluated

For `Inner(value)`, an Infix's `ref int value` changes the captured argument, not the outer variable. If `Inner` itself takes `ref int value`, the patch and callee write the original storage.

`[HarmonyOuter]` selects the containing method's scope, with no fallback for missing names. To bind a real argument named `__result`:

```csharp
static void Before(
    [HarmonyArgument("__result", ArgumentMode.Original)] int innerValue,
    [HarmonyOuter, HarmonyArgument("__result", ArgumentMode.Original)] int outerValue)
    => Console.WriteLine(innerValue + outerValue);
```

Names are case-sensitive. Without `ArgumentMode.Original`, Harmony's special injection names still apply.

## An argument array is writable without `ref`

```csharp
static void Edit(object[] __args) => __args[0] = 7;
static void Observe(int value, bool __runOriginal)
    => Console.WriteLine($"{value}: {__runOriginal}");
```

`Edit` writes back the first argument. Use typed values to observe arguments, though passing a mutable object by value still allows mutation.

After a prefix returns false, Harmony skips later prefixes according to their signatures. `Observe` is exempt. A void return alone is not enough; argument arrays and writable parameters affect that classification.

## Outer captures keep the last value in that invocation

An outer local such as `__var_builder` starts at its default each invocation. Each write replaces it. A loop iteration that skips the writer can leave the previous iteration's value, so reset or check captures as needed.

Inner `__state` resets on each operation. Recursive and concurrent invocations have separate locals. In iterator/async `MoveNext`, outer locals last one invocation, not across suspension.

## Automatic body selection is opt-in

`[HarmonyInfix(typeof(Helper), "Decide", OuterBody = InfixOuterBody.Auto)]` searches a supported iterator or async method's generated `MoveNext`; other methods keep their declared bodies. Manual registration uses `HarmonyMethod.infixOuterBody`. The default is `Declared`.

Ordinary patches still target the supplied method. Use separate processors if the bodies differ. Resolve `MoveNext` with `AccessTools.StateMachineMoveNext(source)` for `GetPatchInfo` or direct unpatching. These calls do not redirect from the source factory. The installing processor remembers its target; `UnpatchAll(owner)` includes generated bodies.

## Construction has a result, not an incoming object

In `new Widget(NextId())`, `NextId()` runs before the Infix. Skipping prevents allocation and constructor effects, not argument evaluation. The new object is `__result`; there is no incoming `__instance`. Supply a replacement if later code needs one. Base-constructor initialization through `call` is not a `newobj` target.

## Virtual dispatch and exceptions still matter

If the instruction names `Base.Draw`, select that method even when the receiver is a `Derived`. The call still dispatches to its runtime override and runs that method's Harmony patches.

Postfixes run after normal or skipped operations. An exception stops remaining postfixes. Finalizers cover the prefix/operation/postfix pipeline and can preserve, replace or suppress `__exception`. They also run on success, with a null exception. Suppression does not resume postfixes; supply `__result` if the operation produced none.

Receiver and argument evaluation happen before this protection. Outer handlers and finalizers still apply, but do not receive exceptions already caught or suppressed.

### Finalizers can run again and see an earlier result

This throws on success, then runs again with the unsuppressed exception:

```csharp
static void Finish(Exception __exception)
{
    if (__exception is null) throw new InvalidOperationException();
}
```

Returning postfixes commit only when their whole phase succeeds. If the operation returns `1`, postfix A returns `2`, and B throws, a suppressing finalizer sees `__result == 1`. Execution resumes with `1` unless the finalizer changes it.

## Indirect-call signatures use a historical convention mapping

`InlineSignature` describes a `calli` function-pointer call. `PopCount` includes the pointer and any implicit receiver. `PushCount` is zero for void, otherwise one.

Its historical encoding uses `CallingConvention.Winapi` for the default **managed** convention, not the platform's default unmanaged convention. Preserve a parsed operand's convention. For new unmanaged calls, specify the actual convention, such as `Cdecl` or `StdCall`; do not copy `Winapi` from native interop.

This does not make `calli` an Infix target or remove function-pointer import restrictions.

## Assemblies that look identical to the loader

Two different `Patch.dll` files can advertise the same name and version. Direct generated calls can retain exact method objects, but generated assemblies may need names to resolve dependencies. Exception-handling wrappers can use that path.

If a wrapper would bind the wrong copy, or needs both indistinguishable identities, installation fails and the previous patch stays active. Framework/Mono rejects competing loaded identities for these wrappers. Give independently loaded builds distinct names or versions.

Loading the same target/patch binary twice, or losing its assembly, can make a saved Infix record unrebuildable. You can still inspect and remove its owner through the normal APIs. Harmony validates surviving records instead of guessing. Malformed stored data remains an error.
