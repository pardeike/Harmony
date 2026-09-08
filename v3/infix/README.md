# Infix

Infix is implemented and unreleased. It supports selected method/property calls, field reads and writes, construction and literal loads; independent inner prefixes, postfixes and finalizers; automatic iterator/async body selection; captured-variable binding; persistent patch-owned state across await/yield; and optional patch-body inlining.

## Documentation

- [User guide](../../Documentation/articles/patching-infix.md): declarations, targets, scope, ordering and argument write-back.
- [Odd cases and limits](../../Documentation/articles/patching-infix-limits.md) and [authoring recipes](../../Documentation/articles/patching-infix-authoring.md): supported workflows and executable examples.
- [Implementation specification](../../drafts/INFIX-NEW-IMPL-V3.md), [operation-target contract](../../drafts/INFIX-OPERATIONS-ADDENDUM.md) and [feature-completion contract](../../drafts/INFIX-FEATURE-COMPLETION.md): the core contracts and their operation/lifecycle details. All three describe the current implementation.
- [Testing strategy](TESTING-STRATEGY.md): required observations, regression lessons and runtime boundaries.
- [Compatibility strategy](../../drafts/INFIX-COMPATIBILITY-TESTS.md) and [runner documentation](../../HarmonyTests.Compatibility/README.md): mixed-version cases, pinned providers, reproduction commands and per-case reports.

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
