# Method-body patching: review corrections and local acceptance

Status: historical runtime evidence followed by the 2026-11-06 review-fix checkpoint below.
The final section records the fixes integrated after `61064108`. Earlier results do not validate those later changes.
The corrected private mod passed the bounded all-mod fixture on 2026-11-03 and the updated engine on 2026-11-04.
No commit, push, public release or public post was made.
Andreas approved delivery after the warning comparison. The private ZIP was sent to Sam and downloaded back with the same hash.

## Current checkpoint: updated runtime, 2026-11-04

This section supersedes the historical 2026-11-03 runtime and adapter limits below where explicitly stated.
The current host is RimWorld `1.7.4998 rev1299`, Steam build `26415840`, on macOS ARM64.
Its bundled runtime identifies itself as `.NET 10.0.15-waylithclr.dev`.
Tests use an isolated copy of that installed engine. They do not use stock .NET as runtime proof.

### What changed

Sam shipped renamed body/token APIs, native DynamicMethod imports, dependency retention and an ordered transformer pipeline.
DecompilerServer inspection established the actual implementation before adapter changes.
Sam confirmed Harmony can run early at priority 0. The native instrumentation profiler runs at priority 1000.
He also confirmed that Harmony owns its stage rather than the final output of later transformers.

Harmony core retains the same general BCL-only `MethodBodyPatching` contract, registered through `SetEligibility`, `SetBodySource` and `SetInstaller` since 2026-11-04.
The only additional core correction exports actual DynamicMethod identities to host importers.
Nongeneric structured helpers now use Harmony's existing DynamicMethod generator.
Generic helpers keep their existing exact assembly generator. The default installer retains its ordinary proxy behavior.
The new identity regression failed with the old proxy path and passed after the correction.

Harmony Mod now registers a runtime transformer and replays immutable, module-scoped body bytes.
Harmony and user transpilers execute outside runtime callbacks. Callbacks capture raw input and offer frozen output.
They take no Harmony lock, acquire no adapter lock and perform no game logging or reflection-based clause decoding.
This avoids the lock inversion identified by the independent challenge.

Changed upstream input detaches the stage. A queued host error reports the failure outside the runtime callback.
`OnTransformFailed` returns normally, so the runtime can notify other stages and finish its bookkeeping.
Reads preserve the detached fault. Explicit `RuntimeILProvider.Rebuild(method)` captures new input and invokes the existing patch processor.
The host must call it outside runtime callbacks with normal Harmony operations quiescent.
A per-generation witness proves that the current candidate was offered and accepted.
Preparation of the installed method still detects invalid IL before Harmony publishes patch metadata.
Rollback restores and prepares the previous generation. A recovery failure quarantines the target.

The runtime now owns token memoization and body dependency retention. The adapter's duplicate cache and permanent member roots were removed.
Native tests verify cross-assembly collectible signatures and old-frame keepAlive retention without that cache.
A separate probe requested equal strings from distinct objects and identical generic instantiations 20 times each.
Each case returned one distinct token. Frozen replay imports no tokens.
DynamicMethod imports still retain their method for the importing module's lifetime, as documented by the runtime author.

### Executed checks

| Layer | Current result |
| --- | --- |
| Complete .NET 10/x64 Release suite | 1,277 passed. Two skipped. [TRX](../artifacts/tests/run-tfaogjww/tests_net10.0_20261104020035.trx). |
| Focused .NET 11/x64 provider fixture | 32 passed. [TRX](../artifacts/tests/run-7ls24dqi/tests_net11.0_20261104020913.trx). |
| Legacy library | net35 build passed after the helper-generation change. This is compilation, not legacy runtime execution. |
| Bundled runtime, disabled tiering | `bridge`, `generics`, `failure`, `multi`, `reentry`, `protocol`, `readbody`, `coexist` and `runtime-contracts` passed against the candidate DLL. |
| Bundled runtime, default tiering | `bridge` and `generics` passed against the same candidate DLL. A separate hot-generics probe passed 60 million patched calls across method and declaring-type definitions. JIT output records Tier0 and Tier1 before and after patching. Warmed and late instantiations and final unpatch passed. |
| Native lifetime probes | A collectible callback with a separate collectible signature dependency stayed callable through GC. Its type collected after revert. A DynamicMethod stayed callable. An explicit keepAlive object survived an old frame and released after return. |
| Invalid IL and recovery | Invalid installed bodies failed preparation. Initial and existing installations recovered. Locals, exception regions and published patch metadata remained consistent. |
| Runtime pipeline | Downstream instrumentation, both Harmony owners, repeated rebuilds, detach notification, preserved read failure and explicit rebasing passed. |
| Private mod build | Canonical offline build, standard-runtime smoke, bundled-runtime smoke, packaging and 17-file payload verification passed. Mod compilation reported zero warnings and errors. |
| Actual game profiler | Native instrumentation selected and instrumented one nontrivial target. Enable/disable, repatching under instrumentation, partial unpatch and final unpatch preserved the expected callback count and results. |
| Isolated all-mod fixture | All 20 installed authored mods loaded with 27 active entries including Harmony and Core/DLC. The original save loaded, 600 ticks passed, a new save reloaded, and 300 more ticks passed. |
| Clean candidate control | The same save/tick/save/reload sequence passed in another process without loading the private profiler proof. |

The first native-profiler proof selected a trivial method, matched one target and instrumented none.
Its assertion failed before the target was called. That result did not demonstrate a lost Harmony patch.
The corrected nontrivial target was actually instrumented. The proof passed both before and after loading the save.
Both runs restored profiler targets and enabled state in `finally` and removed their test patch owners.

The live proof reported `tracked=1478, applied=1478, detached=0, broken=0` after save/reload.
That total includes the proof's retained zero-patch stage. The game-only process starts with 1,477 provider stages.
The rendered map and UI were inspected in [the final screenshot](../artifacts/runtime-pipeline-20261104/all-mods/candidate-final.png).
No obvious rendering failure appeared. This is not exhaustive feature coverage for every mod or a long-running colony.

### Comparison, artifact and delivery

Released Harmony Mod `3.0.1.0` was tested on the same copied 4998 engine and save.
Startup and gameplay warning messages matched the candidate. Neither tested sequence logged a gameplay error.
Shared warnings include the Unity hideFlags stub, key conflict, shader variants, old save version and ritual precept repair.
Both versions report `CloudSystem destroyed` on reload.
Both versions also report persistent allocations at shutdown. Those reports are separate from startup/gameplay warnings.
An initial unmatched comparison reported 56,324,580 bytes for released Harmony and 93,237,580 bytes for the clean candidate.
The matched 600-tick/save/reload/300-tick control reported 89,341,580 bytes for released Harmony and 93,237,580 for the candidate.
The profiler-proof process reported 95,185,580 bytes. These are shutdown reports, not measured retained managed heap or proof of a Harmony leak.
The remaining candidate/control difference is 28 allocation records and 3,896,000 bytes in repeated identical size groups.
Its ownership and run-to-run variation remain unresolved. The full reports are retained for runtime-author follow-up.
No warning was suppressed. This private functional acceptance does not claim equivalent shutdown allocation totals.

The current private mod version is `3.0.1.2`. It pins local Harmony package `3.0.0-rc.4.runtime-il.20261104`.
The ZIP SHA-256 is `7594a46dec1af1945832ff3475c429aefe71e11f0d16cef20df0eed6268dca44`.
All 17 allow-listed payload files matched the deployed files. The ZIP includes no game assemblies, private messages, source or test logs.
The [private Discord delivery](https://discord.com/channels/@me/708325707868012645/1567349645653577748) was read back and downloaded with the identical SHA-256.
The player's active mod configuration was byte-identical after testing. Tests used copied saves and their own newly written saves.

The local [evidence manifest](../artifacts/runtime-pipeline-20261104/evidence.json) records hashes, reports, raw logs, source snapshots and delivery state.
The Harmony Mod implementation remains in its private worktree. Its files are local and do not exist in a fresh public clone.
No commit, push, public release or public post was made.

### Remaining limits

- Sam explicitly reports that field injection is not integrated with this pipeline yet. This prototype does not claim field-injection compatibility.
- Changed upstream IL requires an explicit rebuild. Automatic EnC composition and repair of returned snapshots are not implemented.
- A direct runtime replacement that bypasses a pipeline-managed target is superseded by its next rebuild. This deliberately follows Sam's ownership contract.
- Generic validation covers selected closed witnesses. It cannot prove every future instantiation. Constructed requests never silently broaden to a whole definition.
- Runtime updates and same-target patching require the accepted serial, non-reentrant host contract. Installation and compilation are separate operations.
- A DynamicMethod token retains its helper for the importing module's lifetime. Zero extra helper assemblies does not mean zero retained helpers or bounded churn.
- Generic generated helper assemblies have their existing retention limit. Collectible target modules remain declined.
- Older Harmony copies, independent detours and arbitrary external IL writers do not automatically cooperate with this protocol.
- The current native evidence covers macOS ARM64. Windows/Linux behavior and a supported public runtime reference package remain release gates.
- Shutdown allocation totals differ by 3,896,000 reported bytes in the matched control. Their cause remains unresolved.
- The hot-call probe preserves direct-call performance in its small workload. Repatching remains slower than native detours there. No whole-game startup speedup is claimed.

## Historical checkpoint: review corrections and acceptance, 2026-11-03

The remaining sections describe the prior engine and ZIP. They are retained as the correction record.

## Design retained

Harmony constructs the complete patched body with its existing patch semantics and validation.
The optional provider supplies the baseline, metadata imports, installation and recovery.
Harmony Mod owns the Waylith adaptation. Harmony core has no game or runtime-fork dependency.
Ordinary calls execute the installed body directly. No new forwarding layer was added.
The [Fable review](../artifacts/drafts-archive-20261106/METHOD-BODY-PATCHING-CLAUDE-REVIEW.md) remains the design challenge and adjudication record.
This document records implementation after that review. Earlier evidence manifests identify historical source snapshots.

## Focused corrections

| Object | Reproduced problem | Correction |
| --- | --- | --- |
| Generic context | A shadowed class/method parameter name changed a snapshot receiver from `Shadowed<string>` to `Shadowed<int>`. | Assign disjoint internal method-parameter names. Preserve reflection identity and positional binding. |
| Own-type references | A returned generic snapshot calling its own constructor and sibling methods failed with `TypeLoadException`. | Import the instantiated declaring type for own-type method references. |
| Shared v1 record | A compatible appended slot caused type initialization to fail. | Accept the stable three-slot prefix. Preserve the existing array and optional state. Reject incompatible prefix types. |
| `Clone(object)` | Replacing a type operand retained its previous instantiation annotation. | Clear the annotation only when the caller explicitly replaces the operand. Ordinary copies retain it. |
| Declined definitions | A late provider rejected a definition before checking whether it would accept that definition. | Preserve native selection when declined. Continue rejecting accepted definition/native-family conflicts. |

The first regressions failed before these corrections. The provider fixture subsequently passed all 31 cases.
Structured tests keep the existing nongeneric Infix callback and inner-target contract.
They test generic context carried by the outer snapshot and generated helper.
They do not add support for an arbitrary open generic inner call.

The late-definition test uses a reported native owner. It does not prove successful native installation of an open definition.
The constructed-target fake installer proves selection and bookkeeping. It does not prove runtime instantiation isolation.

## Verification

| Layer | Executed result |
| --- | --- |
| Complete .NET 10/x64 Release suite | 1,276 passed. Two skipped. |
| Focused .NET 11/x64 provider and sorting suite | 44 passed. |
| Legacy library build | net35 build passed. |
| Installed Waylith apphost | `generics`, `bridge`, `failure`, `multi`, `reentry` and `protocol` passed against the exact candidate Harmony DLL. |
| Corrected adapter reader | `readbody`, `bridge` and `failure` passed after the exception-buffer correction below. |
| Private mod build | Canonical offline build, standard-runtime smoke, installed-runtime smoke, packaging and payload checks passed. Mod compilation reported zero warnings and errors. |

The `protocol` mode creates a compatible four-slot record before the first Harmony load.
It then exercises actual provider and native patches. The unit cases separately reject incompatible prefix types.
A same-identity assembly-copy experiment encountered MonoMod's existing `ILGeneratorProxy` binding boundary in the test process.
It was not used as evidence of successful mixed-copy patching. No unrelated MonoMod workaround was added.

The runtime accepted a managed call-site signature through `GetTokenForLocals` and resolved the same signature bytes.
That check proves signature import acceptance. It does not execute a `calli` body.
Runtime modes used disabled tiering for this checkpoint. Earlier default-tier results remain historical evidence.

## All-mod acceptance found and fixed an adapter defect

The private mod separates its normal-reference wrapper from `HarmonyMod.RuntimeIL.dll`, which uses runtime-specific references.
The wrapper discovers Waylith and registers the optional provider before mod patching.
Constructed generic targets and other declined targets retain their existing mechanism.
The candidate uses mod version `3.0.1.1` and a locally built Harmony package. Neither version was published.

The first full-game run failed while VisualExceptions patched `Verse.Map.MapPostTick`.
The runtime reported 22 exception clauses, but returned success with buffers of zero, 16 and 64 entries.
The adapter had treated success as proof that every clause fitted. It retained only 16 clauses.
That truncated both the replacement body and the serialized recovery body. Compilation and recovery failed loudly.
Later gameplay encountered the damaged recovery body. The first candidate must not be distributed.

The corrected reader retries whenever the reported clause count exceeds its supplied buffer.
It checks that condition for both baseline and active-body reads.
The focused regression compares all 22 baseline clauses with reflection, including catch types and exact ranges.
It serializes and compiles the recovery body, patches and unpatches, and verifies the final active clause count.
The correction changes only the adapter. It does not relax IL validation or change the provider API.

The isolated fixture used the installed RimWorld `1.7.4995 rev309`, copied game files and copied save data.
Its 27 active entries include Harmony, Core/DLC and every one of the 20 installed authored mods.
The bridge confirmed the active and loaded configuration. No compatibility check was bypassed.
The ordinary Steam installation, player configuration and original saves were preserved.

| Run | Observed result |
| --- | --- |
| Released Harmony Mod `3.0.1.0` | Original save loaded. 300 ticks passed without errors. |
| Corrected private candidate `3.0.1.1` | Original save loaded. 600 ticks passed. A new save was written and reloaded. Another 300 ticks passed without errors. |
| Released version, reload control | Loaded the original save and then the candidate-created save. The reload warning also occurred here. |

Both versions produced the `hideFlags` Unity-stub warning, key-binding conflict, GUILocator notice and shader-variant warnings.
Both produced the old-save version warning and the missing ritual-precept warning.
Reloading produced `CloudSystem destroyed` on both versions.
No additional warning or error remained in the corrected candidate's tested sequence.
Shutdown allocation warnings appeared in both versions. They were preserved in full logs and excluded from gameplay warning comparison.
An inspected in-game screenshot showed a rendered map and UI without an obvious failure.
This is bounded acceptance, not proof of every feature in every mod or a long-running colony.

## Artifact and remaining limits

The candidate ZIP is `artifacts/runtime-patching-20261102/mod-worktree/dist/HarmonyMod-3.0.1.1.zip`.
Its SHA-256 is `8839359ecc247bfae37fcf7e1bf1a70dfb78bdb4a61dde24d2082a6d5c648a46`.
It contains 17 allow-listed mod files. It contains no game assemblies, private messages, source or test logs.
The deployed payload matched the packaged hashes. The isolated game was stopped after verification.
The local [evidence manifest](../artifacts/review-fixes-20261103/evidence.json) records hashes, reports, logs and delivery state.
The adapter and private runtime evidence remain under ignored local paths. A fresh clone cannot recover them.

Sam's promised lifetime, signature-binding and DynamicMethod-token changes are absent from the tested installed CoreLib.
Replay their focused probes against the actual new engine before claiming those fixes or adopting the new token path.
Representative patch-time measurements, field-update ownership and profiler/EnC cooperation remain separate adoption gates.
Generic validation covers selected closed witnesses, not every future instantiation.
Runtime updater calls and same-target rebuilds require serial, non-reentrant host use.
Imported metadata remains rooted for process lifetime. No reclamation or generic specialization coordinator was added.

## 2026-11-04 review follow-up

After the review changes recorded in [the decision record](METHOD-BODY-PATCHING-DECISIONS.md), the stock suite and the adapter probes were rerun.
Stock .NET 10 x64 Debug: complete suite 1,279 passed, 0 failed, 2 ignored. Focused fixtures with the new constructor and pin-release tests: ok.
Adapter worktree on build 1.7.4998, all eleven probe modes passed: bridge, coexist, generics, failure, reentry, readbody, runtime-contracts, hot-generics, pipeline, multi, protocol.
The bridge mode now covers an instance constructor installed directly. The coexist mode now covers `Injection.HarmonyRegenerate` and the delayed detach report.
Logs: `artifacts/runtime-patching-20261102/adapter-tests-*/`.

## 2026-11-04 decisions follow-up

The maintainer chose the split registration API, no callable copy for definition-wide targets, and no batch API. Sam confirmed type-initializer support.
Stock .NET 10 x64 Debug: focused method-body fixtures 39 passed, 0 failed. Complete suite 1,283 passed, 0 failed, 2 ignored by their own conditions.
New stock tests: a body source without an installer, an installer without a body source, the once-per-part rule, and the null replacement for a definition-wide installer.
The generic stock tests now generate their callable snapshots through an internal test observer, because `Patch()` returns the definition.
Adapter worktree on build 1.7.4998, all eleven probe modes passed: bridge, coexist, generics, failure, reentry, readbody, runtime-contracts, hot-generics, pipeline, multi, protocol.
The bridge mode now patches a type initializer before its type is initialized, checks that patching did not run it, and checks the postfix when it runs.
The generics mode checks that definition-wide targets return their definition and no callable copy.
The adapter project builds with 0 warnings and 0 errors against local package `3.0.0-rc.4.runtime-il.20261104.2`, packed from this working tree. No mod ZIP was built from it.
Logs: `artifacts/runtime-patching-20261102/adapter-tests-202611041621/`, `artifacts/runtime-patching-20261102/adapter-build-1793805774.log`, `artifacts/tests/run-*` for the stock suite.

## 2026-11-04 defaults through the same delegates

Harmony's own reflection reader and detour now run as delegates of the same two shapes behind one `MethodBodyPatching.Install` entry. No public API change.
Stock .NET 10 x64 Debug: focused method-body, alias and reverse-patch fixtures 42 passed, 0 failed. Complete suite 1,283 passed, 0 failed, 2 ignored by their own conditions.
Adapter worktree on build 1.7.4998, all eleven probe modes passed with the rebuilt core.
The adapter project builds with 0 warnings and 0 errors against local package `3.0.0-rc.4.runtime-il.20261104.3`, packed from this working tree. No mod ZIP was built from it.
Logs: `artifacts/runtime-patching-20261102/adapter-tests-202611041730/`, `artifacts/runtime-patching-20261102/adapter-build-1793809889.log`, `artifacts/tests/run-*` for the stock suite.

## 2026-11-04 private build 3.0.1.3

The private mod version is `3.0.1.3`. It pins local Harmony package `3.0.0-rc.4.runtime-il.20261104.3`, packed from this working tree at the current `v3` state.
Canonical offline build with `--no-deploy`: build, standard .NET 10 smoke and installed-runtime smoke passed. Payload: 17 allow-listed files, no game assemblies, private messages, source or test logs.
The ZIP is `artifacts/runtime-patching-20261102/mod-worktree/dist/HarmonyMod-3.0.1.3.zip`. Its SHA-256 is `e9cd0f5ddbeabf8e77381efb3e69e2ba5b513d41acc0ddebec112e2428c86dc7`.
The bundled `1.7/Assemblies/0Harmony.dll` is byte-identical to the package asset (SHA-256 `35110f1c...`, see `dist/build-report.json`).
The [private Discord delivery](https://discord.com/channels/@me/708325707868012645/1567578129910996994) was read back and downloaded with the identical SHA-256.
Not repeated for this build: the isolated 20-mod save/load/tick acceptance run. The build report records `inGameAcceptance: Not performed`.
Since 3.0.1.2: Sam's three points, type initializers, the three-call registration API, no callable copy for definition-wide targets, and Harmony's defaults through the same delegate shapes.

## 2026-11-05 generic instantiation families and signature kinds

Core: closed instantiations of a host-accepted definition install through that definition. Prefix, postfix and finalizer callbacks registered on an instantiation run under `GenericFamily.Is` guards and bridge by-value bindings with `GenericFamily.As`. Transpilers and Infix on an instantiation throw before installation. The callable copy takes its signature from the original's reflection types, and a function pointer local is encoded as a native int.

Stock suite on .NET 10 x64, Debug: Passed!  - Failed:     0, Passed:  1287, Skipped:     0, Total:  1287, Duration: 7 s - HarmonyTests.dll (net10.0)
New stock tests: closed instantiations install through their definition and return themselves, definition and instantiation patches compose in one body, bridged instance, argument, result, field and `__originalMethod` bindings, transpiler rejection with intact family, definition and closed targets sharing one installer, and function pointer parameter and return targets.

Adapter worktree on build 1.7.4998 with local package `3.0.0-rc.4.runtime-il.20261105.2`: all eleven probe modes passed. The adapter project builds with 0 warnings and 0 errors.
The generics mode now checks: an instantiation patch reaches only its instantiation, including the shared reference-type code (`Closed<string>` patched, `Closed<object>` not); two owners on two instantiations; a definition patch composing with them; member unpatch rebuilding the family; patch identity per instantiation; transpiler rejection leaving the family intact; bridged instance, argument, result, field and `__originalMethod` on `Bag<int>.Take` and `Bag<string>.Take`; prefix skip, passthrough postfix and finalizer on instantiations of a method with a generic result.
The bridge mode now checks signature kinds: a static `in` parameter, a virtual `in` parameter and a virtual `ref readonly` return with their `modreq`, an `init` setter, and function pointer parameter and return targets all install through the runtime stage.
Observed: C# emits `modreq(InAttribute)` for `in` parameters and `ref readonly` returns only on virtual and interface members. A static `in` parameter carries none.

Logs: `artifacts/runtime-patching-20261102/adapter-tests-202611050008/`, `artifacts/runtime-patching-20261102/adapter-build-1793833681.log`, `artifacts/runtime-patching-20261102/pack-20261105-2.log`, `artifacts/tests/run-7jzmgt7m`.
Not covered: transpilers and Infix on instantiations, the 20-mod in-game acceptance run, `PrepareGenericVariants` (not yet shipped), and the detour-free 1.7 packaging.

## 2026-11-05 detour-free 1.7 folder

Core: `DetourEngine` probes MonoMod.Core once. `AccessTools.Identifiable` and the .NET 11 compile watch skip the engine when absent. `PatchTools` loads without it. Reverse patch stand-ins go through `MethodBodyPatching.Prepare`, so a provider that owns the stand-in installs the reverse body.
Stock suite on .NET 10 x64, Debug: 1,288 passed, 0 failed. New stock test: a reverse patch stand-in installs through the provider.

Adapter strict mode on build 1.7.4998, package `3.0.0-rc.4.runtime-il.20261105.3`: the regular eleven probe modes passed with the merged build. The new `nodetour` mode passed with the Thin build and its managed dependencies, without `MonoMod.Core.dll`, first with five libraries and again with the final four:
the runtime resolved Mono.Cecil, MonoMod.Utils and MonoMod.Backports from the probe directory, asked for MonoMod.Core once and continued without it. Ordinary method, constructor, generic definition, guarded instantiation, reverse patch stand-in installed through the runtime, structured Infix helper, unpatch, and `Harmony.GetOriginalMethod` all passed.
Refusals with reasons: a synchronized method, a target in a collectible assembly, and a partially open generic method. MonoMod.Core never loaded.

Private build 3.0.1.4 from the canonical offline build with `HARMONY_THIN_DIR`: build, the plain .NET 10 smoke and the installed-runtime smoke passed, 24 checks each. Payload: 22 allow-listed files. The 1.7 folder ships the Thin `0Harmony.dll` (SHA-256 `4a21c11b...`, identical to the thin build) plus MonoMod.Utils, Mono.Cecil, Mono.Cecil.Rocks, MonoMod.Backports and MonoMod.ILHelpers. `Harmony-provenance.json` records `detourEngine: absent` and the dependency hashes.
The installed-runtime smoke runner contained no `MonoMod.Core.dll` or `MonoMod.Iced.dll`, so its 24 checks, including the mod's own patches, ran through the runtime stage. The plain .NET 10 smoke borrowed both files into its own directory, since that runtime has no body replacement. The payload does not contain them.
Archive `dist/HarmonyMod-3.0.1.4.zip`, SHA-256 `3ea601df394d2a7fa7dbd59720dc4feacd2ea42f25e26c1c2fe38516f196d2b2`.

Logs: `artifacts/runtime-patching-20261102/adapter-tests-202611050044-nodetour/` (first run, diagnostic), the passing `adapter-tests-2026110500*-nodetour/` run, `adapter-tests-202611050044/` and `adapter-tests-2026110500*-rest/` for the regular modes, `artifacts/runtime-patching-20261102/mod-build-3.0.1.4-1793836213.log`, `artifacts/tests/run-*` for the stock suite.
Not covered: the 20-mod in-game acceptance run with the detour-free folder, `MonoMod.ILHelpers` is shipped as a declared reference of MonoMod.Utils and MonoMod.Backports without an observed load, and the two remaining MonoMod.Core users in Harmony (`Identifiable`, the compile watch) have no stock test for the absent-engine path beyond the probe.

## 2026-11-05 private build 3.0.1.5

Mono.Cecil.Rocks removed from the detour-free set after reading the assembly reference tables of the shipped DLLs: nothing references it. The `nodetour` probe passed again with the four libraries MonoMod.Utils, Mono.Cecil, MonoMod.Backports and MonoMod.ILHelpers.
Canonical offline build with `HARMONY_THIN_DIR` and `--no-deploy`: build, the plain .NET 10 smoke with a borrowed detour engine, and the installed-runtime smoke with the payload only passed. Payload: 21 allow-listed files. The 1.7 folder: Thin `0Harmony.dll`, the four libraries, `HarmonyMod.dll`, `HarmonyMod.RuntimeIL.dll`.
Archive `dist/HarmonyMod-3.0.1.5.zip`, SHA-256 `6487ee7ad8cdb77666f650a31835dcefcbb3580b4559f8acc009512cc9174e4b`. Log `artifacts/runtime-patching-20261102/mod-build-3.0.1.5-1793837055.log`.
Observed: the installed-runtime smoke runner directory was reused across builds and still held `Mono.Cecil.Rocks.dll` from 3.0.1.4. Nothing references that file, so the smoke result stands. The build script now empties the runner first.
Not repeated: the 20-mod in-game acceptance run.
Delivered to Sam's DM as message 1567691238973636610, attachment 1567691238541365340. The attachment read back from Discord has the identical SHA-256.

## 2026-11-05 review corrections and private build 3.0.1.6

Core changes for the seven adversarial findings (resolution table in [the review](../artifacts/drafts-archive-20261106/METHOD-BODY-PATCHING-REVIEW.md)) plus generic callback binding on closed instantiations.
Stock suite, .NET 10 x64 Debug: 1,294 passed, 0 failed, 2 skipped (`artifacts/tests/run-lkabhuzt`). Six new tests in `HarmonyTests/Patching/MethodBodyPatching.cs`.
Metadata scan of the Thin `0Harmony.dll` (`3.0.0-rc.4.runtime-il.20261105.5`): 0 field types and 0 method signatures reference MonoMod.Core or MonoMod.Iced. The `.20261105.3` build had one field, `RuntimeAsyncCompileWatch+<>O::<0>__OnMethodCompiled`.
Adapter probes on 1.7.4998 with package `.20261105.5`: all twelve modes passed.

- `generics` (`adapter-tests-202611051023-review`): a closed member of a constrained definition patched without an explicit witness; `States<int>` 101, `States<string>` 1001, `States<object>` 1.
- `coexist` (`adapter-tests-202611051024-review-coexist`): a rejected reconstruction keeps the working generation (`107`, `detached=0`, `applied=1`); after a changed default body a rejected reconstruction stays `detached=1`; an explicit rebase recovers.
- `nodetour` (`adapter-tests-202611051023-review-nodetour`): regenerating the reverse stand-in keeps `6`; every Harmony field type reads without loading MonoMod.Core.
- the nine other modes (`adapter-tests-202611051024-review-rest`).

Private build 3.0.1.6: canonical offline build with `HARMONY_THIN_DIR`, both smokes passed, 21 files, the shipped `0Harmony.dll` is the `.5` thin build. Archive `dist/HarmonyMod-3.0.1.6.zip`, SHA-256 `af8f26cdd7f3bf7f4883144eeb95df5e58a49b7e326a2ad59d764eae1e0ac93f`. Log `artifacts/runtime-patching-20261102/mod-build-3.0.1.6-1793870676.log`.
Not repeated: the all-mod game run. Not delivered.

## 2026-11-05 closed bodies for instantiation transpilers and private build 3.0.1.7

Core: an instantiation with transpilers or Infix patches gets a closed body built by the ordinary path and entered from the family body under its type guard (decision record in [the decisions](METHOD-BODY-PATCHING-DECISIONS.md)).
Stock suite, .NET 10 x64 Debug: 1,295 passed, 0 failed, 2 skipped (`artifacts/tests/run-5a_vrb9s`). Two new tests: a transpiler on `FamilyReject<int>` with a bridged generic argument and result, next to a guarded `<string>` sibling and a definition postfix, then transpiler removal; an inner postfix on `ClosedOuter<int>`.
Metadata scan of the Thin `0Harmony.dll` (`3.0.0-rc.4.runtime-il.20261105.6`): 0 field types and 0 method signatures reference MonoMod.Core or MonoMod.Iced.
Adapter probes on 1.7.4998 with package `.20261105.6`: all twelve modes passed. `generics` (`adapter-tests-202611051330-closed`) adds a GUILocator-shaped transpiler on `Closed<int>` (a call with the first argument inserted at the body start): it runs only for `<int>` and keeps the definition's postfix (`106`, observed `5`), siblings stay guarded (`<string>` `1106`, `<object>` and `<double>` `106`, nothing observed), a second owner's postfix and a second transpiler compose (`122`), `GetCurrentInstructions` of the instantiation shows its transpiler and the sibling's does not, and removing the transpilers returns the instantiation to the guarded body. `nodetour`, `coexist` and the nine other modes: `adapter-tests-202611051332-closed-*`.

Private build 3.0.1.7: canonical offline build with `HARMONY_THIN_DIR`, both smokes passed, 21 files, the shipped `0Harmony.dll` is the `.6` thin build (SHA-256 `c655fb43...`). Archive `dist/HarmonyMod-3.0.1.7.zip`, SHA-256 `314042e5695312c5585a21b458a03dd085b8c9434307cb3b8c15a84348658f17`.

All-mod game run (`harmony-runtime-pipeline-17`, 27 active entries, GUILocator 1.4.1.0 unchanged): the launch log has zero error lines. GUILocator's static constructor completed and logged its normal "GUILocator enabled" notice at the line where the 3.0.1.5 run logged the `NotSupportedException`. The log journal holds four known warnings and no error, Visual Exceptions recorded none. The original save loaded, 600 ticks ran, a new save reloaded, and 300 more ticks ran. Evidence in `artifacts/runtime-patching-20261102/allmods-3.0.1.7/` (launch log, deploy log, screenshot, summary).
Delivered to Sam's DM as message 1567879035982454874 (reply to his 1567855084560322633), attachment 1567879035554504845, with a three-sentence note that his first option was taken.

## 2026-11-05 merged build restored and private build 3.0.1.8

Sam's reading of 3.0.1.7 (DM messages 1567881939355172905 to 1567892924384022569): the thin folder breaks mods and the game's own PrepatcherCompat, which reach Mono.Cecil types through `0Harmony`. He asked for the merged build back. The evidence and the decision are in [the decisions](METHOD-BODY-PATCHING-DECISIONS.md), "Merged build restored".

Static evidence: `RimWorldWaylith.PrepatcherCompat.dll` carries 55 `Mono.Cecil` type references scoped to `0Harmony`; the rc.3 and `.20261105.6` merged builds have zero public types under `Mono.Cecil`, `MonoMod.Utils` and `MonoMod.Core` and the thin build has none at all; `RimWorld.runtimeconfig.json` sets `WaylithClr.SkipAccessChecks`. The fixture's 22 mods (197 assemblies) reference neither Cecil through 0Harmony nor a Prepatcher assembly, which is why the thin 3.0.1.7 run booted.

Core: `DetourEngine.cs` deleted, `AccessTools.Identifiable` and `RuntimeAsyncCompileWatch` back to the rc.4 text, `PatchTools.detours` typed `ICoreDetour` again. Stock suite, .NET 10 x64 Debug: 1,295 passed, 0 failed (`artifacts/tests/run-_szuvzcz`).
Package `3.0.0-rc.4.runtime-il.20261105.7`, merged only (`artifacts/local-pack/pack-20261105-7.log`). Its `0Harmony.dll` SHA-256 `e99641d14b66dab06a9cd8b55e9dfac5c8c3bf9cafc52630940180b485af80e1`.
Adapter: strict mode, its auto-detection, the `nodetour` mode and `HARMONY_DEPENDENCY_DIR` removed. Probes on 1.7.4998 with the merged `.7` DLL: all eleven modes passed (`artifacts/runtime-patching-20261102/adapter-tests-202611051443-merged/`).

Private build 3.0.1.8: canonical offline build without `HARMONY_THIN_DIR`. Both smokes passed, 24 checks each. Payload: 17 files. The 1.7 folder holds `0Harmony.dll` (identical to the package asset), `HarmonyMod.dll` and `HarmonyMod.RuntimeIL.dll`. Archive `dist/HarmonyMod-3.0.1.8.zip`, SHA-256 `eeab1e6ce2f72c05fb41b74c6967af15976a0419320d4ca2b6a9c9dcf55727a5`. Log `artifacts/runtime-patching-20261102/mod-build-3.0.1.8-*.log`.

All-mod game run (27 active entries): launch log zero error lines; provider status at startup `tracked=3, applied=3, detached=0, broken=0`; GUILocator's enabled notice at line 400; journal four known warnings and no error; Visual Exceptions none. The original save loaded, 600 ticks ran, a new save `Harmony_Merged_3018_20261105` reloaded, 300 more ticks ran, screenshot, clean stop. Evidence in `artifacts/runtime-patching-20261102/allmods-3.0.1.8/`.
Not covered: none of the fixture's mods declares a Prepatcher free patch, so the merged build's Cecil contract is verified statically, not by executing one. Not delivered.

## 2026-11-05 compatibility review on 1.7.5000

- Steam updated the game to 1.7.5000 rev1275; the fixture copy was synced. Build 5000 adds `MethodBodyUpdater.PrepareGenericVariants`, `FreePatchStage` (-2000) and `InjectionStage` (-1000).
- Probe `compat-detour` and `compat` on 5000 (`artifacts/runtime-patching-20261102/compat-probe-5000-202611052207/` before the adapter change, `...-202611052212-b/` after): detour throws on every open definition and leaves closed reference-type instantiations unpatched; provider installs all definition shapes after the change and keeps a staged free patch through patch and unpatch.
- All 13 probe modes on 5000 after the adapter change: `artifacts/runtime-patching-20261102/adapter-tests-202611052213-5000/` (13 PASS).
- Static scan: `artifacts/runtime-patching-20261102/compat-scan-20261105/aggregate.txt`.
- Workshop run, 85 entries, Harmony Mod 3.0.1.9 (adapter change): `artifacts/runtime-patching-20261102/ws17-3.0.1.9/` (launch log 10,477 lines, 209 error lines, all mod-versus-save mismatches or the NoPauseChallenge transpiler conflict; 600 ticks, save `Harmony_WS17_3019_20261105`, screenshot). Visual Exceptions: three groups, none attributed to Harmony.
- Same list with `-harmony-detours`: provider reported disabled, same four startup errors.
- Harmony Mod 3.0.1.10 (package `.20261105.8`, closed-body frame mapping) built with both smokes; see the entries below for its runs.
- Stock suite after the core change (`HarmonySharedState.RegisterReplacement`): `artifacts/tests/run-km2n0q7p`, 1,295 passed, 0 failed, net10.0 x64.
- Harmony Mod 3.0.1.10 on the 27-entry fixture, 1.7.5000: `artifacts/runtime-patching-20261102/allmods-3.0.1.10/` (load `Harmony_Merged_3018_20261105`, 600 ticks, save `Harmony_Merged_30110_20261105`, reload, 300 ticks, screenshot, clean stop; launch log 4,274 lines, 0 error lines; Visual Exceptions 0; provider tracked=3 applied=3 detached=0 broken=0).
- Harmony Mod 3.0.1.10 on the 85-entry workshop list: startup only, same four NoPauseChallenge transpiler lines as 3.0.1.9 and as the `-harmony-detours` run, no Harmony exception.
- Archive: `dist/HarmonyMod-3.0.1.10.zip`, SHA-256 `033445839916b923f0f69f12a5a5f5b44a82548b2500bd547453fbf47992b542`. Delivered to Sam on 2026-11-06, see below.

### Delivery, 2026-11-06

Harmony Mod 3.0.1.10 (`dist/HarmonyMod-3.0.1.10.zip`, SHA-256 `033445839916b923f0f69f12a5a5f5b44a82548b2500bd547453fbf47992b542`) was sent to Sam in Discord DM 708325707868012645 as message 1568055654437097562 at 2026-11-06T00:13:21Z, with the compatibility summary above. Sam's 1500-mod run is the next gate.

## 2026-11-06 importer refusal, module host and hot reload host

Scope: the four items from the design discussion on the shape of the contract. Core change: `DynamicMethodBodyWriter.Import` treats an importer result of zero as a refusal and escalates a static method to a collectible stand-in, then to a stand-in that lives for the process (`DynamicMethodProxy.Create(target, collectible: false)` through `GeneratedAssemblyLoader.Load(module, collectible)`). Any other refusal throws `NotSupportedException` before installation. The contract text in `MethodBodyPatching` and the design draft now name the runtime class and the host requirements.

Stock test hosts, both built on one `System.Reflection.Metadata` emitter in `HarmonyTests/Patching/MethodBodyExportHost.cs`:

| Fixture | Tests | What it checks |
| --- | --- | --- |
| `MethodBodyExportHost` (module host) | `Exported_bodies_run_from_a_module_with_every_patch_kind`, `A_DynamicMethod_callback_imports_through_one_shared_stand_in`, `An_Infix_patch_runs_from_a_module_host`, `A_refused_value_without_a_stand_in_fails_before_anything_is_installed` | prefix, postfix, transpiler and finalizer on an instance method with a string literal, a `List<Point>` local, a constructed generic call, catch and finally; a by-reference struct receiver; unpatch restores the baseline through another installation; one shared stand-in per DynamicMethod callback; Infix with the host; refusal of a string fails `Patch()` with nothing installed and no pin |
| `MethodBodyHotReloadHost` | `Patches_install_into_the_original_method_without_a_detour`, `A_definition_wide_body_reaches_warm_and_cold_instantiations`, `A_DynamicMethod_callback_takes_a_stand_in_that_lives_for_the_process` | the body lands in the original method and `PatchTools` holds no detour; an open generic method and a method of a generic type get one body for a warm and a cold instantiation, a closed instantiation installs through its definition, unpatch restores through another generation; the module refuses the collectible stand-in and runs with the permanent one |

Dead end recorded: a `DynamicILInfo` host failed with `TypeLoadException: Could not load type 'HarmonyLibTests.Tools.TestCallerDiscovery' ... due to value type mismatch`, because a dynamic scope resolves tokens inside signature blobs against its owner module. `MethodBuilder.SetMethodBody` is absent on .NET Core. The emitter writes a real module instead.

Hot reload rules, learned in `artifacts/enc-host-20261106/Spike` against a Roslyn `EmitDifference` delta from `artifacts/enc-host-20261106/Reference`: a body at offset zero of the IL delta fails `ApplyUpdate` with "The assembly update failed"; the delta's MVID is a fresh guid heap entry; heap offsets and row numbers continue from every earlier generation (strings unaligned, blobs and user strings aligned, the guid heap keeps its prefix); the same method accepts repeated updates at the same or a different offset, tiny or fat; a generic definition's new body reaches an instantiation compiled before the update. Reflection returns the updated body after a delta, so the host reads its baseline from the image on disk through `ImageBody`. The runtime refuses a reference from the non-collectible test assembly to a collectible stand-in with `NotSupportedException: A non-collectible assembly may not reference a collectible assembly`, which the permanent stand-in resolves.

Runs on net10.0 x64 Debug:

- Focused fixtures without `DOTNET_MODIFIABLE_ASSEMBLIES`: 53 passed, 3 ignored (the hot reload fixture), 0 failed (`artifacts/tests/run-got66zj0`).
- Hot reload fixture with `DOTNET_MODIFIABLE_ASSEMBLIES=debug`: 3 passed, 0 failed (`artifacts/tests/run-6sotpvsg`).
- Complete stock suite before the permanent stand-in: 1,299 passed, 0 failed, 4 ignored (`artifacts/tests/run-zhw3j1nm`, log `artifacts/tests/stock-20261106-hosts.out`).
- Core packed as `3.0.0-rc.4.runtime-il.20261106.9` (`artifacts/local-pack/pack-20261106-9.log`); mod worktree pinned to it and built as Harmony Mod 3.0.1.11, `dist/HarmonyMod-3.0.1.11.zip`, SHA-256 `8051f6d8f0f3ec4c9f5afbd9b536de9276e76abac335eeea5decc7738efc630a`, not delivered.
- A first probe run against the intermediate `obj/` assembly failed to load `Mono.Cecil.ModuleDefinition` (`adapter-tests-202611060220-hosts`); that is the unmerged input, not a defect. The rerun against `bin/Lib.Harmony/release_net10.0/0Harmony.dll` is recorded below.
- Adapter probes against the merged `bin/Lib.Harmony/release_net10.0/0Harmony.dll` on 1.7.5000, all thirteen modes (`bridge,coexist,compat-detour,compat,failure,generics,hot-generics,multi,pipeline,protocol,readbody,reentry,runtime-contracts`): 13 PASS, run.py `ok` (`artifacts/runtime-patching-20261102/adapter-tests-202611060221-hosts-merged`).
- Complete stock suite on the final sources: 1,299 passed, 0 failed, 5 ignored, 1,304 total (`artifacts/tests/run-wa_zjvwc`, log `artifacts/tests/stock-20261106-hosts-final.out`).

Formatting note: `dotnet format` with style fixers wrote merge markers into `GeneratedAssemblyLoader.cs`, whose code differs per target framework. The file was restored from the markers' "before" side, checked against HEAD, and formatted with `dotnet format whitespace` only.

## 2026-11-06 documentation and cleanup

- User documentation: `Documentation/articles/hosting.md` ("Host Harmony in a runtime"), linked from `toc.yml`, `index.md`, `new.md` and the generics section of `patching-edgecases.md`. Its code comes from the compiled example `Documentation/examples/hosting.cs`, which `HarmonyTests/Patching/HostingDocumentation.cs` runs: the documented host receives a fat CLI body and installs nothing, so the original keeps running. A local docfx 2.78.5 build into `artifacts/docs/` rendered the page with its snippets and resolved links; the only warnings are the usual unresolved framework assembly references of the metadata step.
- Cleanup: the unused third slot of the shared protocol record is gone from `MethodBodyPatching.Shared()`. The design draft shrank from 672 to 320 lines: the prototype, review narratives, Sam's proposal, the implementation records and the 1.7.4998 plan are replaced by a current "Harmony Mod adapter" section, a rewritten "Generic compilation and failure", a 5000 paragraph under "Profiler, EnC and field injection" and a short "History". The two review handoffs left `drafts/`. Full copies of all five drafts as of this morning are in `artifacts/drafts-archive-20261106/`, and the two cross references point there. The spike build outputs under `artifacts/enc-host-20261106` were deleted; sources and deltas stay.
- Complete stock suite after the documentation example and the slot removal: see `artifacts/tests/stock-20261106-docs-final.out` for the counters and the run directory.

## 2026-11-06 release readiness check

- Complete stock suite in Release on net10 x64: 1,300 passed, 0 failed, 5 ignored (`artifacts/tests/run-8efjau6u`, log `artifacts/tests/stock-20261106-release.out`).
- Whole solution compiled in Release for every configured target framework (`artifacts/local-release-all/build-20261106.log` and `-2.log`). The first pass found one error: `A_definition_wide_export_keeps_local_initialization` used `AssemblyBuilder.DefineDynamicAssembly`, which net35 lacks. It now uses `PatchTools.DefineDynamicAssembly`. The second pass has 0 errors, and no warnings in the new or changed files.
- Not run locally: the Platform Tests matrix (Mono, Windows .NET Framework, QEMU lanes) and the compatibility lanes against published Harmony 2.x. Both run in CI on the release commit and are release gates by the documented process.

## 2026-11-06 first CI runs on v3

Pushed as `46fdfcc3..b725dfaf` (seven commits). Platform Tests run 42157275636 on `b725dfaf`: 47 lanes, all successful, including the compatibility lanes. Documentation build 42157275606: successful, no annotations.

The two earlier runs failed and were cancelled after their lanes had reported:

- Every Mono lane aborted with a stack overflow: Mono's reflection recurses in `IsValueType` for a function pointer type while MonoMod imports the signature. The function pointer test now ignores itself on Mono, and `patching-edgecases.md` records the limit.
- `FunctionPointerSignatures` failed on .NET 11 and on the netcoreapp3.x and x86 lanes, which run on a newer runtime than their target: `GetDynamicILInfo` rejects a function pointer type in a dynamic signature there. The callable copy now uses a native int in its place (`DynamicMethodBodyWriter.Native`). Verified locally on .NET 10, .NET Core 3.1 and .NET 11 x64.
- `A_closed_sibling_keeps_a_detour_held_family` asserted the result of a detoured shared generic instantiation, which the detour does not reach on .NET Core 3.1, x86 and some .NET 11 runs, and MonoMod throws `NotImplementedException` for a generic method instantiation on .NET Framework (`Fx.V48.MethodTable.GetMethodDescForSlot`). The test checks the registration and ignores itself on .NET Framework.
- `An_already_selected_reverse_standin_receives_a_complete_body_writer` ran on net35, where body export throws by design. It ignores itself there.
- The generic importer forwarded assembly names to MonoMod's obsolete overload. It uses Cecil's default importer now. The category tests use the explicit-assembly overloads.

Job logs of a running or cancelled run come from `GET /repos/pardeike/Harmony/actions/jobs/<id>/logs` with the `gh auth token`; `gh run view --log-failed` refuses them until the run completes. Copies are under `artifacts/tests/ci-42155186328/` and `artifacts/tests/ci-42156217487/`.

## 2026-11-06 review rounds and integrated corrections

Two read-only review rounds covered `46fdfcc3..61064108` on 2026-11-06.
Each round reviewed protocol/lifecycle, generic construction, body export, and tests/documentation.
The first round used Claude Opus. The second used Codex.
The eight reports are `artifacts/reviews/opus-{protocol,generics,export,tests}.md` and `artifacts/reviews/rc4-{protocol,generics,export,tests}.md`.
The reviews traced code without executing tests. The fix packages subsequently added and ran regressions.

Integrated source commits are `381db566` for lifecycle, `bda80584` for generics, and `231322a2` for export.
Package reports are `artifacts/reviews/fix-lifecycle.md`, `fix-generics.md`, and `fix-export.md`.
The following results come from those reports and their committed fixtures. They are separate worktree results, not a combined runtime gate.

### Generic construction fixes

Tests below are in `HarmonyTests/Patching/MethodBodyPatchingGenerics.cs`.

| Item | Correction | Regression tests |
| --- | --- | --- |
| A1 | Require guaranteed assignment from open source types. | `Definition_rejects_a_closed_argument_binding`, `Definition_rejects_a_closed_instance_binding`, `Definition_rejects_a_closed_pointer_binding` |
| A2 | Validate passthrough results against closed types. | `Instantiation_rejects_an_incompatible_passthrough` |
| A3 | Preserve reference writes and aliasing. | `Instantiation_reference_prefix_updates_the_original_argument`, `Instantiation_reference_arguments_keep_aliasing` |
| A4 | Close `__resultRef` validation and retain its delegate-local address. | `Instantiation_result_ref_can_redirect_the_returned_address` |
| A5 | Preserve registration identity for closed-helper `__state`. | `Closed_body_keeps_state_per_registration` |
| A6 | Reject fixed generic callback registrations throughout a family. | `Instantiation_rejects_a_fixed_generic_callback`, `A_factory_keeps_its_fixed_callback_after_a_family_rebuild` |
| A7 | Preserve pointer and byref-like storage, explicitly reject function-pointer bridges. | `Instantiation_pointer_binding_preserves_the_address`, `Instantiation_span_binding_and_return_keep_the_storage`, `Closed_body_function_pointer_is_rejected_before_installation` |
| A8 | Reject foreign generic parameters in member operands. | `Definition_rejects_foreign_generic_parameters_in_member_operands` |
| A9, exception stack | Rethrow an unchanged exception when sibling finalizers do not replace it. | `A_sibling_finalizer_preserves_the_original_exception_stack` |

The complete net10/x64 Debug suite passed 1,327 cases, with five unexecuted TRX entries.
Log: `artifacts/fix-worktrees/generics/artifacts/tests/run-fwk3b9dw/test.log`.
With fixes temporarily removed, 22 cases failed and five positive controls passed in `run-bky460ui`.
These tests export through a fake importer and execute generated snapshots. They do not establish original-method installation or Waylith behavior.

### Lifecycle fixes

Tests below are in `HarmonyTests/Patching/MethodBodyPatchingLifecycle.cs`.

| Item | Correction | Regression tests |
| --- | --- | --- |
| B1 | Rebuild a definition through member records when it has no record. | `Rebuild_definition_uses_member_records` |
| B2 | Remove closed-helper persistent state and dispose all completed preparations on rollback. | `Removing_closed_infix_clears_registration_slots_and_dispose_hook`, `Failed_closed_preparation_rolls_back_completed_preparations` |
| B3 | Reject registration after an accepted pin and preserve family-owner eligibility. | `Registration_cannot_move_accepted_targets` |
| B4 | Retain selection once a registered installer starts. | `Installer_failure_keeps_selection_without_publishing_metadata`, `Failed_family_installation_retains_both_pins_and_can_retry`, `Construction_failure_releases_member_and_definition` |
| B5 | Lock wrapper and reverse construction through publication. | `Reflective_wrapper_construction_holds_protocol_lock`, `Reverse_construction_holds_protocol_lock` |
| B6 | Reject host-owned generic reverse stand-ins before pinning. | `Generic_reverse_standins_are_rejected_before_pinning` |
| B7 | Track active reverse ownership and default-detour recipes. | `Ordinary_patch_invalidates_reverse_recipe`, `Default_reverse_recipe_can_be_rebuilt`, `Rebuild_follows_the_installation_from_another_copy` |
| B8 | Reject installer re-entry into a target or its family. | `Installer_cannot_rebuild_or_patch_its_target`, `Installer_cannot_patch_a_sibling_in_its_family` |
| B9 | Restore the reserved third slot with its required empty BCL list. | `New_shared_record_satisfies_the_delivered_three_slot_validator` |
| B10, documented behavior | Keep null returns for open and closed family constructors. | `Family_constructor_patch_returns_null` |

The complete net10/x64 Debug suite passed 1,323 cases, with five skipped entries.
Log: `artifacts/fix-worktrees/lifecycle/artifacts/tests/run-qtw0t5aa/test.log`.
With fixes temporarily removed, 21 cases failed and two unchanged-behavior cases passed in `run-_uptwc_y`.
Cross-copy tests use a renamed, noncollectible copy and a recording host. They do not establish cooperation with every delivered Harmony version.

New protocol roots have three slots. Slot 2 contains an unused empty `List<Func<MethodBase, bool>>`.
This supersedes the earlier slot-removal entry. Existing two-slot records remain accepted without replacement.
The earlier claim that import refusal leaves no pin is also superseded. Once the registered installer starts, even export failure retains selection.
A retained pin without metadata is not proof of a successful installation.

### Export fixes

Tests below are in `HarmonyTests/Patching/MethodBodyPatchingExport.cs`.

| Item | Correction | Regression tests |
| --- | --- | --- |
| C1 | Export self-instantiated own members and `__originalMethod` tokens. | `Hot_reload_preserves_own_generic_members_and_original_method`, `Hot_reload_preserves_recursive_generic_method_and_original_method`, `Hot_reload_preserves_methods_with_type_and_method_parameters` |
| C2 | Restore original callback operands after generating callable assemblies. | `Assembly_generation_preserves_exported_dynamic_method_identity` |
| C3, partial | Reuse permanent closed-helper stand-ins and publish delegate changes after installation. | `Rebuilt_helpers_reuse_one_permanent_stand_in_per_signature`, `Failed_installation_preserves_the_previous_helper_and_other_originals` |
| C4 | Offer permanent generic helper images and reject collectible dependencies. | `Hot_reload_accepts_permanent_generic_Infix_helpers`, `Permanent_generic_helpers_report_collectible_dependency_limits` |
| C5 | Refuse stand-ins for identity-bearing method operands. | `A_call_stand_in_cannot_replace_a_method_identity` |
| C6 | Name the missing collectible-load-context requirement. | `Generic_helpers_require_a_runtime_with_collectible_load_contexts`, conditional legacy case, not run by net10 gates |
| C7 | Omit local-signature import when no locals exist. | `A_body_without_locals_does_not_import_a_local_signature` |
| C8 | Preserve function-pointer signatures and normalize arrays and helper references. | `Dynamic_signatures_normalize_function_pointer_arrays`, `Helper_signatures_normalize_nameless_function_pointer_references`, `Stand_ins_preserve_function_pointer_callback_signatures`, `Stand_ins_keep_exact_nested_function_pointer_signatures` |

With `DOTNET_MODIFIABLE_ASSEMBLIES=debug`, the complete net10/x64 Debug suite passed 1,325 cases, with two existing unexecuted entries.
Log: `artifacts/fix-worktrees/export/artifacts/tests/run-dl1ujq9x/test.log`.
Without that variable, 1,317 cases passed, eight hot reload cases skipped, and two existing cases remained unexecuted in `run-lajwd_yx`.
With fixes temporarily removed, all 20 cases then present failed in `run-u373b816`.
The later collectible-dependency diagnostic also failed before its guard and passed afterwards.

### Deliberate limits and remaining review work

- A7 function-pointer bridges remain rejected. The pointer-signature exporter fixes do not remove that family-bridge restriction.
- B10 constructors installed through a family still return null. The public return type remains `MethodInfo`.
- C3 permanent open generic helper images can accumulate across rebuilds. Unrelated DynamicMethods also retain separate permanent stand-ins.
- C4 permanent helpers cannot reference collectible dependencies. No dependency-graph promotion was implemented.
- C6 has no legacy generic assembly generator. .NET Framework and Mono refuse Infix patches on a generic method definition before installation.
- Host access checks remain a runtime requirement. Private callbacks and internal guard helpers need appropriate access from the installed module.
- The test-host emitter still has bounded signature and assembly-resolution coverage. It is not a complete host implementation.
- Tests/host infrastructure findings outside these three fix packages and the documentation package remain separate work. This includes hot reload delta failure state.
- No Waylith run or release of these review fixes is established. The game results identify earlier commits.

### Documentation package

E1, E3 and E4 update the hosting guide, generic edge cases, API remarks and v3 changes to match the integrated code.
E5 records the new decisions, evidence boundaries and deliberate limits.
E2 replaces fabricated metadata tokens with an abstract runtime contract and a separate baseline reader.
The `HostingDocumentation` fixture compiles that example and executes exported bodies through the existing module emitter.
Its tests cover the latest external baseline, rebuild and unpatch behavior, permanent stand-in selection, and direct collectible-member refusal.
Those checks supersede the earlier header-only example test. They do not install bytes into the original method.
Final documentation-package gates and exact logs are recorded in `artifacts/reviews/fix-docs.md`.

Documentation regressions in `HarmonyTests/Patching/HostingDocumentation.cs`:

- `The_documented_host_exports_the_latest_external_baseline`
- `The_documented_host_uses_a_permanent_stand_in_for_a_dynamic_callback`
- `The_documented_importer_refuses_collectible_members_and_markers`

All three passed in `artifacts/fix-worktrees/docs/artifacts/tests/run-kyf_g24m/test.log`.
Restoring the old reflection-baseline behavior caused two expected failures in `run-_lzud7kf`.
Restoring collectible acceptance caused two expected failures in `run-45bej0vi`. Both mutations were reverted.
The complete net10/x64 Debug suite then passed 1,369 cases in `run-mp_3evj3`, with eight hot reload cases and two existing cases unexecuted.

DocFX 2.78.5 built from the documented net35 library input, with output isolated under `artifacts/fix-worktrees/docs/artifacts/docs-package/`.
The link verifier passed 104 pages and 2,754 local links and assets. Generated hosting snippets and XML remarks were also checked.
DocFX reported 357 `InvalidAssemblyReference` warnings and zero errors. This gate proves local generation and link integrity, not publication.
The first staging attempt omitted the tracked API landing page. It failed link verification and passed after that page was included.
Logs and the staging command are recorded in `artifacts/reviews/fix-docs.md`.

## 2026-11-06 warnings, tie order and runtime detection

Platform Tests run 42195753010 reported these warnings:

- CS0649 for `MethodBodyExportHost.Carrier<T>.Value`, 102 annotations.
- NU1902 for `Microsoft.Build.Tasks.Git` 8.0.0 (GHSA-23fw-v26w-5fgq) in every restore. The package came from `Microsoft.SourceLink.GitHub` 8.0.0.
- NU1510 for `System.Text.Json` in single-framework net10.0 builds of the compatibility lanes.
- MonoMod's skip warning in every net11.0 build. It has no code, so `NoWarn` cannot address it.
- Chocolatey's ignored-checksum warning on the Windows Mono lanes, and Git line-ending notices in the documentation deployment.

Fixes: the SDK's Source Link replaces the package reference. net10.0 and net11.0 use the shared-framework `System.Text.Json`.
`Directory.Build.targets` and the Thin package targets replace MonoMod's skip target with a low-importance message.
The test field is initialized. Chocolatey verifies checksums. The deploy clone sets `core.safecrlf false`.
Whole-solution builds for every framework then reported 0 warnings and 0 errors in Release and Debug.
A net11.0 consumer of the published rc.4 Thin package shows MonoMod's warning. A consumer of a locally packed Thin package shows none.
The `actions/checkout` hint about the default branch name and GitHub's macOS arm64 capacity notice are outside the repository.

Tie order: `Equal_priority_ties_put_definition_patches_first` failed on the old sorter with `DIDIDIDIDIDIDIIDIDDI`. It passes with the stable sort.

Runtime detection: a probe showed `IsNetCoreRuntime` false on .NET Core 3.1, .NET 8 and .NET 10. It showed `IsNetFrameworkRuntime` true on .NET Core 3.1.
On .NET Core 3.x, `RuntimeInformation` is outside the core library, so the unqualified type lookup failed. .NET 5 and later describe themselves as ".NET", not ".NET Core".
Both properties now use the core library name.
The old detection had made `A_closed_sibling_keeps_a_detour_held_family` skip itself on netcoreapp3.0, 3.1, net5.0 and net6.0 as ".NET Framework".
Run 42224853192 executed it there for the first time. On Ubuntu netcoreapp3.0, `Mixed<int>()` returned 1 instead of 2. Its runtime exception now covers .NET Core 3.x, not only 3.1.
Platform Tests passes `HARMONY_EXPECTED_RUNTIME` to every lane. The Windows Mono lanes expect .NET Framework, because VSTest starts their net4x test host on the .NET Framework CLR.

Local gates: net10 x64 Debug and Release passed 1,394 cases each. net11 passed 1,401. The focused .NET Core 3.1 run passed 112.
The hot reload fixture passed 8 cases. `scripts/test_workflow_test.py` passed.
