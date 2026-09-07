# Infix: implementation specification

> **Implemented, unreleased specification.** This core contract covers binding, ordering, identity and installation. Read it with the [operation-target addendum](INFIX-OPERATIONS-ADDENDUM.md) for fields, construction, literals and indirect-call metadata, and the [feature-completion contract](INFIX-FEATURE-COMPLETION.md) for inner finalizers, generated-body selection, captured variables and optional inlining. See [validation status](../docs/infix/README.md) for executed checks and runtime boundaries.

## 1. The feature in one page

An **Infix** applies prefixes, postfixes or finalizers to a selected operation inside another method. The **outer method** contains the operation. For a method call, the **inner method** is the method being called. A **site** is one matching instruction in the outer method. Calls are the core example below; property accessors, field reads/writes, construction and literal loads use the same per-site rules within their documented boundaries.

The rule is ordinary Harmony behavior at the selected call. For two prefixes and two void postfixes with high and low priorities, without additional ordering constraints or skipping:

```text
evaluate receiver and arguments once
high-priority prefix
low-priority prefix
original call
high-priority void postfix
low-priority void postfix
continue the outer method
```

Prefixes, postfixes and finalizers are independent patches, not fixed pairs. Sort each role using Harmony's existing priority, before/after, and registration-index rules. Preserve its prefix skip policy and its separate later phase for postfixes that return a replacement result. Exact and generic-family selectors feed these same lists at a site; neither gets special precedence. The example above has no finalizers; when present, they use ordinary finalizer behavior.

Public API:

- `[HarmonyInfix]` selects the operation and its occurrences; existing prefix/postfix/finalizer roles select when patch code runs.
- Parameters refer to the inner call by default. `[HarmonyOuter]` selects the containing method explicitly.
- A closed target means exactly that target. An explicit generic definition means all constructions in that generic family.
- Existing `Inner*` registration and inspection APIs remain, with the corresponding inner-finalizer role. Repeated `AddInner...` calls accumulate. There is no second binder, new patch processor or selector callback API.

## 2. Declaration and registration

### Attribute form

```csharp
[HarmonyPatch(typeof(Outer), nameof(Outer.Run))]
static class DecidePatch
{
    [HarmonyInfix(typeof(Helper), nameof(Helper.Decide), typeof(string), Positions = new int[] { 2 })]
    [HarmonyPrefix]
    static bool Before(ref string value, ref bool __result, [HarmonyOuter] int mode)
    {
        if (mode == 0)
        {
            __result = false;
            return false;
        }
        value += ".";
        return true;
    }

    [HarmonyInfix(typeof(Helper), nameof(Helper.Decide), typeof(string), Positions = new int[] { 2 })]
    [HarmonyPostfix]
    static void After(ref bool __result) { }
}
```

Core call-selector API shape; constructor bodies and validation are omitted here. Extended selectors and body-selection options are specified in the linked contracts:

```csharp
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class HarmonyInfix : HarmonyAttribute
{
    public int[] Positions { get; set; } = [];
    public HarmonyInfix(Type declaringType, string methodName) { }
    public HarmonyInfix(Type declaringType, string methodName, params Type[] argumentTypes) { }
    public HarmonyInfix(Type declaringType, string methodName, Type[] argumentTypes, ArgumentType[] argumentVariations) { }
}

[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = true)]
public sealed class HarmonyOuter : Attribute { }

// HarmonyMethod field:
public InnerMethod innerMethod;
```

Reuse Harmony's existing argument-variation transformation for `ref`, `out`, and pointer overload lookup. Accessors can use their actual method names, such as `get_Value`, or the property selector. Constructor targets and `OuterBody.Auto` use the explicit forms in the linked contracts. There are no string-type-name or target-factory overloads.

Select the outer method on the patch class or with `TargetMethod`/`TargetMethods`. A method-level `[HarmonyPatch]` on an Infix method is rejected: older Harmony can merge it over the declaration safeguard described in section 8. Different outer targets use separate patch classes or manual registration. Priority and before/after annotations remain supported.

For class discovery, `HarmonyInfix` requires exactly one prefix, postfix or finalizer role, either by the ordinary attribute or by the supported `Prefix`/`Postfix`/`Finalizer` and `InnerPrefix`/`InnerPostfix`/`InnerFinalizer` naming conventions. Normalize equivalent names and attributes to one role; reject genuinely conflicting roles, and combinations with transpiler or reverse-patch roles. An inner role without a target is an error. Keep normal declared-method discovery; `Inherited = true` does not introduce inherited patch-method scanning.

Infix patch methods must be static and nongeneric, on nongeneric patch types with stable metadata. No automatic specialization or Infix patch factories are supported. A factory has ordinary Harmony's complete signature: a static method returning `MethodInfo` or `DynamicMethod` with exactly one `MethodBase` parameter. The return type alone is not enough: `MethodInfo After(MethodInfo value)` and `DynamicMethod After(DynamicMethod value)` are valid passthrough postfixes for matching results. These restrictions preserve serializable patch identity; call targets may be generic.

Discover the two new attributes by exact full type name, as Harmony already does for cross-assembly annotations. Keep inner-target constructor data out of the `HarmonyMethod` fields that select the outer method. New discovery recognizes the declaration and clears its old-engine marker, but resolves the target only after the applicable class and per-original `HarmonyPrepare` calls accept the patch job. The patch `MethodInfo` retains the attribute; re-read that declaration when resolving it instead of adding persistent unresolved-target state.

Missing or ambiguous inner methods therefore do not break a class whose prepare returns false. Once accepted, resolution is strict. `HarmonyPrepare` can skip an outer-method job; it cannot inspect the final output of all transpilers or skip just one missing Infix in that job. There is no optional-call flag in this version.

### Manual form and existing API

```csharp
var prefix = new HarmonyMethod(prefixMethod)
{
    innerMethod = new InnerMethod(calledMethod, 2)
};
harmony.CreateProcessor(outerMethod).AddInnerPrefix(prefix).Patch();
```

`AddInnerPrefix`, `AddInnerPostfix` and `AddInnerFinalizer` accumulate pending registrations. Their `MethodInfo` form can obtain the target from `[HarmonyInfix]`; their `HarmonyMethod` form can supply it explicitly. The manual entry point supplies the role, so another role annotation is optional but must agree if present. Share annotation recognition with `HarmonyMethod.ImportMethod()` so importing an annotated method neither resolves a missing target too early nor leaves the invalid marker active in a new engine. If explicit and attributed targets are both supplied, require equivalent targets and positions rather than choosing one silently. Calling `Patch()` again on the same processor adds its pending registrations again; it is not an update or replacement operation.

Passing target-bearing or `[HarmonyInfix]` patch metadata to ordinary `AddPrefix`, `AddPostfix`, or another ordinary role is an error. Attribute processing deliberately remaps the roles; manual registration must use its explicitly named inner operation.

`Patch(HarmonyMethod, ...)` copies a snapshot into the existing readonly `Patch.innerMethod`. Snapshot the full target and positions, not only the outer reference. Later edits to the input `InnerMethod.Method`, positions, or arrays must not change an installed patch.

Keep `InnerMethod`, `HarmonyPatchType.InnerPrefix/InnerPostfix`, `PatchInfo.innerprefixes/innerpostfixes`, and `Patches.InnerPrefixes/InnerPostfixes`. These names already shipped. The finalizer role has corresponding enum, storage and inspection members. Do not add aliases or enlarge `Harmony.Patch()` merely to rename the feature consistently.

## 3. Calls, families, and positions

The supplied `MethodInfo` defines the selector:

| Supplied target | Calls selected |
| --- | --- |
| Closed method on a closed type | Only that exact construction. |
| Method on a generic type definition | That member on all constructed declaring types; any supplied closed method arguments stay exact. |
| Generic method definition on a closed type | All method constructions on that exact declaring-type construction. |
| Generic method definition on a generic type definition | All constructions of both dimensions. |

For example, `Container<int>.Use(int)` does not select `Container<string>.Use(string)`. The `Use(T)` member obtained from `Container<>` selects both. A definition is an explicit request for broad matching, not permission to discard other closed arguments.

Reject arbitrary partially open targets, including closed constructions that contain unrelated free generic parameters. Attribute lookup must be unambiguous. If an overloaded generic definition cannot be expressed with the small attribute constructor set, use manual registration with its `MethodInfo`. No placeholder-type language or new factory API is needed.

Match the member encoded in the instruction, not the runtime override chosen by virtual dispatch. Preserve the actual matched operand for parameter names, receiver and result types, `__originalMethod`, and binding. A family patch can request a common compatible type such as by-value `object`; it must bind successfully at every selected construction. One incompatible site fails that outer-method rebuild, not just that site. Open operands whose storage cannot be emitted safely are rejected; family selection does not expand Harmony's support for patching open generic outer methods.

Positions keep the existing contract:

- Empty means every matching occurrence. Zero is invalid; null is invalid.
- Positive values count from one; negative values count back from the end (`-1` is last).
- Duplicates, including two positions resolving to the same site, wrap that site only once for that record.
- Every requested position must resolve. No matches is an error, including for an empty positions array.

Count after all ordinary transpilers, independently for each selector, in instruction order. All constructions of a family share one count; matching `call` and `callvirt` instructions share it too. Resolve signed positions without overflowing on `int.MinValue`. Validate mutable input again at registration.

## 4. Ordinary Harmony execution at a call site

### Independent patch roles

First resolve all selectors and positions against the same finalized, post-transpiler body. Then group matches by physical instruction index. Generated Infix instructions are never scanned as new targets.

At each site, collect every matching prefix, postfix and finalizer into its own role list. A selector and its positions decide only where its patch participates. Role counts, targets, priorities, and before/after sets are independent. Multiple patches of one role from one class are valid; there is no pair key, one-per-half restriction, or requirement for matching metadata.

Sort each list with the existing `PatchSorter`, retaining the records' normal registration indexes. Higher priorities normally execute earlier, explicit before/after dependencies use their existing meaning, and ties use the current priority/index comparer. Preserve dependency-cycle handling and diagnostics. Do not add identity-based tie order or interpret successive registration as wrapping outside previously installed patches.

Execute prefixes in their sorted order. Execute postfixes exactly as ordinary Harmony does: first the void postfixes in their relative sorted order, then the passthrough postfixes in their relative sorted order. A **passthrough postfix** returns the replacement result and receives the previous result through its first parameter. Sort the postfix list once before applying those existing phases; do not reverse it or independently redefine its phases. Priority does not move a passthrough postfix ahead of the void-postfix phase.

Thus two methods that sort as 1 then 2 on both sides give `12x12` for void postfixes, not `12x21`. Before/after annotations on a postfix refer to that postfix's execution, not to another method's prefix. This follows [ordinary sorting](../Harmony/Internal/PatchFunctions.cs) and [postfix phase execution](../Harmony/Internal/MethodCreator.cs).

Preserve distinct registered records, including reuse of a patch method under different owners or selectors. Deduplicate overlapping positions within a record, not independent registrations. Use record-occurrence identity during Infix dependency handling without changing the comparer, tie policy or public `Patch` equality. Do not introduce synthetic records.

### One argument set for the selected call

Evaluate the original receiver and argument expressions exactly once before any Infix prefix. Capture them into one set of typed site storage. All matching prefixes and postfixes bind to that same storage; do not make per-patch copies. An earlier prefix's argument changes are visible to later prefixes, the call, and subsequent postfixes.

Example: a high-priority prefix changes the captured `x` from 1 to 2. A later prefix and a postfix see 2. If the callee takes `int x`, its own reassignment of x does not replace the captured slot. If it takes `ref int x`, callee writes reach the shared referenced storage. A **managed pointer** here is the actual storage reference used by `ref`, `out`, `in`, and an unboxed struct receiver. Referenced objects remain shared even when the reference itself is passed by value.

Changing an Infix's `ref int x` when the callee takes `int x` changes the captured call argument, not the outer expression that produced it. `[HarmonyOuter] ref int x` explicitly changes the outer argument slot. There is no expression-origin tracking or inferred write-back to fields, properties, or locals.

### Skip, result, and exceptions

A prefix returns `void` or `bool`. Use the existing `AffectsOriginal` classification and prefix scheduling. Once a prefix returns false, skip the selected call and later prefixes that Harmony classifies as affecting it. Prefixes exempt from that guard still run. Do not substitute a new test based only on the presence of `ref` or a bool return, and do not skip a postfix because some prefix did not run. On normal completion of the prefix phase or call, all postfixes run in their normal phases, including after the call was skipped.

There is one result for the selected call, with normal Harmony result and passthrough behavior. It starts at the normal default where a prefix or skipping can expose it; prefixes can modify it; executing the call supplies its return value; and postfixes can transform the result. A skipped call leaves the default or prefix-provided result for postfix processing. Void calls have no result injections. Reuse ordinary result threading, rather than inventing another chain of result locals. An Infix passthrough postfix's return and first-parameter types must exactly match the actual selected call's return type. Its first parameter is always the previous result, not an injection: ignore its name and injection annotations during both storage setup and emission.

By-value `bool __runOriginal` observes the site-local run flag, initially true. After a prefix skips, later prefixes that still run and all postfixes observe false. It reports whether the selected call instruction executes, not whether the callee's own prefixes allow its body to run. It is read-only; skip by returning false from a prefix.

For ref returns, preserve the existing safe dummy-reference and `__resultRef` replacement rules at the site. Create a default reference when a prefix can expose a pre-call result or skip the call, or when an inner finalizer can suppress a failure before a result exists; a postfix-only site can use the returned address directly. Reject a demanded default that its element type cannot represent safely. Reset demanded result, run flag, ref-replacement temporary, and state on every site execution, including loop iterations. There are no per-patch default-result allocations.

A prefix, call, or postfix exception stops normal site execution. Remaining postfixes do not run: they are not finalizers. Inner finalizers, when registered, can observe, replace or suppress that failure using the ordinary finalizer contract described in the completion contract. An escaping exception reaches the existing surrounding outer-method handlers and ordinary Harmony finalizers. Re-emit the original call instruction so callee patches, virtual dispatch, and null checking stay intact. A prefix may replace a null receiver or skip the call before its `callvirt` null check.

## 5. Injection and state

Use Harmony's existing argument conversions and exact-name behavior, with an explicitly selected source context. Do not implement a second interpretation of patch parameter names.

| Requested value | Default inner scope | `[HarmonyOuter]` scope |
| --- | --- | --- |
| Named argument, `__N`, `HarmonyArgument` | Shared captured call arguments | Outer argument slots |
| `__instance` | Captured call receiver | Outer receiver |
| `___field` | Field on the effective called receiver type | Field on the outer declaring type |
| `__originalMethod` | Actual matched `MethodInfo`, including its construction | Outer original `MethodBase` |
| `__args` | Current call arguments | Current outer arguments at this site |
| `__result`, `__resultRef` | Site result/replacement | Rejected: outer result is not available here |
| `__runOriginal` | Site run flag, as for an ordinary patched method | Rejected: the outer body is already running |
| `__state` | This patch type and site execution | This patch type and outer invocation |
| `__exception` | Site exception in an inner finalizer | Rejected: not an outer finalizer |
| Harmony delegate | Resolve against inner context/receiver | Resolve against outer context/receiver |
| `__var_N` | Rejected | Original outer local slot N |
| `__var_name` | Rejected | Named synthetic local for this patch type and outer invocation |

`[HarmonyOuter]` is valid only on Infix parameters. Missing names and invalid injections fail in the selected scope; there is no fallback to the other method. Do not implement the historical `o_` shorthand: there is one explicit scope marker, without additional underscore rules.

### Preserve exact argument names

`ArgumentMode.Original` preserves exact argument-name binding:

```csharp
static void Before(
    [HarmonyArgument("__result", ArgumentMode.Original)] int innerArgument,
    [HarmonyOuter, HarmonyArgument("__state", ArgumentMode.Original)] string outerArgument) { }
```

These are real arguments literally named `__result` and `__state`, not special injections. Scope selection supplies the method context; the existing exact-mode path then performs direct, case-sensitive name lookup. It bypasses aliases, special names, fields, numeric forms, generated locals, and delegate fallback. A missing exact name is an error. Default `[HarmonyArgument("__result")]` still has its existing special-name behavior.

Keep the `InjectedParameter` name/annotation cache keyed by patch method. Bind argument indexes, concrete types, conversions and destinations separately for each actual outer method and call site.

### Receiver and lifetime rules

For a static context, a reference-typed by-value `__instance` receives null as in normal Harmony. By-ref or value-typed instance injection is invalid when no receiver exists. Instance fields need a receiver; static fields do not.

For `constrained. T; callvirt`, use concrete T as the effective receiver type and retain the managed pointer. Direct struct instance calls likewise preserve their address. Box only where the existing requested conversion requires it; reject an unresolved or incompatible receiver binding.

Inner `__state` follows ordinary Harmony's patch-declaring-type grouping, with a separate lifetime for each site execution. Prefixes, postfixes and finalizers in the same patch type share it when they participate at that site, without becoming a fixed pair. An exact selector and a family selector in the same class do not create separate state slots. Use separate patch types when separate state is needed. All declarations sharing a slot must agree on its element type. Reset it at each site execution; a patch observes default until an earlier executed patch writes it. Different sites and recursive invocations are isolated.

Outer `__state` intentionally shares normal outer patch state for the same patch declaring type. `[HarmonyOuter] __var_name` similarly shares a default-initialized named slot across that type's selected sites for one outer invocation. Conflicting element types fail. `[HarmonyOuter] __var_N` uses the original body's local numbering, not locals introduced by transpilers or Harmony. Exact-mode real arguments that look like these names must not request any special storage.

### Mutable argument arrays: pay only for the applicable case

`object[] __args` permits element writes such as `__args[0] = value`. Reject `ref object[]`, which would replace the whole array. Arrays exclude the receiver. Values unrepresentable in `object[]`, such as byref-like structs or pointers, reject that array injection, not an otherwise valid typed Infix.

Use at most one array per requested scope per site execution. Share it between that site's receiving patches, not across later loop iterations or invocations. Allocate it on the first receiving patch that actually runs; a postfix-only request is first prepared after the call or prefix skip. Restore immediately after each receiving patch, before the next patch or call. An unrequested scope needs no array, and a skipped receiving prefix does not force allocation. A demanded array local can start null with an allocation check at its receiving calls; do not assume that an earlier conditional prefix initialized it.

Choose refresh behavior separately for each requested scope during wrapper generation:

| Possible storage changes | Required work |
| --- | --- |
| Only one receiving patch | Fill once when that patch runs. |
| By-value slots, with no other writer between receiving patches | Initial fill and immediate element write-back keep the array current. No refill. |
| Another patch can write represented slots between receiving patches | Refill before receiving patches; earlier typed writes must be visible and must not be overwritten by stale elements. |
| The array includes managed-pointer arguments | Refill before each receiving patch. The call or another patch may change the storage, and aliases within a previous array restoration can leave some entries stale. |
| Outer array, with inner managed-pointer operands that may reach outer argument storage | Refill before receiving patches when such intervening work can change that storage. Include unboxed struct receivers, not only parameters. |

Use a conservative per-scope decision from the actual bindings and scheduled prefix/call/postfix sequence. If a source-changing operation can occur between that scope's receiving patches, the simple implementation may refresh that scope at every receiving patch. Otherwise it needs only its initial fill. Mutating an array and immediately restoring distinct by-value slots does not itself require a later refill. A skipped call does not prove freshness: an earlier executed prefix may already have changed arguments. In particular, a skipped array-receiving prefix must not suppress a refresh needed by a later postfix. No runtime dirty flags, shadow arrays, address comparisons, or general patch-body analysis are needed. The common observation-only, by-value case keeps the cheap path.

Initialization is not refilling. If an inner `__args` request exposes `out` arguments, default that storage once at physical-site entry, before any prefix can write it, matching Harmony's established pre-call array preparation contract. Do not repeat initialization for another prefix or a postfix. Outer output initialization, when demanded by outer arrays, belongs at outer-method entry and must be shared with existing normal setup. Array allocation itself remains lazy. This gives a defined output view when a prefix skips the callee. Test aliased output arguments explicitly; preparing a later patch must never erase an earlier prefix's or callee's output.

Two scopes can be requested in one patch, for example:

```csharp
static void Before(object[] __args,
    [HarmonyOuter, HarmonyArgument("__args")] object[] outerArgs) { }
```

Allow this with either disjointness proof: the inner signature has no managed-pointer parameters, or the outer method has no arguments. In the first case, inner element replacement writes captured locals and outer replacement writes outer slots; references to the same object do not make those slots identical. A managed-pointer receiver is not an inner-array element and does not invalidate this proof.

If inner parameters include managed pointers and the outer method has arguments, the two arrays may write the same storage. Reject that dual request and suggest typed by-value observations for one scope, or typed refs instead of arrays when both scopes must write. Absence of `ref` on the patch's array parameter is not a read-only guarantee. An explicit read-only array API is outside this version. For accepted dual arrays, restore inner then outer; their proven disjointness, not that order or patch-parameter order, makes the result safe.

Also check separately requested writable views within one patch: an array plus a typed writer to possibly overlapping storage, or a boxed copy-back competing with another writer, can lose writes after the patch returns. Reject only those possibly overlapping combinations. By-value observations, proven distinct slots, and direct typed refs sharing real storage without delayed copies remain valid. These checks use requested bindings; there is no general alias-analysis subsystem. **Aliasing** here simply means two paths reaching the same storage.

Inside a single array, preserve normal Harmony's argument-index restoration order, including when two ref arguments alias. If writes through aliases must follow statement order in the patch body, use typed refs. Array/typed view restrictions do not promise to analyze arbitrary side effects or references retained by user code. Treat injected arrays as borrowed during their receiving patch, not as persistent access to future calls.

## 6. Instruction generation and shared binding

Keep one binder. Separate `EmitCallParameter()`'s conversion rules from its assumption that sources are outer argument slots. A small storage descriptor supplies load-value, load-address, and store operations for an argument slot or local. A method context supplies the selected method, receiver, arguments, and special/state storage. Normal patches use the outer context; all matching Infixes at a site share its inner context, captured locals, and managed-pointer operands.

Scope demand checks too: an inner `__args` or `__state` must not accidentally allocate the same-named outer storage through global `AnyFixHas()`/`WithFixes()` checks. Preserve reverse-patch source/original distinctions. Reuse normal prefix scheduling, both postfix phases and finalizer control flow against the selected context, as well as shared parameter emission and cleanup. Inner finalizers use the typed helper boundary in the completion contract. Do not silently change ordinary boxing/copy-back behavior.

Construct and validate every selected site before installation:

1. Materialize the post-transpiler instructions as indexed occurrences, cloning each occurrence's labels/block lists. Repeated references to one `CodeInstruction` object are still different occurrences.
2. Resolve selectors and positions; collect all records by physical instruction index; sort matching prefixes, postfixes and finalizers independently.
3. Resolve the complete supported prefix/call unit, actual receiver/parameter/return types, shared site storage, and each patch's binding plan.
4. Emit one block per site. Capture original operands in reverse stack order, initialize demanded site state, and execute prefixes under the ordinary skip rules. If the run flag remains true, reload operands in declaration order and re-emit the original call.
5. Execute the ordinary void and passthrough postfix phases, performing demanded array refresh and shared patch-call cleanup. When finalizers are present, run this pipeline inside its typed helper with ordinary finalization. Leave exactly the original operation's stack effect.

Use inline generated IL for sites without finalizers, and one typed helper per site requiring finalization. Do not use runtime delegates or separately detoured helpers per patch. Values below the call operands remain on the caller's stack. A zero-argument static void call still needs a real first/last instruction anchor when carrying labels or region markers; use a `nop` where necessary.

Support ordinary `call`/`callvirt` with stable `MethodInfo` operands, including static/reference/struct instances and concrete `constrained. T; callvirt` instance dispatch. Typed pointer/byref-like arguments or returns are allowed only where the existing binder and runtime can emit the requested conversion; `object` boxing is not universally available.

The bundled MonoMod signature importer cannot represent C# function-pointer types (`delegate*`) correctly. Reject function-pointer-containing selected call or patch signatures, including by-reference forms, with a clear error before installation. Native pointers remain supported. Replacing the underlying importer is outside this feature.

Reject selected constructor-initialization calls using `call`, `calli`, varargs, `tail.`, malformed/unsupported call prefixes, constrained static-interface calls, and unresolved open storage. Construction using `newobj` and other extended targets follow the operation-target contract. These exclusions are about the selected site, not unrelated instructions in the outer body. Generated outer bodies can be selected explicitly or through the opt-in `OuterBody.Auto` behavior in the completion contract.

Keep the complete generated block in the same surrounding exception region as the original call:

- labels and opening-region markers attach to its first instruction;
- closing-region markers attach to its last instruction;
- the re-emitted prefix and call do not duplicate that metadata.

Respect existing marker ordering when a boundary carries multiple markers. Cover try, catch, filter, finally, fault, and nested regions, not only a call in the middle of a try block. A branch may enter the start of an absorbed `constrained.`/call unit, but not enter only its call while bypassing the prefix. Reject such incompatible entry boundaries. Duplicate label definitions remain invalid IL; repeated instruction object references alone are not an error.

## 7. Durable target identity

Shared patch state must remember the same selector after another compatible Harmony assembly reads it. A metadata token identifies a member definition, not `Container<int>` versus `Container<string>`. Store those closed constructions explicitly and recursively.

An Infix patch method's existing module/token identity must also identify exactly one loaded module. Check uniqueness for both cold resolution and cached candidates before rebuilding; a cached `MethodInfo` cannot make a later reader's ambiguous identity safe. Reuse the target module resolver, retain ordinary patch lookup behavior, and allow owner removal before validating survivors.

`InnerMethod` stores this private serialized data with the same meanings in both backends:

| Field | Meaning |
| --- | --- |
| `identityVersion` | `1` for this complete identity format. |
| `moduleGUID`, `methodToken` | Existing module build identifier (MVID) and method-definition token. |
| `targetKind` | `0` exact; `1` declaring-type family; `2` method family; `3` both families. |
| `declaringTypeArguments` | Canonical closed-type identities in declaring-type argument order, empty for a type family or nongeneric type. |
| `methodArguments` | Canonical closed-type identities in method argument order, empty for a method family or nongeneric method. |
| `positions` | Existing occurrence selection, independently validated. |

The resolved member definition already supplies declaring-type identity and generic arities; do not persist duplicate tokens/counts for them. A family flag requires an actual generic definition dimension. A closed dimension requires exactly that definition's argument count. Missing fields in version 1, null lists, wrong counts, unknown kinds, or free generic parameters are invalid, not implicit wildcards. Store the new kind as nullable and leave absent new arrays null so BinaryFormatter's field defaults cannot disguise missing data as the valid exact/empty case.

Use canonical strings for closed type arguments so the same data works in JSON and BinaryFormatter without another family of serializable node classes. The grammar is small and recursive:

```text
definition = D(mvid;token)
type       = definition                 // only a nongeneric named type here
           | G(definition;type;...)      // fully constructed generic type
           | V(type)                    // vector array: T[]
           | A(rank;type)               // ranked array, including rank-one T[*]
```

`mvid` is the canonical lower-case GUID `D` format; tokens and positive ranks are invariant decimal integers; no whitespace or optional spellings. Parse complete input and validate tokens against the indicated modules. `G` must supply the definition's full ordered argument list, including enclosing-type arguments for nested types. `V` and `A(1;...)` remain different. Generic arguments cannot be pointer, by-ref, or function-pointer types, so do not add unused grammar nodes for those call-signature forms.

Resolve the method definition, reconstruct its closed declaring type when required, obtain that exact member on that type, then construct the method when required. `InnerMethod.Method` must return that exact method or the explicitly requested definition form after its runtime cache has been cleared. Use the same canonical selector for matching, equality, and persistence; positions are not part of selector identity. Selector identity does not define execution order or a prefix/postfix pair.

Do not fall back to names, discard arguments, or resolve an ambiguous module to the first candidate. Reuse loaded-module lookup where unambiguous; conflicting loaded copies with no stable distinction in this format must fail explicitly. This is in-process shared-state identity, not a promise to relocate selectors across different builds of target assemblies.

An old identity with no version contains too little information for a generic target. Recognize that legacy shape only when the other new identity fields are absent too. A surviving nongeneric legacy target can be normalized when its token uniquely determines the complete method. A generic or targetless legacy entry cannot be executed safely; allow its removal as described next. Both serializers must round-trip a cold identity, not merely return a cached `MethodInfo` from the writer.

## 8. Compatibility and safe rebuilds

### Old Harmony must not mistake an Infix for an outer patch

When assemblies are loaded side by side, old Harmony recognizes `[HarmonyPrefix]` but ignores fields it does not know. Without protection, it can install an Infix as an ordinary outer prefix.

`HarmonyInfix` therefore sets its inherited `HarmonyMethod.methodType` to the permanently reserved invalid value `(MethodType)int.MinValue`. A new engine recognizes the actual attribute and removes the marker before outer-target merging; an old engine retains it and fails target selection. Keep inner-target data separate from those outer-selector fields. The restriction on method-level `[HarmonyPatch]` prevents supported declarations from overwriting this marker. Test class targets, `TargetMethod(s)`, role-by-name, attribute order, and all supported priority/dependency annotations against actual old assemblies.

This protects supported attribute discovery. It cannot prevent an old caller deliberately extracting the method and manually forcing it into ordinary `AddPrefix`; that caller has explicitly chosen the wrong old API. New manual entry points reject that mistake. When only old Harmony is loadable for a binary using the new API, missing-type/member failures are the expected boundary; there is no compatible fallback that silently changes its role.

### Active Infix state must stop old readers before user patch code runs

For a method with no inner records, preserve legacy serialization, including byte-for-byte no-Infix JSON output. For valid active Infix state, prefix the complete payload with:

```text
ASCII "HARMONY-INFIX\0"
format version byte: minimum required version (1, 2 or 3)
serializer byte: 1 = JSON, 2 = BinaryFormatter
payload for that serializer
```

Version 1 covers method-only Infixes; version 2 adds extended targets and `__originalMember`; version 3 adds inner finalizers and captured-variable binding. Derive the minimum version from the surviving records and downgrade when demanding records are removed. The linked contracts define each extension's payload and declaration safeguards.

The leading byte is invalid for the old JSON and BinaryFormatter entry formats. An old engine fails while reading state, before it can run transpilers or rebuild without Infixes. New readers check the full header, version, and available backend before decoding. Unknown or truncated headers and unavailable backends fail explicitly; do not attempt a second backend or treat malformed versioned data as legacy data.

Remove the envelope after the last Infix is removed. Keep using the existing shared-state dictionary and its current layout/version. No guard transpiler, reserved patch owner, parallel target arrays, or side dictionary is required. Old inspection of a method with active Infixes also fails: returning a partial view would let old code make decisions from missing patches.

`Patch` JSON includes `innerMethod` only for Infix records. Read `Patch` and `InnerMethod` properties by name, tolerate property order, skip unknown noncritical properties, and reject duplicate or missing identity-defining properties. Do not reorder or add properties in ordinary patch JSON output.

For a version-1 JSON envelope, require all six role arrays and `VersionCount` exactly once with their expected value types. Do not let duplicate top-level fields replace active arrays or missing fields normalize a versioned payload to empty state. Any envelope, in either backend, must contain at least one inner record. Keep absent-field normalization for actual unenveloped legacy payloads.

BinaryFormatter's type binder remaps `InnerMethod` into the reading Harmony assembly. Retain formatter settings and mark new identity fields optional where version tolerance requires it; validate semantic completeness separately. Normalize absent legacy inner arrays to empty. Missing fields do not always throw: [.NET's formatter source](https://raw.githubusercontent.com/dotnet/runtime/v8.0.0/src/libraries/System.Runtime.Serialization.Formatters/src/System/Runtime/Serialization/Formatters/Binary/BinaryObjectInfo.cs) conditions that check on assembly-format mode. Actual old-assembly tests, not a blanket claim about `OptionalField`, decide compatibility.

### Removal must work even when an old entry lacks its target

Structurally readable old entries without targets must remain inspectable and removable. Ordinary unpatching removes requested records before validating survivors. If an invalid entry remains, identify the owner/method to remove and refuse the rebuild. Do not guess, silently delete or add a recovery API. Syntactically corrupt input remains a read error.

The same rule applies to a complete stored target whose assembly is missing or has become ambiguous after another copy was loaded. Reading validates the identity's format without requiring it to resolve. Removal filters the requested records first; every survivor must then resolve exactly before serialization or any transpiler runs. Incorrect token kinds, malformed type identities and incomplete fields remain read errors. No name fallback or first-loaded-module choice is allowed.

Class unpatching, remove-by-method, remove-by-owner, remove-by-role, and remove-all paths cover all three inner roles. Route class unpatch updates through the existing patch-update lock as well. Surviving records retain their targets and snapshots. Inspection returns the stored target through the normal detached read path. No invalid surviving entry may be serialized as a successful candidate simply because it would otherwise avoid the envelope.

### Installation boundary

Within the existing patch-update lock, for one outer method:

1. Read state and apply the requested add/remove to a candidate.
2. Validate surviving metadata; calculate the candidate's next version and serialize it successfully before installation.
3. Run ordinary transpilers, build and validate every selected site, and generate the complete replacement.
4. Install its detour, then publish the already-serialized bytes and replacement mapping. Do not serialize again or increment the version twice after installation.

No predictable selector, binding, or serialization failure introduced by Infix may happen after the new detour is installed. Preserve the existing distinction between the original/source method during generation. This is not process-crash atomicity, nor rollback of side effects inside user prepare/transpiler code. A class targeting several outer methods still has Harmony's normal job-by-job behavior.

The existing lock is per Harmony assembly and is reentrant. Hosts must serialize updates to the same original across assemblies and must not reenter that original's update from prepare/transpiler callbacks. Otherwise an operation holding an earlier candidate can overwrite a later nested/concurrent update, for ordinary patches as well as Infixes. The envelope guards operations that read already-published Infix state; it cannot intercept an old operation that consumed legacy bytes before activation. Keep a deterministic compatibility diagnostic for that inherited race, separate from the sequential no-drop tests. Do not add a cross-version locking protocol to this feature.

## 9. Maintenance boundaries

The implementation uses `MethodCreator*` for shared binding and emission, `Infix` for indexed selection and site assembly, existing processors and patch records for registration, and existing serializers/shared state for persistence and publication. Operation-specific selector details and generated-body/finalizer support stay in their linked contracts; no parallel patch registry or shared-state layout is needed.

Keep one storage descriptor, one context, one shared binder/cleanup path, and one site emitter. No provider interfaces, separate inner/outer injection enums, general pipeline framework, alias analyzer, or per-call target lookup. Separate an independently discovered ordinary-patch bug from this feature unless fixing it is a demonstrated prerequisite.

Errors should explain the failed action, with the outer method, owner/patch, requested target/position, actual operand/index, scope/name, and expected/actual types where relevant. For example: “Both argument arrays may write the same value because `Helper.Update(ref int)` can receive an outer argument by reference. Use a typed by-value parameter to observe one scope, or typed refs instead of the arrays to edit both.” Do not make users decode an internal record name.

## 10. Verification and documentation contract

Use generated cases where they increase coverage cheaply. Do not take a full cross-product of unrelated dimensions or build another test framework. For scheduling comparisons, ordinary patches can target a thin method whose body performs the same selected call, keeping the observation boundary equivalent. Infix cannot observe a callee's private by-value parameter reassignment after that callee returns.

| Area | Compact test method and required assertions |
| --- | --- |
| Positions | For small call counts, loop over positive/negative/all/duplicate/out-of-range selectors and integer extremes. Compare selected instruction indexes with a simple independent list model. Mix exact/family constructions and `call`/`callvirt`. |
| Scheduling parity | Apply generated sets of prefixes and postfixes to equivalent ordinary and Infix targets, then compare traces, results, state, and run flags. Enumerate high/normal/low priority, equal-priority registration permutations, before/after constraints, independent role counts, and void/passthrough mixtures. Include a high-priority passthrough postfix after a low-priority void postfix, as ordinary Harmony requires. |
| Skip and exceptions | Compare ordinary/Infix traces for each prefix returning false, later guarded versus exempt prefixes, and throws in each prefix/call/postfix. All postfixes run after a skipped call unless execution throws. Verify one shared run flag and no fixed-pair suppression. |
| Record identity | Exact plus family at one site; reused patch method under distinct owners/selectors; overlapping/different position sets; multiple prefixes/postfixes in one class; independent additions/removals and differing ordering metadata. Each selected record remains present, without selector-driven reordering. |
| Binding | Table-driven names, indexes, aliases, and collisions with `__result`, `__state`, `___field`, `__0`, and `__var_*`. Apply the unchanged ordinary exact-name tests to both scopes. Reuse the same patch method across differing outer methods and generic operands to catch wrongly cached conversion plans. |
| Typed storage | By-value call operands versus real refs, out/in, struct/reference/static receivers, fields, delegates, value/reference/ref returns, boxing and boxed copy-back, passthrough and `__resultRef`. Later prefixes and postfixes see earlier prefix writes to the shared captured slots, while callee-only by-value reassignment stays private to the callee. |
| Arrays | No array requested; prefix-only; postfix-only; multiple receivers in both phases; safe dual scopes; rejected overlapping views; accepted distinct typed writes. Swap parameter declaration order and require unchanged acceptance/results. Check callee out/ref results survive postfix preparation, output initialization happens once, and one array's aliased elements keep index-order behavior. An intervening typed prefix write must remain visible even if a later array-receiving prefix is skipped. |
| Array cost | Inspect generated IL and counters where needed: no unrequested `__args` allocation; at most one allocation per demanded scope/site execution; no refill for stable by-value observations; reuse plus refill where other patches or the call may change storage. Skipped consumers must neither allocate unnecessarily nor prevent later consumers from initializing/refilling. Include outer storage changed via a struct receiver. Count ref-return dummy arrays separately and require none where neither a pre-call result nor skipping needs a default. |
| State | Loop and recursion cases, multiple sites, exact/family sharing state in one class, separate classes isolating state, default state before any writer, multiple postfixes sharing earlier state writes, ordinary outer state bridging, named outer locals across sites, conflicting types, and scoped demand isolation. |
| Instruction boundaries | Remaining stack values below operands; zero-argument void calls; null receiver replace/skip; direct struct, boxed interface, and constrained value/reference dispatch. Calls at branches and region boundaries in try/catch/filter/finally/fault and nested combinations; repeated instruction objects versus duplicate labels. Unsupported selected forms fail, unrelated forms remain untouched. |
| Transpilers and callee patches | Calls introduced, removed, or duplicated by ordinary transpilers; positions count their final output; rebuilds do not select generated wrappers. Original callee Harmony patches and outer finalizers still execute at the specified points. |
| Recursive identity | Closed and family selectors across independent type/method dimensions; nested generics, nested declaring types, vectors, multidimensional and rank-one non-vector arrays. Clear cached `MethodInfo` before both backend round-trips and compare exact reconstructed identities and match sets. Corrupt required fields, versions, counts, kinds, and tokens; none may broaden a selector. |
| Lifecycle/failure | Attribute/manual equivalence, prepare false before missing-target resolution, rejected factory/generic patch code, snapshots, all unpatch paths, readable targetless legacy entries removable but not executable, last-Infix legacy restoration, and failure at a later site leaving the previous detour/bytes unchanged. |

Mixed-version proof uses actual released Harmony assemblies: 2.4.0.0, 2.4.1.0, 2.4.2.0, and a pre-inner-array version such as 2.3.6.0. Run compile-old/use-new and compile-new/use-old checks, side-by-side declaration discovery, legacy-to-new state reads, new-to-old active-state rejection, and two new-compatible assemblies reading each other's cold identities. Exercise JSON and BinaryFormatter where available, including missing legacy fields and unavailable-backend errors. A user transpiler with a counter must not run when old Harmony rejects active Infix state.

The supporting [compatibility test strategy](INFIX-COMPATIBILITY-TESTS.md) defines the loading arrangements, ordinary cross-engine controls, pinned artifacts, and expected failure stages. Prove which assemblies actually loaded and whether they share patch state before drawing conclusions about Infix compatibility.

Use net9/x64 for the focused edit loop. Before release, run relevant normal argument/patching/reverse-patch suites and these Infix cases on representative supported CoreCLR and Mono targets, plus API/package compatibility checks. Report runtime availability honestly: a built x64 test assembly is not an executed x64 test run.

Public documentation should contain a small working example; exact versus family selection and positions; the scope table; ordinary independent prefix/postfix ordering, skipping, and exceptions; the by-value/ref distinction; safe dual arrays and their mutable nature; exact original-name binding; supported call forms; and actionable failures. Explain the concept before internal names. Include high/low priority examples and a skip example showing later exempt prefixes and postfixes observing the site-local `__runOriginal`. Compile documentation examples through test fixtures or an existing docs-example check.

Acceptance requires these contracts, unchanged ordinary behavior, and actual mixed-version tests proving that old engines cannot silently reinterpret supported declarations or drop published Infixes within the serialized-update boundary above. Generated IL and serializer-only tests do not replace runtime proof.
