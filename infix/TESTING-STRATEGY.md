# Infix testing strategy

This document defines verification requirements. Behavior is specified by the [implementation specification](../../drafts/INFIX-NEW-IMPL-V3.md), [operation-target addendum](../../drafts/INFIX-OPERATIONS-ADDENDUM.md) and [feature-completion contract](../../drafts/INFIX-FEATURE-COMPLETION.md). The [compatibility strategy](../../drafts/INFIX-COMPATIBILITY-TESTS.md) defines loader and serialization checks. Keep executed candidate results in [README.md](README.md), separately from this strategy.

## What a green run must prove

Each requested framework must execute passing tests, exit successfully and produce its own completed TRX report. Missing, malformed, empty, unfinished or failed reports fail the job. A later success must not hide an earlier failure. Valid skips are allowed when real tests also executed.

[Test-Frameworks.ps1](../../.github/scripts/Test-Frameworks.ps1) owns validation for platform framework loops and the Windows Mono timeout workaround. Remove stale reports before replacement runs. Upload failed reports and retain them when build, test or publication fails or is cancelled. Experimental results keep their separate artifact names and nonblocking policy.

[Test-CI.ps1](../../.github/tests/Test-CI.ps1) exercises this gate with real PowerShell child processes, including nonzero exits followed by success, missing/malformed/stale/zero-test reports, incomplete counters, valid skips and artifact cleanup. These checks run without building Harmony.

Compatibility children must verify the actual runtime major version, architecture, loaded provider identities, shared dictionaries, results and callback traces. Successful loading alone does not prove coexistence. Keep successful feature execution, intentional compatibility rejection and known loader/concurrency limitations as separate outcomes.

## Independent expectations and test families

Prefer short input tables over duplicated fixtures. Calculate expected traces and results independently of the implementation. Compare ordinary patches with Infixes where their contracts agree, while retaining direct expected traces for important scheduling rules. Invoke the final wrappers returned by the patch APIs and assert that the ordinary control ran its callbacks. Test entry-point redirection and repeated rebuilding separately.

| Test family | Dimensions | Required observation |
| --- | --- | --- |
| Scheduling and registration | Priority, before/after edges, insertion order, void/passthrough phases, skip position, duplicate records | Exact callback trace and result; new equal-priority registrations follow survivors after early owners are removed. |
| Occurrence selection | Positive/negative positions, installation order, callback-generated calls, finalizer helpers, removal and reinstallation | Every selector uses the same post-transpiler, pre-Infix body; generated patch instructions do not become new occurrences. |
| Storage and state | Inner/outer scopes, values, references, managed references, aliases, repeated sites and loops | Writes reach the intended storage; results, run flags and state reset at their specified boundaries. |
| Invocation isolation | Recursion and overlapping calls using deterministic barriers | Per-invocation state stays separate; intended object sharing remains visible. |
| Wrapper lifetime | Repatch, invoke, remove, reinstall, force collection; ordinary and Infix combinations | Removed callbacks stop; current callbacks remain reachable; references and exceptions retain identity. |
| Failure and recovery | Invalid batches, incompatible later sites, failed rebuilds, corrected retries | Rejection leaves the installed wrapper and stored state unchanged. |

Registration tables cover all three accumulating inner roles, both overloads, null inputs, processor reuse and owner/role removal. Ordinary processor replacement and duplicate class-target behavior remain separate controls.

## Operations, binding and execution

- Methods and property accessors normalize to the same call selector. Compare manual accessor and property selection, exact and generic-family identity, static/instance dispatch, receiver replacement and argument evaluation once.
- Field reads expose a result; writes expose a value argument. Cover private/static/reference-instance fields and supported `volatile`/`unaligned` prefixes, including static fields declared on structs. Reject struct instance fields, init-only writes and literal fields. Adjacent read/write/address instructions prove exact selection.
- Constructor `newobj` cases verify arguments, skipped construction, result replacement and exceptions. Base initialization via `call` remains a distinct, unsupported selected operation.
- Literal tests cover string, `int`, `long`, `float` and `double` categories. Compact/full `ldc.i4` forms share integer matching. Compare floating-point bits for signed zero and distinct NaN payloads; neighboring categories stay untouched.
- Argument-array tests exercise lazy allocation, scope-specific refresh, aliased `out` initialization once at site entry, restoration order and rejection of competing delayed write-back. Typed refs retain their actual shared storage; inner by-value argument edits do not rewrite their source expressions.
- Finalizer tests compare prefix, operation, postfix and finalizer failures and observe/preserve/replace/suppress decisions with ordinary Harmony. Retain explicit traces and phase-wide passthrough result-commit assertions. Test field operations, construction, literals, constrained/virtual calls, filters, fault/finally/catch boundaries, and pending structs, managed references and function pointers.
- Fault recovery must inspect the result before the finalizer writes it, through both the returned wrapper and patched entry point. Run older runtimes in Release as well as Debug. Structured helpers explicitly initialize demanded locals and disable implicit initialization; every added helper local must preserve that definite-assignment requirement. Use a nongeneric outer fixture for a real `constrained.` interface call so a generic-outer detour limitation cannot replace the receiver-mutation test.
- Generated-body tests install and execute patches on explicit `MoveNext` and automatic iterator/async/async-iterator targets. Cover closed generics, ordinary controls, malformed metadata, factory/body aliases and removal through the original processor. Force an incomplete await between async-iterator yields and verify captured writes survive resumption. Resolution alone is insufficient.
- Captured-variable tests distinguish working iterator fields from saved restart arguments, captures from Harmony state, inner/outer receivers and closure arguments, and readonly value links from mutable referenced objects. Verify writes across real yields/awaits and both suppressed and escaping exceptions. Named outer locals remain per invocation.
- Inlining tests compare copied and called bodies, branch/argument/local remapping, private by-value slots, local reset per loop visit, cleanup, exceptions and unnamed passthrough parameters. A refused hint must leave the wrapper's local list unchanged and execute the ordinary-call fallback successfully. Cover function-pointer locals and operands on runtimes that erase their reflected type, with and without finalizers. The explicit Release comparison checks actual patched execution and reports timings without a speed threshold.
- Indirect-call tests execute parsed and re-emitted managed and unmanaged `calli` operands, with ordinary DynamicMethod prefixes absent and present. Use a matching managed entry point or explicitly Cdecl delegate thunk; retain the delegate through forced collection. Check the wrapper and patched entry point. Test the historical convention mapping separately; parsing `ThisCall` or `FastCall` does not prove runtime execution support.

Retain the end-to-end capture example: capture a constructed `StringBuilder` in a named outer local, then use a later selected string literal as the insertion point. Verify invocation identity and untouched neighboring builders/constants under recursion and controlled overlapping calls.

## Serialization, removal and generated assemblies

Use Harmony's actual serialization entry point. The Fat assembly may contain internalized serializer types and attributes that a test-owned serializer ignores. For each malformed-state test, first round-trip the valid payload, verify the mutation changed the intended field, then assert the specific rejection.

When removal recovers ordinary state, test raw legacy deserialization and patch contents. Do not require a separately constructed empty BinaryFormatter payload to have identical bytes: shared empty-array references vary across framework builds without changing semantics. Byte equality is appropriate when rejection must preserve the same stored payload.

Test structurally valid but unavailable or ambiguous target identities, including nested generic arguments. In an isolated child, install working patches before loading a duplicate target module. Inspection and complete owner removal must remain possible; a surviving invalid target must stop rebuilding before transpilers or publication. Keep duplicate test assemblies out of the main test process.

Compatibility checks cover current/current cold rebuilding, both prior/current load orders, declaration rejection independently of active-state rejection, unchanged bytes and behavior after rejection, and removal through versions 3, 2, 1 and ordinary state. Method-only `__originalMember` injection requires version 2 even without an extended target. Exercise both scopes, aliases, exact-name exclusions and ignored passthrough parameters. Stored-state version protection does not establish the separate old-engine declaration guarantee.

Generated-wrapper tests distinguish missing dependencies from distinct loaded assemblies with the same identity. Exercise private callback-owned state across a real outer `finally`, exact dispatch to same-name callbacks, and rejection without publication when one metadata scope cannot distinguish both dependencies. Mono can omit generated proxy assemblies from public enumeration, so retain the actual emitted method's assembly. Saved Reflection.Emit fixtures must not coexist with temporary emitters sharing a module ID but different metadata-token positions.

## Runtime matrix and release record

Keep the configured framework/architecture matrix for ordinary behavior, builds and representative Infix execution. Add focused lanes for distinct mechanisms rather than multiplying every selector and binding permutation:

- Published Fat assemblies from the four selected older releases, both serializers on net8/x64, net9 JSON and explicit Mono loading-boundary probes.
- Windows .NET Framework net472/x64 BinaryFormatter with actual old/new engines and both initialization orders. Require the ordinary coexistence control before counting feature execution.
- CoreCLR Windows/x86 repeated repatching with production tiering enabled. The deterministic compatibility children disable tiering and do not establish behavior under JIT defaults.
- Mixed-engine DynamicMethod factories around exception-handling targets, including collection and owner removal without callback/proxy identity substitution.
- Pinned prior Infix formats with both serializers and two distinct current engines. The version-1 baseline is `573914745451f8278720257db37a4a4ebaae853d`; version 2 is `22d4069bac2bf963d8238dd1fd0940cdf57ae084`. Reuse the source-baseline jobs for extension and completion checks.

Retain lifetime stress for previously observed native Windows/x86 repatching failures. A macOS .NET 3.1/x64 Release ordinary-control bypass did not reproduce in exact-runtime reruns; it did not establish an ordering defect. Report any recurrence with the actual runtime and entry-point behavior. Known Mono unification/loader limits and concurrent-update limits remain classified boundaries, even when their expected-outcome tests pass.

At a release checkpoint, run the complete normal suite and required coexistence lanes against the same candidate artifacts. Record runtime, architecture, configuration, provider hashes and case classifications in the reports, and summarize the current candidate in [README.md](README.md). Keep failed artifacts. Distinguish compilation, successful execution, intentional rejection, known limitations and untested hosts. Earlier commits' green CI runs do not validate later edits.
