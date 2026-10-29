# Harmony rules for Codex

## Branch policy

* Develop Harmony 3 on `v3`.
* Apply only urgent fixes to Harmony 2 on `master`, unless Andreas explicitly authorizes other work.
* If a request would apply non-urgent changes to `master`, remind Andreas of this policy before making changes.

## Development

* Relevant drafts and information are located in `./drafts/` and is named according to the topic.

* The Infix design reference starts at `drafts/INFIX-NEW-IMPL-V3.md`. Its linked operation-target addendum and `drafts/INFIX-FEATURE-COMPLETION.md` define the implemented, unreleased extensions. Read them as one specification, with the completion document overriding earlier exclusions only where stated. Executable generation follows `drafts/ISSUE-783-METHOD-GENERATION.md`, which supersedes the earlier backend prescriptions. Executed checks and remaining runtime boundaries are recorded in `docs/infix/README.md`.

* For simple edits, limit testing to net10 and x64.

* Use the latest C# language features, like `var`, shorter array syntax etc. Longer than usual lines (~ 140 chars are ok)

* During editing C# files, don't bother with formatting. Instead, run `dotnet format` at the end to format the code - it will respect the .editorconfig file.

## Local verification

Build with the .NET 11 RC3 SDK pinned in `global.json`. On Mac Studio, it is installed at `$HOME/.dotnet/dotnet` alongside .NET 10. Set `HARMONY_DOTNET="$HOME/.dotnet/dotnet"` for the local workflow when `dotnet` on PATH still selects the Homebrew installation.

Use `./scripts/test.sh` for local build/test work. It forwards `dotnet test` options and run settings, saves full logs and fresh TRX reports under ignored `artifacts/tests/run-*`, and prints only `ok` after a successful exit and nonempty completed reports. Failures report the log path and return nonzero. Cancellation stops the Unix test process group. Python 3 and the .NET SDK are required. Set `HARMONY_DOTNET` to override the SDK executable.

Use .NET 10/x64 for routine local verification. Install the .NET 10 x64 runtime and point `DOTNET_ROOT_X64` at it; `LatestPatch` keeps execution on .NET 10. For the usual focused check:

```bash
HARMONY_DOTNET="$HOME/.dotnet/dotnet" \
DOTNET_ROOT_X64="$PWD/artifacts/runtimes/dotnet10-x64" DOTNET_ROLL_FORWARD=LatestPatch \
  ./scripts/test.sh -c Debug -f net10.0 \
  -p:TargetFrameworks=net10.0 -p:PlatformTarget=x64 \
  -p:GeneratePackageOnBuild=false \
  --artifacts-path "$PWD/artifacts/local-debug" \
  --filter FullyQualifiedName~InjectionValidation
```

Remove `--filter` for the complete suite. Use `-c Release` and a separate artifacts directory for Release. The workflow owns result-directory and logger options, and enables VSTest's `RunConfiguration.TreatNoTestsAsError`. Its own checks run with `python3 scripts/test_workflow_test.py`. Framework and coexistence release gates remain documented in `docs/infix/README.md` and `HarmonyTests.Compatibility/README.md`.

For .NET 11 checks, use `-f net11.0 -p:TargetFrameworks=net11.0` and set `DOTNET_ROOT_X64` to `artifacts/runtimes/dotnet11-x64`. Both runtimes use `LatestPatch` to prevent accidental execution on another major version.
