# Second review verification, 2026-09-08

The Desktop report `Harmony-release-review-2026-09-08-part2.md` reviewed `2bece8f`..`f482fb5`. Its claims were rechecked against `df8b5e0d`, after the first review fixes and persistent Infix state were implemented. [TODO.md](../../TODO.md) contains only the resulting open work.

The probes in [Part2.cs](Part2.cs) run in separate .NET 9.0.19 x64 processes on macOS, with tiered compilation disabled. [results-part2.json](results-part2.json) records the observations, expected errors, engine hashes, and five additional startup trials. All seven case checks and the five additional trials matched the assertions described below. These are investigation probes, outside the normal test suite; no new runtime implementation changes were made for this review.

## Accepted findings

| TODO | Report item | Verified observation |
| --- | --- | --- |
| T12 | 2 | Two current Harmony copies initialized concurrently, with no shared state initially loaded, both succeeded but created two shared assemblies and distinct patch dictionaries. A third current copy threw `TypeInitializationException`. All six concurrent trials reproduced this. Sequential initialization created one assembly and one dictionary; the third copy succeeded. |
| T13 | 3 | Public annotation merging produced `methodType = -2147483648` and overwrote a class's explicit `Normal` target kind. The internal target resolver then returned null. The equivalent merge using `new HarmonyMethod(callback)` kept `Normal` and resolved the outer method. Normal class installation still succeeded. `GetOriginalMethod(HarmonyMethod)` itself is internal, contrary to the report's implication. |
| T14 | 4 | After a duplicate callback module load, a fresh Infix `Patch` record threw `SerializationException` in self-equality, hashing, and `HashSet<Patch>`. A record resolved before the duplicate kept its hash. Removal using the held callback succeeded and restored the original result, so the report's removal claim is already fixed. |
| T15 | 5–7 | Instrumented entry counts for one simple prefix registration: `ValidateSurvivingMetadata` 2, `RequiresInfixV3` 2, `RequiresInfixV4` 2, `RequiresInfixV2` 1, `ResolveModule` 15. The target still returned 10. This proves repeated work; it does not establish a particular startup delay or speedup. |
| T16 | 9 | After warmup, 10,000 calls to the same bound `InnerTarget.Matches` delegate with one reused, nonmatching double instruction allocated 880,000 bytes. Reusing a `nop` instruction allocated zero. Instructions and the delegate were allocated before measurement. |
| T17 | 12, 13, 19 | Source searches found no callers for the three `MethodCreatorConfig` accessors or `Infix.InnerMethod`. `Infix.Matches` and the five-argument `PatchBindingContext` constructor are used only by tests. Production position selection already has coverage through `InfixExecution`. This is cleanup, not an observed runtime failure. |

T12 differs from the first review's excluded T07. That earlier probe started with state already split by older copies; rejecting it remained the correct behavior. The new probe starts with zero shared-state assemblies and proves current copies can create the split themselves. Fix creation and field initialization together; do not remove the duplicate-state guard.

The startup copies use test assembly versions 2.4.4 and 2.4.5 built from the same current runtime implementation. The default-version copy is the third engine. Production code is unchanged between `aad5e346` and `df8b5e0d`; the intervening commits only strengthen GC probes.

## Fix verification

- T12: discovery, creation, and field initialization now share the AppDomain lock across Harmony copies. Fresh-process concurrent and sequential compatibility cases pass on .NET 9 x64, including ordinary patch/rebuild/removal and a later third copy sharing all three dictionaries. These cases also run in compatibility CI. Already split state remains rejected; older copies without this lock still need host coordination.

- T13: the public annotation reader clears the marker on copied metadata. The raw attribute still carries it, and missing or ambiguous inner targets remain deferred until registration. Public list/merge/import paths retain the class target; all 29 metadata and target tests pass on .NET 9 x64.

- T14: Infix equality and hashes now use stored callback module/token identity, independent of callback resolution. Ordinary records keep their existing behavior; ordinary and Infix records compare unequal so the two identity rules cannot violate equality consistency. Owners and selectors remain excluded. Nineteen metadata tests and both real duplicate-module compatibility cases pass on .NET 9 x64; executable rebuilds still reject ambiguity and normal removal recovers.

- T15: one rebuild now validates surviving metadata once and determines the required format version once, reusing that version for the payload and envelope. The same instrumented registration dropped from 15 to 9 module resolutions, with one validation and one capability scan. No cross-rebuild cache was added. All 79 focused metadata, target, captured/persistent-state, and serialization tests pass on .NET 9 x64. This measures work removed, not elapsed-time savings.

- T16: immutable literal metadata caches its decoded value; matching compares values or exact floating-point bits without formatting candidates. The same warmed 10,000-double probe now allocates zero bytes (previously 880,000), and its control remains zero. All 72 operation and target tests pass on .NET 9 x64, including signed zero and distinct NaN payloads after serialization. Compact integer opcodes still use the existing boxed normalization path; no claim of zero allocation for every opcode.

- T17: removed the unused local accessors, Infix member accessor, and obsolete position matcher. The constructor convenience now lives in TestTools; constructor zero-position rejection and the production position model remain covered. All 188 focused binding/position/execution tests pass on .NET 9 x64.

## Claims not added to TODO

| Report item | Disposition |
| --- | --- |
| 1 | Documented opt-in limit. The completion specification explicitly defines inlining as a snapshot until rebuild, and the user guide tells authors to rebuild the outer wrapper after changing patches on the callback. No dependency registry was promised. |
| 7, suggested fix | An unconditional cache shortcut is unsafe: strict rebuild validation must notice a duplicate module loaded after a callback was cached. The public `PatchMethod` getter already caches successful resolution; the validation path deliberately rechecks identity. T15 retains this constraint. |
| 8 | No measured bottleneck established for inlining analysis. Caching an entire verdict across rebuilds would miss changes to patches on the callback itself. Consider only after profiling, with that dependency preserved. |
| 10 | The loader's repeated assembly-name comparisons are visible in source, but no material cost was measured. Any future index must preserve duplicate-identity and actual-assembly checks. |
| 11 | The per-branch single-element array is visible in source. No separate TODO for this small allocation without evidence that it matters. |
| 14 | A duplicated exception-parameter guard is not a demonstrated failure. No separate cleanup item for removing a defensive validation check. |
| 15 | Cleared by execution. An `int` postfix taking `string` first was rejected with an explicit signature error. The prior result remained 12 and the previous single postfix remained installed. `MethodCreator.EmitPostfixes` checks the exact signature before emitting the callback. |
| 16 | Future-refactor concern, without a current signature mismatch or failing invocation. Precomputing all transport would need its own justification. |
| 17 | No demonstrated runtime regression or measured parsing cost. The local-signature fallback participates in function-pointer rejection; do not remove it solely because another reflection check looks similar. |
| 18 | Repeated finalizer return-type checks are not a verified defect. No isolated TODO for consolidating these guards. |
| 20 | Sharing a two-token helper does not justify additional coupling by itself. No observed failure. |

## Reproduce

Build a completed net9.0 x64 Harmony DLL first, following the aligned-reference instructions in [README.md](README.md). Build the probe and plugin with absolute paths:

```sh
dotnet build HarmonyTests.Compatibility/ReviewProbes/ReviewProbes.csproj -c Release \
  -f net9.0 -p:TargetFrameworks=net9.0 -p:ArtifactsPath=/tmp/harmony-part2-probes \
  -p:HarmonyPath=/absolute/path/to/0Harmony.dll
dotnet build HarmonyTests.Compatibility/ReviewProbes/Plugin/Plugin.csproj -c Release \
  -f net9.0 -p:TargetFrameworks=net9.0 -p:ArtifactsPath=/tmp/harmony-part2-plugin \
  -p:HarmonyPath=/absolute/path/to/0Harmony.dll
```

Run these cases separately, replacing `part2-annotations` in this command with `part2-patch-equality`, `part2-passthrough`, `part2-validation`, or `part2-constant-allocation`:

```sh
DOTNET_TieredCompilation=0 /path/to/net9-x64/dotnet \
  /tmp/harmony-part2-probes/bin/ReviewProbes/release_net9.0/ReviewProbes.dll \
  part2-annotations /tmp/harmony-part2-plugin/bin/Plugin/release_net9.0/ReviewPatchPlugin.dll
```

For startup, supply two additional current Harmony builds with different test assembly versions, as used by the compatibility harness. They must be distinct from the probe's referenced engine. Start a fresh process for each trial and the sequential control:

```sh
DOTNET_TieredCompilation=0 /path/to/net9-x64/dotnet \
  /tmp/harmony-part2-probes/bin/ReviewProbes/release_net9.0/ReviewProbes.dll \
  part2-shared-race /absolute/path/to/first/0Harmony.dll /absolute/path/to/second/0Harmony.dll
```

Replace `part2-shared-race` with `part2-shared-sequential` for the control. The concurrent probe synchronizes only the start of the two real type initializers; it does not alter Harmony code or inject a delay inside initialization. A race need not reproduce on every machine or run.

As in the earlier probes, exit zero means observations were collected without a failed prerequisite; expected library exceptions appear in the JSON. Compare the observations with the accepted-findings table. Do not interpret an empty or missing observation as a passing check.
