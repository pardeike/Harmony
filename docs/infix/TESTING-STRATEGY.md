# Infix testing strategy

This is the test plan, not another feature specification. [INFIX-NEW-IMPL-V3.md](../../drafts/INFIX-NEW-IMPL-V3.md) and its [operation-target addendum](../../drafts/INFIX-OPERATIONS-ADDENDUM.md) define behavior; [the mixed-version strategy](../../drafts/INFIX-COMPATIBILITY-TESTS.md) defines the loader and serialization checks. Historical results remain in [README.md](README.md). A test being implemented does not mean its runtime lane has passed.

## What a green run must prove

Each requested framework must run, exit successfully, and produce its own completed TRX report containing executed, passing tests. A later success must not hide an earlier failure. Missing, malformed, empty, unfinished, or failed reports fail the job. Valid skipped tests remain allowed when real tests also executed.

[Test-Frameworks.ps1](../../.github/scripts/Test-Frameworks.ps1) owns that check for the platform framework loops. The Windows Mono timeout workaround uses the same report validation before treating a stuck process as a completed run. A report from a previous attempt is removed before launching its replacement. Failed reports are uploaded even when the test step fails; cleanup retains them when an upstream build/test job or publication fails or is cancelled. Experimental results retain their existing nonblocking policy and separate artifact names.

[Test-CI.ps1](../../.github/tests/Test-CI.ps1) exercises the gate with real PowerShell child processes. Its table includes first/middle/last nonzero exits, later successful frameworks, missing/malformed/empty/zero-test reports, stale reports, incomplete counters, valid skips, and the actual artifact cleanup query. These checks run in the platform workflow without building Harmony.

Compatibility children separately assert actual runtime major version, process architecture, loaded provider identities, shared dictionaries, expected results and traces. A successful process launch or a matching assembly version is not coexistence proof. Known Mono loading limits remain diagnostics, not successful Infix execution.

## Small test families, independent expectations

Prefer short input tables and loops over another copy of a large fixture. Use expected traces or arithmetic results that do not call the implementation being tested to calculate the answer. Compare ordinary patches and Infixes where their behavior is deliberately shared, but keep a direct expected trace for important ordering rules so a shared defect cannot make both sides agree.

The scheduling comparison invokes the final executable wrappers returned by each patch API and asserts the ordinary control actually ran its callbacks. Entry-point redirection and repeated rebuilding belong to separate runtime tests. One macOS .NET 3.1/x64 Release run returned the unpatched value from the ordinary control, while the Infix result was correct; exact-runtime local reruns passed. Its cause is unresolved, not a proven ordering defect or a fixed detour defect. Keep this observation alongside the earlier Windows/x86 native failure when testing runtime lifetime behavior.

| Test family | Useful independent dimensions | Required observation |
| --- | --- | --- |
| Scheduling | Priorities, before/after edges, equal-priority insertion order, void/passthrough phases, skip position, duplicate registrations | Exact callback trace and result; no artificial prefix/postfix pairing. |
| Storage and state | Inner/outer scope, value/reference/managed-reference arguments, aliasing, repeated sites, loop iterations | Changes reach only the intended storage; state resets at the right boundary. |
| Invocation isolation | Recursive calls and overlapping invocations, with deterministic barriers | Each invocation retains its own result, run flag and state; shared objects remain shared only where intended. |
| Wrapper lifetime | Repatch, invoke, remove, reinstall, explicit collection; ordinary and Infix combinations | Old callbacks stop, current callbacks remain reachable, references and exceptions retain identity. |
| Target operations | Methods, properties, fields, constructors and constants | Exact operation/site selection, evaluation once, receiver and value typing, untouched neighboring operations. |
| Failure and recovery | Invalid registration or later-site binding, failed rebuild, corrected retry | Published state and existing executable behavior remain unchanged until a successful update. |

For serialized-state recovery, test readability by the raw legacy serializer and the recovered patch contents. Do not compare a removed-to-empty BinaryFormatter payload with a separately constructed empty payload: BinaryFormatter preserves whether empty arrays share an object, and that relationship varies across framework builds without changing patch semantics. Byte equality remains appropriate when asserting that a rejected update left the *same stored payload* untouched.

The target-operation cases follow the selected compact API:

- Methods, property getters and property setters normalize to the same call selector. Compare manual accessor selection with property selection, including static/instance access, argument evaluation once and receiver replacement.
- Field reads behave as `read() -> T`; writes behave as `write(value) -> void`. Cover static fields, reference-type instance fields, private fields and `volatile`/`unaligned` prefixes. Include static fields declared on structs. Reject struct instance fields, init-only writes and literal fields; field-address loads are not selected. Keep adjacent read/write/address instructions in one fixture to prove exact selection.
- Constructor `newobj` sites expose constructor arguments and the constructed result. Assert expression evaluation once, skipped-construction behavior, result replacement and exception handling. A constructor call used for base initialization is not the same operation.
- String, `int`, `long`, `float` and `double` constants retain their raw IL categories. Generate the compact/full `ldc.i4` forms as one integer-match family. Compare floating-point bits, not numeric equality, for signed zero and multiple NaN payloads. Constants of another category and neighboring instructions remain untouched.
- State-machine code is reached through the existing explicit `MoveNext` facility. Named `[HarmonyOuter] __var_name` locals provide multiple captures; test their declaration, availability and invocation isolation rather than inventing another state store. Infixes do not gain inner finalizers.

Include the concrete capture workflow: intercept a `StringBuilder` construction, keep the builder in a named outer local, then target the later string constant that marks where text should be inserted. Assert both captures refer to the same invocation and that unrelated builders/constants are untouched. Repeat the operation recursively and under controlled overlapping calls so an accidentally shared capture cannot pass.

## Runtime coverage without a full cross-product

The existing platform matrix covers the supported target frameworks and architectures. Keep that breadth for ordinary behavior, compilation, and representative Infix execution. Use focused fixtures to make a failing runtime combination reproducible without rerunning every test.

The broad compatibility matrix uses published Fat assemblies for all four selected old releases, both supported serializer backends on net8/x64, net9 JSON, and explicit Mono loading-boundary probes. Its normal children currently disable tiered compilation. That deterministic setting must not be presented as evidence for the production JIT defaults.

Use narrow lanes when they answer a new question:

- The added Windows .NET Framework net472/x64 lane uses BinaryFormatter, actual old/new engines and both initialization orders. It requires the ordinary coexistence control before counting feature cases. A loading failure is an unresolved host boundary, not an acceptable replacement for successful coexistence on a claimed supported lane. [The first extended-target CI run](https://github.com/pardeike/Harmony/actions/runs/34093242634) passed all 35 Windows cases on .NET Framework 4.8.9339.0/x64, with distinct engines sharing the same dictionaries and no limitation classifications.
- One supported CoreCLR Windows/x86 smoke test with default tiering that repeatedly changes and invokes a patch. This complements the deterministic compatibility matrix and targets native detour lifetime failures that may appear only after many rebuilds.
- One mixed-engine dynamic-factory case around an exception-handling target. Distinct callbacks installed by separate engines must survive rebuilding, collection and owner removal without resolving to each other's generated proxy assemblies.
- The added pre-extension V3 lane pins commit `573914745451f8278720257db37a4a4ebaae853d` alongside two extended engines, with JSON and BinaryFormatter. Constructor, field-read and constant cases exercise cold identity, rebuilding, declarations, rejection and recovery. Existing method patches retain version-1 interoperability. Generalized declarations and version-2 extended state fail before callbacks or transpilers run, without changing installed state. Removing the last extended target restores version-1 state when method Infixes remain, or ordinary unframed state when none remain. All three children pass locally on net9/JSON and net8/BinaryFormatter, including both V3/current load orders. A published-2.4.2 net9/JSON child separately passes the generalized-declaration/state rejection and recovery checks. The same CI run passed both V3 lanes with distinct current test versions 2.4.4 and 2.4.5 and no limitation classifications.

Do not multiply every selector, declaration, state and argument permutation across these lanes. Run the representative sequence first; expand only where it exposes a distinct mechanism.

Include capability changes that keep the old selector shape. In particular, method-only `__originalMember` injection requires version 2 even without a field, constructor or constant target. Test both scopes, renamed injection parameters, exact argument-name exclusions and the ignored first passthrough parameter. Preserve owner-removal when callback metadata is missing or ambiguous; a failed rebuild must still stop before transpilers. State-version protection is not evidence that the old method-style declaration rejects before publication: that narrower discovery guarantee belongs to the generalized declaration marker.

## Priority and release evidence

1. **Required:** Keep the executable CI completeness checks green and retain failed result artifacts. Never accept a publisher's success as proof that every requested framework produced results.
2. **Required before release:** Keep the now-proven Windows .NET Framework mixed-version lane green. Resolve or explicitly report the existing Windows/x86 native repatching failure and any supported Mono lane that never reaches the ordinary coexistence control. Do not suppress a failure merely because it occurs before Infix.
3. **Keep the added coverage:** Wrapper lifetime/default-JIT stress, recursive and concurrent invocation isolation, and operation-specific selection now run in the normal suite. Add independent scheduling traces where existing comparisons still rely on the ordinary engine as their only oracle. Preserve the executable pre-extension V3 checks as these features change. Use small fixtures or generated tables, not a new testing framework.
4. **At the release checkpoint:** Run the complete normal suite and selected actual old/new lanes against the same candidate artifacts. Record runtime, architecture, configuration, assembly hashes and case classifications. Earlier successful binaries do not validate later fixes.

Report separately what compiled, what executed successfully, what was intentionally rejected, what reached a known loader boundary, and what remains untested. On 2026-09-07, the first local candidate passed the full net9/x64 Debug and Release suites (727 each), net5/x64 (714, with 12 JSON-only skips), and Mono/net452 (704, with six existing ignores and one explicit test). All seven new compatibility children were rerun against that candidate and passed; the actual engine hashes are in their provider reports. Local PowerShell completeness checks pass, the launcher passes shell syntax validation, and the changed workflows parse as YAML.

All 16 compatibility CI jobs passed for `cde7122`. Only the new Windows and V3 jobs reported no limitations. The existing Mono jobs still classify assembly-unification and ordinary-loader boundaries; the published-CoreCLR jobs still classify default-context loading, old-engine foreign-instruction rebuilding, and concurrent-update limitations. Green jobs that assert these known boundaries do not prove successful Infix execution in those arrangements.
