# Review verification, 2026-09-08

These probes verify the two supplied review findings and the nine candidate issues recovered from Claude session `57cd0364-3dfb-4990-84be-fe40bfbf1a16`. They use runtime calls, separately loaded plugin assemblies, and base/current comparisons. They do not change Harmony implementation code or run as part of the normal test suite.

Eight defects were verified in the original review and have now been fixed and removed from [TODO.md](../../TODO.md). Three candidates remain excluded for the reasons below.

## Fix verification

The fixes were checked in the working tree based on `bef9be62` (the intervening commit after `7700fd56` changes documentation). The contract remains [Infix V3](../../drafts/INFIX-NEW-IMPL-V3.md), its [operation-target addendum](../../drafts/INFIX-OPERATIONS-ADDENDUM.md), and the [feature-completion contract](../../drafts/INFIX-FEATURE-COMPLETION.md).

| Finding | Fixed behavior and regression coverage |
| --- | --- |
| T01 | Ordinary emitted prefix/postfix callbacks and transpiler calls work in exception wrappers. Six new NUnit cases cover normal and exceptional returns; the original emitted-callback probe returns 99 for both targets. |
| T02 | Inner state and named outer locals use the actual declaring type. The two separately loaded plugin builds now produce 3 in both probes. |
| T03 | An explicitly null catch type starts a filter handler; an omitted type still catches all exceptions. Two new NUnit cases and both filter probes return 7. |
| T04 | Retired generated assemblies can collect on .NET 9. After 10 and 20 patch/unpatch cycles, one additional collectible assembly remains for the current replacement, versus 20 and 40 noncollectible assemblies before the fix. The same bound holds with dynamic callback proxies. Held retired wrappers remain callable; released wrappers collect. An in-flight wrapper completes after removal and collection, and its stack frame still maps to the original. |
| T05 | Ordinary callbacks named `InnerPrefix`, `InnerPostfix`, and `InnerFinalizer` and the reverse-patch stand-in work. The named cases and renamed controls return the same expected values; existing declaration-validation tests still reject actual inner-only metadata on ordinary paths. |
| T06 | Removal by a held callback method succeeds after a duplicate module load for all three inner roles. A new NUnit case proves removal preserves an unrelated invalid callback and that surviving metadata still fails validation. |
| T10 | Ordinary metadata reserializes byte-for-byte after its callback assembly unloads, through both library and public JSON APIs. A new NUnit case also covers metadata with an unavailable callback. |
| T11 | Method and field selector dictionary lookups and self-equality survive duplicate module loads. Registration still rejects the ambiguous module. A new NUnit case preserves dictionary lookup across legacy identity normalization. |

The full .NET 9.0.19 x64 suites pass in **Debug and Release, 992 tests each**, including 11 new cases. Both builds also pass **21 isolated fixed-behavior cases and controls**; each run includes 11 baseline comparisons, for 32 processes per configuration. [results-after-fixes.json](results-after-fixes.json) contains the assertions, observations, and assembly hashes. The original [results.json](results.json) is retained unchanged as the failing comparison record.

The wrapper-lifetime check also verifies lookup through the actual shared dictionary used by the older baseline Harmony assembly. It does not claim that all of that older engine's public APIs work side by side: initializing its separate MonoMod implementation encounters an existing proxy-type conflict in this fixture. Older .NET Framework and Mono runtime paths were not executed in this fix pass. Existing broader runtime evidence and limits remain in [docs/infix/README.md](../../docs/infix/README.md).

## Original review configuration

- Current source: `7700fd56`, rebuilt from the working checkout with no production source edits.
- Base source: `2bece8f8466a38fa2b591bd17dfd733c52acdedc`, rebuilt from a fresh `git archive` in a separate directory.
- Both Harmony assemblies: Debug, `net9.0`, `PlatformTarget=x64`, SDK 10.0.301.
- Probe executable: Release, `net9.0`, x64.
- Runtime: actual .NET 9.0.19 x64 on macOS, `DOTNET_TieredCompilation=0`, patch-only roll-forward.
- 33 separate process runs. Thirty-one exercised their intended cases. The two extern runs recorded a failed prerequisite before unpatching.

[results.json](results.json) records each observation and the exact assembly hashes. No all-framework or full-suite result is claimed by this pass. The base archive build emitted only the expected missing-repository/SourceLink warnings.

## Originally verified defects

| TODO | Probe cases | Observed evidence |
| --- | --- | --- |
| T01 | `emitted-callback` | Base returns 99 for both targets. Current returns 99 for the plain control and rejects the emitted assembly for the exception wrapper. |
| T02 | `inner-state`, `named-state` | Both storage probes produce 4 instead of 3. Real plugins have equal full identities, different module IDs, and different declaring types. Unpatch restores the original result. |
| T03 | `filter-constructor`, `filter-initializer` | Current constructor-based filter throws; assigning the null catch type afterward returns 7. Base rejects both, so this is a new API defect rather than a previously working filter regression. |
| T04 | `assembly-retention` | Current adds 20 then 40 retained, noncollectible assemblies after 10 then 20 cycles. Base adds zero. Both preserve invocation results. Memory bytes and resolver latency are unmeasured. |
| T05 | `ordinary-names`, `ordinary-control`, `reverse-name`, `reverse-control` | Base accepts the bare inner-role names on ordinary paths. Current rejects them. Renamed controls pass on both. Expected ordinary results are 42, 3, and 0; reverse-patch result is 1. |
| T06 | `remove-prefix`, `remove-postfix`, `remove-finalizer` | All three install and change 10 to 11. After a duplicate callback-module load, removal by the held method throws and leaves 11. Owner removal succeeds and restores 10. |
| T10 | `json-unloaded`, `json-public-unloaded` | After actual callback assembly unload, readable metadata cannot be reserialized because the writer dereferences a null callback. The library serialization path fails on both builds; current public JSON serialization also fails. This is pre-existing. |
| T11 | `method-selector-equality`, `selector-equality` | An unchanged method-selector dictionary key and self-equality work at the base after a duplicate module load. Current throws on both operations, including for `InnerTarget`. No selector or positions mutation is involved. |

## Candidates left out of TODO

| Former item | Executed result and disposition |
| --- | --- |
| T07, duplicate shared-state startup | Two real base copies create two `HarmonySharedState` assemblies. Current rejects initialization; a third base copy creates a third state instead. Rejection prevents joining already-split patch state. Removing this safeguard is not justified as a bug fix. |
| T08, exception-marker layout | An unchanged-body transpiler observes two begin markers at the base and one now. Both builds return the correct `[7, 11, 3]` across two catches and normal completion. No previously working consumer failure was established. |
| T09, extern unpatching | Both builds behave identically before unpatching. Direct native calls still return 3. Reflection returns the transpiler's -3 once, then 3 on subsequent calls. The probe cannot establish a stable replacement body before testing removal. No unpatch regression is claimed. |

The earlier reports' other discarded claims, such as requiring old Harmony to read active Infix state, conflict with the specified compatibility boundaries and were not reinstated.

## Reproduce

Build completed Fat Harmony DLLs from both revisions in separate source directories. In each directory, build only the requested framework and architecture:

```sh
dotnet build Lib.Harmony/Lib.Harmony.csproj -c Debug \
  -p:TargetFrameworks=net9.0 -p:PlatformTarget=x64 \
  -p:GeneratePackageOnBuild=false \
  -p:CustomBeforeMicrosoftCommonTargets=/absolute/path/to/ReviewProbes/net9-reference-pack.targets
```

The supplied targets file aligns the .NET 9 reference pack with this repository's 9.0.19 package references. SDK 10.0.301 otherwise selects 9.0.17 locally, causing the Fat merge to embed another copy of `System.Text.Json`; seven existing public JSON tests then fail because their converter attributes belong to the embedded copy. The aligned Debug and Release builds have no such failures. Use fresh build outputs when changing reference packs. This test setting does not alter the production build defaults.

From the repository root, supply absolute paths to both DLLs and an x64 .NET 9 runtime executable:

```sh
python3 HarmonyTests.Compatibility/ReviewProbes/run.py \
  --runtime /path/to/net9-x64/dotnet \
  --current /path/to/current/Lib.Harmony/bin/Debug/net9.0/0Harmony.dll \
  --baseline /path/to/base/Lib.Harmony/bin/Debug/net9.0/0Harmony.dll \
  --output /path/to/review-results
```

The script uses `dotnet` for builds; `--sdk /path/to/dotnet` overrides it. It builds the probe and two plugin variants, snapshots both engines into separate execution directories, and launches each case in a fresh process. All artifacts and logs go under `--output`. Use a fresh output directory for each verification run.

Select fewer cases with repeated `--case` options, such as `--case ordinary-names --case ordinary-control`. Infix-only cases are not run against the pre-Infix base. The native case uses macOS `libSystem` and must be omitted on other platforms.

Without `--verify-fixes`, exit zero means observations were collected without an unexpected probe failure; expected library failures remain in the result objects. Read `errors`, `probeError`, `incomplete`, and `unpatchNotExercised`, and compare with the controls above.

Add **`--verify-fixes`** to assert the corrected behavior of the current engine. It selects the 21 fixed cases and controls by default, excludes the unresolved extern prerequisite and intentionally rejected shared-state startup cases, and returns nonzero if any required observation fails or is missing. Baseline failures remain recorded as comparisons. Repeated `--case` options can narrow this assertion mode too. The assertions also reject the failures in the original review results.

To run the normal suite with the same local configuration, set `DOTNET_ROOT_X64` to the directory containing the x64 .NET 9 host and use an absolute artifact directory:

```sh
DOTNET_ROOT_X64=/path/to/net9-x64 DOTNET_ROLL_FORWARD=LatestPatch \
  dotnet test HarmonyTests/HarmonyTests.csproj -c Debug -f net9.0 \
  -p:TargetFrameworks=net9.0 -p:PlatformTarget=x64 \
  -p:GeneratePackageOnBuild=false \
  -p:CustomBeforeMicrosoftCommonTargets=/absolute/path/to/ReviewProbes/net9-reference-pack.targets \
  --artifacts-path /path/to/fresh/debug-artifacts
```

Repeat with `-c Release` and a separate artifact directory. The Fat DLL supplied to the probes is under `bin/Lib.Harmony/debug_net9.0/0Harmony.dll` or `bin/Lib.Harmony/release_net9.0/0Harmony.dll` inside those directories.

The JSON comparison uses the library's serialization entry points because the base embeds its own JSON implementation. A separate current-only case verifies public `JsonSerializer` calls. Neither case installs the serialized callback; the tested behavior is metadata round-tripping after a real assembly unload.
