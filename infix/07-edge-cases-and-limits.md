# Edge cases and current limits

## Supported in v1
- call and callvirt to normal methods.
- Instance, static, generic methods.
- Inner return void or non‑void.
- By‑ref parameters when captured as managed pointers and passed through unchanged.

## Special cases
- constrained. prefix: absorbed and re‑emitted inside block.
- Value‑type instances under constrained.: handled by preserving the instance operand exactly as captured.

## Not in v1
- tail. preservation (semantics require immediate ret).
- calli, ldftn/ldvirtftn indirections.
- newobj as inner target (optional later).
- Exotic argument construction that relies on side effects between operand pushes. In practice this still works because we pop exactly what was pushed, then re‑load from locals, but do not promise behavior if side effects depend on relative timing.

## Performance
- Minor overhead from locals and calls to inner fixes. No global flow analysis.
