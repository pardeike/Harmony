# Stack-depth validation audit

Investigation date: 2026-10-19. Audited source: branch `v3`, commit `2a97275b3035ed304c40c5af9c4d663dc2ab8eb1`.
The findings below describe that snapshot. The implementation record describes the subsequently authorized corrections.

## Decision rule

Harmony should reject IL only when it can demonstrate a clear, undisputed violation.
Ambiguous or runtime-dependent cases should pass Harmony's validation.
Passing validation does not guarantee that the runtime can compile or execute the method.

The priority is avoiding false rejections. Broader rejection is not an objective of this work.
Keep the existing rejection of demonstrated stack underflow and inconsistent stack depths on reachable paths.
Do not infer a requirement for `ret` merely from the method's return type or its last instruction.
Do not require unreachable code to produce a return value.

Stack-depth analysis counts values. It does not establish their types or provide complete IL verification.
Its existing per-instruction depth calculation is a useful foundation. This investigation proposes no new Infix behavior or type analysis.

## Outcome

The audit found one reproducible compatibility rejection and one separate patch-installation defect.
It found no rejection among 2,000 selected framework bodies or the valid handcrafted controls.
Those results bound the investigation. They do not prove the absence of false rejections.

### 1. Unreachable exception handlers: rejection exceeds the proposed policy

[`MaximumStack`](../Harmony/Internal/DynamicMethodBodyWriter.cs) independently schedules every exception handler and filter.
It does so even when the associated try block cannot execute.

The reproduction returns immediately. A try/catch follows that return, with no incoming branch.
The catch contains two `pop` instructions. Its entry stack would contain one exception object if the handler could execute.

```il
ret
.try {
    nop
    leave end
}
catch System.Exception {
    pop
    pop
    leave end
}
end: ret
```

Verified on .NET 10.0.11/x64:

- The Cecil assembly path generates this method, and the runtime executes it successfully.
- Harmony's direct writer rejects it with stack underflow at the second `pop`.
- Adding a no-op prefix through `Harmony.Patch` reproduces that rejection.
- The original remains executable after this generation failure.

The catch would underflow if entered. Runtime acceptance does not prove that every instruction satisfies every CLI validity rule.
However, rejecting a method for this unreachable handler conflicts with the agreed conservative policy.
Treat this as a demonstrated compatibility rejection, not proof that the body is universally valid IL.

**Recommendation:** tie handler analysis to reachable protected code. Do not seed unrelated dead handlers as independent roots.
Retain ordinary catch/filter entry depths when their protected code is reachable.
Do not expand this change into whole-program reasoning about which calls can throw.

### 2. Failed replacement can remove an existing working patch

This is independent of the false-rejection question.

The reproduction installs a prefix that changes a method's result from `42` to `7`.
It then submits a transpiler whose output passes the current depth calculation but fails JIT compilation.
The invalid sequence has an extra live value beneath a `localloc` allocation size.

Observed after the failed update:

- Calling the target returns `42`. The working prefix has been removed.
- Harmony's metadata still lists one prefix and no transpiler.
- The update reports `InvalidProgramException` during detour installation.

[`PatchTools.DetourMethod`](../Harmony/Internal/PatchTools.cs) disposes the old detour before creating the replacement detour.
MonoMod can JIT the replacement during that creation.
[`PatchFunctions.UpdateWrapper`](../Harmony/Internal/PatchFunctions.cs) publishes new metadata only after installation succeeds.
This explains the observed mismatch between executable behavior and metadata.

**Recommendation:** preserve the previous working installation when replacement preparation or installation fails.
More validation cannot guarantee this property because the runtime can fail after Harmony's checks.
Simply reversing creation and disposal is not an established fix. Disposing the old detour could overwrite the new detour.
MonoMod's unapplied-detour option alone is also insufficient. Its constructor does not guarantee replacement JIT compilation.
Resolve this separately through the existing detour lifecycle, with a focused failure-preservation test.

This audit proves the defect in the inspected source. It does not establish when the defect was introduced.

## Throw, return and runtime differences

Andreas's historical concern is present in the existing tests:

- [`Specials.Test_PatchException`](../HarmonyTests/Patching/Specials.cs) expects .NET to accept postfix code after an always-throwing method.
  Its Mono branch expects `InvalidProgramException`.
- [`FinalizerPatches2`](../HarmonyTests/Patching/FinalizerPatches.cs) skips several non-void, always-throwing cases on Mono.
- [`ThrowingStringReturningMethod`](../HarmonyTests/Patching/Assets/FinalizerPatchClasses.cs) explains the historical dead-code difference.
- Commit `7b5158aefb1aba88827fa598d5e37e67d1a58380` changed dead-code handling in January 2024.
  Commit `1f78263f61cb9740657f72206ba7c0f6e89c3637` adjusted it again in April 2025.

The current analyzer stops normal traversal at `throw`, `ret`, `rethrow` and `jmp`.
It does not demand a final `ret` after `throw`.
An otherwise unreachable trailing `ret` does not cause a missing-result rejection.

New .NET 10/x64 controls verified:

| Body | Harmony analysis | Runtime result |
| --- | --- | --- |
| `ldnull; throw`, returning void or int | Pass | Throws as intended |
| Same body with trailing `ret` | Pass | Throws as intended, including the int case with no value for the dead `ret` |
| Extra value beneath the exception at `throw` | Pass | Throws as intended |
| Finally containing `ldc.i4.1; endfinally` | Pass | Executes successfully |
| Call an always-throwing void helper, then `ret` | Pass | Throws as intended |
| Call that helper without a following `ret` | Pass | JIT rejects on this runtime |

The last case distinguishes an explicit terminal instruction from knowledge about a callee's behavior.
Do not turn a structural depth pass into interprocedural analysis to resolve that distinction.
The runtime rejection does not justify introducing a new Harmony rejection in this investigation.

The [CLI specification](https://www.ecma-international.org/wp-content/uploads/ECMA-335_6th_edition_june_2012.pdf)
defines different stack behavior for `ret`, `throw`, `leave` and `endfinally`.
In particular, throwing and ending a finally are not equivalent to returning a value.
A general rule that every terminal instruction must consume the entire stack would reject permitted cases.

The historical Mono behavior was established from repository tests and comments. No new Mono execution occurred in this audit.

## Existing implementations and reuse

The repositories were inspected at pinned commits. Their upstream test suites were not executed here.

| Project | Relevant implementation | Assessment |
| --- | --- | --- |
| AsmResolver | [`CilMaxStackCalculator`](https://github.com/Washi1337/AsmResolver/blob/0715000c1b3a5104531d5ff189a14ebef0a9d5ac/src/AsmResolver.DotNet/Code/Cil/CilMaxStackCalculator.cs) | Closest match. Worklist traversal records instruction depths. It schedules handlers when their try start is reached. Its tests cover unreachable handlers and nonempty stacks at `throw`. |
| dnlib | [`MaxStackCalculator`](https://github.com/0xd4d/dnlib/blob/9ab9b58ab31b99b3f6cdef36efec613fc11ddbe2/src/DotNet/Writer/MaxStackCalculator.cs) | Small implementation with underflow and depth-consistency checks. It uses dnlib instructions and is not a complete return-contract verifier. |
| Cecil | [`CodeWriter`](https://github.com/jbevain/cecil/blob/882ca5eedda1e62eb41bd5869aeb15d8f1538e51/Mono.Cecil.Cil/CodeWriter.cs) | Existing dependency, but its maximum-stack calculation merges conflicting branch heights using their maximum. It does not provide the strict join check needed here. |

AsmResolver provides useful logic and focused regression cases. It is not a drop-in implementation for Cecil bodies.
Its `endfinally` opcode uses `PopAll`. Cecil uses `Pop0`.
Copying its terminal-stack check without preserving that distinction would create a false rejection.
The inspected `ScheduleIndex` bounds guard also uses an impossible conjunction. Review adapted code rather than assuming provenance establishes correctness.

All three repositories use MIT licenses. Any copied substantial code must retain its applicable notices.
Prefer a small, attributed adaptation of relevant logic and tests over a dependency or a second IL representation.
The current worklist already matches much of the required algorithm. A wholesale replacement is not justified by this audit.

## Coverage and remaining limits

The handcrafted investigation contains 35 distinct bodies.
Twenty serve as valid controls, including calls, returning `calli`, custom-modified void, loops, switches, filters, catches and finally blocks.
All valid controls passed analysis.
The .NET 10.0.11 survey imported and analyzed 2,000 selected framework bodies, including 827 with exception handlers.
No body in that sample was rejected.
The survey did not invoke arbitrary framework methods.

Three clearly malformed controls were rejected: reachable underflow, a missing value at a reachable int `ret`, and inconsistent join depths.
Other bodies passed analysis and failed runtime compilation, including surplus values at a reachable `ret`, invalid try entry and invalid `localloc` state.
These demonstrate the pass's limited scope. They are not a recommendation to add every missing check.
Wrong return types and a branch targeting an instruction outside the body were inspected without executing those bodies.

[`MethodBodyGenerator`](../Harmony/Internal/MethodBodyGenerator.cs) selects several executable representations.
`MaximumStack` currently belongs to the direct writer. Its checks are not a uniform validation boundary across all representations.
Moving a rejection into a shared boundary would expand its effect. Each such move needs its own justification and compatibility evidence.
Do not implement shared validation merely for architectural symmetry.

At the audited snapshot, the writer and generator files had no diff from `v3.0.0-rc.3`.
Tests ran on stock .NET 10.0.11/x64 on macOS. Mono, .NET Framework and WaylithCLR execution remain untested here.
There is no full type verifier, universal IL-validity proof or new startup/performance measurement in this audit.

## Focused next work

1. Correct the demonstrated unreachable-handler rejection under the conservative policy.
   Reuse AsmResolver's reachability principle and relevant regression cases within the existing Cecil traversal.
2. Preserve the existing working patch after a failed replacement. Keep that lifecycle correction separate from new IL rejection rules.
3. Keep the throw-only, unreachable-return and stack-clearing controls as positive regressions.
   Retain the existing Mono/.NET expectations until runtime evidence supports changing them.
4. For any proposed new rejection, identify the exact rule, demonstrate a reachable violation and add a nearby accepted control.
   If the interpretation remains disputed, omit that rejection.
5. Run affected .NET 10/x64 checks while implementing. Run established cross-runtime gates before any release claim.

No new abstraction, type analysis, Infix feature, backend or dependency is needed.

## Implementation record: 2026-10-19

Andreas authorized careful implementation with TDD after reviewing the audit.
Implementation started on `fix/conservative-stack-validation`. Andreas moved the work to `v3` on 2026-10-22.

The implementation keeps the existing depth worklist and Cecil representation.
It indexes handlers by their try start and schedules them when that instruction becomes reachable.
Bodies without exception handlers allocate no handler index.
It does not add terminal-stack checks, type analysis or other rejection rules.

Failed replacement installation now restores the previous target through the same detour factory.
The successful path still creates one detour. It adds no JIT preflight, executable wrapper or invocation indirection.
The original installation exception propagates after successful restoration.
If restoration also fails, an explicit exception reports both failures and removes the stale detour entry.
This uses APIs available on the existing framework targets.
Restoration does not make the update atomic for concurrent method callers.

Permanent regressions follow the existing main test-project layout:

- [`StackValidation`](../HarmonyTests/Patching/StackValidation.cs) covers dead catch/filter/finally/fault handlers and their reachable-underflow counterparts.
  It also covers nested handlers, clear ordinary stack errors, throw-only methods and stack-clearing finally behavior.
- [`PatchFailure`](../HarmonyTests/Patching/PatchFailure.cs) covers late JIT failure on the direct and assembly generation paths.
  It checks execution, unchanged metadata, garbage collection, a subsequent valid update and unpatching.

TDD evidence under `artifacts/tests/`:

| Run | Result |
| --- | --- |
| `run-yl3ad61b` | Red: four dead-handler compatibility cases and both failed-replacement cases failed. Nine controls passed. |
| `run-1nmvg_fj` | Green: all initial 15 regression cases passed after the production corrections. |
| `run-kiy0268q` | Final complete .NET 10/x64 Debug suite: 1,208 passed, two existing skips, zero failures. All 19 new regression cases passed. |

The two existing skips are the nullable-result configuration issue and the optional hot-site timing comparison.
`dotnet format whitespace` formatted only the four changed C# files. `git diff --check` passed.
No dependency, backend selection, public API or production file outside the two corrected implementation files changed.
No new cross-runtime execution or performance benchmark occurred during that implementation phase.
The changes remained local at that point. The integration record below identifies the later commits.

## Integration record: 2026-10-28

Andreas approved committing and pushing the reviewed changes to `v3`.

| Commit | Change |
| --- | --- |
| `895b616de2265b7b751d145421ea8f9f3a24fab9` | Schedule exception handlers only when their protected code is reachable. |
| `9f008dbcc792f5257c44e88c039e41fb6528e1aa` | Restore the previous working target after replacement installation fails. |
| `c741b20d9c15af069cddb3136bc649e349df5454` | Separate caller-discovery hardening, inference deprecations and documentation updates. |

The focused .NET 10/x64 Release run `run-ai36rg0i` passed all 126 tests on 2026-10-28.
It covered stack validation, failed replacement recovery, assembly inference, caller discovery and injection validation.
The prior complete Release run `run-6oh44xoe` passed 1,224 tests with two existing skips on 2026-10-22.
The platform CI matrix remains the cross-runtime release gate.

## Local evidence

Ignored investigation files live under `artifacts/stack-validation-audit/`:

- `StackValidationAudit.cs` and `Audit.targets`: temporary probe source and test-project import.
- `results/*.json`: individual method observations, public API reproductions and framework survey.
- `results/failed_update.json`: before/after execution and metadata for the failed replacement.
- `results/unreachable_public_patch.json`: the public API rejection and subsequent successful original call.
- `results/framework_survey.json`: runtime identity, sample counts and failures.

Successful quiet-workflow runs:

| Run directory under `artifacts/tests/` | Result |
| --- | --- |
| `run-f9spo8_s` | 27 initial body observations |
| `run-pcrvad1f` | Two allocation observations and failed-update reproduction |
| `run-z2cb9265` | Framework survey |
| `run-yox8i7gr` | Public unreachable-handler reproduction |
| `run-8jj_943j` | Seven throw observations and 22 existing throw/finalizer regression cases |

Passing observational tests mean the probes recorded the described behavior. They do not mean every submitted body was valid.
Two earlier setup runs failed compilation and produced no behavioral evidence. They exposed reference-assembly visibility and a `CallSite` name ambiguity.
The corrected invocations use `ProduceReferenceAssembly=false` and an explicit alias.
No full suite was needed for this investigation.
The investigation phase changed no production code, dependencies or committed tests.
The later authorized implementation and integration are recorded above.
