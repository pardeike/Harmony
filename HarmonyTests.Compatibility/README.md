# Infix compatibility tests

This suite runs released Harmony binaries and the current Fat assembly in fresh child processes. It distinguishes a fixture compiled against another version from two engines updating the same patch state. The feature contract is [INFIX-NEW-IMPL-V3.md](../drafts/INFIX-NEW-IMPL-V3.md); the full case rationale is [INFIX-COMPATIBILITY-TESTS.md](../drafts/INFIX-COMPATIBILITY-TESTS.md).

The dedicated [Infix Compatibility workflow](../.github/workflows/test-infix-compatibility.yml) builds and runs the CoreCLR matrix, Mono binding/loading probes, and a Windows .NET Framework lane independently of the ordinary NUnit process. Native detours, assembly loading and serializer switches stay confined to each child. A child timeout, crash, unexpected exception or assertion failure fails the runner.

Final-candidate execution on 2026-09-05 met all 388 expectations across the nine CoreCLR lanes: 44 per lane, or 40 for pre-inner 2.3.6. Of those, 289 are successful behavior or intended API/compatibility boundaries; 99 characterize known loading/concurrency limits. The final Mono 2.4.2 run met 35 diagnostic expectations, but only 7 are executable behavior/API controls and none establishes mixed-engine Infix operation. Exact counts, hashes and local report locations are in [the execution notes](../drafts/INFIX-COMPATIBILITY-TESTS.md#1-what-has-been-checked). The workflow has not yet run on GitHub Actions.

## Run a lane

Use the .NET 10 SDK to build. Supply a completed current Fat DLL and an actual x64 runtime host for the requested framework. The script downloads the unchanged old NuGet package, verifies both package and DLL hashes against [published-artifacts.json](published-artifacts.json), snapshots the current DLL and its dependencies, and builds fixtures against explicit file references. Its projects have separate build properties and output directories from the main solution.

```bash
dotnet build Lib.Harmony/Lib.Harmony.csproj -c Debug \
  -p:TargetFrameworks=net9.0 -p:PlatformTarget=x64

CURRENT_HARMONY="$PWD/Lib.Harmony/bin/Debug/net9.0/0Harmony.dll" \
RUNTIME_HOST=/path/to/x64/dotnet \
FRAMEWORK=net9.0 BACKEND=json OLD_HARMONY_VERSION=2.4.2 \
bash HarmonyTests.Compatibility/run.sh
```

The child asserts the actual runtime major version and process architecture. Major-version roll-forward cannot turn a .NET 10 execution into net9 evidence. `RUNTIME_HOST` can be the ordinary `dotnet` host when it has the requested x64 runtime installed.

For the required net8 matrix, build the matching current net8 DLL once, then run each old release with both serializer settings:

```bash
dotnet build Lib.Harmony/Lib.Harmony.csproj -c Debug \
  -p:TargetFrameworks=net8.0 -p:PlatformTarget=x64

for old_version in 2.4.2 2.4.1 2.4.0 2.3.6; do
  for backend in json binary; do
    CURRENT_HARMONY="$PWD/Lib.Harmony/bin/Debug/net8.0/0Harmony.dll" \
    RUNTIME_HOST=/path/to/x64/dotnet \
    FRAMEWORK=net8.0 BACKEND="$backend" OLD_HARMONY_VERSION="$old_version" \
    bash HarmonyTests.Compatibility/run.sh
  done
done
```

Set `REPORT_DIRECTORY` to retain a separately named run. `CASE_FILTER` selects child names containing the supplied text, such as `cold-identity`, `active-state` or `foreign-`. `FEATURE_TESTS=0` runs the binding and ordinary-engine cases before a new API build is available. `SDK_HOST` overrides the SDK executable. The shell requires `curl`, `unzip`, `jq` and either `sha256sum` or `shasum`.

Standard output is the JSON summary. Build and progress diagnostics go to standard error. Each run retains the exact child request, complete result and diagnostic log under `reports/`; package downloads and current-engine snapshots stay in ignored `artifacts/` directories. Those files are not production patches or releases.

## What the assertions cover

| Cases | Required behavior |
| --- | --- |
| `old-control`, `new-control`, `compile-old-use-new`, `compile-new-use-old` | Direct typed API calls and class discovery, executable prefix/postfix behavior, transpiler execution, inspection and removal. A runtime refusing a newer fixture reference before entry is a separately reported loader boundary. |
| `ordinary-old-first`, `ordinary-new-first` | Two real Harmony objects naturally share state/replacement dictionaries. A prefix, another engine's postfix, rebuild and cross-engine removals preserve the expected trace and owners. No dictionary references or fake shared-state type are injected. |
| `prepare-false-missing-target` | The current prepare callback returns false before resolution of a nonexistent inner target; no patch, state or executable behavior changes. |
| `missing-api-*` | An old-only process rejects new attribute materialization or direct access to the new `HarmonyMethod.innerMethod` member without installing anything. |
| `declaration-*` | Nine declarations cover both roles, ordinary/inner role names, equivalent name plus attribute roles, attribute order, class targets, target methods and ordering annotations. Old discovery sees attributes supplied by current Harmony, then rejects the declaration marker. Pre-inner releases may ignore an unrecognized inner role name. Current discovery executes the same declaration as an Infix. |
| `active-state-*` | An old reader's inspection, addition and owner/method/all removals reject the envelope before entering or enumerating an already installed counter transpiler. State bytes, version, mappings and patch-body counters stay unchanged. Removing one of two Infixes retains the envelope; removing the last restores old-readable state and the original old operation succeeds on retry. |
| `cold-identity` | Both current readers reconstruct all four generic selector dimensions, nested declaring types and arguments, vectors, multidimensional arrays and rank-one non-vector arrays. Decoded graphs belong to the reader; caches are cleared; input and detached snapshots are independent. The second engine rebuilds real generic call sites, then the first rebuilds them again. A foreign `HarmonyOuter` attribute must mutate only the outer argument. Same-named classes from distinct fixture modules retain independent per-site/per-iteration `__state`, read by multiple postfixes, through both readers' rebuilds. Malformed envelopes and JSON identities reject; reordered properties and unknown noncritical properties remain readable. |
| `duplicate-module` | Loading the target DLL a second time makes its otherwise valid MVID/token identity explicitly ambiguous. This deliberate duplicate is isolated from positive cases. |
| `duplicate-patch-module-*` | The target stays unique while the patch fixture is duplicated byte-for-byte. New registration, an explicitly cached candidate rebuild and a cold public rebuild reject ambiguous patch identity without changing state or executable behavior. Owner removal still recovers. |
| `legacy-recovery-*` | The old public incomplete-inner operation is attempted and recorded first. A separate, explicitly labeled recovery setup serializes two targetless records using the released assembly's own graph and serializer. New inspection can expose them, additions and incomplete removals cannot rebuild, complete role/method removal succeeds, and a corrected addition advances the version once. The ordinary survivor remains unless remove-by-method also intentionally targets its method. |
| `foreign-*` | Ordinary foreign `CodeInstruction` conversion is tested separately from envelope protection, in both directions with private/default context placement and with/without contextual reflection. Known released-code and generated-proxy loading failures are recorded as limitations, not counted as Infix protection. |
| `concurrent-old-candidate` | A one-shot gate pauses an old transpiler after its unpublished candidate has read legacy state. Current installs and executes an Infix; resuming old overwrites it with the stale legacy candidate. This deterministic diagnostic records the inherited concurrent-update boundary, not a passing no-drop guarantee. |

The active-state counter is compiled against the old engine. Current Harmony converts and runs it during successful installation; old Harmony can run its own counter again after the last Infix is removed. This keeps the old/new rejection test independent of the released foreign-instruction conversion defect documented below.

Every child records requested and actual runtime, file hashes, module build identifiers (MVIDs), assembly references, load context or application domain, compile-time Harmony reference hash, actual typed-call provider, attribute/processor providers, load/resolve events, target traces and counters. Negative operations also record state hashes, replacement-map contents and version counts before and after the operation. The ordinary JSON controls compare exact bytes across current/released readers whose legacy record shape matches; pre-inner 2.3.6 is not required to emit the newer fields.

## Known ordinary loading boundaries

Published 2.4.2 reconstructs a foreign instruction list by converting its constructed runtime type back to a name and resolving that name from the core library. In two private CoreCLR contexts this can fail before the user transpiler runs. Current Harmony directly constructs the list from the actual element type. The `foreign-*` probes retain the published-old failure as `known-published-ordinary-limitation`, with a strict exception and stage assertion.

Putting one same-identity Fat Harmony in the default context exposes another ordinary boundary on the tested CoreCLR host: MonoMod's generated `ILGeneratorProxy` can bind its constraint to that copy while receiving the other copy's `CecilILGenerator`. The failure occurs during ordinary wrapper generation. The probe reports `known-ordinary-loader-limitation` and does not continue to claim feature protection in that arrangement.

These are independently diagnosed loading limits. `Passed: true` means the requested test expectations held. `HasPublishedLimitations: true` means at least one explicit diagnostic encountered a known loading or inherited concurrent-update limit. Read both fields; passing the suite does not claim that every loader policy permits arbitrary old/new transpiler composition.

Cross-engine host updates must be serialized. The envelope protects an old operation that reads already-published Infix state; it cannot invalidate an old engine's candidate that was read before the Infix existed. The concurrent diagnostic uses explicit gates and bounded waits, not timing sleeps, to reproduce that distinction.

## Pre-extension V3 and extended targets

The dedicated `v3` workflow job checks out commit `573914745451f8278720257db37a4a4ebaae853d` as the pre-extension source fixture. It builds that source and two current engines into isolated directories with test-only versions 2.4.3.0, 2.4.4.0 and 2.4.5.0. The commit check prevents a moving branch from silently replacing the baseline; child reports also retain each binary's hash and actual provider identity. This runs only on net9/JSON and net8/BinaryFormatter, not throughout the released-version matrix.

Set `V3_HARMONY=/path/to/v3/0Harmony.dll` with the usual `CURRENT_HARMONY`, `SECOND_CURRENT_HARMONY`, runtime and framework settings to run the same focused cases locally. The launcher snapshots all three engines and defaults to the `extensions-` case filter. This mode does not download or substitute a published old release.

The cases loop constructor, field-read and integer-constant selectors through cold decoding, opposite-engine rebuilds and removal. Both V3/current load orders must first pass the ordinary coexistence control. They then prove that generalized declarations reject before user callbacks/transpilers or installed state change, extended version-2 state blocks V3 reads/rebuilds/removals, and removing the last extended target restores readable version-1 method-Infix state. V3 must successfully rebuild that state and remove the remaining method Infix, leaving ordinary unframed state. Separate current/current cases verify that a reader uses its own selector type with the exact shared member/value and snapshotted positions.

Version 2 describes required behavior, not just target kinds. Method-only Infixes using the new `__originalMember` injection also require it, in either inner or outer scope; the V3 cases explicitly verify rejection before its transpiler runs. An exact original-argument binding or a passthrough postfix's first result parameter does not request that injection. Readers accept method-only version-2 records, and successful writes select the required version afresh. Missing or ambiguous callback metadata remains owner-removable: only resolvable callbacks are classified while reading old state, while rebuilding still validates every surviving callback before running transpilers.

That state protection starts after publication. A pre-extension V3 engine discovering the old method-style attribute plus the new injection can reach binding before rejecting it; this is not the generalized-declaration marker guarantee. The generalized `InnerTargetKind.Method` declaration deliberately remains distinguishable to that reader. V3 is an unreleased source baseline, and these tests do not claim a new parameter marker or a pre-publication guarantee for the old declaration form.

Local x64 execution passes all three children on net9/JSON and net8/BinaryFormatter, with no known-limit classifications. The published-2.4.2 net9/JSON matrix also includes one `extensions-released` child covering all three declarations, rejection of extended state before its transpiler runs, and recovery after removal; it passes locally too. Local current/current probes load the same completed binary into two real contexts; CI additionally builds distinct current test identities. A real workflow run is still needed for that exact CI configuration and the Windows lane.

## Mono and .NET Framework

The host also builds for net472 and uses one application domain with explicit `Assembly.LoadFile` plugin loading and owning-plugin dependency resolution. `MONO_PLUGIN_LOAD_MODE=bytes` selects the separately recorded `Assembly.Load(byte[])` probe. It reports the actual provider assemblies, so runtime assembly unification cannot stand in for two-engine proof. There is no CoreCLR contextual-reflection setting on this path.

```bash
dotnet build Lib.Harmony/Lib.Harmony.csproj -c Debug \
  -p:TargetFrameworks=net472 -p:PlatformTarget=x64

CURRENT_HARMONY="$PWD/Lib.Harmony/bin/Debug/net472/0Harmony.dll" \
RUNTIME_HOST=/path/to/mono-sgen64 \
FRAMEWORK=net472 BACKEND=binary OLD_HARMONY_VERSION=2.4.2 \
bash HarmonyTests.Compatibility/run.sh
```

The Mono runtime may be extracted into a task-local directory from an official package; installing it globally is unnecessary. The [official stable download](https://www.mono-project.com/download/stable/) supplies Mono 6.12.0.206. Published old assets are never rebuilt, renamed internally or modified.

On the tested Mono 6.12.0.206 x64 runtime, actual one-engine prefix/postfix/transpiler/removal, opposite compile/runtime binding, missing APIs and prepare-false cases execute successfully. Both file and byte loading bind unsigned typed Harmony references to the first copy. Two separately built current assemblies with test-only versions 2.4.3.0 and 2.4.4.0 do not change that result. A direct `--assembly-loader=strict` probe also retains the unification; [Mono documents that option for strong-named requests](https://www.mono-project.com/docs/about-mono/releases/5.2.0/).

The separately labeled `reflection-ordinary-*` cases route public APIs through the exact requested assembly rather than claiming typed binding. Their prerequisite state probe reaches a second real Harmony assembly, then fails during ordinary `HarmonySharedState` initialization: its embedded MonoMod `ILGeneratorShim.GetProxy` rejects generic arguments while constructing a `StackFrame` field accessor. Reversed engine order reaches the corresponding failure in the actual published old assembly. Neither the cross-engine public patch operations nor Infix operations are reached. Mono mixed-engine Infix compatibility therefore remains unproven on this loading arrangement.

Those exact stages are reported as `known-runtime-assembly-unification` or `known-ordinary-loader-limitation`, not feature rejections. `REQUIRE_COEXISTENCE=1` makes them fail the process instead of accepting the diagnostic expectation. The Mono CI job deliberately retains the controls and known-boundary reports; it does not claim a successful mixed-engine Infix lane.

To reproduce the distinct-version experiment without changing repository version files:

```bash
dotnet build Lib.Harmony/Lib.Harmony.csproj -c Debug \
  -p:TargetFrameworks=net472 -p:PlatformTarget=x64 -p:HarmonyVersion=2.4.3.0 \
  --artifacts-path /tmp/compat-first
dotnet build Lib.Harmony/Lib.Harmony.csproj -c Debug \
  -p:TargetFrameworks=net472 -p:PlatformTarget=x64 -p:HarmonyVersion=2.4.4.0 \
  --artifacts-path /tmp/compat-second

CURRENT_HARMONY=/tmp/compat-first/bin/Lib.Harmony/debug_net472/0Harmony.dll \
SECOND_CURRENT_HARMONY=/tmp/compat-second/bin/Lib.Harmony/debug_net472/0Harmony.dll \
RUNTIME_HOST=/path/to/mono-sgen64 FRAMEWORK=net472 BACKEND=binary \
REQUIRE_COEXISTENCE=1 bash HarmonyTests.Compatibility/run.sh
```

On Windows, use Git Bash with `RUNTIME_HOST=native FRAMEWORK=net472 BACKEND=binary`. The script builds an x64 host and executes its EXE directly; its children do the same. The Windows CI lane uses test-only current versions 2.4.3.0 and 2.4.4.0, published old 2.4.2, and `REQUIRE_COEXISTENCE=1`. It must execute the ordinary coexistence control before counting Infix cases. Its report directory contains a space to exercise native child argument quoting. This newly added lane still requires a successful Windows run; a portable build or Mono process launch is not Framework execution proof.
