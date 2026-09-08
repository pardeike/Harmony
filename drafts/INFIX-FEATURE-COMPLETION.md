# Infix feature completion

**Implemented, unreleased contract.** This part of the [V3 specification](INFIX-NEW-IMPL-V3.md) covers accumulating registration, inner finalizers, generated methods, captured variables and optional inlining. It shares the core binding, ordering, identity and compatibility rules with V3 and the [operation-target contract](INFIX-OPERATIONS-ADDENDUM.md). The public [odd cases and limits chapter](../Documentation/articles/patching-infix-limits.md) describes the resulting behavior. See [validation status](../docs/infix/README.md) for executed checks and runtime boundaries.

## 1. The model to preserve

An Infix is ordinary Harmony patching at one selected operation inside an outer method. An **operation** is a call, field read/write, construction, or literal load. A **site** is one matching instruction, not one loop iteration.

Adding or removing registrations rebuilds the outer method from its original instructions: apply transpilers, find sites, and combine surviving patches at each site. Multiple registrations from one owner remain independent; do not collapse them by owner or selector.

Prefixes, postfixes and finalizers are independent lists. Use ordinary Harmony's sorting and execution rules for each role. Exact and generic-family selectors that meet at one instruction share that pipeline. There are no paired wrappers, special family priority, or extra "last capture" phase.

The capability decisions are:

| User capability | Decision |
| --- | --- |
| Calls, properties, fields, construction, literals | Supported within the operation contract's boundaries. |
| Several installed patches at the same site | Independent registrations share one sorted site pipeline. |
| Several `AddInner...` calls before one `Patch()` | Accumulate them; section 2. |
| Inner exception observation, recovery, and cleanup | Ordinary-style inner finalizers; section 3. |
| Several named state values | Use the existing named slots, not another state store; section 4. |
| Iterator and async body selection | Automatic body resolution is an explicit option; section 5. |
| Local functions, lambdas, and their captured variables | Focused method resolvers and explicit captured-variable binding; sections 4–5. |
| Several targets or every overload | Resolve an explicit method set, then register ordinary selectors; section 5. |
| Arbitrary instruction insertion/replacement | Existing transpilers and `CodeMatcher`; supply equivalent examples, not a competing rules language. |
| Runtime configuration and removable groups | Existing processors and Harmony owner IDs; document group ownership. |
| Inlining small patch bodies | Optional optimization with a normal-call fallback; section 6. |
| Understanding indirect calls | Public `InlineSignature` is implemented; it does not make a runtime function pointer a stable Infix target. |

The public guide and executable examples must describe these same contracts, including ordinary Harmony scheduling and explicit scope selection.

## 2. Repeated `AddInner...` calls accumulate

The pending processor configuration follows what its `Add` names suggest:

```csharp
var processor = harmony.CreateProcessor(outer);
processor.AddInnerPrefix(a);
processor.AddInnerPrefix(b);
processor.AddInnerPostfix(c);
processor.AddInnerFinalizer(d);
processor.Patch(); // Installs all four, alongside existing registrations.
```

Here `a` through `d` are `HarmonyMethod` objects with their own selectors. They may select the same site or different sites. Both overloads of every `AddInner...` method append. Keep the fluent return type.

Keep one pending list per inner role. Append each list to `PatchInfo` once, then build and publish one replacement per actual outer method. Preserve insertion order for registration indexes; priority and before/after dependencies still govern execution. No second registry, deduplication or replace-by-owner rule.

One `Patch()` is one installation attempt for that method. Validate the complete candidate, including all selected sites, before replacing its working wrapper or publishing state. If `b` is invalid, `a` must not become partially installed. Existing registrations remain intact.

Match ordinary processor reuse: pending configuration remains after `Patch()`. Calling it again adds those registrations again; it is not an update operation. Repeating an identical method is allowed. Normal removal by patch method or owner retains its existing removal scope, rather than gaining a per-add handle.

A null `HarmonyMethod` contributes no patch and must not clear earlier additions. A null passed to the `MethodInfo` overload retains its current `ArgumentNullException`, before appending anything. Invalid non-null metadata still fails validation. Test both overloads; do not turn invalid patch metadata into a successful empty installation.

**Do not change ordinary `AddPrefix`, `AddPostfix`, `AddTranspiler`, or `AddFinalizer`.** They retain single pending selections. Class processing and manual inner configuration both collect lists.

## 3. Inner finalizers use ordinary finalizer behavior

`[HarmonyFinalizer]` is the third role accepted with `[HarmonyInfix]`, with both `AddInnerFinalizer` overloads, `HarmonyPatchType.InnerFinalizer`, and matching inspection/removal support. New enum values are appended; existing public overloads and constructors remain unchanged.

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

Emit sites without inner finalizers inline. For a site with finalizers, generate one typed static helper containing the complete pipeline and call it at the original instruction's position.

In `BaseValue() + Parse(text)`, an inline catch would discard the pending `BaseValue()` result. A helper handles its exception while leaving that value in the caller. It also works inside a filter, where a new protected region is invalid. Neither case needs whole-method stack-type analysis. See [CLI I.12.4.2.7–8 and III.3.34](https://ecma-international.org/wp-content/uploads/ECMA-335_6th_edition_june_2012.pdf).

The helper is an emission detail, not a new public patch target:

- Capture only the selected operation's operands, preserving their exact types and real managed references. Leave unrelated pending values in the caller.
- Pass demanded outer argument, receiver, state, and local storage by typed reference. Writes must remain visible even when the helper throws. Do not copy these values back only on successful return, box them into an array, or form `T&&` from an existing `T&`.
- Keep logical binding metadata separate from helper transport parameters. Passing an outer value slot as a reference must not change its advertised argument type or create a false argument-array alias conflict.
- Emit the original opcode and supported prefixes inside the helper, including virtual/constrained dispatch and field memory prefixes. Keep calling the ordinary patched callee where the original instruction would do so.
- Use the existing DynamicMethod emitter for the helper's structured finalizer regions so callback tokens retain their exact runtime assembly identity across private loaders. Keep Cecil emission for outer bodies that need their original exception table, including the existing typed DynamicMethod proxy support. A direct typed helper call needs no reflection invocation or per-patch delegate dispatch. Retain generated helpers with the outer wrapper; they are not serialized.
- Keep original branches and exception-region boundaries around the replacement call unit in the outer method. A remaining exception reaches its surrounding handlers at that location. Do not split or widen the outer exception regions.

Shared finalizer emission accepts a finalizer list and binding contexts, like prefix/postfix emission. State, locals and arguments use the same storage representation and binder. Classify roles explicitly: a returning finalizer's first parameter is an injection, not a passthrough result.

Argument arrays remain demand-driven. Allocate only requested scopes and retain the existing selective alias checks. Refresh a demanded array before a receiving finalizer invocation when earlier writes or a different exception path could have made it stale; do not assume a preceding prefix/postfix ran. Preserve ordinary cleanup behavior if the receiving patch throws. No arrays or refresh work are introduced at sites that do not request them.

Unsupported signatures remain local restrictions. An unrelated managed reference, struct, or function pointer waiting below the selected operands is not grounds to reject the site.

## 4. Several state values and captured source variables

**Patch-owned state** is storage Harmony creates, with multiple named slots:

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

Use existing `HarmonyArgument` aliases for friendlier `__var_name` parameter names, with executable example coverage. No `[HarmonyState]` attribute or second store is needed. `ArgumentMode.Original` bypasses synthetic names. Numeric `__var_0` refers to an existing IL local, not a named slot; its compiler-sensitive numbering is not source-local discovery.

Named Infix slots belong to the patch declaring type, not a registration group spanning classes. Keep cooperating methods in one class or use explicitly owned shared storage. Ordinary patches cannot bind these slots; use an outer `__state` struct to share several values between ordinary and inner patches in one invocation.

**A captured source variable** is compiler-created storage, such as an iterator parameter or lambda capture. Select it with `ArgumentMode.Captured`:

```csharp
static void Before(
    [HarmonyOuter, HarmonyArgument("limit", ArgumentMode.Captured)] ref int limit)
    => limit = Math.Max(limit, 1);
```

Choose inner/outer scope first. This mode resolves a known compiler-generated field path from that scope's receiver or a proven compiler-supplied closure argument, then uses ordinary typed field storage. An unrelated local's closure is not automatically reachable. There is no other-scope search, special-injection fallback or IL-local guessing. The mode is Infix-only; ordinary binding remains unchanged.

Only `ArgumentMode.Default` performs magic-name, numeric, field-prefix or synthetic-local classification. Both explicit source modes bypass it. Captures literally named `__state`, `___field`, `__var_name` or `__0` still mean those variables. Classification checks must test `mode == Default`, not `mode != Original`.

Resolve actual working fields, not an iterator's saved parameter template. A write changes the live captured storage. Follow reference fields normally and value-type fields by address when required; do not mutate a boxed copy. Evaluate a field path once per parameter binding and do not retain an address beyond the patch call. Null intermediate objects fail normally. Reject readonly writes, unsupported types, ambiguous captures, and variables that the compiler did not preserve. Source names are a convenience over recognized compiler layouts, not a promise to reconstruct optimized-away variables.

Limit discovery to verified compiler-generated layouts and report candidates on ambiguity. Explicit field injection/reflection remains available for known generated fields. Support claims require compile-time fixtures for closure nesting, captured `this`, iterator parameters and async variables. Resolve field paths through the shared binder, not a general object-graph search.

## 5. Select the body and methods users mean

### Iterator and async methods

`HarmonyInfix.OuterBody` and `HarmonyMethod.infixOuterBody` select `InfixOuterBody.Declared` (the compatibility default) or `Auto`. `Auto` uses a recognized state machine's generated body, otherwise the declared method:

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

The shared `AccessTools.StateMachineMoveNext(MethodBase)` resolver returns recognized bodies unchanged, null for no recognized relationship, and an error for malformed or ambiguous recognized metadata. Resolve closed generic state-machine types and the appropriate interface implementation, including explicit implementations; the older iterator helper's single-`newobj` heuristic is insufficient. For async iterators, where metadata and runtime support exist, select internal `IAsyncStateMachine.MoveNext`, not public `MoveNextAsync`. Use metadata-name checks on legacy targets and compile-time fixtures for every supported layout.

Automatic resolution applies only to inner roles. Ordinary roles retain the target explicitly selected by existing Harmony facilities, including `MethodType.Enumerator`, `MethodType.Async`, and direct `MoveNext` targeting. Class processing groups inner and ordinary declarations into separate existing jobs only when their actual methods differ. Keep class-level `Prepare` before discovery; per-method `Prepare`, `Cleanup`, replacement results, and diagnostics identify the actual method. Diagnostics also show the requested factory when redirected. Atomic installation remains per actual method, not across a class.

If a class's target list contains both a factory and its explicit body, add the same `Auto` Infix declaration only once to their resolved body job. This removes duplicate target aliases within that automatic expansion, not independently requested registrations or repeated manual `AddInner...` calls. Ordinary declarations and `Declared` Infixes retain their existing duplicate-target behavior. Class unpatching repeats the same resolution and grouping.

Manual `PatchProcessor.Patch()` continues to produce one method replacement. If its pending patches resolve to different actual methods, reject before any installation and ask the caller to use separate processors. Do not silently move ordinary patches or expand its return contract to hide two installations. Several pending inner patches resolving to the same body still install in one batch.

After successful installation, retain the actual method on the processor for both `Unpatch` overloads. Later additions must not silently move it elsewhere; failed attempts must not change it. No global alias registry is needed.

Store registrations under the actual method. Existing inspection and explicit method-based unpatching address that method; examples must show obtaining it with `StateMachineMoveNext`. Class unpatching must repeat the same body selection, and owner-wide unpatching must remove all of that owner's registrations normally. No durable factory-to-body alias registry is needed. This physical-method rule is an intentional difference from adding an implicit alias to every Harmony inspection/unpatch API.

Inside a redirected body, `[HarmonyOuter] __instance` is the state-machine receiver and `[HarmonyOuter] __originalMethod` is `MoveNext`. Its arguments and local-state lifetime are those of `MoveNext`, not the factory's. Use explicit captured-variable binding for preserved source values. A captured field can survive suspension because the original program owns it; a Harmony local cannot.

`Auto` selects one body, not its entire generated call graph. Iterator cleanup, lambdas, or local functions may live in separate helpers and require separate explicit targets. A no-match diagnostic must name the actual body searched. Likewise, a finalizer on a call returning `Task<T>` catches a synchronous failure of that call, not a later fault delivered by an awaiter's `GetResult`; target the relevant operation or supply a usable replacement task. No async continuation interceptor is implied.

### Local functions, lambdas, and sets of methods

Focused `AccessTools` resolvers return real `MethodInfo` objects:

```csharp
MethodInfo LocalFunction(MethodBase containingMethod, string name, Type[] parameters = null);
IEnumerable<MethodInfo> Lambdas(MethodBase containingMethod);
```

Resolve generated functions directly referenced by that exact containing method, with support for nested local calls by passing the resolved local function as the next containing method. Use known generated naming/ownership patterns plus referenced metadata to distinguish overloads and closure types. This is compiled-reference ownership, not recovery of lexical C# nesting: a nested function can also reference a sibling that the compiler places in the same generated family. Missing or ambiguous local functions fail descriptively. Lambda enumeration has a documented deterministic metadata order, but that order is not stable across recompilation; selecting by signature or explicit `MethodInfo` is preferable to "the second lambda".

Returned methods use existing outer `TargetMethod`/`TargetMethods` or inner `InnerTarget` APIs. Discover ownership through the actual enclosing body's method/delegate references, using its recognized state-machine body when appropriate. An optimized-away or unreferenced source function is not discoverable. These helpers do not add wildcard matching to persistent target identity. Do not recursively patch unrelated generated methods, and do not claim that arbitrary hand-written IL retains recoverable C# source ownership.

Selecting all overloads or several explicit methods needs no new selector protocol: enumerate the intended reflection methods and append one inner registration per method. The accumulating processor makes this one installation attempt. Normalize members and freeze the selected set before publication; future rebuilds do not discover new overloads. A generic family remains the explicit way to select all constructions of one definition. Validate the patch signature against every selected site and roll back the whole method on an incompatible match.

### Persistent patch-owned state across suspension

`[HarmonyOuter, HarmonyArgument("name", ArgumentMode.Persistent)]` opts an Infix parameter into one logical async execution or enumeration. This overrides the earlier exclusion of persistent synthetic state only for this explicit binding. Existing `__state`, `__var_name`, captured fields and ordinary patch bindings retain their lifetimes. Synchronous methods use ordinary invocation-local storage.

Initialize each slot to its type's default. Key it by the actual patch declaring `Type` and explicit name; share it across that type's inner prefixes, postfixes and finalizers. Require consistent types and lifetimes across the registered bindings, including collisions with ordinary named slots. Reject missing outer scope, empty names and values that cannot live on the managed heap before installation. Typed `ref`/`out` binding, helper transport and optional inlining must use the same storage.

Keep independent state for concurrent, recursive and nested async calls, and for separate or repeated enumerations. Preserve it across both synchronous and asynchronous await completion, thread changes, and async iterator disposal that itself awaits. A recognized generated `MoveNext` is the boundary; do not silently include unrelated helpers. Installing during an execution starts state on its next intercepted entry. Existing suspended slots survive a compatible wrapper rebuild, including a rebuild by another current Harmony assembly. A changed slot type starts fresh on its next binding, while already executing wrappers retain their original typed storage. Never reinterpret a saved value or throw an unhandled exception into an async continuation merely because its wrapper was rebuilt.

Use the public awaiter registration protocol, with a struct adapter implementing the runtime's completion interfaces. Preserve the original builder, state-machine type and result task; do not use private compiler fields, task identity or the address of a movable struct as the execution key. Continuations carry a shared token into the next body invocation. Custom builders must honor the public protocol and tolerate another conforming awaiter type. Reject incompatible generic constraints. An Infix targeting the suspension/completion bookkeeping itself is unsupported while persistent state is enabled. Async iterator completion must be identifiable through the public builder completion call; reject an unrecognized layout. Do not claim future protocols or arbitrary handwritten state machines are covered.

Reference-type enumerators use a weak association that permits collection even when state refers back to its enumerator. Completion, faults, observed cancellation and disposal release stored references without disposing user objects. Synchronous iterator `Dispose` needs a cleanup finalizer because it may not enter `MoveNext`; remove the hook with the body's last persistent registration. Stage any user-transpiler-dependent cleanup rebuild before publishing removal. Removing persistence clears suspended state, while an already executing wrapper releases its remaining slots on exit. Weak execution tracking must not retain completed or abandoned executions. The usual cross-engine serialization and per-method update rules remain in force.

Use the runtime's native weak table even for a net35 Harmony build. The original CLR 2.0 has no equivalent dependent-handle facility; its backport cannot collect value-to-enumerator cycles. Explicit completion, disposal or unpatching is required for those cycles on that runtime. This is a documented legacy-runtime exception to abandoned-cycle collection, not permission to use the backport on a runtime with native support.

Use shared-state envelope version 4 when surviving callbacks request `ArgumentMode.Persistent`, with the existing version-3 payload shape and required role arrays. Earlier readers reject the header before callbacks or transpilers; reject persistent binding disguised as older state. Derive the minimum version on removal. Reuse `HarmonyArgument`'s old-known invalid binding marker for declaration rejection: an ignored new `HarmonyOuter` property would not protect against older Infix engines. The internal continuation protocol carries only shared BCL types, allowing another current engine to resume existing state.

Prove normal and exceptional lifetime cleanup, weak cycles, live unpatching, iterator reuse, actual suspension, notification-only awaiters, Task/ValueTask/async void and pooled builders where available. Include real prior version-1/2/3 engines in both load orders and current/current rebuilds during suspension, with JSON and BinaryFormatter. Keep the ordinary patching and full runtime matrix as regression gates. Allocation and continuation indirection are opt-in costs; no performance parity with invocation locals is promised.

## 6. Parity without a second patch framework

**Instruction rules.** Lower-level editing uses a transpiler with `CodeMatcher` and `CodeInstruction`. Keep short recipes for insertion before/after a match, replacement/deletion, method-entry/exit insertion, a required minimum match count, and a first-N limit. Distinguish "process at most N" from "reject more than N". Existing transpiler ordering, locals, and branch/exception-label responsibilities still apply.

**Runtime groups.** Existing runtime selectors plus a dedicated Harmony owner ID provide a group that can be installed and removed together. Document one owner per independently removable group; do not imply that `UnpatchAll` removes only the last processor's additions. A group spanning methods has existing per-method installation semantics, not a new transaction across all methods.

**Delegates and bypass.** Preserve method/base delegate injections, bool-prefix skip rules, observer-prefix behavior and before/after dependencies. Require explicit `[HarmonyOuter]`; never silently bind an inner typo to an outer parameter.

**Optional patch-body inlining.** Method-level `[HarmonyInline]` permits copying a suitable small static inner patch body into the pipeline; without it, emit the ordinary call. Manual registration uses the same annotated `MethodInfo`. Read the hint on rebuild, without per-record flags or serialized fields. It changes code generation, not patch selection, binding or exception protection. It does not inline the original operation or recursively inline callees.

Reuse the existing IL reader and label/local remapping. Bind parameters first using the shared binder, preserve the patch's private by-value parameter slots, and run the same write-back cleanup afterward. Initialize copied locals on every execution when the patch's `InitLocals` requires it, including repeated visits to a loop site. Wrapper-entry initialization alone is not equivalent to a fresh patch call. Remap returns to a local continuation; carry a returned value through the same prefix/postfix/finalizer handling as a normal call. For bodies with exception regions, recursion, pinned locals, stack allocation, unsupported operands/signatures, or context-sensitive operations, fall back to the normal call and explain why in debug output. Do not reject an otherwise valid patch because its optimization is unavailable.

The optimized and normal forms must have identical binding, ordering, results and exception behavior. Two opt-in limits apply: stack traces/profiling can differ, and copied code is a snapshot until the outer wrapper is rebuilt. Do not inline an already Harmony-patched patch method. If it is patched later, rebuild affected outer wrappers or leave the hint off; no dependency-tracking registry is provided. Respect `NoInlining`, explicit stack-crawl behavior and static type initialization. Fall back if the importer cannot preserve a required context. One-level copying needs no transitive recursion analyzer or general optimizer. Benchmark a representative hot site before recommending the hint.

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

For metadata inspection, Infix `Patch.Equals` and `GetHashCode` use the stored callback module ID and method token without resolving the callback. Owners, target selectors and occurrence positions remain excluded from equality. Ordinary patch records retain their existing method-based equality and hashes; ordinary and Infix records compare unequal so their different identity rules remain consistent. This overrides the core specification's public `Patch` equality constraint for Infix metadata only. Execution and rebuilding still validate that each durable callback identity resolves uniquely.

## 8. Acceptance and regression coverage

Keep executable documentation examples and focused tests aligned with these implemented contracts. A local passing fixture is not proof of every supported runtime arrangement.

1. **Accumulate pending inner patches.** Test both overloads, same/different sites and owners, duplicate methods, insertion-order ties, nulls, reused processors, rollback on one bad member, and removal/rebuild. Characterize ordinary `Add...` behavior unchanged.
2. **Shared finalizer behavior.** Prove unchanged ordinary behavior. Compare ordinary and inner event traces for success, skip, exceptions in each phase, suppression/replacement, and finalizers that throw on both paths. Include the exact result-commit case: original returns 1, returning postfix A returns 2, returning postfix B throws, suppressing finalizer observes 1. Include finalizer-only sites, default/explicit/ref results, named state, arrays and aliasing, and outer writes visible on escaping exceptions.
3. **Prove helper boundaries.** Generate pending stack values of different types, including live managed references, structs, `calli` results, and function pointers used later. Exercise try/catch/filter/finally/fault and nested/branch-boundary sites. Test every supported operation kind, original dispatch, GC retention, and removal of the last finalizer.
4. **Resolve generated bodies and captures.** Cover iterator, async, and available async-iterator fixtures; Debug/Release compiler layouts; calls before/after suspension; explicit/default/automatic selection; preserved factory patches; generic identity; mixed manual rejection; `Prepare(false)`; processor patch-then-unpatch; factory/body target aliases; inspection and all removal routes. Test working versus saved iterator fields, nested closures, captured `this`, colliding magic names, missing/ambiguous values, and real writes across suspension. Prove that synthetic state still resets per body invocation.
5. **Authoring parity.** Cover nested local functions/lambda selection, overloaded parents, several explicit inner targets, named-state examples, raw-rule translations, and independent owner groups. Do not use method enumeration order as a silent first-match policy.
6. **Optional inlining.** Run the same behavioral tests with the hint off/on, including supported copies and fallback cases. Verify mutation, cleanup, type initialization, return handling, finalizer protection, and mixed patches; record a small performance comparison.
7. **Run compatibility and release coverage.** Exercise JSON and BinaryFormatter round trips, downgrade/removal, old/new declaration discovery, and old/new published-state rebuilds in both load orders. Assert that a user transpiler never runs after a rejected state header. Run the existing runtime/architecture matrix and report loader limitations separately from real coexistence success.

Prefer small generated case matrices over one fixture per combination. Keep an independent ordinary-patching oracle for scheduling and exception behavior, and compare actual callback traces/results rather than only generated instruction shapes. Reuse the [testing strategy](../docs/infix/TESTING-STRATEGY.md); a passing focused net9/x64 run is a development checkpoint, not proof of the full matrix.

## Design scope

These contracts and acceptance tests define Harmony's ordering, state lifetime and exception semantics, including preservation of ordinary patching behavior.
