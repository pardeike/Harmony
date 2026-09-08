# TODO

The eight confirmed findings from the first 2026-09-08 review are fixed. These follow-ups come from the second review, checked against `df8b5e0d` on .NET 9.0.19 x64.

Keep reproduced defects, measured performance opportunities, and cleanup verified against the current code. Remove completed items; retain their evidence in the [first verification record](HarmonyTests.Compatibility/ReviewProbes/README.md) or [part-two verification record](HarmonyTests.Compatibility/ReviewProbes/PART2.md).

## Measured performance opportunities

- [ ] **T16 · P3 — Avoid encoding every numeric literal during constant matching.** Checking 10,000 reused, nonmatching `ldc.r8` instructions allocated 880,000 bytes; the nonnumeric control allocated zero. Compare the already decoded value or bits without formatting each candidate. Preserve the selector's exact floating-point rules, including signed zero and NaN payloads. See `Harmony/Public/InnerTarget.cs`.

## Verified cleanup

- [ ] **T17 · P3 — Remove unused binding helpers and the test-only position matcher.** `MethodCreatorConfig.GetLocal` (both overloads), `HasLocal`, and `Infix.InnerMethod` have no call sites. `Infix.Matches` is used only by `InfixPositions`, while production uses `InnerTarget.Matches` and `ResolvePositions`; the five-argument `PatchBindingContext` constructor is also test-only. Remove the unused members, move test construction convenience into tests, and consolidate position assertions onto the shipping path, which already has coverage in `InfixExecution`.
