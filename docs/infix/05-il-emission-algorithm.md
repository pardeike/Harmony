# IL emission at a call site

## Inputs
inner: target MethodBase of the call.
prefixes, postfixes: sorted for this call occurrence.
callPrefixes: any immediate IL prefixes before the original call that apply to the call (e.g., constrained., tail.).

## Output contract
Same stack effect as the original call.
No change to prior argument‑loading code.
Only the original call is replaced. The immediate call‑only prefixes are absorbed.

## Algorithm (pseudocode, IL‑level)
At the position of the original call:

1) Capture instance and args from stack into locals, popping in reverse order.
```
for operand in [argN .. arg1, instance?]:
    stloc <cap_local_operand>   // exact stack type, including managed pointers
```

2) Init per‑site locals
bool __runOriginal = true;
TResult __result = default; (only if non‑void)

3) Inner prefixes in order:
If any prefix can skip, guard subsequent prefixes on __runOriginal.
Emit call to prefix with bound parameters (see binding doc).
If prefix returns bool, store to __runOriginal.
```
if (canSkipAny)
    if (!__runOriginal) goto AfterPrefixes;
call InnerPrefix(...);        // may return bool
if (returnsBool) stloc __runOriginal
AfterPrefixes:
```

4) Conditional original call
If __runOriginal == false, skip to step 5.
Re‑emit captured call‑only prefixes here if present (e.g., constrained.).
Reload instance/args from locals and perform the call.
If non‑void, stloc __result.
```
if (!__runOriginal) goto AfterCall
[constrained.|tail.]?         // only if originally present
ldloc instance?
ldloc arg1 .. ldloc argN
call/callvirt inner
if (hasResult) stloc __result
AfterCall:
```

5) Inner postfixes in order:
Call with bound parameters.
Optional passthrough: if postfix returns TResult, take it as new __result.

6) Write‑backs
For any captured by‑ref argument that you converted to a value local, store back to the original address now.
Prefer not to convert by‑ref args to values in v1: capture their managed pointer and pass it through, so no write‑back is needed.
Restore original stack effect
If inner is non‑void: ldloc __result to leave the result on the stack.
If void: push nothing.
Done.

## Notes
- This design does not remove prior argument loads. It only replaces the call with a block that starts by popping the already‑prepared operands into locals, then proceeds.
- If a constrained. prefix existed, remove it from the original stream and re‑emit it immediately before the reissued call inside the block. tail. cannot be preserved when extra IL follows; document as unsupported.
