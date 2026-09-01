# Concepts and API surface

## Terminology
- Outer: the method being patched.
- Inner: the target method being called inside the outer method.
- Call site: one occurrence of a call to the inner method.

## Attributes
Names are placeholders. Adjust to Harmony naming once decided.
- [HarmonyPatch] on a class or method: selects the outer target (existing).
- [HarmonyInfixTarget] on an inner prefix/postfix method: identifies the inner target method and optional call index within the outer.
- [HarmonyInfixPrefix] marks an inner prefix.
- [HarmonyInfixPostfix] marks an inner postfix.

## Call selection
- Indexing: index = 1..N, -1 means “all occurrences”. Optional future: int[] indices.

## Parameter injection (summary)

### Inner defaults:
- __instance = inner call instance (or null if static).
- Inner arg names map to captured arg locals.
- __result = inner call result (for prefix by‑ref and postfix by‑value).

### Outer access with o_ prefix:
- o___instance, o_paramName, o___fieldName, o___result (rarely needed).

### Outer locals:
- __var_<index> → outer local by index.

### Synthetic locals scoped to the outer method:
- __var_<name> → new local shared across all infix patches at this outer method.

### Use by‑ref to request mutability where supported.

Details in 04-parameter-binding.md.
