# Infix compatibility tests

This suite runs released Harmony binaries and current Fat assemblies in fresh child processes. It tests both compile-time/runtime substitution and two engines updating shared patch state. The [compatibility strategy](../drafts/INFIX-COMPATIBILITY-TESTS.md) defines the required proof; [docs/infix/README.md](../docs/infix/README.md) is the current execution-status record.

The [Infix Compatibility workflow](../.github/workflows/test-infix-compatibility.yml) runs published-version CoreCLR lanes, prior-Infix source baselines, Mono loading probes, and Windows .NET Framework coexistence. Native detours, loaded assemblies, and serializer switches stay confined to each child. Timeouts, crashes, unexpected exceptions, and failed assertions fail the runner.

## Run published-version lanes

Run commands from the repository root. Use the .NET 10 SDK to build, a completed current Fat DLL, and an actual x64 runtime host for the requested framework. The launcher verifies unchanged old NuGet packages and DLLs against [published-artifacts.json](published-artifacts.json), snapshots the supplied engines and their dependencies, and builds fixtures against explicit file references.

```bash
dotnet build Lib.Harmony/Lib.Harmony.csproj -c Debug \
  -p:TargetFrameworks=net9.0 -p:PlatformTarget=x64

CURRENT_HARMONY="$PWD/Lib.Harmony/bin/Debug/net9.0/0Harmony.dll" \
RUNTIME_HOST=/path/to/x64/dotnet \
FRAMEWORK=net9.0 BACKEND=json OLD_HARMONY_VERSION=2.4.2 \
bash HarmonyTests.Compatibility/run.sh
```

The published CoreCLR matrix consists of net9/JSON against 2.4.2, plus net8 against 2.4.2, 2.4.1, 2.4.0, and 2.3.6 with both JSON and BinaryFormatter:

```bash
dotnet build Lib.Harmony/Lib.Harmony.csproj -c Debug \
  -p:TargetFrameworks=net8.0 -p:PlatformTarget=x64

for old_version in 2.4.2 2.4.1 2.4.0 2.3.6; do
  for backend in json binary; do
    CURRENT_HARMONY="$PWD/Lib.Harmony/bin/Debug/net8.0/0Harmony.dll" \
    RUNTIME_HOST=/path/to/x64/dotnet \
    FRAMEWORK=net8.0 BACKEND="$backend" OLD_HARMONY_VERSION="$old_version" \
    bash HarmonyTests.Compatibility/run.sh || exit $?
  done
done
```

The child asserts its actual runtime major version and architecture. The launcher permits patch-version roll-forward only, and children disable tiered compilation. These runs do not establish behavior under production JIT defaults. CoreCLR BinaryFormatter coverage uses net8 and its matching asset; running that case on net9 does not prove the older backend.

The shell requires `curl`, `unzip`, `jq`, and either `sha256sum` or `shasum`.

| Setting | Purpose |
| --- | --- |
| `CURRENT_HARMONY`, `RUNTIME_HOST` | Required completed Fat DLL and runtime executable. Use `native` for Windows .NET Framework. |
| `FRAMEWORK`, `BACKEND`, `OLD_HARMONY_VERSION` | Default to `net9.0`, `json`, and `2.4.2`. The net472 asset requires `binary`. |
| `SECOND_CURRENT_HARMONY` | Optional second current engine. Otherwise both current readers load the same snapshot into separate contexts. |
| `REPORT_DIRECTORY` | Use a distinct directory to retain each run; the default is `reports/<framework>-<backend>-<old-version>`. |
| `CASE_FILTER` | Select child names containing this text, such as `cold-identity`, `active-state`, or `foreign-`. |
| `FEATURE_TESTS=0` | Run binding and ordinary-engine controls without feature fixtures. |
| `SDK_HOST` | Override the build executable, which defaults to `dotnet`. |
| `REQUIRE_COEXISTENCE=1` | Fail net472 assembly-unification and ordinary-loader boundaries instead of accepting their diagnostic expectations. |
| `MONO_PLUGIN_LOAD_MODE=bytes` | Select the separately reported Mono byte-loading probe; the default uses files. |

## Prior-Infix source baselines

These unreleased baselines test rejection by earlier Infix engines, separately from published Harmony releases:

| State capability | Pinned source commit | Cases |
| --- | --- | --- |
| Version 1, method-only Infix | `573914745451f8278720257db37a4a4ebaae853d` | `extensions-` and `completion-` |
| Version 2, operation targets | `22d4069bac2bf963d8238dd1fd0940cdf57ae084` | `completion-` |

The workflow verifies those commits and builds them in isolated directories with test-only version 2.4.3.0. Its two current engines use 2.4.4.0 and 2.4.5.0. These numbers distinguish test identities; they are not releases. Both baseline lanes run on net9/JSON and net8/BinaryFormatter with `REQUIRE_COEXISTENCE=1`.

For a local run, build the pinned source and current engines first, then supply their completed DLLs:

```bash
PRIOR_INFIX_HARMONY=/path/to/prior/0Harmony.dll \
PRIOR_INFIX_STATE_VERSION=2 \
CURRENT_HARMONY=/path/to/current/0Harmony.dll \
SECOND_CURRENT_HARMONY=/path/to/second-current/0Harmony.dll \
RUNTIME_HOST=/path/to/x64/dotnet FRAMEWORK=net9.0 BACKEND=json \
REPORT_DIRECTORY="$PWD/HarmonyTests.Compatibility/reports/completion-v2" \
REQUIRE_COEXISTENCE=1 CASE_FILTER=completion- \
bash HarmonyTests.Compatibility/run.sh
```

Repeat with the version-1 baseline and `PRIOR_INFIX_STATE_VERSION=1`. Run its `extensions-` filter separately. `V3_HARMONY` remains an alias for the version-1 input. Without an explicit filter, version 1 selects `extensions-` and version 2 selects `completion-`. This mode snapshots the supplied baseline; it does not build or download it.

The extension cases cover constructor, field-read, constant, and method-only `__originalMember` capabilities. Completion cases cover inner finalizers, captured binding, automatic-body declarations, cold reconstruction by another current engine, and removal through state versions 3, 2, 1, then ordinary unframed state. Both prior/current initialization orders must first pass ordinary coexistence.

## Mono and Windows .NET Framework

The net472 host uses one application domain, explicit plugin loading, and dependency resolution through the owning plugin. Its reports identify actual providers, so unification onto one Harmony assembly cannot count as two-engine proof.

```bash
dotnet build Lib.Harmony/Lib.Harmony.csproj -c Debug \
  -p:TargetFrameworks=net472 -p:PlatformTarget=x64

CURRENT_HARMONY="$PWD/Lib.Harmony/bin/Debug/net472/0Harmony.dll" \
RUNTIME_HOST=/path/to/mono-sgen64 \
FRAMEWORK=net472 BACKEND=binary OLD_HARMONY_VERSION=2.4.2 \
bash HarmonyTests.Compatibility/run.sh
```

On Windows, run from Git Bash with `RUNTIME_HOST=native FRAMEWORK=net472 BACKEND=binary`. The runner executes the x64 host EXE directly. The Windows workflow uses published 2.4.2, current test identities 2.4.3.0 and 2.4.4.0, and `REQUIRE_COEXISTENCE=1`. Ordinary coexistence must succeed before any dependent Infix case counts.

To reproduce distinct current identities without editing version files, build twice with `-p:HarmonyVersion=2.4.3.0` and `-p:HarmonyVersion=2.4.4.0`, each using a separate `--artifacts-path`. Supply their `bin/Lib.Harmony/debug_net472/0Harmony.dll` outputs as `CURRENT_HARMONY` and `SECOND_CURRENT_HARMONY`. Published old assets remain unchanged.

## Coverage and reports

| Child family | Main assertions |
| --- | --- |
| `old-control`, `new-control`, `compile-*`, `ordinary-*` | Typed provider identity, shared dictionaries, actual prefix/postfix/transpiler behavior, rebuilding, and cross-engine removal. |
| `missing-api-*`, `declaration-*`, `prepare-false-missing-target` | Distinct API and declaration rejection stages; accepted prepare-false jobs do not resolve missing targets. |
| `active-state-*`, `legacy-recovery-*` | Rejection before transpilers, unchanged published state and behavior, survivor validation, removal, and corrected retry. |
| `cold-identity`, `duplicate-module`, `duplicate-patch-module-*` | Exact recursive selectors, detached reader-owned objects, state isolation, malformed data, ambiguous identities, and owner-removal recovery. |
| `extensions-*`, `completion-*` | Prior-format rejection, capability downgrade, exact callback dependencies, and current/current rebuilding. |
| `foreign-*`, `reflection-ordinary-*`, `concurrent-old-candidate` | Explicitly classified loading and inherited update limits, independently of Infix rejection. |

Standard output is the JSON summary; build/progress output goes to standard error. Reports retain each child request, result, and diagnostic log, including runtime, hashes, module IDs, providers, load events, state/version/mappings, counters, and execution traces. Downloads and engine snapshots remain under ignored `artifacts/` directories.

Read both summary fields. `Passed: true` means all requested expectations held. `HasPublishedLimitations: true` means at least one case reached a classified loading or concurrent-update limit. A passing diagnostic is not successful Infix execution in that arrangement. Use [current validation status](../docs/infix/README.md) for candidate-specific results.

## Known boundaries retained by the probes

- Published 2.4.2 can lose a foreign `CodeInstruction` type's load-context identity while reconstructing its instruction list by name. The probe reports `known-published-ordinary-limitation` at the exact failing stage.
- With one same-identity Fat engine in CoreCLR's default context, MonoMod's generated `ILGeneratorProxy` can bind to that engine while receiving another engine's `CecilILGenerator`. This is an ordinary-wrapper loading failure.
- Mono 6.12 file and byte loading can unify unsigned typed references onto the first Harmony copy, even with distinct test versions. Reflection-routed probes reach the second engine but can fail in its embedded MonoMod proxy initialization before public cross-engine patch operations. These arrangements do not establish Mono mixed-engine Infix support.
- A generated wrapper needing two different callback assemblies with the same full identity may be unable to represent both dependencies. Completion cases require rejection without publication, rather than execution of the wrong callback. This differs from duplicate-MVID target/patch ambiguity.
- Hosts must serialize updates across engines and avoid reentering an update for the same original. The envelope protects reads of published Infix state; it cannot invalidate an old unpublished candidate created earlier. `concurrent-old-candidate` demonstrates that inherited boundary with deterministic gates.

Keep these classifications separate from ordinary-control and feature failures. A build or launched runtime cannot replace an executed coexistence check, and the covered Mono arrangement does not establish behavior in Unity or other Mono hosts.
