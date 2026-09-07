# Infix operation targets

**Decision, 2026-09-07.** This addendum is part of [V3](INFIX-NEW-IMPL-V3.md). It replaces V3's method-only target restrictions; its ordering, argument binding, state lifetimes, atomic installation, and compatibility requirements remain in force. The [testing strategy](../docs/infix/TESTING-STRATEGY.md) separates new coverage from runtime checks actually executed.

**Completion:** the [feature-completion contract](INFIX-FEATURE-COMPLETION.md) adds implemented, unreleased support for accumulated `AddInner...` calls, inner finalizers, automatic iterator/async body selection, and generated-code authoring support. It overrides this addendum's exclusions only where stated.

## 1. The useful generalization

An Infix surrounds one operation inside a chosen outer method. That operation consumes some values and may produce one value. Capture its inputs once, run the ordinary prefix list, optionally execute the original instruction, and run the ordinary postfix phases. Other instructions and other callers remain unchanged.

This works for more than method calls without creating a second patch engine:

| Target | Inputs, excluding receiver | Result | Selection |
| --- | --- | --- | --- |
| Method call | Declared arguments | Declared return type | Existing exact method or generic family |
| Property getter/setter | Accessor arguments | Accessor return type | Resolve the accessor, then use method selection |
| Field read | None | Field type | Field identity plus read operation |
| Field write | One argument named `value` | None | Field identity plus write operation |
| Object construction | Constructor arguments | Constructed type | Constructor identity, `newobj` only |
| Literal load | None | `string`, `int`, `long`, `float`, or `double` | Exact IL category and literal value |

Every target uses the same positions convention and independent prefix/postfix ordering. Overlapping selectors at a physical instruction join one site pipeline. Generated instructions are never selected again during that rebuild.

### Why constants are worth supporting

xylthixlm's example captures a newly created `StringBuilder`, then intercepts a distinctive string literal later in the same method to append to that builder. The literal is an insertion point, not necessarily a value to replace. This avoids guessing the original compiler's local-variable numbering.

That is a legitimate use case. It also explains the limit: a literal is identified by its value, not by the source expression that produced it. A compiler may fold, remove, or duplicate it. Selecting `1` also finds unrelated boolean and small-integer loads. There is no promise that a source-level `const` declaration survives as a selectable instruction.

Expose this explicitly, with existing occurrence positions and a no-match error. Do not claim source-expression tracking, automatic relocation, or semantic uniqueness. Applications should prefer member identities where those express the intended point equally well.

## 2. Public API and normalization

Add a compact `InnerTarget` selector and `HarmonyMethod.innerTarget`. Keep the existing `InnerMethod`, `HarmonyMethod.innerMethod`, and `Patch.innerMethod` APIs unchanged.

Manual forms:

```csharp
new InnerTarget(methodInfo)
new InnerTarget(propertyInfo, InnerTargetKind.Getter)
new InnerTarget(propertyInfo, InnerTargetKind.Setter)
new InnerTarget(fieldInfo, InnerTargetKind.FieldRead)
new InnerTarget(fieldInfo, InnerTargetKind.FieldWrite)
new InnerTarget(constructorInfo)
InnerTarget.Constant("StatsReport_FinalValue", -1)
```

Each accepts trailing signed occurrence positions. Install it with the existing `AddInnerPrefix` or `AddInnerPostfix` and a `HarmonyMethod` whose `innerTarget` is set. Keep manual and attribute registration equivalent.

Attribute forms extend `HarmonyInfix`, not a new patch role:

```csharp
[HarmonyInfix(typeof(Thing), "Value", InnerTargetKind.Getter)]
[HarmonyInfix(typeof(Thing), "count", InnerTargetKind.FieldWrite)]
[HarmonyInfix(typeof(StringBuilder), InnerTargetKind.Constructor)]
[HarmonyInfix("StatsReport_FinalValue", Positions = new[] { -1 })]
```

Existing method declarations still work unchanged. Resolve new declarations only after the applicable `Prepare` callbacks accept the patch. Reject unsupported kind/member combinations and ambiguous lookups. Indexer accessors can be selected explicitly through their `MethodInfo` when needed.

Normalize property accessors and new method selectors to the existing method representation before persistence. A patch stores exactly one target representation: `innerMethod` for calls, or `innerTarget` for extended operations. Explicit old-style, new-style, and attributed targets must agree, including occurrence positions. Snapshot caller-owned arrays before publication.

The operation kind is part of identity: selecting a field read never also selects its writes or address loads. Selecting construction never matches base-constructor or initialization calls emitted with `call`.

## 3. Binding and execution

Generalize the internal binding context from a method plus `ParameterInfo[]` to an optional member, explicit result/receiver information, and a small logical parameter description. A logical parameter has only the name, type, and out/retval flags the binder already needs. Do not manufacture fake reflection methods or emit adapter methods for fields or constants.

The existing storage abstraction continues to capture operands into typed locals, preserving real managed references. The original operation executes with those same operands. Expressions supplying the receiver and arguments execute exactly once, even when a prefix skips the operation.

- Field writes expose `value`, `__0`, and a one-element `__args`. Changing the captured value changes that store only. Reads expose an empty argument array and the loaded result.
- Field accesses have their actual receiver; static fields have none. Preserve original null-check timing unless a prefix replaces the receiver or skips the operation.
- Construction has no incoming object receiver. `__instance` is null; the newly constructed object or struct is `__result`. A prefix may supply a replacement result and skip construction. Argument evaluation has already happened, but allocation and constructor side effects have not.
- Literal loads have no receiver, arguments, or metadata member. Their result follows the same prefix/default/skip/postfix rules as any other value-producing operation.
- Keep `__originalMethod` method-only, including `ConstructorInfo` for construction. Add Infix-only `__originalMember` for the actual `MethodInfo`, `ConstructorInfo`, or `FieldInfo`. Require a compatible by-value parameter. Both metadata injections reject literal sites; `[HarmonyOuter]` still identifies the outer method there. Do not change ordinary patch-name binding or skip classification; the same patch method may participate in both execution contexts.
- Exact-name argument binding bypasses all magic names, including the new `__originalMember` name, in the selected scope.

Do not add new array rules. Reads and literals have zero inner arguments; field writes have a captured value slot, so their inner and outer arrays are disjoint. Constructor arguments with managed references retain V3's selective alias rejection. The absence of `ref` on `object[]` does not make element replacement read-only.

### Field boundaries

Support static fields using `ldsfld`/`stsfld` and instance fields on reference types using `ldfld`/`stfld`. Reject static field metadata on instance-form instructions, which consume an otherwise ignored receiver. Reads of readonly fields are valid; reject writes to readonly fields and targets for literal fields. `ldflda`/`ldsflda` produce escaping addresses, not immediate reads or writes, and are not selected. This includes source expressions such as `thing.count.ToString()` when the compiler calls through the field's address rather than loading its value.

Preserve `volatile.` and valid `unaligned.` prefixes adjacent to the original field instruction. Patches are not part of an atomic field access and do not add locking or change the memory ordering promised by that instruction.

**Reject instance fields on value types for this implementation.** A legal `ldfld` can consume an unboxed struct value or an address. Declaring a spill local as `T&` based only on the field's declaring type corrupts the value case; declaring it as `T` corrupts the address case and loses mutation semantics. No current Harmony component proves which stack form arrives at every control-flow path. This is a specific unsupported operation, not a blanket ban on structs: static struct fields, construction of structs, and existing supported struct method calls remain valid. Supporting these instance fields later requires actual stack-type analysis and both value/address receiver tests, not a heuristic based on the preceding instruction.

Keep function-pointer signature rejection before emission, including field signatures on older runtimes that erase these types in reflection. Native pointers retain their existing typed support.

### Literal identity

Accept only non-null `string`, `int`, `long`, `float`, and `double`. Do not silently coerce enum, boolean, unsigned, decimal, or arbitrary object arguments. IL does not retain those source types in literal loads; callers must select the actual IL category deliberately.

Normalize all `ldc.i4` encodings, including short and specialized forms, to the same signed integer. Preserve distinct 32/64-bit integer and single/double categories. Match strings ordinally and floating-point values by their bits, including negative zero and NaN payloads. Persist numeric data canonically without culture dependence or JSON non-finite-number shortcuts.

## 4. Capture once, use later: existing state is enough

Use inner `__state` for one site's execution. Use named `[HarmonyOuter] __var_name` slots when several operations in one outer invocation cooperate. Names are scoped to the patch declaring type; different types do not accidentally share them. Multiple names already provide multiple captured values.

The constructor postfix in the motivating example can store its `__result` into `[HarmonyOuter] out StringBuilder __var_builder`. A later literal postfix reads `[HarmonyOuter] StringBuilder __var_builder` and an outer argument. It can append to the captured builder without inspecting any original local.

Each outer invocation starts with default values, including recursive and simultaneous invocations. The later patch must handle a default when its writer did not execute. Do not introduce hidden cross-invocation state, infer that every control-flow path reaches the writer, or make prefix/postfix declarations into fixed pairs. A struct in `__state` remains the simple choice for several values belonging to one site.

## 5. Current boundaries and accepted next work

**Iterator/async redirection:** the current implementation uses explicit outer targeting (`MethodType.Enumerator`, `MethodType.Async`, or the appropriate generated method). The completion draft adds an automatic body-selection option while preserving that explicit behavior. Its arguments, receiver, and invocation lifetime differ from the factory method's; each `MoveNext` call is its own outer invocation. State that must survive across yields belongs to the iterator object or another explicitly owned location, not an Infix local.

**Inner finalizers:** accepted in the completion draft, using ordinary finalizer semantics and a typed helper only at sites that need them. That design handles pending stack values and enclosing exception handlers without adding whole-method stack analysis. Until implemented, existing outer finalizers and surrounding handlers still handle escaping Infix exceptions; an inner postfix is not exception cleanup.

**Indirect calls:** public `InlineSignature` helps transpiler authors understand `calli`; it does not make a runtime function-pointer value into a stable Infix target. Constructor initialization via `call`, `tail.`, varargs, open storage, and unsupported prefixes remain excluded.

## 6. Compatibility without a parallel protocol

Preserve existing method-only shared state and its version-1 envelope. A payload containing any extended target or demanding the new `__originalMember` binding uses version 2. Exact-name real arguments and the first passthrough-result parameter do not demand that binding. Derive this requirement from the patch records; do not store a second capability flag. Readers accept both versions and reject new-capability metadata under version 1 or without an envelope. Removing the last new-capability patch returns to version 1 if ordinary call Infixes remain; removing the last Infix returns to the ordinary legacy format.

This prevents an earlier Infix engine from ignoring a field/constructor/literal selector while rebuilding a method. It must fail while reading the version, before any user transpiler runs. Do not create guard transpilers, shadow arrays, or cross-assembly side dictionaries.

New declaration constructors must also fail loudly in earlier engines. Keep the old `HarmonyInfix.innerName` field null for new forms and put their new name/kind/value in separate declaration fields. The V3 reader already rejects the missing legacy method name. Retain the original old-Harmony rejection marker. An earlier engine must never reinterpret a field name as a method name or install the declaration as an outer patch.

For declarations using `__originalMember` on a method call, use the explicit `InnerTargetKind.Method` form to gain that same early declaration rejection. A legacy method-form declaration carrying the new parameter still fails in the previous, unreleased V3 engine, but at binding time; no format header can protect a patch that has not been published yet. Published new-capability state rejects old V3 readers before user patch code regardless of its original declaration form.

For fields and constructors, persist module ID, metadata-definition token, explicit declaring-type family flag, and recursively encoded closed generic arguments. Reuse the existing type-identity codec. Reconstruct exact closed members; never broaden a closed target to all constructions. Reject unknown versions, malformed/duplicate data, conflicting target representations, ambiguous module copies, and mismatched token kinds. The same rules apply to JSON and BinaryFormatter.

## 7. InlineSignature: useful independently

Make the existing `InlineSignature` and its existing public nested modifier representation public. Keep Cecil conversion methods nonpublic. Expose stack pop/push counts: include the function pointer and implicit receiver when present, count an explicit receiver only once, and count a non-void result once. Account for modified return types. Do not create another signature parser or change the model's existing calling-convention encoding incidentally.

Correct the parser's by-reference type representation with a real managed-ref `calli` regression, not only a constructed-object test. Prove external consumption from an assembly without friend access and exercise a read/re-emit round trip. Public visibility alone would conceal incorrect `T*` versus `T&` metadata from the consumer who requested it.

Returning `calli` instructions also need a correct emitted maximum stack depth. The current MonoMod DynamicMethod emitter omits their returned value from its calculation; .NET 5 rejects a reproduced ref-return case that newer JITs accept. Route these wrappers through the existing Cecil backend and DynamicMethod-proxy handling, with tests both with and without a dynamic prefix factory. Do not add a guessed stack allowance. Keep the reader's existing unsupported generic entries, varargs sentinels, and wrappers around nested function-pointer types documented separately from public stack counting.

## 8. Implementation and acceptance

Keep changes at four boundaries: selector normalization/persistence; indexed instruction matching; logical binding context; existing per-site emission. Use no per-kind prefix, postfix, argument-array, or state engines.

Tests must cover the user-visible combinations, not just constructors of metadata classes:

- Read/write independence; receiver and value expressions once; null repair/skip; readonly and volatile behavior; field metadata on closed generics.
- Exact/family overlap and cold reconstruction for fields and constructors; conflicting selector inputs; position snapshots; rollback after a later incompatible selected site.
- Property accessor parity, indexers, and ordinary callee patches; constructor result transformation and skipped allocation.
- Literal opcode variants, signed positions, exact categories, bit identities, no-match failure, and a real constructor-capture/literal-anchor example.
- Default-before-writer, loops, recursion, and simultaneous invocation isolation; explicit iterator targeting and its per-`MoveNext` lifetime.
- Method-only version-1 preservation; extended version-2 round trips; downgraded header rejection; earlier-engine declaration and state-read rejection before user code.
- Actual managed/unmanaged `calli`, ref arguments/results, and table-driven raw-signature parsing; ordinary patching regression coverage after binder changes.
- CI rejects missing, malformed, empty, stale, or failed test reports even when a launcher returns success; a failed early framework cannot be hidden by a later pass.

Record runtime/architecture and exact binaries for executed checks. A passing macOS CoreCLR run is not proof of Windows Framework, Mono, Unity, or mixed-loader behavior.

## Evidence behind the decisions

The capture/anchor example and feature claims come from [xylthixlm's Discord discussion](https://discord.com/channels/214523379766525963/215496692047413249/1546307390541140018). The public [Disharmony attributes](https://github.com/RossM/RimworldMods/blob/ba7d90c7da2fa8743ae230478dde910fc12ee612/Disharmony/Attributes.cs) confirm explicit literal selection and its IL-category limitations. These motivate the use cases; Harmony's implementation retains its own binding and lifecycle contracts.

The [CLI `ldfld` contract](https://learn.microsoft.com/en-us/dotnet/api/system.reflection.emit.opcodes.ldfld) permits a value-type value or address as receiver. The [`calli` contract](https://learn.microsoft.com/en-us/dotnet/api/system.reflection.emit.opcodes.calli) defines its function-pointer/argument stack consumption.
