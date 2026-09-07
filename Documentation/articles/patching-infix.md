# Infix

An Infix applies a prefix or postfix to a selected operation inside another method: a call, property access, field read/write, object construction, or literal load. The containing method is the **outer method**. For calls, the method being called is the **inner method**. Other callers and unselected instructions are unaffected.

## A working example

This patch changes calls to `Helper.Decide` made by `Outer.Run`. `value` belongs to the inner call. `[HarmonyOuter] int mode` reads the containing method's argument.

[!code-csharp[example](../examples/patching-infix.cs?name=example)]

Install the class with your existing Harmony instance:

[!code-csharp[install](../examples/patching-infix.cs?name=install)]

For `Outer.Run("hello", 1)`, the trace is:

```text
high priority
low priority: True
call: hello.
postfix: True, True
```

For `Outer.Run("hello", 0)`, the high-priority prefix returns false. The call is skipped, but the observation-only prefix and postfix still run:

```text
high priority
low priority: False
postfix: False, False
```

The examples are compiled and exercised by the test project.

## Selecting calls

Put the outer target on the patch class, or use its existing `TargetMethod` or `TargetMethods` callback. Put `[HarmonyInfix]` on each inner prefix or postfix. Do not put a method-level `[HarmonyPatch]` on that same Infix method.

For calls, the attribute accepts a declaring type, method name, optional argument types, and the usual `ArgumentType[]` variations for overloads taking `ref`, `out`, or pointers. Property accessors can use their real method names, such as `get_Value`, or the explicit forms below.

`Positions` selects occurrences after ordinary transpilers have finished:

- Omit it, or supply an empty array, to select every matching call.
- `Positions = new int[] { 2 }` selects the second call.
- `Positions = new int[] { -1 }` selects the last call.
- Repeated positions select that occurrence once for that registration. Separate patch registrations still run separately.

Zero, null, an out-of-range position, or no matching calls is an error. Each requested position must exist. A family selector counts all its matching constructions together.

### Exact targets and generic families

A closed method is exact. Selecting `Container<int>.Use` does not select `Container<string>.Use`.

A generic definition explicitly requests a family. A member obtained from `Container<>` selects that member on all constructed `Container<T>` types. A generic method definition selects all constructions of that method. The declaring-type and method dimensions are independent: opening one does not broaden the other.

The patch must bind successfully at every selected call. For example, a by-value `object value` can observe both an `int` and a `string` argument. A `ref int value` cannot edit both. Partially open targets with free parameters are rejected.

For a generic overload the attribute cannot identify unambiguously, pass its `MethodInfo` manually. There is no automatic generic specialization of patch code.

### Manual registration

Use the existing `AddInnerPrefix` or `AddInnerPostfix` operations:

[!code-csharp[manual](../examples/patching-infix.cs?name=manual)]

The `MethodInfo` overload can read `[HarmonyInfix]` from the patch method. An explicit target and an attributed target must agree, including positions. Ordinary `AddPrefix` and `AddPostfix` reject Infix metadata.

Patch methods must be static, nongeneric methods on nongeneric patch types. Dynamic patch methods and patch factories are not supported for Infix. Target and position inputs are copied when registered; changing those inputs later does not change the installed patch.

### Properties, fields, constructors, and literals

Select the operation explicitly:

```csharp
[HarmonyInfix(typeof(Thing), "Value", InnerTargetKind.Getter)]
[HarmonyInfix(typeof(Thing), "Value", InnerTargetKind.Setter)]
[HarmonyInfix(typeof(Thing), "count", InnerTargetKind.FieldRead)]
[HarmonyInfix(typeof(Thing), "count", InnerTargetKind.FieldWrite)]
[HarmonyInfix(typeof(StringBuilder), InnerTargetKind.Constructor)]
[HarmonyInfix("StatsReport_FinalValue", Positions = new[] { -1 })]
```

These are separate example declarations, each combined with its own `[HarmonyPrefix]` or `[HarmonyPostfix]`. Getter/setter argument types identify indexer parameters, excluding the setter's value parameter. Constructor argument types identify its overload; no argument types select the parameterless constructor.

For manual registration, set `HarmonyMethod.innerTarget` to `new InnerTarget(methodInfo)`, `new InnerTarget(propertyInfo, InnerTargetKind.Getter)`, `new InnerTarget(fieldInfo, InnerTargetKind.FieldWrite)`, `new InnerTarget(constructorInfo)`, or `InnerTarget.Constant("marker")`. Trailing positions work as with `InnerMethod`. Existing `innerMethod` registrations remain valid. If multiple input forms are supplied they must agree.

| Operation | Inner arguments | Inner result and receiver |
| --- | --- | --- |
| Property getter/setter | Actual accessor arguments | Same as calling its accessor method |
| Field read | None | Field value; actual instance for instance fields |
| Field write | `value` or `__0` | Void; actual instance for instance fields |
| `newobj` | Constructor arguments | New object or struct; no incoming `__instance` |
| Literal load | None | The loaded literal; no receiver |

A field-write prefix can change `ref int value` or skip the store. A field-read postfix can change `ref int __result`. Construction exposes its new value as `__result`, not `__instance`; skipping construction suppresses allocation and constructor effects, but not argument evaluation. Reads and writes are separate selectors. Property targets are ordinary accessor calls and do not bypass accessor behavior or its other Harmony patches.

`__originalMember` is an Infix-only injection. For method-call declarations that use it, prefer the explicit `[HarmonyInfix(typeof(Thing), "Method", InnerTargetKind.Method)]` form: the previous, unreleased method-only Infix engine rejects that declaration early. That engine rejects the older attribute form with this new parameter later, during binding. Published state using the new binding is always protected by the newer state format.

Literal selectors accept non-null `string`, `int`, `long`, `float`, or `double`, without coercing other source-language types. All short/special `ldc.i4` encodings match the same integer. Numeric categories remain distinct; floating-point matching preserves bits, including negative zero and NaN payloads. A distinctive literal can be useful as an insertion point even when the patch never changes its result. Common values such as `0` or `1` can match unrelated expressions. Compiler folding can remove a source constant entirely; positions describe the actual instructions after transpilers, not source lines.

### Capture at one operation, use at another

Named outer locals already support multiple captured values without depending on the original method's local numbers:

[!code-csharp[capture](../examples/patching-infix.cs?name=capture)]

Both postfixes belong to the same patch class. The first saves a constructed builder; the second uses it at a later string-literal load. Each outer invocation has its own default-initialized slot, including recursive and simultaneous invocations. Handle the default if execution may reach the reader without the writer. Use different `__var_name` names for additional captures, or a state struct for several values belonging to one site's `__state`.

## Ordering, skipping, and exceptions

Infix uses ordinary Harmony ordering. Prefixes and postfixes are independent, not matched pairs. Exact and family patches join the same lists at a selected call.

Sort each role by the existing priority, before/after, and registration rules. Higher-priority prefixes normally run first. Void postfixes run in their sorted order, followed by postfixes that return a replacement result. Those returning postfixes receive the previous result through their first parameter, as with ordinary passthrough postfixes.

An Infix passthrough postfix's return type and first parameter type must exactly match the selected call's return type. That first parameter always receives the previous result; its name and injection annotations do not change its meaning. This includes method-valued results: `MethodInfo After(MethodInfo value)` and `DynamicMethod After(DynamicMethod value)` are valid postfixes. A patch factory instead has exactly one `MethodBase` parameter and returns `MethodInfo` or `DynamicMethod`; Infix does not support that factory signature.

Without dependency overrides, high and low prefixes plus high and low void postfixes execute as:

```text
high prefix → low prefix → call → high postfix → low postfix
```

A prefix returning false skips the call and later prefixes that ordinary Harmony classifies as affecting it. Exempt prefixes still run. All postfixes run after a normally completed or skipped call. A prefix, the call, or a postfix throwing an exception stops the remaining inner pipeline. Surrounding exception handlers and ordinary outer finalizers still apply. There are no inner finalizers.

The by-value `bool __runOriginal` tells you whether this call instruction will run or was skipped. It cannot be assigned through `ref`. It does not report whether a separate Harmony prefix on the callee skipped the callee's own body.

## Arguments and scope

Receiver and argument expressions are evaluated once before the first Infix. All its patches use the same captured arguments.

If the inner method takes `int value`, an Infix's `ref int value` changes the captured value passed to that call. It does not reassign the outer argument, field, or expression that supplied it. If the inner method itself takes `ref int value`, its real storage reference is preserved, so both the Infix and callee write that storage. `[HarmonyOuter] ref int value` explicitly writes the outer argument.

| Injection | Default inner scope | `[HarmonyOuter]` scope |
| --- | --- | --- |
| Named argument, `__N`, `HarmonyArgument` | Captured call argument | Outer argument |
| `__instance` | Call receiver | Outer receiver |
| `___field` | Field on the call's effective receiver type | Field on the outer type |
| `__originalMethod` | Actual called method, including generic arguments | Outer original method |
| `__originalMember` | Actual method, constructor, or field; not available for literals | Outer original method |
| `__args` | Mutable array of call arguments | Mutable array of outer arguments |
| `__result`, `__resultRef` | Call result or ref-return replacement | Not available |
| `__runOriginal` | Call's run flag | Not available |
| `__state` | Shared by this patch type for this call execution | Shared with ordinary outer state for this patch type |
| `__var_N` | Not available | Original outer local N |
| `__var_name` | Not available | Named local shared by this patch type for the outer invocation |
| Harmony delegate | Resolve against the call receiver | Resolve against the outer receiver |
| `__exception` | Not available | Not available here |

Inner state resets on every call execution, including loop iterations. Prefixes and postfixes from the same patch type share it at that call; other sites and recursive invocations are separate. Shared state and named local declarations must agree on their type.

Use `[HarmonyArgument("__result", ArgumentMode.Original)]` when the real argument is literally named `__result`. Exact mode performs direct, case-sensitive argument lookup in the selected scope and bypasses all special injection names. The same applies to names such as `__state`, `___field`, `__0`, and `__var_name`. Without `ArgumentMode.Original`, the existing special-name behavior remains.

`[HarmonyOuter]` is only valid on Infix parameters. There is no implicit fallback to the other scope and no `o_` naming shorthand.

## Mutable argument arrays

`object[] __args` writes back element replacements even without `ref`. `ref object[]` would mean replacing the whole array and is rejected for Infix. Arrays do not include the receiver.

You may request both arrays in one patch when their destinations are disjoint: either the inner signature has no managed-pointer parameters such as `ref`, `out`, or `in`, or the outer method has no arguments. A struct receiver alone does not make the inner argument array overlap.

When the inner call can hold references to outer arguments, two arrays could write different values to the same storage after the patch returns. Harmony rejects that request. Use typed by-value parameters to observe one scope, or direct typed refs when both scopes must write. Similarly, an array and a typed writer to possibly overlapping storage, or a boxed copy-back competing with another writer, are rejected. Distinct slots and typed observations remain valid.

Only requested arrays are allocated, when a receiving patch actually runs. Receiving patches share their scope's array within that call execution. Harmony refreshes it when intervening writes can make it stale, including callee writes through refs. Do not retain an injected array as access to future calls. Values that cannot be boxed, including pointers and byref-like structs, require typed injections.

## Supported calls and failures

Infix supports ordinary `call` and `callvirt`, static and instance methods, concrete struct/reference receivers, and concrete `constrained.` instance calls. The original instruction still performs virtual dispatch and executes any patches on the callee. A prefix can replace a null receiver or skip before a `callvirt` null check. It also supports `newobj`, the listed literal loads, and static/reference-type instance fields. `__originalMethod` remains method-only; use `__originalMember` for field metadata. Neither exists for a literal, although their outer-scoped forms remain available.

Constructor initialization via `call`, `calli`, varargs, `tail.`, unsupported call prefixes, constrained static-interface calls, and unresolved open storage are not supported targets. Unrelated instructions are left alone.

Field targets preserve `volatile.` and valid `unaligned.` prefixes on the original instruction; patches add no locking or atomicity. Readonly reads are valid, but readonly writes, literal-field metadata, and field address operations (`ldflda`/`ldsflda`) are not supported targets. Static fields require `ldsfld`/`stsfld`, instance fields `ldfld`/`stfld`. Instance fields on structs are rejected: their receiver can be either a value or an address, and the current engine cannot reliably distinguish these stack forms. This does not prevent static fields on structs or existing supported struct method calls.

Async and iterator bodies need an explicitly selected generated outer method, for example an iterator target using existing `MethodType.Enumerator` or `AccessTools.EnumeratorMoveNext`. Harmony does not silently redirect an outer target. Each `MoveNext` invocation has separate outer state; named outer locals do not persist across yields.

Typed native pointers such as `int*` and byref-like values such as `Span<int>` are supported when no boxing is required. C# function-pointer signatures (`delegate*`) are not supported by the bundled signature importer and are rejected for Infix targets and patch methods, including references to those pointer types.

Invalid targets, positions, bindings, or surviving metadata fail before the replacement is installed. The previously installed patch remains active. A missing target is only resolved after the relevant `Prepare` callbacks accept the job.

If a host loads the identical target or patch assembly twice, saved Infix records cannot distinguish those copies. Harmony rejects that ambiguous operation. Different assemblies containing the same type names remain valid.

Normal inspection and unpatch APIs include inner prefixes and postfixes. Older Harmony versions cannot safely inspect or rebuild a method with active Infix state and will fail before running user transpilers. Removing its last Infix restores the legacy state format. Supported declarations also carry an old-engine rejection marker so an old engine cannot silently install an Infix as an outer patch. A binary requiring new API types cannot run against an old-only Harmony installation.

As with ordinary Harmony patches, serialize updates to the same method across different Harmony assemblies, and do not start another update to that method from its own prepare/transpiler callbacks. The old-version safeguard applies when an operation reads already-published Infix state; it cannot stop an old operation that read an earlier state and is still rebuilding it.
