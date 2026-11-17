# .NET 11 support

Implementation on `v3`. RC3 was verified locally and in GitHub CI on 2026-10-29. The 11.0.0 release was verified locally on 2026-11-17.

## Scope

- Use the published `MonoMod.Core 1.4.0-prerelease.2` package.
- Extend the existing .NET 10 framework, reference, test and CI settings for `net11.0`.
- Build with SDK `11.0.100`. CI installs the current 11.0 runtime, as it does for every other framework.
- Preserve the legacy framework targets and conditional test dependencies.
- Test release packages against the newest advertised framework.

## Native async boundary

A real runtime-async target compiled in `TestLibrary` returned the expected value before patching. Installing a normal postfix succeeded, but calling the replacement threw `NullReferenceException`.

Runtime async has a different calling convention and multiple entry paths. Supporting it needs more than copying the IL into an ordinary replacement. This agrees with the limitations in [MonoMod's .NET 11 work](https://github.com/MonoMod/MonoMod/pull/307).

For now, Harmony rejects the .NET 11 `MethodImplAttributes.Async` flag before replacing a method or publishing patch metadata. Reverse patches check both source and stand-in. Ordinary Task-returning methods and compiler-generated async state machines remain supported.

## Packaging and runtime details

MonoMod.Core 1.4.0-prerelease.2 accepts `net11.0`. The build-guard override and the Thin `buildTransitive` workaround for prerelease.1 are removed.

prerelease.2 detours the runtime-async variant of an ordinary `Task`-returning method. Patches reach runtime-async callers, also when the patch is installed before the first call. The bypass diagnostic `RuntimeAsyncCompileWatch` is removed. prerelease.2 refuses to detour a generic method instantiation on .NET 11 with `NotSupportedException`. It also compares the argument types of a detour, see the function pointer entry in `METHOD-BODY-PATCHING-VERIFICATION.md`.

.NET 11 RC3's native `DynamicILGenerator.DeclareLocal` rejects function-pointer local types. The signature-only consumer fixture now uses `ReadMethodBody(method, null)` without declaring unused locals. The tests still re-emit and execute the real managed and unmanaged `calli` signatures. This does not add support for declaring function-pointer locals through the runtime emitter.

The documentation compilation project used one output directory for all frameworks. Removing its overrides restores the SDK's separate framework directories and prevents concurrent builds from overwriting each other.

SDK 11's VSTest 18.11 removed Mono execution support. CI still compiles with SDK 11, but Unix Mono lanes explicitly select the installed SDK 10 VSTest runner. The existing framework and completed-report checks remain mandatory.

## Executed checks

| Check | Result |
| --- | --- |
| Full .NET 11.0.0 Debug suite with prerelease.2, macOS x64 through Rosetta and macOS ARM64 | 1,497 passed, 17 skipped on each |
| Full Debug suites with prerelease.2 on .NET Core 3.1, .NET 8, 9 and 10, macOS x64 | All passed |
| Full .NET 11 RC3 Release suite, macOS x64 through Rosetta | 1,248 passed, 2 skipped |
| Full .NET 11 RC3 Release suite, macOS ARM64 | 1,248 passed, 2 skipped |
| Full .NET 10 Release suite, macOS x64 | 1,244 passed, 2 skipped |
| Complete solution build across the retained framework matrix | Passed |
| Independent Fat and Thin NuGet consumers on .NET 11 x64 | Build, patch, execute and unpatch passed |
| Local workflow tests, release tooling tests and YAML parsing | Passed |
| SDK 11 and SDK 10 VSTest dispatch against the .NET 10 MethodInvoker tests | Both passed |
| PowerShell CI completeness and runner-selection regressions | 39 passed |
| [Platform Tests at `5266eead`](https://github.com/pardeike/Harmony/actions/runs/40915741928) | All 48 jobs passed |

CI covers .NET 11 on Windows x86/x64, Linux x64 and macOS x64 in Debug and Release. The retained .NET, .NET Framework, Mono and coexistence lanes also pass. No release has been published.
