# TODO

The eight confirmed findings from the first 2026-09-08 review are fixed. These follow-ups come from the second review, checked against `df8b5e0d` on .NET 9.0.19 x64.

Keep reproduced defects, measured performance opportunities, and cleanup verified against the current code. Remove completed items; retain their evidence in the [first verification record](HarmonyTests.Compatibility/ReviewProbes/README.md) or [part-two verification record](HarmonyTests.Compatibility/ReviewProbes/PART2.md).

## Verified defects

- [ ] **T12 · P2 — Serialize shared-state startup across Harmony copies.** Two current copies starting together create two `HarmonySharedState` assemblies and separate patch dictionaries; a third current copy then fails initialization. Reproduced in six fresh processes; sequential startup creates one shared dictionary. Protect discovery, creation, and initialization of the shared fields as one cross-copy operation. Keep the rejection of already split state. See `Harmony/Internal/HarmonySharedState.cs`.

- [ ] **T13 · P2 — Normalize the Infix marker in public annotation results.** `GetMergedFromMethod` and the equivalent `GetFromMethod`/`Merge` path expose `methodType = -2147483648`. Merging this over a class's `MethodType.Normal` loses the valid outer target; importing the same callback through `new HarmonyMethod(method)` works. Return usable metadata from the public annotation API while preserving the raw declaration marker needed to reject older readers. See `Harmony/Public/HarmonyMethod.cs` and `Harmony/Public/Attributes.cs`.

- [ ] **T14 · P2 — Make Infix `Patch` equality and hashing independent of callback resolution.** After loading a second real copy of the callback module, a fresh record from `Harmony.GetPatchInfo` throws `SerializationException` in self-equality, `GetHashCode`, and `HashSet<Patch>`. A previously resolved record keeps working, and removal by the held callback now succeeds. Use stable stored identity for these operations while retaining strict ambiguity checks when rebuilding executable patches. See `Harmony/Public/Patch.cs`.

## Measured performance opportunities

- [ ] **T15 · P3 — Reduce repeated metadata work within one rebuild.** Instrumenting one simple Infix registration counted two `ValidateSurvivingMetadata` passes, two checks each for V3/V4 capability, and 15 `ResolveModule` calls. Consolidate repeated work within a rebuild, preserving public serialization validation and checks for newly loaded duplicate modules. A global cache or unconditional `??=` shortcut must not hide changed loader state. Startup time savings have not been measured. See `PatchFunctions`, `PatchInfoSerialization`, `PatchInfoJsonConverter`, and `PatchInfo`.

- [ ] **T16 · P3 — Avoid encoding every numeric literal during constant matching.** Checking 10,000 reused, nonmatching `ldc.r8` instructions allocated 880,000 bytes; the nonnumeric control allocated zero. Compare the already decoded value or bits without formatting each candidate. Preserve the selector's exact floating-point rules, including signed zero and NaN payloads. See `Harmony/Public/InnerTarget.cs`.

## Verified cleanup

- [ ] **T17 · P3 — Remove unused binding helpers and the test-only position matcher.** `MethodCreatorConfig.GetLocal` (both overloads), `HasLocal`, and `Infix.InnerMethod` have no call sites. `Infix.Matches` is used only by `InfixPositions`, while production uses `InnerTarget.Matches` and `ResolvePositions`; the five-argument `PatchBindingContext` constructor is also test-only. Remove the unused members, move test construction convenience into tests, and consolidate position assertions onto the shipping path, which already has coverage in `InfixExecution`.
