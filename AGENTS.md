# Harmony rules for Codex

* Relevant drafts and information are located in `./drafts/` and is named according to the topic.

* The Infix design reference starts at `drafts/INFIX-NEW-IMPL-V3.md`. Its linked operation-target addendum and `drafts/INFIX-FEATURE-COMPLETION.md` define the implemented, unreleased extensions. Read them as one specification, with the completion document overriding earlier exclusions only where stated. Executed checks and remaining runtime boundaries are recorded in `docs/infix/README.md`.

* For simple edits, limit testing to net9 and x64.

* Use the latest C# language features, like `var`, shorter array syntax etc. Longer than usual lines (~ 140 chars are ok)

* During editing C# files, don't bother with formatting. Instead, run `dotnet format` at the end to format the code - it will respect the .editorconfig file.

## Local verification

Use `./scripts/test.sh` for local build/test work. It forwards `dotnet test` options and run settings, saves full logs and fresh TRX reports under ignored `artifacts/tests/run-*`, and prints only `ok` after a successful exit and nonempty completed reports. Failures report the log path and return nonzero. Cancellation stops the Unix test process group. Python 3 and the .NET SDK are required. Set `HARMONY_DOTNET` to override the SDK executable.

For the usual focused net9/x64 check:

```bash
DOTNET_ROOT_X64=/path/to/net9-x64 DOTNET_ROLL_FORWARD=LatestPatch \
  ./scripts/test.sh -c Debug -f net9.0 \
  -p:TargetFrameworks=net9.0 -p:PlatformTarget=x64 \
  -p:GeneratePackageOnBuild=false \
  -p:CustomBeforeMicrosoftCommonTargets="$PWD/HarmonyTests.Compatibility/ReviewProbes/net9-reference-pack.targets" \
  --artifacts-path "$PWD/artifacts/local-debug" \
  --filter FullyQualifiedName~InjectionValidation
```

Remove `--filter` for the complete suite. Use `-c Release` and a separate artifacts directory for Release. The reference-pack override is explained in `HarmonyTests.Compatibility/ReviewProbes/README.md`. The workflow owns result-directory and logger options, and enables VSTest's `RunConfiguration.TreatNoTestsAsError`. Its own checks run with `python3 scripts/test_workflow_test.py`. Framework and coexistence release gates remain documented in `docs/infix/README.md` and `HarmonyTests.Compatibility/README.md`.
