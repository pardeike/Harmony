# Parameter binding: reuse and extend

## Principle
Do not invent a new binder. Extend the existing injection resolver with a context flag: Outer vs Inner.

## Binding sources
- Inner context
  - __instance → captured instance local for this call site.
  - Inner arg names → captured arg locals by name/position.
  - __result → the infix result local for this call site.
  - __args (optional later) → array from captured arg locals.
- Outer context (when name starts with o_)
  - Drop o_ and resolve using the current outer method resolver:
    - __instance, __args, arguments by name or index,
    - ___field, __state, __exception, outer __result if present.
- Outer locals
  - __var_<index> → load/address original outer local slot.
- Synthetic locals
  - __var_<name> → declare once per outer method; share between inner pre/post.

## Ref/out handling
- If a patch parameter is by‑ref:
  - Provide ldloca for a value local, or the captured managed pointer local for by‑ref arguments.
- Result passthrough (optional):
  - If an inner postfix returns the same type as the inner result and takes it as first parameter, treat return as new __result.

Keep the emitted IL paths identical to existing binder behavior where possible. Only the source of values changes by context.
