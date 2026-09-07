# Infix

Infix is implemented and unreleased. It supports selected method/property calls, field reads and writes, construction and literal loads; independent inner prefixes, postfixes and finalizers; automatic iterator/async body selection; captured-variable binding; and optional patch-body inlining.

## Documentation

- [User guide](../../Documentation/articles/patching-infix.md): declarations, targets, scope, ordering and argument write-back.
- [Odd cases and limits](../../Documentation/articles/patching-infix-limits.md) and [authoring recipes](../../Documentation/articles/patching-infix-authoring.md): supported workflows and executable examples.
- [Implementation specification](../../drafts/INFIX-NEW-IMPL-V3.md), [operation-target contract](../../drafts/INFIX-OPERATIONS-ADDENDUM.md) and [feature-completion contract](../../drafts/INFIX-FEATURE-COMPLETION.md): the core contracts and their operation/lifecycle details. All three describe the current implementation.
- [Testing strategy](TESTING-STRATEGY.md): required observations, regression lessons and runtime boundaries.
- [Compatibility strategy](../../drafts/INFIX-COMPATIBILITY-TESTS.md) and [runner documentation](../../HarmonyTests.Compatibility/README.md): mixed-version cases, pinned providers, reproduction commands and per-case reports.

## Current verification, 2026-09-07

These local results were recorded for the changes accompanying this document revision. Remote results are commit-specific: check the revision under test in [Platform Tests](https://github.com/pardeike/Harmony/actions/workflows/test.yml), [Infix compatibility](https://github.com/pardeike/Harmony/actions/workflows/test-infix-compatibility.yml), and [documentation CI](https://github.com/pardeike/Harmony/actions/workflows/docs.yml). A green run for an earlier revision does not validate later edits.

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
