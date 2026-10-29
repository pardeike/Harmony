# .NET 11 support

Implementation on `v3`, verified locally on 2026-10-29.

## Scope

- Use the published `MonoMod.Core 1.4.0-prerelease.1` package.
- Extend the existing .NET 10 framework, reference, test and CI settings for `net11.0`.
- Build with SDK `11.0.100-rc.3.26522.148` and test runtime `11.0.0-rc.3.26522.148`.
- Preserve the legacy framework targets and conditional test dependencies.
- Test release packages against the newest advertised framework.

## Native async boundary

A real runtime-async target compiled in `TestLibrary` returned the expected value before patching. Installing a normal postfix succeeded, but calling the replacement threw `NullReferenceException`.

Runtime async has a different calling convention and multiple entry paths. Supporting it needs more than copying the IL into an ordinary replacement. This agrees with the limitations in [MonoMod's .NET 11 work](https://github.com/MonoMod/MonoMod/pull/307).

For now, Harmony rejects the .NET 11 `MethodImplAttributes.Async` flag before replacing a method or publishing patch metadata. Reverse patches check both source and stand-in. Ordinary Task-returning methods and compiler-generated async state machines remain supported.

## Packaging and runtime details

The prerelease contains a `net11.0` asset and a CoreCLR 11 implementation, but its build guard still rejects framework versions above 10. The repository override is limited to `net11.0` with that exact prerelease. Thin includes a `buildTransitive` workaround limited to .NET 11, including OS-qualified targets such as `net11.0-windows`, so consumers need no manual override. Remove these workarounds when MonoMod updates its guard.

.NET 11 RC3's native `DynamicILGenerator.DeclareLocal` rejects function-pointer local types. The signature-only consumer fixture now uses `ReadMethodBody(method, null)` without declaring unused locals. The tests still re-emit and execute the real managed and unmanaged `calli` signatures. This does not add support for declaring function-pointer locals through the runtime emitter.

The documentation compilation project used one output directory for all frameworks. Removing its overrides restores the SDK's separate framework directories and prevents concurrent builds from overwriting each other.

SDK 11's VSTest 18.11 removed Mono execution support. CI still compiles with SDK 11, but Unix Mono lanes explicitly select the installed SDK 10 VSTest runner. The existing framework and completed-report checks remain mandatory.

## Executed checks

| Check | Result |
| --- | --- |
| Full .NET 11 Release suite, macOS x64 through Rosetta | 1,248 passed, 2 skipped |
| Full .NET 11 Release suite, macOS ARM64 | 1,248 passed, 2 skipped |
| Full .NET 10 Release suite, macOS x64 | 1,244 passed, 2 skipped |
| Complete solution build across the retained framework matrix | Passed |
| Independent Fat and Thin NuGet consumers on .NET 11 x64 | Build, patch, execute and unpatch passed |
| Local workflow tests, release tooling tests and YAML parsing | Passed |
| SDK 11 and SDK 10 VSTest dispatch against the .NET 10 MethodInvoker tests | Both passed |
| PowerShell CI completeness and runner-selection regressions | 39 passed |

Cross-platform execution remains subject to GitHub CI. No release has been published.
