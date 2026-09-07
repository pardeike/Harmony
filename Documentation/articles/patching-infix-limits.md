# Infix odd cases and limits

An Infix selects compiled operations, not C# expressions. See the [Infix guide](patching-infix.md) for registration and signatures.

These illustrative C# fragments need static, nongeneric patch classes, target attributes, and the relevant imports.

## Adding another patch

Separate `Patch()` calls accumulate registrations, including the same owner. Harmony rebuilds all surviving registrations into one ordered pipeline per instruction.

Both forms install targeted `HarmonyMethod` objects `a` and `b`:

```csharp
harmony.CreateProcessor(outer).AddInnerPrefix(a).Patch();
harmony.CreateProcessor(outer).AddInnerPrefix(b).Patch();
// Or accumulate both before installing:
harmony.CreateProcessor(outer).AddInnerPrefix(a).AddInnerPrefix(b).Patch();
```

All three `AddInner...` methods append. Reusing `Patch()` installs the same pending lists again. Ordinary `AddPrefix` has one slot: adding twice before installation keeps only the second.

## A captured result can later be replaced

A void postfix sees the result when that postfix runs. All void postfixes run before returning postfixes, which pass a replacement result to the next returning postfix.

```csharp
static void Capture(StringBuilder __result,
    [HarmonyOuter] out StringBuilder __var_builder)
    => __var_builder = __result;

static StringBuilder Replace(StringBuilder result)
    => new StringBuilder("replacement");
```

If both target the same construction, the saved reference points to the first builder, while the outer code receives the replacement. Saving a reference does not clone its object; later edits remain visible.

`Priority.Last` cannot move a void capture past returning postfixes. This is a normal conflict between mods' result logic: capture at a later operation or coordinate with the other author.

## A field expression may take an address

```csharp
class Counter
{
    public int count;
    public int Read() => count;
    public string Text() => count.ToString();
}
```

`Read` loads the field value directly. `Text` can take the address of `count` to call the integer's instance method. `FieldRead` selects direct loads, `ldfld` or `ldsfld`, and does not select field-address instructions, `ldflda` or `ldsflda`. Similar-looking source expressions can therefore match differently.

Instance fields declared on structs remain unsupported because their receivers can be values or addresses. Static fields on structs and supported struct method calls remain usable. A property target selects its accessor call, not its internal field accesses.

## Be careful with indices

`Positions = new[] { 2 }` selects the second matching instruction, not instruction number two in the whole method. `-1` selects the last match. Method calls, field operations, construction and literals use the same rule.

All Infixes count against the same original instruction body for that rebuild, after ordinary transpilers have finished and before any Infix changes are inserted. Adding or removing another Infix does not shift those positions. Calls copied from an inlined patch body and calls moved into an inner-finalizer helper do not change the count either.

```csharp
Total(); // Match 1
Total(); // Match 2: Positions = new[] { 2 } selects this call
Total(); // Match 3
```

An Infix on the first call can skip it, change its result, or invoke `Total` again from its callback. The second Infix still selects the original second call. Every rebuild starts from the original method and reruns its ordinary transpilers; it does not patch the previous Infix-generated wrapper.

That stability does not extend across changes to the original code or ordinary transpilers. If an update inserts another `Total()` before these calls, the old first call becomes match 2. The patch can then install successfully at the wrong place. A no-match or out-of-range error catches a missing position, not a changed meaning. Review positional targets after updates. If surrounding instructions determine which occurrence you mean, use a transpiler with `CodeMatcher` and explicit match checks.

## Literal positions count instructions

```csharp
for (var i = 0; i < 10; i++)
    Console.WriteLine("marker");
```

There is one `"marker"` load here, executed repeatedly. Position `1` selects that instruction on every iteration; position `2` does not mean the second iteration.

Literal targets match compiled values after transpilers. Constant folding can turn `2 * 3` into one `6` load. Common values such as `0` may belong to unrelated expressions. Prefer distinctive values; the [position caveats above](#be-careful-with-indices) apply to literals too.

## Generic observation and replacement differ

Selecting `Container<int>.Use` leaves `Container<string>.Use` untouched. Selecting the member on `Container<>` requests all constructions. Every selected operation must accept the patch's signature.

```csharp
static void Observe(object __result) => Console.WriteLine(__result);
static int Transform(int result) => result + 1;
```

The observer accepts integer and string results; the returning postfix requires integers. Returning `object` is not a universal transformer: its return type and first parameter must exactly match each selected result type. Unboxable values, such as `Span<int>`, need typed observations.

## Inner arguments are already evaluated

For `Inner(value)`, an Infix's `ref int value` edits the captured argument passed to `Inner`. It does not reassign the outer variable that supplied it. If `Inner` itself takes `ref int value`, both patches and the callee can write the original storage.

`[HarmonyOuter]` selects the containing method's scope, without fallback for missing names. For a real argument named `__result`, use exact binding:

```csharp
static void Before(
    [HarmonyArgument("__result", ArgumentMode.Original)] int innerValue,
    [HarmonyOuter, HarmonyArgument("__result", ArgumentMode.Original)] int outerValue)
    => Console.WriteLine(innerValue + outerValue);
```

Both selected methods must have that exact, case-sensitive parameter name. Without `ArgumentMode.Original`, Harmony's special injection names still apply.

## An argument array is writable without `ref`

```csharp
static void Edit(object[] __args) => __args[0] = 7;
static void Observe(int value, bool __runOriginal)
    => Console.WriteLine($"{value}: {__runOriginal}");
```

The array writes back its first argument. Request typed values for observation; a mutable object passed by value still allows mutation.

Harmony also uses the signature when deciding which later prefixes to skip after a prefix returns false. The second example is an exempt observer. A void return alone does not make a prefix observation-only; an argument array or writable parameter can change its classification.

## Outer captures keep the last value in that invocation

A named outer local such as `__var_builder` starts at its default value for each outer invocation. Every execution of its writer overwrites the same slot. In a loop, a reader can therefore see the previous iteration's value if the current iteration bypasses the writer. Handle missing captures and reset them where your logic requires it.

Inner `__state` resets whenever its operation executes. Recursive and concurrent invocations have separate locals. In iterator/async `MoveNext`, outer locals last one invocation, not across suspension.

## Automatic body selection is opt-in

`[HarmonyInfix(typeof(Helper), "Decide", OuterBody = InfixOuterBody.Auto)]` searches a supported iterator or async method's generated `MoveNext` body. Other methods keep their declared bodies. Manual registration sets `HarmonyMethod.infixOuterBody`. The default, `Declared`, keeps the supplied method. Ordinary outer patches target that supplied method; use separate processors when these resolve to different bodies.

Inspect the actual `MoveNext` with `Harmony.GetPatchInfo`, and supply that method when unpatching directly. `AccessTools.StateMachineMoveNext(source)` resolves it. A processor that installed the Infix remembers its actual target for unpatching; `UnpatchAll(owner)` also includes generated bodies. Inspecting or directly unpatching the source factory does not redirect automatically.

## Construction has a result, not an incoming object

For `new Widget(NextId())`, `NextId()` has already run before the constructor Infix. Skipping construction suppresses allocation and constructor effects but cannot undo argument evaluation. The new object is `__result`; there is no incoming constructed `__instance`. Supply a usable replacement when skipping if the following code expects an object. Constructor initialization through `call`, such as a base-constructor call, is not a `newobj` target.

## Virtual dispatch and exceptions still matter

A virtual-call selector matches the method recorded in the instruction. If that instruction names `Base.Draw`, selecting `Derived.Draw` does not match it merely because the runtime receiver is a `Derived`. The original call still dispatches to the runtime override and runs its own Harmony patches.

An inner postfix runs after normal completion or a skipped operation. An exception from a prefix, the operation, or an earlier postfix prevents remaining postfixes from running. Inner finalizers cover that pipeline and can preserve, replace, or suppress its exception through `__exception`. They also run on normal completion, when `__exception` is null. Suppression does not resume skipped postfixes. Set a usable `__result` if the failed operation produced none.

Receiver and argument evaluation precede this protection. Surrounding handlers and ordinary outer finalizers still apply; an outer finalizer does not receive exceptions already suppressed by an inner finalizer or caught inside the outer method.

### Finalizers can run again and see an earlier result

This finalizer throws on normal completion, then runs again with that exception, which remains unsuppressed:

```csharp
static void Finish(Exception __exception)
{
    if (__exception is null) throw new InvalidOperationException();
}
```

Returning postfixes commit only when their whole phase succeeds. If the operation returns `1`, postfix A returns `2`, and postfix B throws, a suppressing finalizer sees `__result == 1`. Execution resumes with `1` unless the finalizer changes it.

## Indirect-call signatures use a historical convention mapping

`InlineSignature` describes a `calli` instruction, which calls through a function pointer. Its `PopCount` includes the pointer and any implicit receiver; `PushCount` is zero for a void return and one otherwise.

Its `CallingConvention` property retains Harmony's historical encoding: `CallingConvention.Winapi` means the default **managed** convention here, not the platform's default unmanaged convention. When inspecting and re-emitting an existing operand, preserve its convention. When constructing an unmanaged call, name its actual convention, such as `Cdecl` or `StdCall`; do not copy `Winapi` from a native interop declaration and expect the same meaning.

Public signature inspection does not make indirect calls selectable Infix targets or remove the documented function-pointer import restrictions.

## Assemblies that look identical to the loader

Suppose two plugins load different `Patch.dll` files, both advertising assembly version `1.0.0.0`. A direct generated call can retain the exact method object. A generated assembly that must refer to both files by their identical assembly identity cannot reliably distinguish them. Exception-handling outer wrappers can need this form of generation.

Harmony resolves dependencies only for the wrapper it generated and checks that the actual assemblies match. If the runtime would substitute the wrong copy, or one wrapper needs both indistinguishable identities, installation fails and the previous patch stays active. On Framework/Mono, competing loaded identities are rejected for these wrappers because the runtime cannot verify an isolated binding. Use distinct assembly names or versions for independently loaded builds.

Loading the very same target/patch binary twice also makes its saved Infix identity ambiguous. A previously installed record can become unusable for rebuilding, but its owner can still be inspected and removed through the normal APIs. The same applies if its assembly is no longer available. Harmony validates the remaining records before rebuilding; it never guesses which copy you meant. Malformed stored data is still an error, not a recoverable member lookup.
