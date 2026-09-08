# Callsite integration details

## Occurrence indexing
- Do a first pass over finalized outer IL to count occurrences per MethodBase.
- On emission pass, maintain a per‑method counter to know the current occurrence index k and total N.

## Result use downstream
- Always finish by reproducing the original stack effect.
- If the next original instruction was stloc, it still works.
- If it was brtrue/brfalse on stack, it still works.
- If it was pop, it still works.

## Exception boundaries
- The infix block sits where the call was. Keep it inside the same try region.

## Transpilers
- Apply transpilers to the outer method before inserting infixes. Emit infixes on the finalized instruction list.
