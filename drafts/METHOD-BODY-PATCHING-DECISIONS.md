# Method-body patching: review decisions

Status: review record, written 2026-11-04 after the second Claude review of the uncommitted `v3` work.
Scope: the method-body patching extension (`MethodBodyPatching`, registered through `SetEligibility`, `SetBodySource` and `SetInstaller`), its internal plumbing and the Harmony Mod adapter.
Each point lists the finding, the alternatives, their trade-offs, the preferred alternative and the current status.
Points 1 to 8 need no maintainer decision. They were implemented on 2026-11-04 in the same working tree.
After that change the complete .NET 10 x64 suite passed: 1,279 executed, 0 failed, 2 ignored by their own conditions. The adapter uses only `SetProvider` and was not rebuilt.
Points 9 to 20 need a maintainer decision or stay deferred. The recommendation is marked.
The maintainer decided points 9, 11, 13 and 14 on 2026-11-04. Those decisions are implemented. See the status lines under each point and the sections at the end.

## Implemented without a decision

### 1. The patch lock changed identity when a provider registered

Finding: `PatchProcessor.locker` returned a per-copy object until a provider registered, then the shared protocol record.
A patch operation holding the old lock and one taking the new lock could run at the same time.

| Alternative | Advantages | Disadvantages |
| --- | --- | --- |
| A. Always lock the shared record | One lock. No switch. Separately loaded Harmony 3 copies serialize their patch operations, which they already share state for. | Harmony 3 copies can no longer patch in parallel with each other. Harmony 2 copies are unaffected. |
| B. Keep the per-copy lock and take the shared lock only inside provider calls | Copies patch in parallel. | Two locks with ordering rules. The provider path still needs cross-copy exclusion, so the complexity stays. |

Preferred: A. Status: implemented. `SyncRoot` and the per-copy lock object were removed.
Note: reverse patching never took the patch lock. That is unchanged and predates this work.

### 2. A failed first patch left its installer pinned

Finding: `Prepare` recorded the installer choice before construction. If construction failed, for example a throwing transpiler, the choice stayed.
A declined constructed generic method then blocked a later definition-wide patch of its family with a false conflict.

| Alternative | Advantages | Disadvantages |
| --- | --- | --- |
| A. Release the pin when the first attempt fails before installation | Construction still sees a stable choice. No phantom pins. Four lines. | `UpdateWrapper` tracks whether installation completed. |
| B. Pin only after installation | No release step. | Construction sites need the choice before installation, so a second provisional state is required anyway. |
| C. Ignore pins of targets without patch metadata | No change to the patch flow. | Reverse-patch stand-ins have no patch metadata, so their pins would be ignored too. |

Preferred: A. Status: implemented with a regression test.

### 3. Two ownership registries

Finding: the provider path recorded its choices in the shared per-target dictionary. The default path recorded ownership in the MonoMod detour dictionary and exposed it through a list of owner callbacks.
Both had to agree, and the owner callback scanned all detours for every open generic definition.

| Alternative | Advantages | Disadvantages |
| --- | --- | --- |
| A. The default path pins its choice in the same shared dictionary | One registry. The owner callbacks and their registration disappear. Reverse-patch stand-ins pin at installation. Separately loaded copies see each other's default installations. | Every patched method gets one dictionary entry. A definition installed natively before the provider registered now stays native silently instead of throwing. |
| B. Keep the owner callbacks | No change. | Two sources of truth. One linear scan per definition patch per copy. |
| C. One installer object with delegates per target, the default being one instance | Reads as a single abstraction. | The shared record must stay BCL-only, so the object cannot live in it. With one provider per process a boolean already identifies the installer. |

Preferred: A. Status: implemented. Slot 2 of the protocol record stays reserved and unused so the distributed prototype still validates the record.
The silent-default behavior for definitions matches the existing behavior for all other late registrations. If the maintainer prefers the old exception, it is one condition in `Prepare`.

### 4. The definition-wide policy was spelled out at five sites

Finding: "is generic and is a provider target" appeared in three construction sites and two callback helpers. It tied "provider" to "supports open generic definitions".

Alternatives: keep the five expressions, or ask one helper. Preferred: one helper, `MethodBodyPatching.IsDefinitionWide`. Status: implemented.

### 5. Provider slots were read through inline array casts

Alternatives: keep the casts, or add named accessors like the existing `Targets` accessor. Preferred: named accessors. Status: implemented.

### 6. Harmony holds its patch lock while the host installs

Finding: a host callback that waits for another thread using Harmony deadlocks. The adapter's own callbacks avoid this.
Alternatives: release the lock during installation, or document the rule. Releasing the lock would let another patch read a half-installed target.
Preferred: document. Status: implemented in the `SetProvider` remarks and the design draft.

### 7. Tests reached into private state

Finding: tests used reflection for the internal default installer, the shared record and the record validator. No test covered point 2.
Preferred: call internals directly through the existing `InternalsVisibleTo`, add an internal `ClearProvider` for test cleanup, make the validator internal. Status: implemented, plus the regression test for point 2 and a two-slot record case.

### 8. Formatting

`dotnet format` runs at the end of the C# edits. Status: done.

### 21. Harmony's defaults were inline fallbacks

Finding of the 2026-11-04 follow-up review: every body read and every installation already went through one funnel each, but Harmony's own reflection reader and detour sat inside those funnels as inline fallbacks. They never took the shape of a body source or an installer, so the two delegate shapes ran in production only when a host registered.
Preferred: keep two per-copy delegates of the same shapes and let both funnels always call a delegate, the registered one for host-owned targets and Harmony's own otherwise. The defaults never enter the shared record, so a host can still register its parts, and the per-copy detour stays per copy.
Status: decided and implemented on 2026-11-04. `MethodBodyPatching.Install` is the single installation entry. The registered installer runs for host-owned targets, and `Installer ?? default` also covers a host that registered eligibility without an installer. Reverse-patch stand-ins now install under the shared patch lock like every other target, which matches the documented lock order. The definition-wide capability still depends on a registered installer, so the three installer-presence checks remain.

## Needs a maintainer decision

### 9. The provider is all or nothing

Finding: a host cannot supply only a baseline reader and keep the default installer, or the reverse.

| Alternative | Advantages | Disadvantages |
| --- | --- | --- |
| A. Keep the three-delegate contract | Smallest API. Relaxing it later is backward compatible because null currently throws. | A second kind of host waits for a change. |
| B. Allow a null `readBody` or `install` to mean "use the default" | Three lines. Covers loaders that rewrite IL at load time and installers that do not change what reflection sees. | The host must keep source and installer consistent. A null `readBody` with a body-replacing installer reads patched IL as baseline. |
| C. Expose the default installer as a public method | Hosts can compose. | New public surface to keep forever. |

Recommendation: A for 3.0. Switch to B when a second host needs it. It is a compatible change.
Maintainer answer, 2026-11-04: one call with several arguments locks the public API. Discuss single-argument calls, with null for the default. See the API shape proposal below.
Decision, 2026-11-04: split. Status: implemented. `SetProvider` is gone. The three calls are described below.

### 10. Reverse-patch stand-ins never reach the provider

Finding: `ReversePatch` installs through the default detour without asking the provider. A host without native detours cannot reverse-patch.

| Alternative | Advantages | Disadvantages |
| --- | --- | --- |
| A. Keep stand-ins on the default detour | Current tested behavior. The Waylith adapter excludes mod assemblies anyway. | A detour-free host has no reverse patching. |
| B. Prepare the stand-in through the provider | Uniform. Cheap. | Changes behavior only for hosts that accept mod methods. The existing late-provider test encodes A. |

Recommendation: A for 3.0. Revisit with the first detour-free host.
Maintainer answer, 2026-11-04: A.

### 11. No batch boundary

What one `Patch()` costs, in both paths:

1. Harmony reads the baseline and builds the combined body. Same work in both paths.
2. Harmony makes the callable snapshot. Default path: this is the body that runs. Provider path: a DynamicMethod that compiles only if someone invokes it, or a small assembly for an open generic definition.
3. Installation. Default path: MonoMod compiles the snapshot and writes a jump at the start of the original. Provider path: the adapter encodes the body with runtime tokens, asks the runtime to rebuild the pipeline for that one method, every registered stage runs once, the runtime publishes the new body, and the adapter compiles it at once so bad IL fails inside `Patch()` instead of at the first call.

Both paths compile once per `Patch()`. The provider path adds the encoding, the token lookups, the pipeline run and the publication.
Measured on one trivial method, uninstrumented, after the preliminary compilation was removed (design draft, historical checks): 114 microseconds default, 325 microseconds direct. About 0.2 milliseconds more per `Patch()`.
A batch could save only the per-call fixed cost of the pipeline run and the publication, because the runtime can rebuild many methods in one transaction. It cannot save the compile per method or Harmony's construction. The saving is therefore at most 0.2 milliseconds per `Patch()`.
Scale: the 20-mod RimWorld run had 1,477 provider targets. If every target were patched once, the whole extra cost is about 0.3 seconds. A target patched by several classes rebuilds once per class.

| Alternative | Advantages | Disadvantages |
| --- | --- | --- |
| A. No batching | No API. | Startup pays about 0.2 milliseconds per `Patch()`. |
| B. Optional begin/end signal, with `PatchAll` as a natural automatic boundary | Hosts coalesce the fixed cost. | New API and a new rule: patches inside a batch become active at its end. |
| C. Host-side deferral to the next frame | No Harmony change. | Breaks the promise that a patch is active when `Patch()` returns. |

Recommendation: A for 3.0. Revisit only with a startup measurement that shows the extra cost matters.
Decision, 2026-11-04: A. No batch API in 3.0.

### 12. No zero-owner signal on unpatch

Finding: after the last patch is removed the host keeps a stage with a re-encoded copy of the original.
Alternatives: no signal, or a marker on install. The install signature is fixed, so a marker needs an overload or a convention.
Recommendation: defer. Low value, and the stage is correct.
Maintainer answer, 2026-11-04: defer.

### 13. Export runs after the snapshot generator rewrote the same definition

In plain words: for a definition-wide patch Harmony writes a small assembly as the callable snapshot, because a DynamicMethod cannot be generic.
An assembly cannot reference a DynamicMethod callback directly. So before writing it, Harmony replaces each such callback reference with a small generated stand-in method, a proxy, in another generated assembly.
It does that in place, in the same instruction list that the exporter reads afterwards. The host therefore receives references to the proxies instead of to the real callbacks.
Not a defect. The runtime accepts DynamicMethod references directly, so the proxies are an unneeded indirection for the host, and each rebuild keeps one more proxy assembly alive.

| Alternative | Advantages | Disadvantages |
| --- | --- | --- |
| A. Keep | Works. | Hidden ordering rule. One retained proxy assembly per rebuild. |
| B. Write no snapshot assembly for provider targets (item 14, option B) | Nothing is swapped. The export sees the real callbacks, exactly as for non-generic targets. No retained assemblies. | Changes what `Patch()` returns for open generic definitions. |
| C. Put the references back after the assembly is written | Three lines. The export sees the real callbacks. | The proxies and their assemblies still exist for the snapshot. |

Recommendation: B. It removes the problem instead of working around it. C if the snapshot must stay.
Maintainer answer, 2026-11-04: the earlier description was confusing. Rewritten above.
Decision, 2026-11-04: B, through item 14. Status: implemented. Nothing is swapped before the export any more. The proxy path remains only for generic structured helpers and for the test snapshots.

### 14. The snapshot is always generated

Finding: `Patch()` returns a callable copy even when the host installs the bytes into the original. The game never executes that copy.
Harmony uses the returned method to map a replacement back to its original, for `Harmony.GetOriginalMethod` and stack traces. The stock tests use it to call patched generic bodies.
Cost: a non-generic target gets a DynamicMethod that is cheap and compiles only when invoked. An open generic definition gets a whole assembly per rebuild, kept for the process lifetime, plus the proxies of item 13.

| Alternative | Advantages | Disadvantages |
| --- | --- | --- |
| A. Keep | No contract change. | Assembly growth on every rebuild of a definition-wide target. Item 13 stays. |
| B. Definition-wide provider targets return the definition itself and write no snapshot | Removes the assembly growth and item 13. The returned method still runs the patched code. Nobody can invoke an open definition without closing it, so callers lose nothing they can do today. | The stock generic tests call the generator directly instead of `Patch()`. A documented exception in the return contract. |
| C. A `Patch()` overload, or an `Apply()`, that returns nothing and skips the snapshot for provider targets, used by `PatchAll` | The current return contract stays exactly. | New public API. Manual `harmony.Patch(...)` callers keep paying until they switch. |
| D. Every provider target returns the original, constructors get a small forwarder | No snapshot at all. One rule. | Larger contract change. Needs the forwarder for constructors. |

Recommendation: B now. D later if the one rule is wanted. C only if the return contract must stay byte for byte.
Maintainer answer, 2026-11-04: asked whether a `Patch()` overload without the return value is possible. Yes, that is C above. Nobody is known to use the return value.
Decision, 2026-11-04: B. Status: implemented.
A definition-wide target gets no callable copy. `Patch()` returns the open definition itself, or null for a constructor of an open generic type, which cannot be returned as a `MethodInfo`.
The installer receives null as the replacement for such a target, so a detour-capable host cannot detour the definition onto itself by mistake.
Self references inside such a body resolve to the original definition. Nothing is written into the shared original/replacement map for it.
The stock generic tests keep executing the patched generic bodies. They capture the constructed body through an internal test observer and generate the callable copy themselves.

### 15. Linear scans for open generic definitions

Finding: `Prepare` scans all pinned targets when a definition is pinned. Bounded to definitions.
Alternatives: keep, or index targets by definition token. Recommendation: keep.

### 16. Private Cecil field

Finding: `GenericMethodContext` reads and writes Cecil's `reflection_importer` by reflection. A rename fails loudly in `Create`.
Alternatives: keep, or add an importer hook to the local MonoMod fork and upstream it.
Recommendation: propose the upstream change. Keep the reflection until it lands.
Maintainer answer, 2026-11-04: do a minimal MonoMod PR with the motivation. Done: https://github.com/MonoMod/MonoMod/pull/349 adds a `DynamicMethodDefinition` constructor overload that takes an `IReflectionImporterProvider`, with a unit test. Base `reorganize`, fork branch `feature/dmd-importer-provider`. Harmony keeps the reflection until a MonoMod release ships it.

### 17. Offline weaving

Finding: the exporter writes complete bodies against any token importer. A host could weave patched bodies into an assembly on disk.
Blocker: targets must be loaded runtime methods and callbacks are runtime objects. Recommendation: keep in mind when freezing the API. No action.

### 18. No stock test for two Harmony copies sharing one provider

Finding: only the runtime probe covers it. A stock test needs a second Harmony copy loaded in the test process.
Recommendation: add it before release if the compatibility fixtures already load a second copy.
Maintainer answer, 2026-11-04: ignore for now.

### 19. A host subclasses three BCL abstract classes to return a baseline

Accepted price of a contract without Harmony types. The adapter does it in about 60 lines.

### 20. The exporter protocol is untyped

The nested install delegate and the `KeyValuePair<Type, bool>` convention are the price of BCL-only sharing.
The remarks on `SetProvider` and the API table in the design draft are the specification. Keep both exact.

## API shape (item 9)

The maintainer chose single-argument calls over one call with three arguments. Implemented on 2026-11-04:

```csharp
public static class MethodBodyPatching
{
	public static void SetEligibility(Func<MethodBase, bool> accepts);
	public static void SetBodySource(Func<MethodBase, MethodBody> readBody);
	public static void SetInstaller(Action<MethodBase, MethodInfo, Func<Func<object, int>, byte[]>> install);
}
```

Rules:

- Each call takes one delegate. A part accepts one delegate; a second non-null registration of the same part throws. Null keeps Harmony's default for that part and registers nothing.
- Register during host initialization, before the first patch operation. This is a documented expectation, as before. Targets pinned earlier keep their installer, which the existing late-provider tests cover.
- `SetEligibility` decides which targets the host owns. Without it the other two calls have no effect.
- Without `SetBodySource`, owned targets read their baseline by reflection. Without `SetInstaller`, owned targets install through the default detour.
- Definition-wide generic installation needs an installer. Without one, generic targets keep the Harmony 2 detour behavior: no definition-wide replacement, no definition/constructed conflict rule, and constructed views do not read the definition's baseline.
- The shared record keeps its layout. Slot 0 holds the three delegates, null for a default.

Trade-offs: a future capability is a new call and never an overload. A host supplies only what it owns.
Tests: a body source without an installer, an installer without a body source, and the once-per-part rule each have a stock test.

## Runtime author feedback, 2026-11-04

Sam reviewed the private 3.0.1.2 prototype and raised three points. All three are addressed in the adapter worktree and covered by the runtime probes.

1. Spurious error log when a lower stage replaces the body. Field injection makes this expected. Now: a detach caused by a changed input is reported only if nobody regenerates the stage within one second. Other stage failures are reported at once.
2. `Injection.HarmonyRegenerate` was not set, so Waylith kept calling `PatchFunctions.UpdateWrapper` by reflection. Now: the adapter sets the delegate at registration and chains any previous value. For a provider target it rebuilds Harmony's stage from the current default body and returns true. For other methods it returns false, so Waylith's stale-method handlers keep serving detour-based Harmony copies. `RuntimeILProvider.Rebuild` forces the same rebuild.
3. The transformer accepted only `MethodInfo`, so constructors used the detour path. Now: instance constructors are eligible. Type initializers keep the detour path until the runtime contract covers them. A stock Harmony test and the bridge probe cover a directly installed constructor.

Asked and answered on 2026-11-04: `ReplaceMethodBody` supports type initializers, and PrepatcherCompat can skip its warning about patches built from original IL for provider targets. Sam added that replacing a type initializer's body does not run it, unlike the detour path.
Now: type initializers are eligible in the adapter. The bridge probe patches one before its type is initialized, checks that patching did not run it, and checks the postfix when it runs.

## Generic instantiation families, 2026-11-05

Sam asked why a patch on a closed instantiation such as `Box<string>.Read` does not install into the definition under a `typeof(T) == typeof(string)` guard. The mechanism was confirmed on 4995 on 2026-11-02 and deferred: the design draft said that no automatic branch composition is added, and constructed targets kept the detour.

The maintainer's criteria: MonoMod will not support generic bodies, Waylith does, mod authors accept adapting their patch code to clear rules, nothing public may be added that cannot be kept, and substantial work is acceptable if it stays contained.

| Alternative | Advantages | Disadvantages |
| --- | --- | --- |
| A. Keep the detour for instantiations | Nothing new | MonoMod stays in the loop on Waylith. Patched instantiations miss callers that were already inlined. Two semantics depending on how a target is named. |
| B. Per-instantiation body copies with guards, transpilers included | Exact behaviour for every patch kind | Duplicated IL per instantiation, transpiler context changes, broad changes to body construction. |
| C. Definition family with guarded callbacks | No public API. Exact behaviour for prefix, postfix and finalizer. Existing instantiation patches keep their signatures. Transpiler and Infix on instantiations rejected until added, which is purely additive. | A transpiler or Infix patch on an instantiation errors on a body-replacing runtime. |

Decision: C, callbacks first. Implemented on 2026-11-05.

- `MethodBodyPatching.Accept` maps a closed instantiation to its definition when the host accepts the definition, and pins both. `FamilyOwner` answers the mapping. `Release` drops a definition pinned only for a failed first member. `ProviderBodyOwner` returns the definition for installed instantiations too, so reverse snapshots see the definition's transpilers.
- `PatchFunctions.UpdateWrapper` rebuilds the definition from the union of its own patch record and the records of every installed instantiation. `PatchSorter` runs with `preserveOccurrences` so one callback registered on two instantiations keeps both occurrences. A transpiler or Infix on an instantiation throws `NotSupportedException` before installation.
- `PatchCall.instantiation` tags a callback with its instantiation. `MethodCreator` emits one `GenericFamily.Is<TParameter, TArgument>()` check per type parameter around a tagged prefix, postfix or finalizer. `MethodCreatorTools` validates against the closed types and bridges by-value bindings with `GenericFamily.As<TOpen, TClosed>()`: instance, arguments, fields, results and passthrough results. By-reference bindings pass unchanged. `__originalMethod` is the instantiation.
- Both helpers are internal generic methods bound over the definition's own type parameters, the same import shape the definition-wide callbacks already prove on 4998.
- The adapter asks about the definition for a closed instantiation and compiles every patched instantiation as a validation witness after each family rebuild.

Semantics differ by runtime and are documented: the detour path keeps Harmony 2's sharing across reference-type instantiations, the runtime path is exact. A mod that relied on the sharing loses it on Waylith.

Also on 2026-11-05: the adapter no longer declines custom modifiers or function pointer types in a target signature. The installed body keeps the signature. The probe found that Harmony's callable copy failed for function pointer signatures on the stock path too, because MonoMod imports such a type as a nameless reference. The copy now takes its signature from the original's reflection types, and a nameless local is encoded as a native int.

Not done: transpilers and Infix on instantiations, and the removal of the detour engine from the 1.7 mod folder. Both are recorded as the next steps.

## Detour-free 1.7 folder, 2026-11-05

Goal: ship the RimWorld 1.7 folder of Harmony Mod without MonoMod.Core, the native detour engine. MonoMod.Utils and Mono.Cecil stay: they build IL and never hook the runtime.

| Layer | Use in Harmony | Decision |
| --- | --- | --- |
| MonoMod.Core | the default detour in `PatchTools`, `AccessTools.Identifiable`, the .NET 11 compile watch | Isolated. Loads only when a detour installs. |
| MonoMod.Utils and Mono.Cecil | every patch body, generic context, Infix helper assemblies, inline signatures | Kept. A rewrite would buy nothing Sam asked for. |

Core changes:

- `DetourEngine.Available` probes MonoMod.Core once through a non-inlined method and catches the load failure. `AccessTools.Identifiable` returns the method itself without the engine. The compile watch subscribes only with the engine.
- `PatchTools` keeps its detours in a dictionary typed with `IDisposable`, so the type loads without MonoMod.Core. The detour body compiles only when called.
- `PatchFunctions.ReversePatch` prepares the stand-in through `MethodBodyPatching`, so a host that owns it installs the reverse body. A failed first attempt releases the pin. Without a provider the stand-in keeps the detour, as before.

Adapter: a strict mode. With `strict`, the eligibility check is the mod's own include predicate, and `ReadBody` and `Install` refuse an unsupported target with `NotSupportedException` naming the reason: collectible assembly, abstract, varargs, synchronized, no IL body, partially open generic, or locals the reader cannot express. `RuntimeILProvider` turns strict on when Harmony neither embeds MonoMod.Core nor has `MonoMod.Core.dll` beside the adapter.

Packaging: the mod compiles against the local Fat package as before. `stage-harmony.py --thin-dir` stages the Thin `0Harmony.dll` and four managed dependencies (MonoMod.Utils, Mono.Cecil, MonoMod.Backports, MonoMod.ILHelpers) into `1.7/Assemblies`, records each hash and `detourEngine: absent` in the provenance, and removes any detour engine file. The payload allow list reads the dependencies from the provenance. The plain .NET 10 smoke has no runtime body replacement, so it borrows MonoMod.Core and MonoMod.Iced from the thin build output into its own directory. The installed-runtime smoke runs the payload as shipped.

Consequences accepted: on 1.7 a mod that patches one of the rare declined kinds gets an error with a reason instead of a silent detour. Older game folders keep the merged build. Stock Harmony packages are unchanged.

Mono.Cecil.Rocks was shipped in the first attempt and then removed: the assembly reference tables of the shipped set show nothing referencing it. Harmony's Cecil.Rocks type forwarders compile only for .NET Framework and netstandard. MonoMod.ILHelpers stays: MonoMod.Utils and MonoMod.Backports reference it.

Verified with a new `nodetour` probe mode on 1.7.4998: the thin Harmony resolved only Mono.Cecil, MonoMod.Utils and MonoMod.Backports, asked for MonoMod.Core once and continued without it, patched an ordinary method, a constructor, a generic definition and a guarded instantiation, installed a reverse patch stand-in through the runtime, ran a structured Infix helper, refused a synchronized method, a collectible target and a partially open generic method with reasons, unpatched, and never loaded MonoMod.Core.

## Adversarial review corrections, 2026-11-05

Codex's adversarial review (archived as `artifacts/drafts-archive-20261106/METHOD-BODY-PATCHING-REVIEW.md`) reproduced seven defects. Decisions per finding:

- Local initialization (R1): the main patch body gets `InitLocals` when `CreateDynamicMethod` creates it, not in the callable-copy generator. Structured helpers keep their own convention in `MethodBodyGenerator`.
- Reverse stand-in rebuild (R2): Harmony keeps the reverse recipe per copy, not in the shared record, which stays BCL-only with three slots. A stand-in installed by another copy is refused with `NotSupportedException` instead of being rebuilt as an empty patch. `MethodBodyPatching.Rebuild` is the one public entry for a host regeneration: patched target or stand-in, through the pinned installer, under the patch lock.
- MonoMod.Core field (R3): an explicit `Delegate.CreateDelegate` in the deferred subscribe method. MonoMod.Core is not added back. A lambda would only move the cached field into a nested class the game's scan also visits.
- Failed forced rebuild (R4): the adapter reinstalls the previous generation only when the pipeline accepts it against the unchanged default body. A changed body stays detached with the pipeline's fault, and a failed restore marks the target broken. Stale bytes are never reinstalled blindly.
- Pending witness (R5): the installer contract is unchanged. The host reads the family through `MethodBodyPatching.Instantiations(definition)`, which includes the member pinned for the current installation. Passing the instantiation to the installer would change what the installer's first argument means.
- State per target (R6): `__state` is keyed by patch class and registration target. A definition patch and an instantiation patch of the same class do not share state.
- Native-family sibling (R7): a closed sibling requested after a detour-held instantiation joins the detour, as in Harmony 2. Only the definition request fails, because definition-wide installation needs the installer. Failing the sibling too would turn a working Harmony 2 registration into an error.
- Found on the way: a generic callback definition registered on a closed instantiation binds to the instantiation's type arguments, in the family and detour paths. Before, it failed with a binding error.

Transpilers and Infix on closed instantiations: the 27-mod run hit GUILocator's transpiler on `Widgets.TextFieldNumeric<int>`. Options put to Andreas on 2026-11-05: accept and adapt the mod, implement guarded closed-body helpers, or decide after Sam's next build. Resolved the same day, see below.

## Closed bodies for instantiation transpilers, 2026-11-05

Sam, asked whether mods with transpilers on closed instantiations must change for 1.7: he is strongly averse to breaking compatibility this close to 1.7 going public, and named two options. Guards that force an instantiation through a helper method, or transpilers and Infix on an instantiation implicitly targeting the open definition. Andreas: keep compatibility.

Decision: the helper. Implicit definition targeting changes what a transpiler sees (open IL, `List<T>` instead of `List<int>`), applies one transpiler twice when a mod patches two instantiations, and cannot express an Infix target that only exists in one instantiation. The helper keeps the detour semantics exactly.

- An instantiation with a transpiler or an Infix patch gets a closed body, built by the ordinary single-target path (`PatchFunctions.ClosedBody`) from the definition's patches and its own, as a callable helper. The family body enters it at the start under the existing `GenericFamily.Is` guard, bridging by-value arguments and the result with `GenericFamily.As` (`MethodCreator.EmitClosedBody`).
- Instantiations without transpilers or Infix keep the guarded callbacks in the family body. Two mechanisms, chosen per instantiation by its patch kinds, instead of moving every instantiation into a helper and duplicating the original IL per instantiation.
- The helper carries the definition's callbacks too, bound to the instantiation, so a definition patch still applies to a transpiled instantiation. The definition's own transpilers run before the instantiation's, in priority order.
- Persistent Infix state of a helper registers under its instantiation, through the same hooks as an ordinary target.
- `MethodBodyPatching.GetPatchInfoForBody` merges the definition's and the instantiation's patch records, so IL inspection and reverse snapshots of an instantiation see the body the helper has.
- The installer contract is unchanged. The helper is a DynamicMethod operand of the exported body, imported like a structured Infix helper. The host already owns those tokens and their lifetime.
- Not done: no attempt to share one helper between instantiations with identical closed IL, and no helper for a partially open target, which the provider still declines.


## Merged build restored, 2026-11-05

Sam read the 3.0.1.7 thin folder and found actual breaks. The 1.7 line needs the merged `0Harmony.dll`.

Evidence:

- `RimWorldWaylith.PrepatcherCompat.dll`, shipped with the game, references 55 `Mono.Cecil` types with resolution scope `0Harmony` (assembly reference `0Harmony 0.11.6.0`, which is Mono.Cecil's own version). Its free-patch executor hands a `Mono.Cecil.ModuleDefinition` from 0Harmony to `[FreePatch]` methods, and `FreePatchRunner.HasPatcherShape` rejects that parameter type from any other assembly. Sam reports several mods depend on this.
- The merged build internalizes every Cecil, MonoMod.Utils and MonoMod.Core type (ILRepack `Internalize="true"`; zero public types in those namespaces in rc.3 and in `.20261105.6`). The game binds them anyway because `RimWorld.runtimeconfig.json` sets `WaylithClr.SkipAccessChecks: true`. On 1.7 the Cecil types inside 0Harmony are therefore a de-facto public contract, and so is their version.
- `HarmonyReapply` in the same assembly reads `HarmonyLib.PatchTools.detours`, `HarmonySharedState.GetPatchInfo(MethodBase)` and `PatchFunctions.UpdateWrapper(MethodBase, PatchInfo)` by reflection. All three still exist with those shapes.
- Why 3.0.1.7 booted: the Cecil references live only in the free-patch executor. `FreePatchRunner.Run` enters it only after it finds a `[FreePatch]` or `[FreePatchAll]` method in a mod assembly, and the runtime resolves a type reference when it compiles the method that uses it. None of the 22 mods in the all-mod fixture (197 assemblies scanned) references Cecil through 0Harmony or any Prepatcher assembly, so the executor was never compiled. With a free-patch mod present, the thin build would have thrown a `TypeLoadException` on `[0Harmony]Mono.Cecil.*` inside `Run`'s try block and logged the free patches as not applied.

Decision: the 1.7 folder ships the merged `Lib.Harmony` asset again, as 3.0.1.0 through 3.0.1.3 did.
Reverted: `DetourEngine` and the deferred `Identifiable` and compile-watch call sites in the core (with them R3's explicit delegate construction), the adapter's strict mode and its auto-detection, the `nodetour` probe mode and `HARMONY_DEPENDENCY_DIR`, the thin staging (`--thin-dir`, `HARMONY_THIN_DIR`, the provenance keys `detourEngine` and `dependencies`, the smoke's borrowed engine).
Kept: `PatchFunctions.ReversePatch` prepares the stand-in through `MethodBodyPatching`. That is provider behaviour, not thin packaging.

### Use MonoMod smarter, or keep distance?

| Option | What it takes | What it gives on 1.7 | Verdict |
| --- | --- | --- | --- |
| A. A MonoMod.Core backend on `MethodBodyUpdater` (Sam's colleague's plan) | `DetourFactory.Current` has no setter and always wraps the `PlatformTriple` factory. `PlatformTriple.SetPlatformTriple` swaps system, architecture and runtime, but a detour stays native bytes written at the entry point. So this is a fork of MonoMod.Core, merged into 0Harmony for one game, which is exactly what blocked the colleague: there was no `MonoMod.Core.dll` to swap. | Body replacement only for targets the runtime accepts, which the provider already body-replaces. A detour maps one closed method to one replacement: no definition-wide generic bodies, no closed bodies for instantiations, no reverse stand-ins through the runtime. | No. Sam: "we have this setup like this now, which is achieving the same thing". |
| B. MonoMod.Utils inside the adapter (`DynamicMethodDefinition`, a Cecil round trip) | Replace the exporter's bytes-and-tokens path with a Cecil body and re-encode it. | Nothing new. `MethodBodyUpdater` takes IL bytes plus module tokens, which is what Harmony's exporter already emits. | No. |
| C. Distance: the provider owns Waylith, MonoMod stays merged | Nothing beyond today's code. | Every accepted target installs through the runtime. MonoMod.Core remains the installer for the kinds the runtime declines (collectible assemblies, synchronized methods, varargs, function pointer locals, partially open generics) and for every other host. Mono.Cecil and MonoMod.Utils keep building bodies, generic contexts and Infix helpers. The merged assembly keeps the binary contract PrepatcherCompat and mods bind to. | Yes. |

What MonoMod.Core still does on 1.7 under C: the fallback detour above and `AccessTools.Identifiable`. The .NET 11 compile watch is inert on .NET 10.

Risks to carry: the Cecil version inside 0Harmony is part of the 1.7 contract. A Mono.Cecil bump, an ILRepack rename, or dropping `Internalize` changes what PrepatcherCompat sees. Keep Mono.Cecil 0.11.6 for the 1.7 line and treat `PatchTools.detours`, `HarmonySharedState.GetPatchInfo` and `PatchFunctions.UpdateWrapper` as externally used.

## Compatibility review for the 1.7 release, 2026-11-05

Sam wrote that he is "very strongly averse to breaking any compatibility" because 1.7 goes public the following week, and offered to run the build against his 1500-mod list. This review looks for mod patterns that behave differently under the provider than under the detour that 3.0.0 used on 1.7, before that run.

Sources: the DM thread with Sam, a static scan of the local mods (20 fixture mods, the 60 workshop mods that ship 1.7 assemblies, 94 workshop mods with 1.6 assemblies only; `artifacts/runtime-patching-20261102/compat-scan-20261105/`), a source scan of 62 RimWorld mod repositories from GitHub, the `compat` and `compat-detour` probe modes on 1.7.5000, and the decompiled PrepatcherCompat and Waylith.Injection of builds 4998 and 5000.

| Pattern | Detour on 1.7 (3.0.0) | Provider | Who does it | Action |
|---|---|---|---|---|
| Patch an open generic definition | throws `ArgumentException: Cannot prepare generic method definition` | installs for every instantiation; constrained definitions threw `NotSupportedException` until today | Zombieland `ParseHelper.FromString<T>`, Vehicle Map Framework `ReservedBy<TDriver>` | adapter uses `PrepareGenericVariants` (build 5000) and no longer requires witnesses |
| Patch a closed reference-type instantiation | no effect at all (`Open<string>` stays unpatched) | patches exactly that instantiation under a type guard | Multiplayer `Gen.HashCombine<Map>`, Replace Stuff `<JobDriver_TakeToBed>`, Smart Medicine `DrawElementStack<AnonymousStackElement>`, Human Resources `GetAllThingsRecursively<Thing>`, RimThreaded `<object,object>` catch-all (dead mod) | none; the detour never delivered the canon-shared behavior Sam described, so nothing depends on it on 1.7 |
| Patch a closed value-type instantiation | works | works, same instantiation only | Multiplayer `Scribe_Values.Look<double>`, `TextFieldNumeric<int>`, GUILocator | none |
| Transpiler or Infix on an instantiation | works per instantiation | closed body per instantiation since 3.0.1.7 | GUILocator, Multiplayer `TextFieldNumeric<int>` (three TargetMethods), `ResolveParseNow<int>` | done; closed-body frames now map to the instantiation (`HarmonySharedState.RegisterReplacement`) |
| Free patch, then a Harmony patch on the same method | Harmony read the live IL, free patch kept | 4998: direct write superseded by the first rebuild (77 → 102 → 2); 5000: `FreePatchStage` at priority -2000 feeds Harmony's stage (77 → 177 → 77) | Performance Fish, Multiplayer loader, Vanilla Skills Expanded | none for 5000; the direct-write loss is recorded for anything that still writes bodies outside the pipeline |
| Field injection on a Harmony-patched method | `HarmonyReapply` reflection into `PatchTools.detours` and `UpdateWrapper` | `InjectionStage` at -1000; `Injection.HarmonyRegenerate` runs our rebase first, so Ludeon's reflection handler and its warning never fire for provider targets | VEF MVCF, Pawnmorpher, Performance Fish `[PrepatcherField]` | none |
| Mono.Cecil or MonoMod.Utils through a publicized 0Harmony | binds | binds, merged build restored | VSE (free patches, Cecil types via 0Harmony), Multiplayer loader, VEF, Combat Extended, Prison Labor, VPE, Fishery | keep Mono.Cecil 0.11.6 and MonoMod.Utils merged |
| Reflection into Harmony internals by name | | | none of the 174 local mods, none of the 62 repositories; only PrepatcherCompat | none |
| Stack-frame to original mapping | replacement frames mapped | original frames are the method itself; helper frames registered | Dubs Performance Analyzer, Visual Exceptions, IsoRim, GUILocator | core change in `.20261105.8` |

Escape hatch: the wrapper skips the provider with `-harmony-detours` or `HARMONY_DETOURS=1` and reports that in the status line. The 85-entry workshop run started both ways: the only startup errors, NoPauseChallenge's transpiler not finding `TexButton.SpeedButtonTextures` and `Prefs.DevMode` in `TimeControls.DoTimeControlsGUI` after another mod's transpiler, are identical with and without the provider.

Residual risks: a definition-wide body compiles every known instantiation at patch time, which costs more than one detour; an instantiation nobody has seen yet compiles on first call, so invalid transpiler IL for it fails there instead of in `Patch()`; `__originalMethod` in a definition-wide body is the definition. Sam's 1500-mod run remains the gate.

## Second and third host, 2026-11-06

Question: the types are neutral, but is the shape of the conversation between Harmony and the host Waylith's shape? Four leaks were named and all four were worked.

### 22. Exported bodies referenced DynamicMethods directly

Finding: closed-body helpers, Infix structured helpers and transpiler-emitted callbacks are DynamicMethods. Only Waylith can mint a module token for one. The export path handed the raw DynamicMethod to the importer, while the assembly path already substituted `DynamicMethodProxy`.
Decision: the importer returns zero for a value it cannot import. Harmony substitutes a stand-in for a static method without open generic parameters and imports again, with one stand-in per callback in a `ConditionalWeakTable`. Any other refusal throws `NotSupportedException` before installation, which releases the pin. Status: implemented in `DynamicMethodBodyWriter.Import`. The Waylith adapter keeps importing DynamicMethods directly and does not change.

### 23. One host was no evidence

Finding: Harmony's own reader and detour have the delegate shapes, but the detour ignores the body writer, so the exporter had exactly one consumer.
Decision: two hosts in the stock suite. The first attempt, a `DynamicILInfo` host, failed with `TypeLoadException: value type mismatch`: a dynamic scope resolves tokens inside signature blobs against its owner module, so a dynamic scope is not a host of this class. The module host writes a fresh module with `System.Reflection.Metadata` instead. `MethodBuilder.SetMethodBody` would have been shorter, but .NET Core removed it.
The hot reload host applies an EnC delta into the original module. A spike under `artifacts/enc-host-20261106` learned the delta rules against the runtime, with a Roslyn `EmitDifference` delta as the reference: a body at IL delta offset zero is rejected, the MVID must be re-added, heap offsets and row numbers accumulate across generations the way Roslyn's `DeltaMetadataWriter` accounts them, and the same method accepts repeated updates. Reflection then returns the installed body, so the hot reload host reads its baseline from the image on disk, exactly the price the Waylith adapter pays with the pipeline's default body.

### 24. What the hot reload host showed about the contract

A definition-wide body installed through hot reload reached a warm instantiation and a cold one, a closed instantiation installed through its definition, and unpatching restored the baseline through another generation. No detour exists for a hot reloaded target. The contract held without changes beyond item 22.
Harmony's stand-ins live in collectible assemblies. The hot reload host showed that a non-collectible module cannot bind one: the first call failed with `NotSupportedException: A non-collectible assembly may not reference a collectible assembly`, after a `Resolving` handler had found the assembly by name.
Decision: the escalation stays inside the refusal protocol. The host refuses the collectible stand-in by checking `Assembly.IsCollectible`, and Harmony imports a second stand-in loaded without a collectible context, which lives for the process. `GeneratedAssemblyLoader.Load` and `DynamicMethodProxy.Create` take a `collectible` flag for that. Status: implemented and covered by the hot reload fixture.
