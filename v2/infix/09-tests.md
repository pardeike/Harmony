# Tests

## Matrix
- Return types: void, bool, reference type, value type.
- Calls: instance vs static, generic, value‑type receiver under constrained..
- Occurrences: single, multiple with index selection.
- Skip behavior: inner prefix returning false with and without preset __result.
- Argument mutation:
  - Inner mutable by‑ref arg modified by prefix and by original.
  - Outer arg modified via o_param by‑ref.
  - Outer local via __var_<index> by‑ref.
- Synthetic locals: __var_<name> shared across inner pre/post.
- Coexistence with outer prefix/postfix and with transpilers.

## Assertions
- Correct ordering and skip semantics.
- Identical stack effect to original call.
- Correct write‑backs for by‑ref when used.
- No change in behavior when no infix patches apply.

## Scaffolding
- Build tiny outer/inner pairs with predictable side effects.
- For constrained. cases, use value‑type receivers with interface calls.
