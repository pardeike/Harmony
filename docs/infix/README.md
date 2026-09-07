# Infix

Read the [Infix user guide](../../Documentation/articles/patching-infix.md) for examples, target selection, scope, ordering, and argument write-back.

The [implementation specification](../../drafts/INFIX-NEW-IMPL-V3.md) and its [operation-target addendum](../../drafts/INFIX-OPERATIONS-ADDENDUM.md) form the design reference for maintainers. The addendum extends selection to properties, fields, construction, and literal loads. The [testing strategy](TESTING-STRATEGY.md) describes coverage and remaining runtime risks.

The supporting [compatibility test strategy](../../drafts/INFIX-COMPATIBILITY-TESTS.md) explains how to test released and new Harmony versions together, including cases where compilation and runtime use different versions.

The older design notes have been removed to avoid conflicting instructions. This work has not been released.

## Operation-target validation, 2026-09-07

The extended targets, public `InlineSignature`, and their ordinary-patching regression tests pass the complete local suites:

| Runtime / configuration | Passed | Failed | Exclusions |
| --- | ---: | ---: | --- |
| .NET 9.0.19/x64, Debug | 727 | 0 | The existing explicit nullable-results test is not selected by default. |
| .NET 9.0.19/x64, Release | 727 | 0 | The same explicit test is not selected by default. |
| .NET 5.0.17/x64, Debug | 714 | 0 | 12 JSON-format cases require the newer JSON-enabled build; the explicit test is not selected. |
| Mono 6.12.0.206/x64, net452 Debug assembly | 704 | 0 | Six existing Mono finalizer ignores and one explicit test. |

The new cases exercise field reads/writes, property accessors, construction, literal identity, the constructor-capture/string-anchor workflow, recursion and concurrent calls, exception boundaries, repeated rebuild/removal with garbage collection, exact-name binding, capability-version recovery, and public signature consumption from a non-friend assembly. The .NET 5 run exposed and now covers an underlying emitter's stack-size undercount for returning `calli` instructions.

Debug and Release test projects and the documentation project build successfully. Release repacking reports mismatched debug symbols in the existing `MonoMod.ILHelpers` dependency; this does not prevent assembly generation or the Release test run. The PowerShell CI-report gate has 36 passing regression cases, including skipped tests and incomplete reports. Mixed-version and remote workflow results are recorded separately in the [testing strategy](TESTING-STRATEGY.md); earlier successful binaries below do not validate these changes.

[Compatibility CI](https://github.com/pardeike/Harmony/actions/runs/34093242634) passed all 16 jobs for `cde7122`, including actual Windows Framework coexistence and both previous-Infix-version lanes. The first platform run exposed an overly strict empty-state byte comparison on Windows and one non-reproducing ordinary-entry-point bypass in the macOS .NET 3.1 scheduling control. The test corrections and remaining runtime proof boundary are described in the testing strategy; neither issue justified changing production serialization or scheduling behavior.

## Review-fix validation, 2026-09-05 (before operation targets)

The review fixes preserve dynamic-method calls in exception-handling wrappers and distinguish genuine patch factories from method-valued postfix results. The Mono run also caught and fixed an unsupported attribute lookup on dynamic patch parameters.

| Check | Result |
| --- | --- |
| Complete NUnit suite, .NET 9.0.19/x64 | 561 passed, 0 failed; the existing explicit nullable-results test is not selected by default. |
| Focused exception/dynamic-method/result-postfix suite, Mono 6.12.0.206/x64, net472 assembly | 64 passed, 0 failed, 0 skipped. |
| Library and documentation projects, all configured target frameworks | Build succeeds with 0 warnings and 0 errors. |

The 42 added net9 cases cover dynamic prefix factories and transpiler calls, original and finalizer-added exception handlers, sibling catch boundaries, debug on/off, aliased references, private signatures, repeated/distinct dynamic operands, garbage collection, exception identity, ref returns, both method-valued postfix types, and complete factory-signature recognition. The ref-return cases require .NET 5 or later; Mono runs the other applicable cases. C# files were formatted with `dotnet format`.

The full .NET 8 and Mono suites and the mixed-version matrix were not rerun for these fixes. Their earlier results below are baseline evidence, not validation of the updated binaries. Remote CI and Windows/Unity execution remain unverified locally.

## Initial implementation validation, 2026-09-05 (before review fixes)

The complete NUnit suite was executed on macOS with actual x64 runtime hosts, without major-version roll-forward:

| Runtime | Passed | Failed | Existing exclusions |
| --- | ---: | ---: | --- |
| .NET 9.0.19 | 519 | 0 | One explicit nullable-results test is not selected by default. |
| .NET 8.0.30 | 518 | 0 | The same explicit test is not selected by default. |
| Mono 6.12.0.206, net472 assembly | 493 | 0 | That explicit test and six existing Mono finalizer ignores. |

The library and documentation projects build across all configured target frameworks with zero warnings and errors. Tests compile and execute the actual Infix documentation example. Exception-filter tests compare against a never-patched control so Mono's existing filter-exception behavior is not confused with an emission regression.

The separate [mixed-version suite](../../HarmonyTests.Compatibility/README.md) records exact old/new assembly providers, artifact hashes, successful feature checks, and measured ordinary loading/concurrency limitations. Its dedicated CI workflow covers CoreCLR and Mono; remote CI and Windows/Unity execution were not performed in this local pass. The user guide documents the unsupported function-pointer signatures and serialized-update requirement.
