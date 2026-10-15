# Issue 783: executable method generation for 3.0.0-rc.3

## Decision and evidence

[Issue 783](https://github.com/pardeike/Harmony/issues/783) exposes a patch-installation regression introduced during v3's Infix work. The detour mechanism is unchanged. The regression is in converting the finished patch body into an executable method: every body containing exception handlers, including ordinary patches and finalizers, is emitted as an assembly. Dynamic callbacks then require additional proxy assemblies. Resolving those assemblies scans the growing process assembly list and repeatedly constructs normalized assembly names.

Local controlled measurements against published 2.4.2 and rc.2 reproduce the mechanism without Infixes. With 1,000 unrelated assemblies, 100 ordinary catch-body patches added no assemblies in 2.4.2 and 100 in rc.2; dynamic callbacks increased rc.2's count to 200. A 600-patch run added 1,200 assemblies and became slower as it progressed. Caching normalized names reduced that cost but left all 1,200 assemblies. These measurements establish the regression; they do not independently reproduce the issue reporter's complete RimWorld workload.

The assembly path was introduced for correctness: v3 preserves the actual exception table instead of reconstructing it through structured `ILGenerator` calls. Returning `calli` instructions also need correct maximum-stack accounting. Reverting these fixes would lose functionality. CoreCLR's public `DynamicILInfo` API can accept the exact IL, local signature and exception table while referencing actual runtime members, including dynamic methods. A feasibility probe has executed multiple catches, dynamic callbacks and two identically named assemblies through this mechanism without creating replacement assemblies.

## Principles

1. Keep one patch-construction pipeline for ordinary patches and Infixes. Patch features determine the body, not the executable backend.
2. Separate three concrete responsibilities: construct a correct Cecil body; materialize that body for the runtime; load an assembly only when that representation is required.
3. Prefer direct runtime method tokens and a `DynamicMethod`. Avoid assembly discovery, proxy calls and assembly loading on the ordinary CoreCLR path.
4. Preserve semantics before optimizing: exact exception ranges, stack depth, signatures, runtime identity, initialization, access, patch ordering, failure atomicity and lifetime are requirements.
5. Reuse Cecil, MonoMod and runtime APIs. Add only the missing body writer and its selection policy. Do not add a general backend framework, a second patcher, a process-wide assembly index, a proxy registry or a new dependency release.
6. Choose the backend deliberately before generation. Do not catch arbitrary generation errors and retry with another backend; that conceals invalid IL and makes behavior unpredictable.
7. Keep legacy runtime and diagnostic behavior explicit. Unsupported raw-IL runtimes retain the established generation path; assembly output retains exact dependency checks and collectible loading where available.
8. Measure installation cost separately from execution cost. Remove work rather than merely cache it. Keep invocation as a direct typed call with no new reflection or delegate dispatch.

## Implementation

- Move backend selection and existing assembly-proxy preparation out of `MethodCreatorConfig` into one internal method-generation boundary.
- Add a CoreCLR writer using `DynamicMethod.GetDynamicILInfo()`. Serialize the completed Cecil instructions, local/call-site signatures and exception clauses without reconstructing exception regions. Resolve operands to exact runtime handles, including constructed generic members and `DynamicMethod` callbacks. Calculate stack depth from control flow, including returned `calli` values; never add an arbitrary allowance.
- Use the direct writer for normal CoreCLR generation. Preserve explicit diagnostic assembly generation (`DMDType=cecil` or `DMDDebug`) and MonoMod generator selection. Harmony patch logging alone does not request an assembly. CoreCLR before .NET 10 uses assembly output for local/call-site signatures containing custom modifiers: exact runtime-handle modifiers are new in [.NET 10](https://github.com/dotnet/runtime/blob/v10.0.0/src/coreclr/inc/corhdr.h). Keep the existing synthetic-helper initialization rule and legacy runtime path.
- Keep the assembly loader and callback proxies confined to assembly output. Check imported runtime hashes before normalizing candidate assembly names. This preserves quoted names and ambiguity checks without adding a cache or retaining collectible dependencies.
- Extend the existing weak shared-method lookup to dynamic methods, whose owner assembly is not itself collectible. Live detours and callers retain executable code; historical lookup keys must not retain retired callbacks.
- Leave detours, patch serialization, patch selection, binding and the public API unchanged. The writer lives inside Harmony because the released Fat and Thin packages both use MonoMod.Core 1.3.6; requiring a separate dependency release is unnecessary for this bounded fix.

This decision supersedes the backend prescriptions in `INFIX-FEATURE-COMPLETION.md` (outer exception-table assemblies and same-name assembly rejection) and `INFIX-OPERATIONS-ADDENDUM.md` (returning `calli` assemblies). Their correctness requirements remain. The assembly backend still rejects identities that one metadata scope cannot represent; direct runtime tokens may represent them exactly.

## Test organization

Infix unit tests remain in the existing HarmonyTests project. Mixed-version checks keep their isolated-process harness because they deliberately load multiple Harmony identities and serializer configurations. Their CI belongs to Platform Tests: use the existing reusable-workflow pattern, consistent platform names, one overall status and one release gate. Keep all published-version, prior-source, Mono and Framework cases; do not duplicate the runs or create a separate feature-specific test system.

## Acceptance and release sequence

1. Add focused executable regressions for exact exception tables, returning `calli`, dynamic callback identity/lifetime, generic operands and signatures, and unchanged installation after rejected generation. Assert that ordinary CoreCLR catch/finalizer/dynamic-callback patches produce no replacement or proxy assemblies after warmup. Preserve explicit coverage of the assembly fallback and quoted names.
2. Run the smallest relevant .NET 10/x64 checks while implementing. Use the project's quiet test command; format changed C# once the implementation is stable. Use structural assertions rather than fragile stopwatch thresholds in unit tests.
3. Repeat the bounded installation probe against the same published 2.4.2 and rc.2 baselines. Record time, allocation and generated-assembly counts. Verify live dependencies survive and retired replacements can collect. Do not claim whole-game speedups from the synthetic probe.
4. Run the complete local suite and the established platform, .NET Framework and mixed-version release gates on the final candidate commit. Any correction gets the affected focused checks and the required final-commit gates.
5. Bump to 3.0.0-rc.3, update release notes and record executed evidence. Use `docs/releases.md` and the existing prepare/publish workflow for one public rc.3 release, including Fat, Thin and Ref packages and GitHub artifacts. No intermediate public test releases.

Work is on `fix/issue-783-method-generation`. Executed local results and the required remote release boundaries are recorded in [the validation record](../docs/infix/README.md#issue-783-method-generation-2026-10-15).
