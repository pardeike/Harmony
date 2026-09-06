# Infix compatibility test strategy

Supporting test plan and execution notes, 2026-09-05. [INFIX-NEW-IMPL-V3.md](INFIX-NEW-IMPL-V3.md) is the sole feature specification. This document turns its sections 7, 8, and 10 into executable checks. It adds no API or compatibility promise. The executable suite is now in [HarmonyTests.Compatibility](../HarmonyTests.Compatibility/README.md); executed lanes and remaining boundaries are recorded below.

The first useful result is proof that two engines can update the same ordinary patch. Only then does testing their Infix interaction answer the intended question. Separately test a patch DLL running against a different Harmony from the one used to compile it. These situations have different loading rules and failure boundaries.

## 1. What has been checked

The child-process suite first reproduced an ordinary CoreCLR coexistence defect: published 2.4.2 and the previous current build created separate shared-state dictionaries, and the second engine's ordinary rebuild dropped the first engine's prefix. A read-only `TypeResolve` observer established the actual lookup path. Current production lookup now discovers the existing generated assembly and resolves later old lookups to that same assembly. No test injects dictionary references or a synthetic singleton. Ordinary add/rebuild/cross-engine removal now succeeds in both initialization orders on the net9/x64 run.

The final frozen production candidate completed all **388 child expectations** on actual net9.0.19 and net8.0.30 x64 runtimes. Each listed lane passed without an unexpected failure:

| Published old release | net9 JSON | net8 JSON | net8 BinaryFormatter |
| --- | ---: | ---: | ---: |
| 2.4.2 | 44 | 44 | 44 |
| 2.4.1 | — | 44 | 44 |
| 2.4.0 | — | 44 | 44 |
| 2.3.6 | — | 40 | 40 |

The totals comprise 122 successful behavior cases, 149 intended compatibility rejections, 18 missing-API/loading boundaries, and 99 explicitly characterized diagnostics: 72 generated-proxy loading limits, 18 published foreign-transpiler limits and 9 concurrent stale-candidate overwrites. A lane has 11 known-limit diagnostics; those are not successful feature operations. The 2.3.6 lanes omit four legacy-inner recovery cases because that release predates those arrays. Its two unrecognized inner role names are observed omissions, not declaration-marker rejections.

Every final lane includes all five active-state rejection and last-unpatch retry operations, nine old-discovery declarations, prepare-false deferred resolution, cold recursive identities and foreign `HarmonyOuter` binding, same-named-class state isolation through both readers, malformed identities/envelopes, duplicate target-module rejection, and duplicate patch-module rejection before registration and during cached/cold rebuilding. No-Infix JSON byte equality is asserted where the historical record shape matches. The final runs supersede earlier partial runs and the corrected harness assumption about 2.3.6's absent `VersionCount`.

Local evidence is retained under `HarmonyTests.Compatibility/reports/final-net9-json-2.4.2/` and `reports/final-net8-{json,binary}-{2.4.2,2.4.1,2.4.0,2.3.6}/`; each directory has `summary.json` plus child requests, results and stderr logs. The current Fat DLL snapshots are in `/tmp/harmony-final-compat-LFEqsr/`, with these identities:

| Current target | MVID | SHA-256 |
| --- | --- | --- |
| net9.0 | `486d721a-3d09-41d4-856f-2fb7bfa7a472` | `7a673973f9f54e91649711a32f7ad60212dd0a5a7cf9102c0cd80491bc9d96e5` |
| net8.0 | `243cf2a8-a878-4192-bc24-66a516ac0639` | `bf4f260b230f1bc4e05e7e1c50a8c10140078570ac0bd1eecdd26510406a842f` |
| net472 | `29a27fd7-89ad-4479-8b21-37d756323e4f` | `b01e7ef4aed85392f7eb15b1d280476f4a58ffc637e41c197486510d4d43852a` |

The ordinary foreign-transpiler probes expose two independent limits of specific loading arrangements. Published 2.4.2 re-resolves a constructed `List<foreign CodeInstruction>` by its full name through the core library, losing the original type's load-context identity. Current code now keeps the constructed type directly. The published-old method remains unchanged. Placing one same-identity Fat engine in the default context can instead make MonoMod's generated `ILGeneratorProxy` bind its constraint to that engine while receiving the other engine's `CecilILGenerator`. Those failures occur before Infix and are recorded separately; a successful envelope test is not a claim that those loader policies support arbitrary old/new transpiler composition.

Additional focused net9 execution verifies state isolation for same-named callback classes in distinct modules, two call sites and repeated loop iterations, two postfix readers per class, and rebuilds through both current engines. The duplicate-patch-module children distinguish that valid case from two copies of the exact same patch module; those require ambiguity rejection and owner-removal recovery.

The deterministic `concurrent-old-candidate` diagnostic reproduces the inherited cross-engine lost update. An old transpiler pauses after its unpublished candidate reads legacy state. Current installs and executes an Infix, then old resumes and replaces it with its stale legacy candidate. The observed result is `known-concurrent-update-limitation`, not a passing no-drop guarantee. The host must serialize updates across engines; sequential envelope protection does not invalidate a candidate an old binary read before the active state existed.

Mono 6.12.0.206 x64 executes the one-engine controls, opposite compile/runtime binding, missing-API boundaries and prepare-false case. Against the final net472 candidate and published 2.4.2, the 35 expectations comprise 5 successful behavior cases, 2 expected missing-API failures, 26 typed-reference unifications and 2 ordinary proxy-initialization failures. Four byte-loader probes reproduce the same boundaries; all four ordinary probes correctly fail the runner when `REQUIRE_COEXISTENCE=1`. Earlier separately labeled version-override builds tested all four old releases: 35 expectations each for 2.4.0/1/2, 31 for 2.3.6, with the same 7 executable controls/API boundaries per lane.

Actual file and byte loading unify unsigned typed Harmony references onto the first copy, even with isolated current builds using test-only versions 2.4.3.0 and 2.4.4.0. Reflection-routed access to the second real assembly reaches an ordinary embedded-MonoMod generic-proxy failure during prerequisite shared-state initialization, in either order; its subsequent public patch operations are not reached. Reports retain exact providers and stages. Mono mixed-engine Infix remains unproven on this host. The dedicated workflow retains this distinction and does not substitute a Mono process launch for mixed-engine execution. The new workflow is checked locally but has not been pushed or executed in GitHub Actions.

Run instructions, pinned package/assembly hashes, the isolated net472 host, and the dedicated CI matrix are in [the suite README](../HarmonyTests.Compatibility/README.md). Reports include actual artifact MVIDs and typed providers. `Passed` describes test expectations; `HasPublishedLimitations` preserves the distinction between passing those expectations and universal loader compatibility. Mono/framework executable coverage must be reported separately from a portable host build or runtime launch.

Repository source was inspected at baseline `a4915e9`. DecompilerServer also inspected these cached NuGet release DLLs, without executing them:

| Artifact | Module build identifier, MVID | SHA-256 |
| --- | --- | --- |
| `Lib.Harmony 2.4.2`, `lib/net9.0/0Harmony.dll` | `c62e7196-1ad0-49f0-8f32-2ede6343e90b` | `a849b726e1f9248d71aabbed8114deaf79beb7acc25e8344ff92a27ad8ac87ab` |
| `Lib.Harmony 2.3.6`, `lib/net8.0/0Harmony.dll` | `dc9d5043-2650-4f94-a106-f1014729e8c2` | `9514a5c06980b2d2b84e6b6429453eb31b7d904832f14d645e69754e0aaf902e` |

The 2.4.2 binary rejects a non-Normal `methodType` in `PatchClassProcessor.BulkPatch` before processing jobs, and its `UseBinaryFormatter` property returns false in the net9 asset. Its shared-state lookup calls `Type.GetType("HarmonySharedState", false)` before creating a dynamic assembly. The 2.3.6 binary has no inner arrays in `PatchInfo`; its serializer has JSON and BinaryFormatter branches, with the latter returning `MemoryStream.GetBuffer()`. Preserve those actual old bytes, including unused trailing buffer capacity.

Tagged source for [2.4.2 discovery](https://github.com/pardeike/Harmony/blob/v2.4.2.0/Harmony/Public/PatchClassProcessor.cs), [2.4.2 serialization](https://github.com/pardeike/Harmony/blob/v2.4.2.0/Harmony/Serialization/PatchInfoSerialization.cs), and [2.3.6 patch state](https://github.com/pardeike/Harmony/blob/v2.3.6.0/Harmony/Public/Patch.cs) supports these findings. Source confirms the ordinary target lookup returns no method for an unknown `MethodType`, giving the declaration marker a second rejection path. The executable declaration cases now verify those paths against each actual old binary.

The initial baseline question was whether the unqualified shared-state lookup actually fails on a real host. The documented [Type.GetType lookup rules](https://learn.microsoft.com/en-us/dotnet/api/system.type.gettype?view=net-9.0) motivated that question; the initial child trace and subsequent production-fix runs above provide the runtime evidence. Keep the ordinary cross-engine control below mandatory, and report any future failure independently of Infix.

## 2. Small executable structure

Use an ordinary parent test to launch a fresh console child for each case. Do not execute mixed Harmony cases inside the NUnit process. Static serialization choices, generated shared state, loaded assemblies, and native detours survive ordinary fixture cleanup. A detour redirects execution of the original method to its generated replacement. Process exit gives each case a clean starting point.

Use four small fixture components:

- A child host with no compile-time Harmony reference. It chooses the requested files and loading policy, invokes fixture entry points, and reports observations.
- One shared target/trace assembly with no Harmony reference. It contains non-inlined outer methods, called helpers, counters, and plain result data. Load it once for positive coexistence cases.
- An ordinary patch fixture compiled separately against each pinned old release and against current Harmony. The old fixture uses only APIs present in its referenced release. Its entry point calls those APIs directly, so it proves compiled-call binding rather than only reflective invocation.
- A new declaration fixture and a new manual-API fixture. Keep attribute materialization and direct use of new members in separate entry points, so a missing new member cannot prevent reporting an earlier successful load.

Compile fixtures with explicit file references from separate old/new directories. Record the compiler reference identity and hash, and prevent normal project-reference copying from replacing the chosen runtime DLL. Stage only the selected runtime dependencies in a case directory. Preserve the compiled patch DLL unchanged when swapping engines. An `extern alias` can select references at compilation; it does not decide runtime loading.

Pass the child a case manifest with runtime host, target framework, exact artifact paths/hashes, loader mode, load order, requested engine, serializer switch, and expected stage/result. Set serializer switches before touching either Harmony. On return, emit one structured result with stage events; keep diagnostic logs separate. The parent checks exit code, timeout, and the full expected result. A crash or an unclassified exception is a failure, even for a negative test.

For cross-engine calls, create each Harmony instance and its `HarmonyMethod` objects through that engine's own assembly. Pass shared `Type`, `MethodInfo`, and primitive values across the host boundary. Never cast old `HarmonyMethod`, `Patch`, or `CodeInstruction` to new types. A transpiler fixture belongs to its compiling engine; existing Harmony conversion of foreign instruction objects is part of the ordinary control when exercised.

## 3. Loading arrangements that mean something

An `AssemblyLoadContext` is a .NET scope that decides which assembly supplies a dependency. CoreCLR permits one assembly per simple name in each context; an already loaded version can satisfy an equal or lower version request. Separate contexts allow separate copies, whose identically named types are still different types. These are [runtime loading rules](https://learn.microsoft.com/en-us/dotnet/core/dependency-loading/understanding-assemblyloadcontext), not Harmony behavior.

| Mode | Setup | What it proves |
| --- | --- | --- |
| One engine, normal binding | A Harmony-free host stages one engine; fixtures resolve through ordinary host loading. On .NET Framework, use a case-local app configuration for any explicit binding redirect. | An old or new compiled patch uses the engine the application actually provides. A newer requested version may fail to bind to an older engine before Harmony runs. |
| Two engines on CoreCLR | Load old and new plugin adapters into separate named, noncollectible contexts. Each resolves its own Harmony and private dependencies; both share the one target/trace assembly and runtime assemblies. | A realistic plugin host with two actual Harmony assemblies. First verify ordinary shared patching and dependency resolution. |
| Two engines on .NET Framework or classic Mono | In one application domain, use the host's normal full-identity/path loading and case-local resolver/configuration where needed. Reverse load order. Record any unification onto one engine. | Actual coexistence if two assemblies act, otherwise the application's one-engine behavior. Separate application domains do not prove shared patch state. |
| Forced substitution or duplicate identity | Only for a documented host policy, such as an explicit .NET Framework redirect, a plugin dependency resolver, or a measured Mono host that selects the first copy. | That particular host's substitution behavior. It does not establish the default behavior of another runtime. |

[.NET Framework redirects can select an older version](https://learn.microsoft.com/en-us/dotnet/framework/configure-apps/redirect-assembly-versions). Its [LoadFrom behavior can also return a previously loaded same-identity assembly despite a different path](https://learn.microsoft.com/en-us/dotnet/framework/deployment/best-practices-for-assembly-loading). Classic Mono added an optional strict assembly loader in [Mono 5.2](https://www.mono-project.com/docs/about-mono/releases/5.2.0/); do not extrapolate that historical behavior to every Unity or modern Mono host.

Current development and released 2.4.2 both identify as `0Harmony, Version=2.4.2.0`. Record MVID/hash and the actual `Assembly` object, not just version or filename. Use separate contexts for a deliberate duplicate-identity test. For a future distinct-version release scenario, use an isolated current build with its intended version, or an explicitly labeled test-only version override. Never rename, re-sign, rebuild, or modify the old artifact to manufacture coexistence. Two current-compatible readers can be copies in separate contexts; their distinct runtime objects matter even if deterministic builds give them the same MVID.

Do not preload a fake `HarmonySharedState`, replace its dictionary references, strip a declaration marker, or transplant serializer bytes to make a coexistence test pass. A separate decoder test may pass bytes directly and a recovery fixture may seed old records, but label those narrower proofs.

### Required ordinary control

Before running Infix in each runtime/loading/backend configuration:

1. Identify both actual Harmony assemblies and the exact same target `MethodInfo`. Initialize them in the requested order.
2. Through each engine, inspect its private `HarmonySharedState.state` and replacement dictionaries. Check reference identity of `state` and `originals`, and `originalsMono` when relevant. Enumerate the dynamic shared-state assemblies and their version field. Matching dictionary contents alone do not prove sharing.
3. Have engine A add an ordinary prefix, B add an ordinary postfix, then A add a counter transpiler. Execute the target after each change. Both owners must remain visible to both engines and each behavior must occur once.
4. Remove A through B, execute, remove B through A, and execute again. Check traces, state records, and replacement mappings at each step. Repeat with reversed engine initialization and update order in a fresh child.

Inspect each engine's detour dictionary and loaded MonoMod dependencies for diagnosis, but do not require private detour objects to be shared. The execution trace proves that the native redirection points at a replacement containing the intended records. A function pointer or a shared managed dictionary alone does not.

If this control fails, the dependent Infix cases are unproven, not passing negative tests. On a claimed supported host this is a release blocker to investigate. Do not classify an observed ordinary compatibility failure as an Infix regression or remove the case by inventing a loader exclusion. Keep the initial sequences single-threaded; the current per-assembly `PatchProcessor.locker` does not itself prove coordination between different Harmony assemblies.

## 4. Purposeful case set

Here, old means one of the released assemblies required by V3, and new means the completed implementation. An engine is the specific Harmony assembly whose processor performs the operation.

| Case | Setup and action | Required outcome |
| --- | --- | --- |
| C1, compile old, use new | Run each old ordinary fixture with only new Harmony available, through direct typed API calls and class discovery. | Binding succeeds where the host permits that reference; patches, inspection, rebuild, and unpatch retain ordinary behavior. Existing argument and reverse-patch regressions pass. |
| C2, compile new common API, use old | Compile an ordinary fixture against new Harmony, using only shared APIs; stage old only. | If the runtime accepts the binding, ordinary behavior matches its old-engine control. If it rejects the version before entry, report the precise loader boundary. Do not count this as C3 or declaration-guard proof. |
| C3, compile new feature API, use old | Stage old only; separately materialize new attributes and invoke a direct new-API entry point. | Assembly/version rejection, missing type, or missing member at the applicable boundary. No outer-patch fallback, registration, patch-body execution, or target mutation. |
| C4, old engine discovers new declaration | Both engines are loaded. Old scans a new fixture whose attributes resolve to new Harmony. New can load those same attributes successfully. | An old-recognized role reaches marker rejection before installation or any transpiler runs. A role name the old release does not recognize can be ignored with no registration or execution. New accepts the declaration as an Infix. No missing-attribute-type shortcut counts as guard proof. |
| C5, old engine meets active new state | New installs an Infix and an ordinary counter transpiler. Old inspects, adds a patch, removes by owner/method, or removes all on that outer method, in separate children. | Old rejects the enveloped bytes during state reading. Counters do not advance, bytes/mappings stay unchanged, and executing the target still shows the installed Infix. |
| C6, remove last Infix and retry old | After C5, new removes the last inner record, preserving ordinary records; old retries its original operation. | State returns to the legacy format. Old can read and rebuild it, and executes only the surviving/new ordinary records. Removing just one of several inner records must keep the envelope. |
| C7, old state read by new | Old writes ordinary state using its real serializer; new reads and updates it. Include pre-inner-array 2.3.6 and invalid old inner records as described below. | Missing legacy arrays normalize to empty. Readable incomplete inner records remain inspectable/removable, but no rebuild executes while any invalid survivor remains. |
| C8, two compatible new readers | A registers exact/family Infixes, B reads cold identities, adds/removes records and rebuilds; then A repeats. | Both reconstruct the same complete targets and match sets, retain record ordering/state, and expose detached snapshots owned by the reading assembly. |

C4 generates a small declaration set covering prefix/postfix attributes, supported role names, duplicate equivalent name-plus-attribute roles, class target versus `TargetMethod` and `TargetMethods`, reversed attribute order, and priority/before/after annotations. Cover each of those choices, not their full cross-product. Use at least one before/after chain and both prefix and postfix guards. New-only negative controls cover conflicting roles and forbidden method-level `[HarmonyPatch]`. Prepare-false cases prove target resolution stays deferred. Prepare and cleanup callbacks can run during discovery; track them separately from transpiler and patch-body counters.

There is a source-proven distinction in [2.3.6 AttributePatch](https://github.com/pardeike/Harmony/blob/v2.3.6.0/Harmony/Internal/PatchModels.cs). Its recognized role list excludes `InnerPrefix` and `InnerPostfix`; `Create` returns null before reading marker metadata if no role matches. A method using only that new-supported name and `[HarmonyInfix]` is therefore predicted to be ignored by 2.3.6. Execute the case and record omission separately from marker rejection. Both outcomes preserve the required invariant that old discovery must never turn an Infix into an outer patch. Active-state rejection remains mandatory regardless of declaration naming.

If the metadata/API slice chooses to guarantee an exception even for those names on pre-inner releases, the declaration would need an old-recognized role, for example a mandatory `[HarmonyPrefix]` or `[HarmonyPostfix]` attribute for those forms. The current marker cannot reject a method the old scanner never selects. This is an explicit API choice for that slice, not a new requirement or proposed compatibility mechanism in this test plan.

For C5, install the counter transpiler successfully first and record its count after new installation. Give it the earliest available ordinary ordering in one case, and exercise dependencies in another. The assertion is zero additional transpiler invocations during the old operation, including a counter at method entry and another during enumeration for iterator transpilers. Call the target afterward to prove retained behavior. An exception after a user transpiler has run is a failure even if installation was prevented.

## 5. Artifacts, backends, and runtime lanes

Pin the published `0Harmony.dll` assets for assembly versions 2.4.0.0, 2.4.1.0, 2.4.2.0, and 2.3.6.0. NuGet package versions omit the final `.0`, for example package `Lib.Harmony 2.4.2`. Obtain them from [NuGet](https://www.nuget.org/packages/Lib.Harmony/2.4.2) or their matching [official GitHub releases](https://github.com/pardeike/Harmony/releases). Store origin, package hash, assembly hash, target framework, actual identity, MVID, and referenced dependencies in a checked fixture manifest. Use published Fat assets for the first release tests; Thin packaging and its independent dependency resolution need a separate ordinary control before extending coverage.

| Lane | Assets and configuration | Purpose |
| --- | --- | --- |
| Focused net9/x64 | Published 2.4.2 net9 plus new net9; JSON. Disable major-version roll-forward and report actual child architecture/runtime. | C1 and the ordinary coexistence control first; C4-C6 and C8 as their implementation becomes available. |
| CoreCLR net8/x64 | Matching old/new net8 assets. Run separate children with `System.Runtime.Serialization.EnableUnsafeBinaryFormatterSerialization` explicitly false, then true. | JSON and BinaryFormatter coverage, including all four old releases. Check the selected backend in both engines before writing state. |
| Classic Mono and Windows .NET Framework | Compatible published framework assets, normally net472 or net48, plus the matching current build. Use existing supported test environments. | Native detours, BinaryFormatter remapping, old/new binding behavior, and Mono replacement mappings. Add net35 representation where V3's supported legacy surface requires it. |
| Release platform coverage | Relevant existing CoreCLR/Mono CI lanes and package/API checks. | Catch platform-specific emission/loading behavior without multiplying every declaration and selector case across every platform. |

Serializer availability depends on the loaded Harmony asset and the executing runtime. The [in-box BinaryFormatter implementation always throws on .NET 9](https://learn.microsoft.com/en-us/dotnet/core/compatibility/serialization/9.0/binaryformatter-removal). Do not enable a replacement formatter or roll a net8 BinaryFormatter case onto net9 and claim old-backend proof. Test an unavailable backend as explicit rejection, separately from successful decoding. Do not change one engine's cached backend field to construct an otherwise impossible mixed process.

No-Infix JSON must preserve the existing writer's exact bytes for the same ordinary records and metadata. Compare new output with the appropriate pre-change baseline, and test old readers against it. Do not require different historical `PatchInfo` shapes to produce identical bytes. Check no-Infix interchange with both engines selecting the same backend; an old legacy JSON/BinaryFormatter mismatch is an ordinary format mismatch, not evidence that the new envelope works.

## 6. Persistence and recovery cases

For C8, keep targets and patch fixture modules loaded once and unique. Register through A, obtain serialized state through its ordinary path, and let B inspect through its own path. Assert the returned `Patch`, `InnerMethod`, and nested objects use B's Harmony types. This exposes a missing BinaryFormatter binder mapping. Clear or verify absence of the resolved `MethodInfo` cache before reading `InnerMethod.Method`; a writer's cached method is not reconstruction evidence.

Generate selectors from a short independent table: closed type/closed method, type family/closed method, closed type/method family, and both families. Add a nongeneric control, nested generic declaring types with enclosing arguments, nested constructed arguments, `T[]`, `T[,]`, and rank-one `T[*]`. For each selector, provide a list of concrete call operands and an explicit expected subset. After both backend round-trips, compare declaring type, method definition/token/module, exact generic arguments, selector kind, positions, `__originalMethod`, and the selected sites. Use the same patch method across different constructions to catch stale binding plans. Pointer/byref-like call storage belongs to the V3 emission tests, not the generic-argument identity grammar.

For state, have a prefix write and multiple postfixes read at repeated sites and loop iterations; include exact and family registrations from one patch class. Add another class, including the same full name in a different assembly, to prove its state is separate. Cover inner state reset, outer-state bridging, and a rebuild by the second reader. Mutate the original input target/position arrays and a detached inspection result; installed behavior and future reads must keep the registered snapshot.

The negative identity table removes or corrupts one meaningful item per case: version, kind, required argument list, argument count, module/token, malformed canonical string, or free generic parameter. Permute JSON property order and insert unknown noncritical properties; reject duplicate or missing identity properties. Test unknown/truncated envelope versions and unsupported backend tags. Run these as decoder tests first, then representative failures through public registration/rebuild. No malformed case may widen a closed target into a family.

A separate duplicate-target-module case deliberately loads the same target DLL twice and supplies an otherwise valid identity. V3 requires explicit ambiguity failure when its MVID/token format cannot distinguish the copies. Do not use duplicated target modules in the positive coexistence setup or accept whichever copy enumeration finds first.

For C7, first find which invalid graphs an old public patch operation actually persists. Do not assume the incomplete old engine can install every graph its public fields can represent. Where it cannot, construct a `PatchInfo` using that released assembly's own types and serialize with its real serializer. Seed those bytes only in an explicitly named recovery fixture with a known previous detour. This proves reading/removal of an old-shaped record, not that a real user release produced it through `Patch()`.

Use two invalid owners and a valid ordinary survivor. Inspect without rebuilding, try an addition and a removal that leaves one invalid record, then remove all invalid records and retry the original addition. Failed operations must preserve stored bytes and the previous executable behavior; successful removal must preserve the valid survivor. Generate owner, method, inner-role, all-role, and class-unpatch variants across both inner roles. Include a surviving unambiguous nongeneric legacy target that normalizes successfully and a generic legacy target that must remain non-executable. Syntactically corrupt bytes stay errors.

## 7. Failure boundary and evidence

Before a candidate update, capture target-specific shared bytes, version count, public records, replacement mappings, and an execution trace. Fail once at each new predictable boundary: target/position validation, identity serialization, binding at a later selected site, and unsupported call emission. A failure at the second site must leave the first site uninstalled as well.

Require unchanged published bytes/version/mappings and the previous trace after failure. Then correct only the invalid request and retry in the same child: exactly one successful version advance, one new record, and the expected behavior. Do not assert rollback of user prepare/transpiler side effects; V3 excludes that. Keep the assertion per outer method, since multi-target classes retain normal job-by-job behavior. A native installation failure or process crash is a separate boundary from the predictable validation failures covered here.

Every child result should make the proof reproducible:

- Requested versus actual runtime host/version/architecture, framework asset, backend and switch; exact command, configuration, and loader policy.
- Fixture compile-time reference identity/hash; each loaded engine/fixture/dependency's identity, path, hash, MVID, and context/domain; the Harmony assembly actually used inside each typed entry point.
- Actual receiver/declaring assembly for reflected processor invocations; attribute type providers for C4; assembly-load and resolve events, including unexpected extra Harmony copies.
- Shared dictionary reference comparisons; target identity; before/after state hashes, header and decoded records through a capable reader; version and replacement mapping changes.
- Stage of failure, complete exception chain, loader exceptions where supplied, transpiler-entry/enumeration counters, patch-body counters, and target traces before/after/retry.

Classify results as expected success, expected loader/API boundary, expected compatibility rejection, ordinary-control failure, feature failure, or infrastructure unavailable. Expected rejection requires reaching its intended stage. `TypeLoadException` while constructing a new attribute is not proof that the old declaration marker worked. Zero counters are not proof if the child never loaded its fixture.

## 8. First implementation steps and release gate

For the current shared-binder slice, start with the existing ordinary conversion/write-back and reverse-patch tests, then C1 with a published-old fixture against the new net9/x64 engine. Add the child identity report and ordinary cross-engine control next. They expose false loading assumptions before the Infix lifecycle exists. Keep lexical binding cases generated inside the normal suite; do not launch a child for every parameter permutation.

When lifecycle work begins, add one supported C4 declaration, one C5 counter-transpiler rejection, C6 removal/retry, and C8 closed-generic reconstruction in JSON. Then add the BinaryFormatter lane and C7 recovery. Expand the small generated declaration/selector sets after these causal paths are sound.

Before release, all four pinned old releases need the required discovery, state, and opposite compile/runtime-binding checks on an applicable real runtime. Both backends need actual execution, as do representative supported CoreCLR, Mono, and .NET Framework loading paths. Run normal argument/patching/reverse-patch suites and package/API compatibility checks at that boundary. Report missing runtimes or failed ordinary controls as unresolved coverage; a build, decompiled method, or decoder-only success cannot stand in for executed mixed-engine behavior.
