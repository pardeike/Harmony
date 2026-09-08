# Infix

An Infix patches an operation **inside** a method: a call, property access, field read/write, constructor, or literal load. It uses the familiar prefixes, postfixes, and finalizers. Other calls to the same member are unchanged.

The containing method is the **outer method**. For calls, the method being called is the **inner method**. For less common cases, see [Limits](patching-infix-limits.md) and [Recipes](patching-infix-authoring.md).

## A working example

This patch changes calls to `Helper.Decide` inside `Outer.Run`. `value` is the inner argument; `[HarmonyOuter] int mode` reads the outer argument.

[!code-csharp[example](../examples/patching-infix.cs?name=example)]

Install it with your Harmony instance:

[!code-csharp[install](../examples/patching-infix.cs?name=install)]

`Outer.Run("hello", 1)` produces:

```text
high priority
low priority: True
call: hello.
postfix: True, True
```

For `Outer.Run("hello", 0)`, the first prefix returns false. The call is skipped, but the observation-only prefix and postfix still run:

```text
high priority
low priority: False
postfix: False, False
```

## Selecting calls

Select the outer method on the patch class with `[HarmonyPatch]`, `TargetMethod`, or `TargetMethods`. Give each Infix callback a `[HarmonyInfix]` and its role, such as `[HarmonyPrefix]`. Do not also put `[HarmonyPatch]` on that callback.

For calls, supply the declaring type, method name, and argument types needed to identify an overload. The usual `ArgumentType[]` variations handle `ref`, `out`, and pointer parameters.

`Positions` chooses which matches to patch:

- Omitted or empty: all matches.
- `Positions = new[] { 2 }`: the second match.
- `Positions = new[] { -1 }`: the last match.
- Repeated positions: that match once per registration.

Zero, null, missing positions, and no matches are errors. All Infixes count from the **same instructions after ordinary transpilers, before any Infixes are inserted**. Another Infix cannot shift these positions. A game update or transpiler can. See [Be careful with indices](patching-infix-limits.md#be-careful-with-indices).

### Exact targets and generic families

`Container<int>.Use` selects only that construction, not `Container<string>.Use`.

To select every `Container<T>.Use`, use the member from `Container<>`. Likewise, a generic method definition selects all its constructions. Opening the declaring type does not also open the method's generic arguments, or vice versa.

The patch must work at every selected call: `object value` can observe an `int` or a `string`, but `ref int value` cannot edit both. Positions count all family matches together. Other partially open targets with free parameters are rejected.

If the attribute cannot identify a generic overload, pass its `MethodInfo` manually.

### Manual registration

Use `AddInnerPrefix`, `AddInnerPostfix`, or `AddInnerFinalizer`:

[!code-csharp[manual](../examples/patching-infix.cs?name=manual)]

Every `AddInner...` call **adds** a registration, including repeated methods. It does not replace existing patches. A processor retains its configuration, so calling `Patch()` again adds those registrations again. Unpatching by method or owner removes all matching registrations.

The `MethodInfo` overload reads `[HarmonyInfix]` from the callback. Alternatively, supply `HarmonyMethod.innerMethod` or `innerTarget`. If both forms are present, their targets and positions must agree. Registered inputs are copied; editing them later has no effect.

Patch callbacks must be static, nongeneric methods on nongeneric types. Dynamic patch methods and patch factories are not supported.

### Properties, fields, constructors, and literals

Use one of these selectors on each callback, together with its prefix, postfix, or finalizer attribute:

```csharp
[HarmonyInfix(typeof(Thing), "Value", InnerTargetKind.Getter)]
[HarmonyInfix(typeof(Thing), "Value", InnerTargetKind.Setter)]
[HarmonyInfix(typeof(Thing), "count", InnerTargetKind.FieldRead)]
[HarmonyInfix(typeof(Thing), "count", InnerTargetKind.FieldWrite)]
[HarmonyInfix(typeof(StringBuilder), InnerTargetKind.Constructor)]
[HarmonyInfix("StatsReport_FinalValue", Positions = new[] { -1 })]
```

For indexers, argument types describe the index parameters, not the setter's value. For constructors, omitted argument types mean the parameterless constructor.

| Operation | Arguments | Result and instance |
| --- | --- | --- |
| Property access | Accessor arguments | Same as calling the accessor |
| Field read | None | Field value; instance for instance fields |
| Field write | `value` or `__0` | No result; instance for instance fields |
| Construction (`newobj`) | Constructor arguments | New object or struct; no incoming `__instance` |
| Literal load | None | Loaded value; no instance |

For example, `ref int value` changes a field write, and `ref int __result` changes a field read. A constructor postfix receives the new value through `__result`. Skipping construction also skips allocation, but its arguments have already been evaluated.

For manual registration, create an `InnerTarget` from the method, property, field, or constructor. Use `InnerTarget.Constant(value)` for literals.

Literal selectors accept non-null `string`, `int`, `long`, `float`, or `double`. Types must match; floating-point values match by their exact bits. Choose distinctive values: `0` may occur in many unrelated expressions, and compiler folding can remove a source constant entirely.

### Capture at one operation, use at another

Use `[HarmonyOuter] __var_name` to save a value for another Infix in the same patch class:

[!code-csharp[capture](../examples/patching-infix.cs?name=capture)]

Each outer invocation gets its own default-initialized slots. Handle the default if the reader can run without the writer. Use more names for more values. A later postfix can replace a result you captured; see [captured results](patching-infix-limits.md#a-captured-result-can-later-be-replaced).

Ordinary patches cannot use these named slots. To share state with an ordinary patch, use its `__state` and request `[HarmonyOuter] __state` in the Infix.

## Ordering, skipping, and exceptions

Infix follows ordinary Harmony ordering, including priority and before/after rules. Prefixes and postfixes are **not pairs**. Exact and generic-family patches join the same lists at each selected operation.

Without dependency overrides, high- and low-priority prefixes and void postfixes run like this:

```text
high prefix → low prefix → call → high postfix → low postfix
```

Returning postfixes run after void postfixes. As with ordinary [passthrough postfixes](patching-postfix.md#pass-through-postfixes), their first parameter receives the previous result. For Infix, that parameter and the return type must exactly match the operation's result type. Method-valued results such as `MethodInfo` are valid too.

A prefix returning false skips the operation and later prefixes that can affect it. Observation-only prefixes still run, following the [ordinary prefix rules](patching-prefix.md). Postfixes run after a completed or skipped operation, but an exception stops the remaining prefixes/postfixes.

By-value `bool __runOriginal` reports whether this operation runs. It does not report whether patches on the called method skip that method's body.

### Inner finalizers

Finalizers run after success or failure. Observe `__exception` with a void finalizer, or return an exception to preserve/replace it. Return null to suppress it.

[!code-csharp[finalizer](../examples/patching-infix.cs?name=finalizer)]

Install `RecoverPatch` with `CreateClassProcessor(...).Patch()`. `Total("3")` returns `8`; `Total("invalid")` returns `5`. Recovery covers this operation's prefixes, call, and postfixes, not earlier argument evaluation or later outer code. An exception left afterward reaches the outer handlers.

Ordinary finalizer subtleties still apply, including re-entry after a finalizer throws and which result is visible after a postfix fails. See [Limits](patching-infix-limits.md).

## Arguments and scope

Arguments are evaluated once, before the first Infix. All patches at that operation share them.

If the call takes `int value`, an Infix's `ref int value` changes what the call receives, not the variable that supplied it. If the call itself takes `ref int value`, writes reach that original storage. Use `[HarmonyOuter] ref int value` to change the outer argument explicitly.

| Injection | Default inner scope | `[HarmonyOuter]` scope |
| --- | --- | --- |
| Named argument, `__N`, `HarmonyArgument` | Call argument | Outer argument |
| `__instance` | Call receiver | Outer receiver |
| `___field` | Field on the call receiver's type | Field on the outer type |
| `__originalMethod` | Called method or constructor, including generic arguments; unavailable for fields and literals | Outer method |
| `__originalMember` | Called method, constructor, or field; unavailable for literals | Outer method |
| `__args` | Mutable call arguments | Mutable outer arguments |
| `__result`, `__resultRef` | Result or ref-return replacement | Unavailable |
| `__runOriginal` | Whether the operation runs | Unavailable |
| `__state` | This patch type's state for this call execution | Shared with this patch type's ordinary outer state |
| `__var_N` | Unavailable | Original outer local N |
| `__var_name` | Unavailable | This patch type's named local for the outer invocation |
| Harmony delegate | Resolve against the call receiver | Resolve against the outer receiver |
| `__exception` | By-value exception in an inner finalizer | Unavailable |

Inner `__state` resets for each execution, including loop iterations. Prefixes, postfixes, and finalizers from the same patch type share it at that site. Shared state and named locals must agree on their type.

For a real argument named `__result`, use `[HarmonyArgument("__result", ArgumentMode.Original)]`. This performs exact, case-sensitive argument lookup in the selected scope, bypassing all special names.

`[HarmonyOuter]` is Infix-only. Harmony never falls back to the other scope, and there is no `o_` shorthand.

## Mutable argument arrays

`object[] __args` writes back changed elements **even without `ref`**. Replacing the whole array with `ref object[]` is not supported. The receiver is not part of either array.

You can request both inner and outer arrays when the inner call has no `ref`, `out`, or `in` parameters, or the outer method has no arguments. Otherwise their write-backs might disagree about the same storage, so Harmony rejects the combination. An array combined with a typed writer or boxed copy-back can cause the same conflict.

Use typed by-value parameters to observe one scope, or direct typed refs to edit both. Distinct slots and typed observations remain valid. Do not retain `__args` for future calls. Values that cannot be boxed, such as pointers and `Span<T>`, need typed parameters.

## Supported calls and failures

Normal `call` and `callvirt` instructions keep their dispatch behavior and any patches on the callee. Concrete struct calls, including `constrained.` instance calls, are supported too.

Some operations cannot be targets: indirect calls (`calli`), constructor initialization via `call`, tail calls, varargs, field-address loads, readonly field writes, and instance fields on structs. Static fields on structs and readonly field reads are supported. Typed pointers and byref-like values work without boxing; C# `delegate*` signatures do not. See [Limits](patching-infix-limits.md) for details.

A failed installation leaves the previous wrapper intact, rather than installing part of the new patch set. Inspection and unpatching include all three inner roles.

An older Harmony that cannot read installed Infix state refuses to rebuild it. Removing the last Infix restores ordinary-patch state. This does not let a binary requiring new API types run against old-only Harmony. For duplicate assembly identities and recovery, see [loader limits](patching-infix-limits.md#assemblies-that-look-identical-to-the-loader).

Serialize updates to the same method across Harmony assemblies. Do not update it from its own prepare or transpiler callbacks.

## Generated bodies and captured variables

Iterator and async bodies usually run in a generated `MoveNext` method. Opt in with `OuterBody = InfixOuterBody.Auto` on `[HarmonyInfix]`, or `infixOuterBody = InfixOuterBody.Auto` on `HarmonyMethod`. The default patches the declared method. `Auto` leaves ordinary methods unchanged.

This selects one body, not its helper methods or whole call graph. A finalizer on a call returning a task handles that call's synchronous exception, not a later task failure.

Use `ArgumentMode.Captured` to access a live source variable stored in a compiler-generated field:

[!code-csharp[generated](../examples/patching-infix.cs?name=generated)]

`Sequence.Count(5)` now yields `5, 1`: the first call's argument was already evaluated, but changing the live `limit` affects the next iteration. Captured fields survive yields; Harmony's ordinary `__state` and `__var_name` locals reset on each `MoveNext` invocation.

Captured lookup is Infix-only, explicit, and case-sensitive. It uses the inner generated receiver or closure arguments; add `[HarmonyOuter]` for variables in the outer generated body. Missing, ambiguous, or optimized-away variables cannot be recovered.

For explicit targets, use `AccessTools.StateMachineMoveNext`, `AccessTools.LocalFunction`, or `AccessTools.Lambdas`. Lambda order is not a stable identity across builds. Direct inspection and unpatching use the resolved generated method; processor and owner-wide unpatching find it automatically.

## Keep patch-owned values across await and yield

Use `[HarmonyOuter, HarmonyArgument("name", ArgumentMode.Persistent)]` for a value that belongs to the whole async call or enumeration. It starts at `default`, survives suspensions, and is separate for concurrent calls and separate enumerators. The name is scoped to the actual patch declaring type. Prefixes, postfixes and finalizers in that type can share it; all bindings must agree on its type and lifetime.

This patch makes `Sequence.Count(3)` yield `1, 2, 3`. Enumerating again starts at `1`:

[!code-csharp[persistent](../examples/patching-infix.cs?name=persistent)]

The same binding works in an async method selected with `OuterBody = InfixOuterBody.Auto`, including calls on either side of an `await`. Use `ref` or `out` to replace a slot's value and a value parameter to read it. Use several names for several values. Ordinary synchronous methods keep the value for one invocation.

Harmony releases its stored references when the execution completes, faults, finishes cancellation, or the iterator is disposed. Removing the last persistent patch from a body also releases its saved state. This does not call `Dispose` on objects you store. See [persistent-state limits](patching-infix-limits.md#persistent-state-follows-one-generated-execution) for supported compiler protocols, cleanup and live-update behavior.

## Optional patch-body inlining and authoring recipes

`[HarmonyInline]` asks Harmony to copy a small patch body into the Infix wrapper. Unsupported bodies keep a normal call. Enable patch debugging or `Harmony.DEBUG` to see why it falls back.

Inlining can change stack traces and is not a promise of faster code. Measure it. If you later change Harmony patches on the callback itself, rebuild the outer method to refresh it.

See [Recipes](patching-infix-authoring.md) for owner groups and `CodeMatcher` examples. Public `InlineSignature` helps transpilers inspect `calli` signatures; it does not make function pointers Infix targets.
