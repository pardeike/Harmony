# Infix

Infix is implemented and unreleased. It supports selected method/property calls, field reads and writes, construction and literal loads; independent inner prefixes, postfixes and finalizers; automatic iterator/async body selection; captured-variable binding; persistent patch-owned state across await/yield; and optional patch-body inlining.

## Documentation

- [User guide](../../Documentation/articles/patching-infix.md): declarations, targets, scope, ordering and argument write-back.
- [Odd cases and limits](../../Documentation/articles/patching-infix-limits.md) and [authoring recipes](../../Documentation/articles/patching-infix-authoring.md): supported workflows and executable examples.
- [Implementation specification](../../drafts/INFIX-NEW-IMPL-V3.md), [operation-target contract](../../drafts/INFIX-OPERATIONS-ADDENDUM.md) and [feature-completion contract](../../drafts/INFIX-FEATURE-COMPLETION.md): the core contracts and their operation/lifecycle details. All three describe the current implementation.
- [Testing strategy](TESTING-STRATEGY.md): required observations, regression lessons and runtime boundaries.
- [Compatibility strategy](../../drafts/INFIX-COMPATIBILITY-TESTS.md) and [runner documentation](../../HarmonyTests.Compatibility/README.md): mixed-version cases, pinned providers, reproduction commands and per-case reports.

## Review fixes, 2026-09-30

A review of the injection validation and opt-out work produced nine fixes, each developed test-first in its own commit:

- **Survivors no longer block a method.** Reference checks apply only to registrations added by the current operation, marked by a transient `Patch.candidate` flag that serialization drops. Survivors keep their accepted bindings; only structural requirements still apply to them. A passthrough pair is rechecked when either side is being added, and `__exception` is checked against `Exception` even without a finalizer. Before, one older registration that fails today's checks blocked every other owner's addition and removal on that method and stopped `UnpatchAll`, while its wrapper kept running.
- **Static originals keep Harmony 2's first-argument fallback** for instance `___field` and instance delegate injections, limited to a first argument that can hold the instance. A missing or unrelated first argument is rejected. Infix keeps its explicit-receiver contract.
- **Transpilers reject `uncheckedReferenceBinding`**, and transpiler records never raise the state version.
- **`Nullable<T>` follows the value/reference rule.** Boxing yields a boxed `T` or `null`; a reference argument declared as `int?` is rejected instead of failing at run time with `InvalidProgramException`.
- **Primitive receivers pass as managed pointers.** The replacement signature and `__instance` emission used `AccessTools.IsStruct`, which excludes primitives and enums, so patching `double.ToString(string)` or `ushort.CompareTo(ushort)` crashed even with an empty prefix. Harmony 2 returned garbage for the same reason.
- Compatibility cases prove override removal through the patch-call counter; `extensions-released` runs on the net10/JSON lane; the shipped XML documentation no longer says "new"; the pasted `TYPE-SAFE-INJECTIONS.md` plan is assimilated into the [core specification](../../drafts/INFIX-NEW-IMPL-V3.md#shared-patch-time-incompatibility-checks) and removed.

The new `PrimitiveReceivers` fixture has five cases; `InjectionValidation` and `UncheckedReferenceBinding` gained 28 cases, and `Primitive_receiver_is_not_boxed_by_the_ordinary_emitter`, which had encoded the primitive defect, became `Primitive_receiver_boxes_like_a_struct`.

| Local verification | Result |
| --- | --- |
| .NET 10.0.11 x64, complete Debug and Release suites | 1,158 passed in each; two existing explicit tests excluded |
| Published Harmony 2.4.2, complete .NET 10/JSON lane | 50 cases with their expected outcomes: 17 successes, 20 rejections, 2 API boundaries, 11 classified pre-existing limitations |
| Unchecked cases with distinct current engines, .NET 10/JSON | `unchecked-cold`, `unchecked-old-first` and `unchecked-new-first` pass with mandatory coexistence |
| Quiet workflow checks | Six tests pass |
| Formatting and whitespace | `dotnet format` on changed C# files; `git diff --check` passes |

The net10/JSON lane now has 50 cases because it includes `extensions-released`; the net8/BinaryFormatter lane keeps 49. Mono, .NET Framework, older CoreCLR versions, BinaryFormatter lanes and remote CI were not run for these fixes. One pre-existing limitation surfaced while writing the survivor tests and remains open: a generic method instantiation used as a patch callback resolves to its open definition after a shared-state round trip, because `Patch` identity is the definition's metadata token.

## Unchecked reference binding, 2026-09-30

`[HarmonyUncheckedReferenceBinding]` and `HarmonyMethod.uncheckedReferenceBinding` now opt an individual patch registration out of the new reference-type compatibility checks. This covers by-value and by-reference object bindings, existing boxing, reference passthrough results and injected delegates. Structural requirements and existing result/state/Infix contracts remain enforced. The [injection guide](../../Documentation/articles/patching-injections.md#unchecked-reference-binding) explains the API and its limits; the [core specification](../../drafts/INFIX-NEW-IMPL-V3.md#shared-patch-time-incompatibility-checks) records the implementation contract.

The flag is snapshotted in each `Patch` and travels with the resolved callback through sorting and emission. A checked registration remains checked even when another registration of the same callback opts out. Current engines retain the flag across serialization, factory resolution and rebuilds. Flagged methods require shared-state version 5, so older engines cannot silently discard the option. Removing the last opted-out registration restores the format required by surviving patches.

The new `UncheckedReferenceBinding` fixture contains 37 cases covering attribute discovery and manual registration, explicit `false`, shared callbacks with unrelated or boxed unused arguments, sibling ref returns, null-only bindings, all ordinary callback roles, factory resolution, registration isolation, rollback, metadata round trips, retained checks and open/nested/recursive generic type relationships.

| Local verification | Result |
| --- | --- |
| .NET 10.0.11 x64, complete Debug and Release suites | 1,125 passed in each; two existing explicit tests excluded |
| Final override fixture, .NET 10 Debug/Release and Mono 6.12.0.206 x64/net472 | 37 passed in each |
| .NET 8.0.30 x64, override and serialization fixtures | 40 passed, including JSON and BinaryFormatter round trips |
| All 12 configured target frameworks, Debug | Harmony, TestLibrary and HarmonyTests build successfully |
| Published Harmony 2.4.2, complete .NET 10/JSON and .NET 8/BinaryFormatter compatibility lanes | All 49 expected outcomes pass in each |
| Published Harmony 2.3.6, 2.4.0 and 2.4.1, .NET 8/JSON and BinaryFormatter | All 18 focused override outcomes pass, with mandatory coexistence |
| Pinned pre-override v3 reader `70593388a249879cff39372b9d6f62342a7d448d`, JSON and BinaryFormatter | Three override cases pass in each, with mandatory coexistence and no classified loader limitations |
| Formatting and whitespace | `dotnet format` on changed C# files; `git diff --check` passes |

Each complete published-version lane includes 17 successful execution cases, 19 expected state rejections, two expected missing-API boundaries, and 11 classified existing loader/concurrent-update limitations. The new cases prove current/current rebuilding, rejection by older readers in both initialization orders without changing published state or installed behavior, and older-reader recovery after removal. They do not remove the inherited concurrent-old-candidate boundary.

Mono initially exposed an invalid test assumption: its legacy `__result` assignability check accepts the tested sibling by-reference types. The retained-contract regression now uses a by-value result, and the complete final override fixture passes on Mono and .NET 10. Production result rules were not changed. The full .NET 10 runs preceded this test-only correction; both final focused runs passed afterward.

Full logs, framework artifacts, compatibility reports and Mono results are retained locally under `artifacts/unchecked-reference-binding/`; TRX reports are under `artifacts/tests/`. CI includes the new cases, a pinned version-4 reader and a net10/JSON published-version lane. These edits have not been tested by remote CI, and local Mono results do not establish Unity behavior.

## .NET 10 local verification, 2026-09-30

Routine local verification now uses .NET 10/x64; the canonical command is in [AGENTS.md](../../AGENTS.md#local-verification). The SDK was already on .NET 10. The local runtime host is now explicitly .NET 10.0.11 x64, with patch-only roll-forward. Earlier runtime results below retain their original versions.

The complete net10.0 Debug suite passes 1,088 tests with zero failures, including all 67 `InjectionValidation` cases. Two existing explicit tests are excluded. The executed report is retained locally at `artifacts/tests/run-j98f0ph_/tests_net10.0_20260930133844.trx`.

A separate six-case reference-return probe compares published Harmony 2.4.2 with current v3 using stand-in `Gene`, `SpecificGene : Gene` and `OtherGene : Gene` classes. The .NET 10.0.11 x64 results match the earlier .NET 9.0.19 x64 and Mono 6.12.0.206 x64 observations:

- `ref Gene __result` can replace a `SpecificGene` result with an `OtherGene`, for both ordinary and by-reference original returns. A `ref Gene` passthrough postfix also executes in both Harmony versions.
- `ref RefResult<Gene> __resultRef` for a `ref SpecificGene` original is rejected in both versions. An exactly typed `RefResult<SpecificGene>` callback that reinterprets a base slot using `Unsafe.As` still executes in both versions.
- A sibling-typed `ref OtherGene` passthrough postfix executes in 2.4.2 but is rejected by v3's new declaration checks.

Each executed case verifies the replacement object's identity and runtime type. Consumers use only the base `Gene` API; these observations do not establish safety for callers that rely on `SpecificGene` members. The validator does not inspect callback bodies or the objects written through a reference. Probe source, runtime identity, provider hashes and outcomes are retained locally under `artifacts/injection-validation/gene-ref-probe/`, including `results-net10.json`.

### Unchecked reference-binding scope investigation

On .NET 10.0.11 x64, one prefix shared by two concrete methods successfully reads only the matching argument under Harmony 2.4.2. Both arguments are passed by value; the unused argument has an unrelated reference type. Current v3 rejects that declaration. Replacing the unused argument with an `int` also executes under 2.4.2 because the existing emitter boxes it, and v3 rejects its boxed-type compatibility. A proposed reference-binding override therefore needs to cover object references supplied by existing boxing as well as reference-to-reference bindings.

A contrasting object-to-`Guid` case is rejected by the runtime with `InvalidProgramException` under 2.4.2, before the callback executes. Current v3 rejects its missing value/reference conversion earlier. Ignoring an argument does not make every value representation usable.

Fifteen direct checks of the current compatibility predicate cover open/nested generic parameters, self-referential and mutually constrained generic types, by-reference and array forms, nested covariance, and invariant closed classes/interfaces. All match the current policy; these are predicate checks, not proof that open generic methods can be patched. Concrete methods whose parameters use a self-referential generic base and nested covariant collections also execute their patches successfully in both Harmony versions. Source and recorded outcomes are retained under `artifacts/injection-validation/unchecked-scope-probe/`, including `results-net10.json`. This investigation preceded the override implementation above.

## Conservative injection validation, 2026-09-30

The shared emitter now rejects proven binding incompatibilities before installing a replacement or publishing patch state. The [injection guide](../../Documentation/articles/patching-injections.md#patch-time-checks-in-harmony-3) describes the policy. Existing Infix restrictions remain in force.

The new `InjectionValidation` fixture adds 67 cases. It covers diagnostic context, ordinary roles and Infix, actual storage and emitted boxing, permissive reference relationships, arrays and variance, exact names and aliases, factory callbacks, delegate signatures and constructor-based callback handles, passthrough chains, unchanged instruction sequences, failed-addition rollback and removal of invalid legacy registrations. The existing `Test_CompatibleStateTypes` and `DifferingStateTypesSuccessPatch` are unchanged and pass.

| Local verification | Result |
| --- | --- |
| .NET 9.0.19 x64, complete Debug suite | 1,088 passed; two existing explicit tests excluded |
| .NET 9.0.19 x64, complete Release suite | 1,088 passed; two existing explicit tests excluded |
| Mono 6.12.0.206 x64, net472 focused bindings | 190 passed; one existing explicit test excluded |
| All 12 configured target frameworks, Debug and Release | Build succeeded for Harmony, TestLibrary and HarmonyTests |
| Published Harmony 2.4.2, .NET 9/JSON | All 47 compatibility-runner cases meet their expected outcomes |
| Published Harmony 2.4.2, .NET 8/BinaryFormatter | All 46 compatibility-runner cases meet their expected outcomes |
| Quiet workflow checks | Six tests pass, including failure/report handling, argument forwarding and cancellation of a descendant that ignores SIGTERM |
| Formatting and whitespace | `dotnet format` on the three changed C# files; `git diff --check` passes |

The Mono selection includes `InjectionValidation`, `PatchCallBindings`, `Arguments` and `InfixBindings`. It uses NUnitLite 3.14 because the installed SDK's VSTest distribution lacks `testhost.net472.exe`. The extracted Mono runtime needs its `lib` directory **and `/usr/lib`** in `DYLD_FALLBACK_LIBRARY_PATH`; replacing the system fallback caused native-loader failures before that setting was corrected. The successful run uses the same final net472 build as the framework matrix.

Each published-version compatibility lane includes 16 successful execution cases, two expected missing-API boundaries, and 11 classified pre-existing loader/concurrency limitations. JSON also has 18 expected compatibility rejections; BinaryFormatter has 17. Passing the runner means those expectations held; it does not turn the limitation cases into working coexistence arrangements.

The canonical local command and .NET 10/x64 setup are in [AGENTS.md](../../AGENTS.md#local-verification). Full logs, TRX results, the focused Mono runner/results, and compatibility reports are retained locally under ignored `artifacts/tests/` and `artifacts/injection-validation/`. Framework builds report existing NuGet advisory warnings; they do not establish runtime behavior on those frameworks. These local results precede the CI follow-up below. Unity remains outside these checks. Run the existing platform and compatibility release gates before publishing.

### CI fixture correction

The [first platform run](https://github.com/pardeike/Harmony/actions/runs/36690793175) for `8392401` failed four new test cases across several runtimes. Their `GenericTarget<T>.Echo` targets encountered generic detour limitations: callbacks were bypassed on affected CoreCLR configurations, and Framework failed inside MonoMod's `GetMethodDescForSlot`. The same four failures reproduce locally on .NET Core 3.1.32 x64. The downcast and array/delegate runtime tests now use concrete targets with the same argument and return types; all assertions remain in place. All 67 fixture cases pass on .NET Core 3.1 x64 in Debug and Release and on .NET 9 x64 in Debug. Generic binding emission cases remain covered without installing generic detours.

That run also had a separate net35/x86 Debug build crash in ILRepack's native PDB writer, before tests ran. Its recurrence requires a fresh Windows CI observation. The [Infix compatibility run](https://github.com/pardeike/Harmony/actions/runs/36690792916) and [documentation build](https://github.com/pardeike/Harmony/actions/runs/36690792840) passed for `8392401`. Remote results remain specific to the tested revision.

## Second-review fixes, 2026-09-08

All six verified follow-ups are fixed: shared-state startup, public annotation merging, Infix metadata equality, repeated rebuild validation, literal matching allocations, and unused binding helpers. The [verification record](../../HarmonyTests.Compatibility/ReviewProbes/PART2.md) contains before/after observations, focused checks and runtime boundaries. [TODO.md](../../TODO.md) has no remaining verified items from these two reviews.

The full .NET 9.0.19 x64 Debug and Release suites each pass 1,021 tests, and all 12 configured target frameworks build in Debug. Mono net35/net452 metadata, target and persistence checks pass. Published 2.4.2 compatibility checks pass in .NET 9/JSON and .NET 8/BinaryFormatter, with existing loader/concurrency limitations classified separately; current-copy startup succeeds in both CoreCLR lanes. The startup probe on Mono encounters assembly unification, so it does not prove concurrent initialization there. CI results remain revision-specific.

## Persistent-state verification, 2026-09-08

`ArgumentMode.Persistent` adds explicit execution-scoped state while preserving ordinary named-local lifetimes. The candidate was built in Debug and Release across all configured target frameworks; the documentation project also builds across that matrix with zero compiler warnings or errors.

| Local runtime / scope | Passed | Failed |
| --- | ---: | ---: |
| .NET 9.0.19 x64, full Debug suite | 1,021 | 0 |
| .NET 9.0.19 x64, full Release suite | 1,021 | 0 |
| .NET Core 3.1 x64, persistence fixture | 28 | 0 |
| Mono 6.12 x64, net452 persistence fixture | 25 | 0 |
| Mono 6.12 x64, net35 persistence fixture | 13 | 0 |
| Mono 6.12 x64, net452 async fixture using the net35 Harmony DLL | 25 | 0 |

Two existing explicit tests are excluded from the full default suites. Persistence coverage includes actual suspensions, concurrent and nested calls, Task/ValueTask/async void, pooled builders where available, notification-only awaiters, synchronous and async iterators, awaited disposal, faults, cancellation, live unpatch/rebuild, typed slot replacement, weak cycles, finalizer helpers and optional inlining. Ordinary `__var_name` locals still reset per body invocation.

The version-3 source baseline (`31e14691e96265b05522cbe8d563186785b1bf4e`) passes all three persistence compatibility cases on .NET 9/JSON and .NET 8/BinaryFormatter: prior-reader rejection in both load orders and current/current rebuilding during suspension. These cases establish actual coexistence and have no classified loader limitations. The workflow additionally tests version-1/2 declaration and state rejection for persistence. Remote results remain revision-specific; use the workflow links below to inspect the exact candidate.

The net35 test runs on Mono's CLR 4 implementation and verifies selection of its native weak table. The net35 DLL also passes the async fixture compiled for net452 there. Substituting it into .NET 9 fails in existing `AccessTools` remoting initialization on both the candidate and the pinned baseline, before any Infix registration; use the appropriate CoreCLR asset. It does not prove abandoned-cycle collection on the original CLR 2.0, which requires explicit disposal, completion or unpatching for values that point back to their enumerator. Custom/future async protocols, generated helper boundaries and allocation costs are described in the [persistent-state limits](../../Documentation/articles/patching-infix-limits.md#persistent-state-follows-one-generated-execution). Unity still requires its own runtime evidence.

## Earlier verification, 2026-09-07

The following local results predate the persistent-state addition. Remote results are commit-specific: check the revision under test in [Platform Tests](https://github.com/pardeike/Harmony/actions/workflows/test.yml), [Infix compatibility](https://github.com/pardeike/Harmony/actions/workflows/test-infix-compatibility.yml), and [documentation CI](https://github.com/pardeike/Harmony/actions/workflows/docs.yml). A green run for an earlier revision does not validate later edits.

Complete local suites pass on actual x64 runtime hosts, without major-version roll-forward:

| Runtime / configuration | Passed | Failed | Exclusions |
| --- | ---: | ---: | --- |
| .NET 9.0.19, Debug | 981 | 0 | Two explicit tests are not selected by default. |
| .NET 9.0.19, Release | 981 | 0 | Two explicit tests are not selected by default. |
| .NET 8.0.30, Release | 980 | 0 | Two explicit tests are not selected by default. |
| .NET 5.0.17, Release | 959 | 0 | 21 JSON-only skips and two explicit tests. |
| Mono 6.12.0.206, net452 Debug assembly | 941 | 0 | Six existing ignores and two explicit tests. |

Debug and Release builds pass across all configured target frameworks. The documentation project also builds across all configured frameworks with zero compiler warnings or errors.

| Compatibility lane | Current local result |
| --- | --- |
| Published Harmony 2.4.2, .NET 9/JSON | 45 expected-outcome checks pass. |
| Published Harmony 2.4.2, .NET 8/BinaryFormatter | 44 expected-outcome checks pass. |
| Pinned version-2 Infix baseline, .NET 9/JSON | All three required completion coexistence cases pass. |
| Pinned version-2 Infix baseline, .NET 8/BinaryFormatter | All three required completion coexistence cases pass. |

Each published-version run includes 11 classified existing loader/concurrency limitations. Passing those checks confirms the expected limitation; it does not prove Infix execution in that arrangement. Provider identities, artifact hashes and individual classifications are retained in the compatibility reports.

The candidate includes executed regression coverage for unavailable or ambiguous target removal, unchanged wrappers after rejected updates, inlining fallback integrity, registration order after removal, and occurrence selection against the same post-transpiler body. It also executes async iterators across an incomplete await, verifies captured writes and processor removal, and re-emits parsed managed/unmanaged `calli` operands with and without ordinary DynamicMethod prefixes.

Local results do not establish Windows or Unity runtime support. Windows requires the applicable remote lanes; Unity needs separate runtime evidence. Around-operation/callable-original APIs, bounded selectors/match-count APIs, and Infix authoring tools/analyzers remain deferred follow-ups, not part of this implemented feature set.
