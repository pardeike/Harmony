# Infix compatibility test strategy

This document defines compatibility proof for the [V3 contract](INFIX-NEW-IMPL-V3.md), [operation-target addendum](INFIX-OPERATIONS-ADDENDUM.md), and [feature-completion contract](INFIX-FEATURE-COMPLETION.md). It adds no API or compatibility promise. [The harness README](../HarmonyTests.Compatibility/README.md) provides commands, pinned baselines, case names, and loader diagnostics. [docs/infix/README.md](../docs/infix/README.md) is the current execution-status record.

Test compilation against a different Harmony version separately from two loaded engines updating the same target. Before counting a mixed-engine Infix result, prove that those engines can preserve ordinary patches through rebuilding and cross-engine removal.

## Isolation and loading

Run each case in a fresh console child, outside NUnit's process. Loaded assemblies, serializer choices, shared state, and native detours survive fixture cleanup. Use:

- A host with no Harmony reference that selects files, loading policy, and fixture entry points.
- One shared target/trace assembly with no Harmony reference, loaded once in positive coexistence cases.
- Ordinary fixtures compiled separately against each pinned old and current engine, with direct typed API calls.
- Current feature fixtures separating new attribute materialization from direct new-member access.

Compile against explicit file references and retain the compile-time identity/hash. Stage only intended dependencies, preserve the compiled fixture when substituting its runtime engine, and set serializer switches before either Harmony initializes. Cross-engine reflection must create Harmony-owned objects through the requested assembly; shared `Type`, `MethodInfo`, and primitive values can cross the host boundary. An `extern alias` selects a compile-time reference, not the runtime provider.

| Arrangement | Required distinction |
| --- | --- |
| One engine | Verify which assembly supplies the fixture's typed calls. Report runtime version rejection before entry separately from Harmony rejection. |
| Two CoreCLR engines | Use separate named, noncollectible `AssemblyLoadContext` instances with their own Harmony/dependencies and one shared target assembly. Identically named types in different contexts are distinct. |
| Two Framework/Mono engines | Use one application domain, explicit file/loading policy, and owning-plugin dependency resolution. Reverse load order and record unification. Separate application domains do not prove shared patch state. |
| Forced substitution or duplicate identity | Label the exact host policy and intended ambiguity. These cases do not establish another runtime's default binding behavior. |

Record each actual `Assembly` object, module build identifier, hash, and provider context; an assembly version or filename alone cannot identify the engine. Current test builds may use explicit version overrides. Never rename, re-sign, rebuild, or modify a published old artifact to manufacture coexistence.

Do not inject dictionary references, preload a fake `HarmonySharedState`, strip declaration markers, or transplant serializer bytes into a positive coexistence case. Decoder-only tests and seeded legacy-recovery fixtures are valid narrower proofs and must be labeled as such.

## Ordinary coexistence control

For each runtime, loader, backend, and initialization order:

1. Identify both engines and the same target `MethodInfo`.
2. Check reference identity of their shared `state` and `originals` dictionaries, plus `originalsMono` where relevant. Enumerate generated shared-state assemblies and their version fields.
3. Let A add an ordinary prefix, B add a postfix, then A add a counter transpiler. Execute after each change; both owners must remain visible and each behavior must occur once.
4. Remove A through B, execute, then remove B through A and execute. Check records, traces, and replacement mappings throughout.

Repeat with reversed initialization/update order in a fresh child. Dictionary contents or a function pointer alone do not prove that execution reaches the correct replacement. Private detour objects need not be shared.

A failed control leaves dependent Infix behavior unproven. Investigate failures on a claimed supported host; do not relabel them as successful feature rejection. The harness can assert specific known loader boundaries as diagnostics, but its required-coexistence lanes must fail them. Initial sequences remain single-threaded because the per-assembly patch lock does not coordinate different Harmony copies.

## Required case families

Here, old means a pinned published release; prior Infix means one of the unreleased source baselines. Test each feature at the stage where its safeguard applies.

| Case | Setup | Required outcome |
| --- | --- | --- |
| C1, compile old/use current | Old ordinary fixture, current runtime engine only. | Typed binding where the host permits it; ordinary discovery, execution, inspection, rebuild, and unpatch work. |
| C2, compile current common API/use old | Current ordinary fixture using only shared APIs, old engine only. | Behavior matches the old control if binding succeeds; otherwise record the exact pre-entry loader boundary. |
| C3, compile current feature API/use old | Separately materialize new attributes and invoke new API members in an old-only process. | Applicable missing-type/member/version rejection, with no registration, callback execution, or target mutation. |
| C4, old discovery/current declaration | Both engines loaded; attribute instances resolve through current Harmony before old discovery. | Old-recognized roles reject the marker before installation/transpilers. Unrecognized old role names may be ignored without registration. Current accepts and executes the same declaration. |
| C5, old operation/active current state | Current installs an Infix and counter transpiler; old inspects, adds, removes by owner/method, or removes all in separate children. | State reading rejects before any new transpiler entry/enumeration. Published bytes, version, mappings, counters, and installed behavior remain unchanged. |
| C6, remove last Infix/retry old | Current removes inner records, preserving ordinary survivors. | Removing one of several keeps protection; removing the last restores old-readable state. The previously rejected old operation succeeds on retry. |
| C7, old state/current reader | Old serializer supplies ordinary state, including pre-inner-array 2.3.6 and labeled incomplete-inner recovery graphs. | Missing legacy arrays normalize; structurally readable invalid entries remain removable, while invalid survivors prevent rebuilding. |
| C8, two current readers | A registers selectors; B reads cold identities, changes registrations, and rebuilds; A repeats. | Exact targets, match sets, record ordering, state lifetimes, and detached reader-owned snapshots survive both directions. |

C4 covers prefix/postfix attributes, ordinary and inner role names, equivalent name-plus-attribute roles, class targets, `TargetMethod(s)`, attribute order, and ordering annotations. Use a compact generated set, not a full cross-product. Include conflicting roles, forbidden method-level `HarmonyPatch`, and prepare-false deferred target resolution. Track prepare/cleanup separately from transpiler and patch-body counters.

Published 2.3.6 does not recognize the inner role names. Its omission must be distinguished from marker rejection; neither outcome may turn an Infix into an ordinary outer patch. Active-state rejection remains required regardless of declaration naming.

Install the C5 transpiler successfully before recording its baseline count. Include early ordering and dependencies, with counters both at entry and during iterator enumeration. A later exception does not rescue a test in which that transpiler already ran. Compile this counter against the old engine so rejection/removal tests do not depend on its separate foreign-instruction conversion limitation.

## Prior-Infix capabilities

Use the [pinned source-baseline lanes](../HarmonyTests.Compatibility/README.md#prior-infix-source-baselines) to test version-1 method-only and version-2 operation-capable readers against current engines, in both initialization orders. Require ordinary coexistence before extension/completion behavior.

- Extension cases exercise constructor, field-read, and constant identities, declarations, cold reconstruction, rebuilds, and removal. Method-only `__originalMember` binding also requires version 2 in either scope; exact real-argument binding and the ignored first passthrough-result parameter do not.
- Completion cases publish inner finalizers and captured-variable bindings, require prior readers to reject version 3 before transpilers or state changes, and then remove records through the lowest remaining supported format.
- Test declaration rejection independently of active state. Automatic-body declarations, the finalizer role, and captured-argument markers must fail deliberately in earlier readers after current attributes materialize. Current must accept the same declaration and publish automatic patches under the actual generated body.
- Current/current cases rebuild an outer method with a real `finally` and private callback-owned state. Ordinary and inner patches must share the intended state while retaining the exact callback assembly.
- Distinct callback assemblies with the same full name must execute their own implementations where the emitter can retain their identity. If a wrapper's metadata cannot distinguish both dependencies, reject before publication and retain the previous behavior. This differs from duplicate-MVID ambiguity.

Published-state protection cannot guard a declaration that has not been installed. A version-1 reader can reach binding before rejecting an old method-style attribute that requests `__originalMember`; the generalized `InnerTargetKind.Method` declaration supplies the earlier rejection marker. Do not claim the stronger declaration guarantee for the older form.

## Artifacts, backends, and runtime coverage

Published Fat assets for 2.4.0.0, 2.4.1.0, 2.4.2.0, and 2.3.6.0 are pinned in [published-artifacts.json](../HarmonyTests.Compatibility/published-artifacts.json). NuGet package versions omit the final `.0`. Keep origin, package/DLL hashes, actual identities, module IDs, and dependencies in the manifest or child reports. Thin packaging needs its own ordinary dependency-resolution control.

| Lane | Required purpose |
| --- | --- |
| net9/x64 JSON, published 2.4.2 | Focused actual-runtime ordinary and Infix compatibility. No major-version roll-forward. |
| net8/x64 JSON and BinaryFormatter, all four releases | Separate children with the formatter switch explicitly false/true; verify both engines' selected backend before writing. |
| net472/x64 Windows .NET Framework | Actual two-engine ordinary control and dependent Infix cases with BinaryFormatter. |
| net472/x64 classic Mono | Actual binding, loading, formatter, and Mono replacement-map checks; preserve explicit loader-boundary classifications when coexistence is not reached. |
| Prior-Infix source baselines | net9/JSON and net8/BinaryFormatter extension/completion checks with required coexistence. |
| Release platform coverage | Relevant normal CoreCLR/Mono suites, supported legacy representation, and package/API checks without multiplying every selector across every runtime. |

The [in-box BinaryFormatter throws on .NET 9](https://learn.microsoft.com/en-us/dotnet/core/compatibility/serialization/9.0/binaryformatter-removal). A replacement formatter or a net8 case rolled onto net9 does not prove the old backend. Test unavailable backends as explicit rejection, and never alter an engine's cached backend choice to manufacture a mixed process.

No-Infix JSON retains exact bytes for equivalent ordinary records with the same historical shape. Different historical shapes need readable interchange, not byte equality. BinaryFormatter recovery checks readability and patch contents; independently created empty payloads can differ in object sharing. Rejected updates must retain the exact previously stored bytes. Preserve actual old serializer output, including trailing buffer capacity.

## Persistence, recovery, and failed updates

For cold reconstruction, clear or verify absence of the resolved member cache before access. Assert that `Patch`, `InnerMethod`, `InnerTarget`, and nested Harmony-owned objects belong to the reader. Use an independent selector table covering exact/type-family/method-family/both-family cases, nested generics and declaring types, vectors, multidimensional arrays, and rank-one non-vector arrays. Compare complete reconstructed identity, positions, actual metadata injections, and explicit expected match sets through both backends.

State tests use repeated sites and loop iterations, exact/family records from one patch class, multiple readers, and same-named classes in different assemblies. Cover per-site reset, outer-state sharing, and rebuilds by both engines. Mutating input arrays or detached inspection results must not change installed snapshots.

Corrupt one identity component at a time: version, kind, required argument lists/counts, module/token, canonical type text, or a free generic parameter. Reordered properties and permitted unknown noncritical fields remain readable; duplicate/missing required fields, malformed versioned role data, invalid envelopes, and unavailable backends reject. No malformed identity may broaden a closed selector. Exercise representative errors through public registration/rebuild after decoder checks.

Duplicate target/patch modules belong in separate children. Check ambiguity at registration and cached/cold rebuilding, preserve state and behavior on failure, and allow complete owner removal. Also keep structurally readable unavailable-target recovery; every survivor must resolve before transpilers or publication.

For legacy recovery, first record what the old public API actually persists. When it cannot install an incomplete graph, seed a separately labeled recovery fixture with bytes from that release's own types and serializer. Use two invalid owners plus an ordinary survivor; partial removal/addition fails, complete removal succeeds, and a corrected retry advances the version once. Cover owner, method, inner-role, all-role, and class removal, as applicable. Unambiguous nongeneric legacy targets may normalize; ambiguous/generic incomplete targets remain non-executable. Corrupt syntax remains a read error.

Before each predictable failure, capture bytes, version, records, replacement mappings, and an execution trace. Fail target/position validation, identity serialization, later-site binding, and unsupported emission. Require the prior state and behavior to remain intact, including when an earlier selected site was valid. Correct the request and retry in the same child. Installation is per actual outer method; this does not promise rollback of user prepare/transpiler side effects, native failure, process crashes, or earlier jobs in a multi-target class.

## Evidence and release gate

Every child result must retain:

- Requested and actual runtime/version/architecture, framework asset, backend/switch, command, and loader policy.
- Compile-time fixture references and actual loaded engine/fixture/dependency identities, hashes, module IDs, contexts, and typed/reflected/attribute providers.
- Shared-dictionary identity, target identity, before/after state hashes and headers, decoded records, versions, and replacement mappings.
- Exact failure stage and exception chain, load/resolve events, transpiler entry/enumeration counts, callback counts, and target traces before/after/retry.

Classify success, loader/API boundary, compatibility rejection, known limitation, ordinary-control failure, feature failure, and unavailable infrastructure separately. A missing attribute type is not declaration-marker proof; zero counters mean nothing if the fixture never loaded. The [harness limitations](../HarmonyTests.Compatibility/README.md#known-boundaries-retained-by-the-probes) describe the specific ordinary loader and concurrent-update diagnostics.

Before release, run required discovery, state, and opposite compile/runtime-binding checks against all four published releases on applicable real runtimes. Execute both backends and representative supported CoreCLR, Mono, and Framework paths, plus the prior-Infix cases and normal argument/patching/reverse-patch and API/package checks. Use the same candidate artifacts. Report unavailable runtimes or failed ordinary controls as unresolved coverage; builds, source inspection, decoder-only success, and earlier candidate results cannot replace applicable mixed-engine execution.
