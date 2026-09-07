# Infix feature completion

**Implementation contract, 2026-09-07. Implemented, not released.** This completes the [V3 specification](INFIX-NEW-IMPL-V3.md) and its [operation-target addendum](INFIX-OPERATIONS-ADDENDUM.md). It overrides their exclusions of inner finalizers and automatic state-machine targeting, and the processor's single pending slot for each inner role. Other binding, ordering, identity, and compatibility rules remain unchanged. The public [odd cases and limits chapter](../Documentation/articles/patching-infix-limits.md) describes the resulting behavior. See [validation status](../docs/infix/README.md) for executed checks; implementation does not imply every runtime lane has been validated.

## 1. The model to preserve

An Infix is ordinary Harmony patching at one selected operation inside an outer method. An **operation** is a call, field read/write, construction, or literal load. A **site** is one matching instruction, not one loop iteration.

Adding a patch adds a registration. Removing a patch removes its registrations. In either case Harmony rebuilds the outer method from its original instructions, applies the transpilers, finds the sites, and combines all surviving patches at each site. This already works, including multiple patches from the same owner. Do not replace it with one registration per owner or selector.

Prefixes, postfixes, and the new finalizers are independent lists. Use ordinary Harmony's sorting and execution rules for each role. Exact and generic-family selectors that meet at one instruction share that pipeline. There are no paired wrappers, special family priority, or extra "last capture" phase.

The capability decisions are:

| User capability | Decision |
| --- | --- |
| Calls, properties, fields, construction, literals | Already implemented within the operation addendum's boundaries. |
| Several installed patches at the same site | Already implemented. Correct the explanation, not the engine. |
| Several `AddInner...` calls before one `Patch()` | Accumulate them; section 2. |
| Inner exception observation, recovery, and cleanup | Add ordinary-style inner finalizers; section 3. |
| Several named state values | Use the existing named slots, not another state store; section 4. |
| Iterator and async body selection | Add automatic body resolution as an explicit option; section 5. |
| Local functions, lambdas, and their captured variables | Add focused method resolvers and explicit captured-variable binding; sections 4–5. |
| Several targets or every overload | Resolve an explicit method set, then register ordinary selectors; section 5. |
| Arbitrary instruction insertion/replacement | Existing transpilers and `CodeMatcher`; supply equivalent examples, not a competing rules language. |
| Runtime configuration and removable groups | Existing processors and Harmony owner IDs; document group ownership. |
| Inlining small patch bodies | Add an optional optimization with a normal-call fallback; section 6. |
| Understanding indirect calls | Public `InlineSignature` is implemented; it does not make a runtime function pointer a stable Infix target. |

This is capability coverage, not a promise to copy another library's spellings, sorting, implicit argument lookup, or experimental optimizer. Differences that affect users must be stated in examples.

## 2. Repeated `AddInner...` calls accumulate

The pending processor configuration should follow what its `Add` names suggest:

```csharp
var processor = harmony.CreateProcessor(outer);
processor.AddInnerPrefix(a);
processor.AddInnerPrefix(b);
processor.AddInnerPostfix(c);
processor.AddInnerFinalizer(d);
processor.Patch(); // Installs all four, alongside existing registrations.
```

Here `a` through `d` are `HarmonyMethod` objects with their own selectors. They may select the same site or different sites. Both overloads of every `AddInner...` method append. Keep the fluent return type.

Replace the pending inner-prefix/postfix fields with lists and add the finalizer list. Pass each list to the existing `PatchInfo` append operation once, then build and publish one replacement per actual outer method. No second registry, deduplication, or replace-by-owner rule is needed. Preserve insertion order as the input to Harmony's registration-index ordering; priority and before/after dependencies still govern execution.

One `Patch()` is one installation attempt for that method. Validate the complete candidate, including all selected sites, before replacing its working wrapper or publishing state. If `b` is invalid, `a` must not become partially installed. Existing registrations remain intact.

Match ordinary processor reuse: pending configuration remains after `Patch()`. Calling it again adds those registrations again; it is not an update operation. Repeating an identical method is allowed. Normal removal by patch method or owner retains its existing removal scope, rather than gaining a per-add handle.

A null `HarmonyMethod` contributes no patch and must not clear earlier additions. A null passed to the `MethodInfo` overload retains its current `ArgumentNullException`, before appending anything. Invalid non-null metadata still fails validation. Test both overloads; do not turn invalid patch metadata into a successful empty installation.

**Do not change ordinary `AddPrefix`, `AddPostfix`, `AddTranspiler`, or `AddFinalizer`.** Their existing single pending selections are outside this feature. Class processing already collects lists; manual inner configuration should now be equally capable.

## 3. Inner finalizers use ordinary finalizer behavior

Add `[HarmonyFinalizer]` as the third role accepted with `[HarmonyInfix]`, plus both `AddInnerFinalizer` overloads, `HarmonyPatchType.InnerFinalizer`, and matching inspection/removal support. Append enum values; preserve existing public overloads and constructors.

Example: the outer method contains `var total = BaseValue() + Parse(text);`.

```csharp
[HarmonyInfix(typeof(Parser), nameof(Parser.Parse), typeof(string))]
[HarmonyFinalizer]
static Exception Recover(Exception __exception, ref int __result)
{
    if (__exception is FormatException)
    {
        __result = 0;
        return null;
    }
    return __exception;
}
```

If recovery succeeds, `BaseValue()` remains evaluated once, the addition completes using zero, and the outer method continues. The finalizer covers the site's prefixes, operation, and postfixes. It does not cover argument/receiver expressions that ran before reaching the site, or unrelated later outer instructions.

Use the ordinary finalizer contract: observe with `void`, or return an exception to preserve/replace it and null to suppress it. Normal completion supplies a null exception. Sort finalizers independently, and preserve Harmony's behavior when a finalizer itself throws. In particular, do not promise "exactly once": ordinary successful-path finalization can fail and enter exceptional finalization. Reuse that control flow and prove parity rather than designing a new cleanup policy.

`__exception` belongs to the inner pipeline. `[HarmonyOuter] __exception` remains invalid: this operation is not executing the outer finalizer. The other supported inner/outer injections retain their meanings. Share the site's `__state` across its prefix, postfix, and finalizer methods in the same patch class. Named outer slots remain shared across sites.

Keep the result in typed storage for finalization, preserving ordinary Harmony's commit points. If a prefix or operation throws, suppression uses the default or prefix-supplied result. If a postfix throws, use the last result committed before that failure. In particular, ordinary returning postfixes carry their replacement values on the stack and commit only after that entire phase succeeds: if one returns a new value and the next throws, a suppressing finalizer sees the value from before that phase. Do not inadvertently commit after every returning postfix while extracting the helper. For reference results, preserve the existing valid default-reference storage and `__resultRef` rules; a finalizer that can suppress may require a default even without prefixes. Do not return a reference to a helper's temporary local.

### One small helper where exception handling needs it

Keep today's inline site emission when there are no inner finalizers. For a site with finalizers, generate one typed static helper containing that site's complete pipeline and call it at the original instruction's position.

Why: in `BaseValue() + Parse(text)`, the value of `BaseValue()` is still waiting on the outer method's evaluation stack. Catching an exception inline would discard it. A helper handles its own exception while that pending value remains in the caller. A helper also works when the selected operation is inside an exception filter; inserting a new protected region directly in a filter is invalid. No whole-method stack-type analyzer is needed for these cases. The [CLI specification, I.12.4.2.7–8 and III.3.34](https://ecma-international.org/wp-content/uploads/ECMA-335_6th_edition_june_2012.pdf) defines these boundaries.

The helper is an emission detail, not a new public patch target:

- Capture only the selected operation's operands, preserving their exact types and real managed references. Leave unrelated pending values in the caller.
- Pass demanded outer argument, receiver, state, and local storage by typed reference. Writes must remain visible even when the helper throws. Do not copy these values back only on successful return, box them into an array, or form `T&&` from an existing `T&`.
- Keep logical binding metadata separate from helper transport parameters. Passing an outer value slot as a reference must not change its advertised argument type or create a false argument-array alias conflict.
- Emit the original opcode and supported prefixes inside the helper, including virtual/constrained dispatch and field memory prefixes. Keep calling the ordinary patched callee where the original instruction would do so.
- Use the existing DynamicMethod emitter for the helper's structured finalizer regions so callback tokens retain their exact runtime assembly identity across private loaders. Keep Cecil emission for outer bodies that need their original exception table, including the existing typed DynamicMethod proxy support. A direct typed helper call needs no reflection invocation or per-patch delegate dispatch. Retain generated helpers with the outer wrapper; they are not serialized.
- Keep original branches and exception-region boundaries around the replacement call unit in the outer method. A remaining exception reaches its surrounding handlers at that location. Do not split or widen the outer exception regions.

Extract ordinary finalizer emission to accept a finalizer list and binding contexts, as prefix/postfix emission already does. Adapt existing state/local storage to use the same storage representation as arguments. Do not add another binder. Classify roles explicitly: a returning finalizer's first parameter is an injection, not a passthrough-result parameter.

Argument arrays remain demand-driven. Allocate only requested scopes and retain the existing selective alias checks. Refresh a demanded array before a receiving finalizer invocation when earlier writes or a different exception path could have made it stale; do not assume a preceding prefix/postfix ran. Preserve ordinary cleanup behavior if the receiving patch throws. No arrays or refresh work are introduced at sites that do not request them.

Unsupported signatures remain local restrictions. An unrelated managed reference, struct, or function pointer waiting below the selected operands is not grounds to reject the site.

## 4. Several state values and captured source variables

These solve different problems and must not share a misleading name:

**Patch-owned state** is storage Harmony creates. Multiple named slots already exist:

```csharp
static void Capture(StringBuilder __result,
    [HarmonyOuter] out StringBuilder __var_builder,
    [HarmonyOuter] out int __var_count)
{
    __var_builder = __result;
    __var_count = 0;
}

static void Later([HarmonyOuter] StringBuilder __var_builder,
    [HarmonyOuter] ref int __var_count)
{
    if (__var_builder is null) return;
    __var_builder.Append(++__var_count);
}
```

Use these slots for any number of named values shared across operations in one outer invocation. Use an inner `__state` struct for several values shared during one site's execution; named `__var_*` slots require `[HarmonyOuter]`. Retain patch-declaring-type isolation and reject conflicting types for one slot. Do not silently extend their lifetime across iterator yields or async suspensions. A captured object reference is a snapshot of which object was seen, not a promise that later postfixes will leave that result unchanged.

No `[HarmonyState]` attribute or second store is needed for multiple Infix state values. Where a friendlier parameter name is useful, exercise the existing `HarmonyArgument` alias machinery against `__var_name`; add an example once verified. Exact `ArgumentMode.Original` must continue to bypass these synthetic names. Numeric `__var_0` refers to an existing IL local, not a new named slot; do not present its compiler-sensitive numbering as source-local discovery.

Two differences are intentional. Named Infix slots are scoped to the patch declaring type, not a runtime registration group spanning classes. Keep cooperating methods in one patch class or use explicitly owned shared storage. Ordinary patches currently cannot bind these named Infix slots: retain ordinary `__state` with a struct when several values must be shared between ordinary and inner patches in the same outer invocation. Do not claim that an existing `__var_name` example already works in an ordinary prefix.

**A captured source variable** is storage the compiler created, for example a parameter retained on an iterator object or a variable retained by a lambda. Add `ArgumentMode.Captured` to the existing parameter annotation:

```csharp
static void Before(
    [HarmonyOuter, HarmonyArgument("limit", ArgumentMode.Captured)] ref int limit)
    => limit = Math.Max(limit, 1);
```

Choose inner/outer scope first. This mode resolves a known compiler-generated field path reachable from that scope's receiver or a proven compiler-supplied closure argument, then uses ordinary typed field storage. A closure held only in an unrelated local is not automatically reachable. It does not search the other scope, fall back to a special injection, or guess an ordinary IL-local number. The mode is Infix-only initially; ordinary patches retain their current binding behavior.

Only `ArgumentMode.Default` performs magic-name, numeric, field-prefix, or synthetic-local classification. Both explicit source modes bypass it. A captured variable literally named `__state`, `___field`, `__var_name`, or `__0` must still mean that captured variable. Audit existing `mode != Original` checks when adding this third mode; merely adding a new enum value is insufficient.

Resolve actual working fields, not an iterator's saved parameter template. A write changes the live captured storage. Follow reference fields normally and value-type fields by address when required; do not mutate a boxed copy. Evaluate a field path once per parameter binding and do not retain an address beyond the patch call. Null intermediate objects fail normally. Reject readonly writes, unsupported types, ambiguous captures, and variables that the compiler did not preserve. Source names are a convenience over recognized compiler layouts, not a promise to reconstruct optimized-away variables.

Limit discovery to verified compiler-generated layouts and report the candidates on ambiguity. Explicit existing field injection/reflection remains available when the user knows the generated field. Add compile-time fixtures for closure nesting, captured `this`, iterator parameters, and async variables before claiming a compiler layout is supported. This is field-path resolution feeding the shared binder, not a general object-graph search.

## 5. Select the body and methods users mean

### Iterator and async methods

Add `InfixOuterBody.Declared` and `InfixOuterBody.Auto`, exposed as `HarmonyInfix.OuterBody` and `HarmonyMethod.infixOuterBody`. Keep `Declared` as the compatibility default. `Auto` means "use the generated body for a recognized state-machine method, otherwise this declared method":

```csharp
[HarmonyPatch(typeof(Worker), nameof(Worker.RunAsync))]
static class Patch
{
    [HarmonyInfix(typeof(Parser), nameof(Parser.Parse), typeof(string),
        OuterBody = InfixOuterBody.Auto)]
    [HarmonyPrefix]
    static void Before(string text) { }
}
```

This is automatic resolution, not a requirement for users to find `MoveNext`. Making it an option avoids silently moving a currently valid patch on the iterator/task factory itself. Explicit generated-method targeting continues to work and is not redirected again.

Resolve the actual method before job grouping and per-method `Prepare`, without resolving the inner selector until `Prepare` accepts the job. Prefer recognized iterator/async state-machine attributes; support the existing iterator fallback only when its generated-type relationship is unambiguous. Resolve closed generic state-machine types without broadening their identity. A recognized but invalid/ambiguous generated body is an error, not permission to patch the factory instead. Never choose a body based on which one happens to contain a match today.

Add one shared `AccessTools.StateMachineMoveNext(MethodBase)` resolver. Return the method unchanged when it is already a recognized body, null when there is no recognized relationship, and an error for malformed or ambiguous recognized metadata. The existing async helper does not close generic state-machine types, and the iterator helper's single-`newobj` heuristic is insufficient for automatic recognition: do not merely combine them. Resolve the appropriate interface implementation, including explicit implementations. Handle async iterators where their metadata and runtime exist: their execution body is the internal `IAsyncStateMachine.MoveNext`, not public `MoveNextAsync`. Use metadata-name checks on legacy framework targets and compile-time fixtures for each supported layout.

Automatic resolution applies only to inner roles. Ordinary roles retain the target explicitly selected by existing Harmony facilities, including `MethodType.Enumerator`, `MethodType.Async`, and direct `MoveNext` targeting. Class processing groups inner and ordinary declarations into separate existing jobs only when their actual methods differ. Keep class-level `Prepare` before discovery; per-method `Prepare`, `Cleanup`, replacement results, and diagnostics identify the actual method. Diagnostics also show the requested factory when redirected. Atomic installation remains per actual method, not across a class.

If a class's target list contains both a factory and its explicit body, add the same `Auto` Infix declaration only once to their resolved body job. This removes duplicate target aliases within that automatic expansion, not independently requested registrations or repeated manual `AddInner...` calls. Ordinary declarations and `Declared` Infixes retain their existing duplicate-target behavior. Class unpatching repeats the same resolution and grouping.

Manual `PatchProcessor.Patch()` continues to produce one method replacement. If its pending patches resolve to different actual methods, reject before any installation and ask the caller to use separate processors. Do not silently move ordinary patches or expand its return contract to hide two installations. Several pending inner patches resolving to the same body still install in one batch.

After a successful installation, retain the actual method on that processor. Both processor `Unpatch` overloads use it; later additions must not silently move that same processor elsewhere. A failed attempt must not change the remembered target. This one processor field makes patch-then-unpatch symmetric without creating a global alias registry.

Store registrations under the actual method. Existing inspection and explicit method-based unpatching address that method; examples must show obtaining it with `StateMachineMoveNext`. Class unpatching must repeat the same body selection, and owner-wide unpatching must remove all of that owner's registrations normally. No durable factory-to-body alias registry is needed. This physical-method rule is an intentional difference from adding an implicit alias to every Harmony inspection/unpatch API.

Inside a redirected body, `[HarmonyOuter] __instance` is the state-machine receiver and `[HarmonyOuter] __originalMethod` is `MoveNext`. Its arguments and local-state lifetime are those of `MoveNext`, not the factory's. Use explicit captured-variable binding for preserved source values. A captured field can survive suspension because the original program owns it; a Harmony local cannot.

`Auto` selects one body, not its entire generated call graph. Iterator cleanup, lambdas, or local functions may live in separate helpers and require separate explicit targets. A no-match diagnostic must name the actual body searched. Likewise, a finalizer on a call returning `Task<T>` catches a synchronous failure of that call, not a later fault delivered by an awaiter's `GetResult`; target the relevant operation or supply a usable replacement task. No async continuation interceptor is implied.

### Local functions, lambdas, and sets of methods

Add focused `AccessTools` resolvers returning real `MethodInfo` objects:

```csharp
MethodInfo LocalFunction(MethodBase containingMethod, string name, Type[] parameters = null);
IEnumerable<MethodInfo> Lambdas(MethodBase containingMethod);
```

Resolve generated functions directly referenced by that exact containing method, with support for nested local calls by passing the resolved local function as the next containing method. Use known generated naming/ownership patterns plus referenced metadata to distinguish overloads and closure types. This is compiled-reference ownership, not recovery of lexical C# nesting: a nested function can also reference a sibling that the compiler places in the same generated family. Missing or ambiguous local functions fail descriptively. Lambda enumeration has a documented deterministic metadata order, but that order is not stable across recompilation; selecting by signature or explicit `MethodInfo` is preferable to "the second lambda".

Returned methods use existing outer `TargetMethod`/`TargetMethods` or inner `InnerTarget` APIs. Discover ownership through the actual enclosing body's method/delegate references, using its recognized state-machine body when appropriate. An optimized-away or unreferenced source function is not discoverable. These helpers do not add wildcard matching to persistent target identity. Do not recursively patch unrelated generated methods, and do not claim that arbitrary hand-written IL retains recoverable C# source ownership.

Selecting all overloads or several explicit methods needs no new selector protocol: enumerate the intended reflection methods and append one inner registration per method. The accumulating processor makes this one installation attempt. Normalize members and freeze the selected set before publication; future rebuilds do not discover new overloads. A generic family remains the explicit way to select all constructions of one definition. Validate the patch signature against every selected site and roll back the whole method on an incompatible match.

## 6. Parity without a second patch framework

**Instruction rules.** Disharmony also has a lower-level pattern/replacement engine, separate from its semantic inner patches. Harmony's equivalent is a transpiler using `CodeMatcher` and `CodeInstruction`, with ordinary ordering, locals, and labels. Add short worked translations for insertion before/after a match, replacement/deletion, method-entry/exit insertion, a required minimum match count, and a first-N match limit. Explain that "process at most N" differs from "reject more than N". Retain Harmony's existing transpiler ordering and branch/exception-label responsibilities rather than copying a phase scheduler.

**Runtime groups.** Existing runtime selectors plus a dedicated Harmony owner ID provide a group that can be installed and removed together. Document one owner per independently removable group; do not imply that `UnpatchAll` removes only the last processor's additions. A group spanning methods has existing per-method installation semantics, not a new transaction across all methods.

**Delegates and bypass.** Preserve existing method/base delegate injections and normal bool-prefix skip rules. Harmony's observer-prefix behavior and before/after dependencies remain authoritative even where the alternative differs. Explicit `[HarmonyOuter]` is preferable to silently binding an inner typo to an outer parameter.

**Optional patch-body inlining.** Add a method-level `[HarmonyInline]` hint for inner patch methods. Without it, emit the ordinary call; with it, allow copying a suitable small static patch body into the generated pipeline. Manual registration uses the same annotated patch `MethodInfo`. Read the hint from that method on rebuild, so no new per-record optimization flag or serialized field is needed. This changes code generation, not which patches run, their bindings, or their exception protection. It is not a request to inline the selected original operation or recursively inline its callees.

Implement this after finalizer semantics are proven. Reuse the existing IL reader and label/local remapping. Bind parameters first using the shared binder, preserve the patch's private by-value parameter slots, and run the same write-back cleanup afterward. Initialize copied locals on every execution when the patch's `InitLocals` requires it, including repeated visits to a loop site. Wrapper-entry initialization alone is not equivalent to a fresh patch call. Remap returns to a local continuation; carry a returned value through the same prefix/postfix/finalizer handling as a normal call. For bodies with exception regions, recursion, pinned locals, stack allocation, unsupported operands/signatures, or context-sensitive operations, fall back to the normal call and explain why in debug output. Do not reject an otherwise valid patch because its optimization is unavailable.

The optimized and normal forms must have identical binding, ordering, results, and exception behavior. Document two opt-in limits: stack traces/profiling can differ, and copied code is a snapshot until the outer wrapper is rebuilt. Do not inline a patch method that is itself already Harmony-patched. If another patch later changes that method, the author must rebuild the affected outer wrappers or leave this hint off; do not add a dependency-tracking registry just for this optimization. Respect `NoInlining`, explicit stack-crawl behavior, and static type-initialization semantics. If the importer cannot preserve a required context, do not inline. One-level body copying needs no transitive recursion analyzer. Benchmark a representative hot-site case before recommending the hint; no speculative general optimizer is part of this plan.

## 7. Refactor and compatibility boundaries

Keep implementation in the existing components:

| Area | Change |
| --- | --- |
| Processors and patch jobs | Pending inner lists, finalizer role, body resolution before grouping. |
| `Infix.Rewrite` | Collect and sort three roles per instruction; use a helper only for finalizer sites. |
| `MethodCreator` and binding/storage helpers | Shared finalizer emission, caller-backed storage, explicit roles, captured field paths. |
| `AccessTools` | Small generated-method resolvers; no new target registry. |
| `PatchInfo`, `Patches`, serialization | Third role, derived capability requirement, unchanged legacy shapes where no new semantics are used. |
| Existing instruction emission | Optional bounded patch-body copying after ordinary-call semantics are established. |

Use shared-state envelope version 3 when surviving records require inner finalizers or captured-variable binding. Version 1/2 readers must reject before deserializing a partial patch set or running user transpilers. A header is not a declaration guard: an old engine can also inspect a newly compiled patch class before anything has been installed.

Version-3 JSON includes the finalizer array and rejects missing required fields and unknown properties. Both backends reject finalizers or captured binding disguised as older state. Read older payloads with an empty third role. JSON without version-3 semantics omits the new empty array and preserves the positional patch representation. BinaryFormatter instead writes the added `[OptionalField]` role: compatibility means older readers ignore that additional empty field, not byte- or field-identical legacy output. Derive the minimum version from surviving records, downgrade after removing the last demanding patch, and keep removal of structurally readable invalid records possible. A body already resolved to a normal `MoveNext` selector needs no extra persistent identity or capability bit merely because its author used `Auto`.

Retain existing declaration rejection for pre-Infix engines. Earlier Infix engines already reject the new finalizer role; prove it against actual prior binaries. An `OuterBody.Auto` declaration must also carry an old-known invalid selector marker, cleared only by a reader that understands the option. Keep its real selector separately in that declaration while marked; a new property ignored by an old reader is not protection. Clear the historical `innerName` so method-only readers reject it, and mark the historical `innerTargetKind` invalid so operation-capable readers also reject it. Store the actual selector in new declaration fields. An invalid kind alone does not protect the oldest Infix reader. Follow the existing rejection-marker approach without adding a fake public target kind.

For `ArgumentMode.Captured`, follow exact binding's old-known rejection-marker pattern in `HarmonyArgument` and validate the new mode explicitly. Do not allow an old reader to ignore it and bind a same-named argument. Runtime substitution with an older assembly must either understand the required API or fail loudly; side-by-side discovery must not execute a partial interpretation. Initial declaration rejection and rejection of already-published state need separate tests.

Inlining is a method annotation and optimization hint: an older compatible reader may ignore it and call the patch normally. Do not require version 3 solely for an optimization or add it to the positional patch format. The pending-list improvement likewise introduces no new persisted record shape.

Do not change DynamicMethod factory support for ordinary patches, exact-name binding, generic identity, or ordinary exception-table generation incidentally. These are explicit regression gates, not cleanup opportunities.

Generated wrappers must retain exact runtime dependencies across plugin load contexts. Use DynamicMethod emission for synthetic helpers' structured finalization and retain Cecil for imported outer exception tables. Cecil wrappers and their DynamicMethod proxies share a narrowly scoped dependency resolver, verified against separately loaded callback assemblies and patch-owned state types. Never use a process-wide first-match name fallback. If one metadata scope requires distinct actual assemblies with the same full assembly identity, or the runtime cannot bind the exact selected dependency, reject before publication. On runtimes without isolated load contexts, conservatively reject competing loaded identities for demanded dependencies. This is an explicit loader limit, not permission to run a different callback.

## 8. Implementation order and acceptance

Each stage updates executable documentation examples and its focused tests. The new public APIs are not documented as available until their stage is implemented.

1. **Accumulate pending inner patches.** Test both overloads, same/different sites and owners, duplicate methods, insertion-order ties, nulls, reused processors, rollback on one bad member, and removal/rebuild. Characterize ordinary `Add...` behavior unchanged.
2. **Share finalizer emission, then add the inner role.** First prove unchanged ordinary behavior. Compare ordinary and inner event traces for success, skip, exceptions in each phase, suppression/replacement, and finalizers that throw on both paths. Include the exact result-commit case: original returns 1, returning postfix A returns 2, returning postfix B throws, suppressing finalizer observes 1. Include finalizer-only sites, default/explicit/ref results, named state, arrays and aliasing, and outer writes visible on escaping exceptions.
3. **Prove helper boundaries.** Generate pending stack values of different types, including live managed references, structs, `calli` results, and function pointers used later. Exercise try/catch/filter/finally/fault and nested/branch-boundary sites. Test every supported operation kind, original dispatch, GC retention, and removal of the last finalizer.
4. **Resolve generated bodies and captures.** Cover iterator, async, and available async-iterator fixtures; Debug/Release compiler layouts; calls before/after suspension; explicit/default/automatic selection; preserved factory patches; generic identity; mixed manual rejection; `Prepare(false)`; processor patch-then-unpatch; factory/body target aliases; inspection and all removal routes. Test working versus saved iterator fields, nested closures, captured `this`, colliding magic names, missing/ambiguous values, and real writes across suspension. Prove that synthetic state still resets per body invocation.
5. **Complete authoring parity.** Cover nested local functions/lambda selection, overloaded parents, several explicit inner targets, named-state examples, raw-rule translations, and independent owner groups. Do not use method enumeration order as a silent first-match policy.
6. **Add optional inlining.** Run the same behavioral tests with the hint off/on, including supported copies and fallback cases. Verify mutation, cleanup, type initialization, return handling, finalizer protection, and mixed patches; record a small performance comparison.
7. **Run compatibility and release coverage.** Exercise JSON and BinaryFormatter round trips, downgrade/removal, old/new declaration discovery, and old/new published-state rebuilds in both load orders. Assert that a user transpiler never runs after a rejected state header. Run the existing runtime/architecture matrix and report loader limitations separately from real coexistence success.

Prefer small generated case matrices over one fixture per combination. Keep an independent ordinary-patching oracle for scheduling and exception behavior, and compare actual callback traces/results rather than only generated instruction shapes. Reuse the [testing strategy](../docs/infix/TESTING-STRATEGY.md); a passing focused net9/x64 run is a development checkpoint, not proof of the full matrix.

## Source comparison and limits of the evidence

The Disharmony inventory is pinned to RossM/RimworldMods commit `ba7d90c7da2fa8743ae230478dde910fc12ee612`. Its source implements automatic iterator/async body selection, named state, generated-function selection, captured-field binding, and always-run postfixes that can recover an inner exception. These informed this draft; its tests were not executed during this review. See [body selection](https://github.com/RossM/RimworldMods/blob/ba7d90c7da2fa8743ae230478dde910fc12ee612/Disharmony/PatchRegistry.cs#L303-L315), [state and captures](https://github.com/RossM/RimworldMods/blob/ba7d90c7da2fa8743ae230478dde910fc12ee612/Disharmony/ParameterBinder.cs#L140-L211), and [exception handling](https://github.com/RossM/RimworldMods/blob/ba7d90c7da2fa8743ae230478dde910fc12ee612/Disharmony/RuleBuilders/PrefixPostfixRuleBuilder.cs#L39-L115).

The additional authoring capabilities are visible in its [generated-member resolver](https://github.com/RossM/RimworldMods/blob/ba7d90c7da2fa8743ae230478dde910fc12ee612/Disharmony/ReflectionTools.cs#L34-L120), [public instruction rules](https://github.com/RossM/RimworldMods/blob/ba7d90c7da2fa8743ae230478dde910fc12ee612/Disharmony/RulesEngine/Ruleset.cs#L11-L187), and [patch inlining](https://github.com/RossM/RimworldMods/blob/ba7d90c7da2fa8743ae230478dde910fc12ee612/Disharmony/RuleBuilders/InlineRuleBuilder.cs#L25-L149). Its automatic state-machine path rejects state, and its finalization/ordering rules differ from Harmony's. Supporting the use cases does not require inheriting those limitations or changing ordinary Harmony behavior.
