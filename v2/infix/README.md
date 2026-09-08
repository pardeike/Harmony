# Harmony Infix: implementer overview

Purpose. Add infix patches that wrap a specific inner call inside an outer method. Inner prefixes run before the call. Inner postfixes run after. Ordering, skip semantics, and argument/result injection mirror regular prefixes/postfixes, but the scope is the call site.

One rule. Replace only the call instruction (and its immediate call‑only prefixes when necessary). Do not restructure prior argument‑loading IL. At the call site, stack shape is well defined; capture it, then operate. This avoids flow analysis. 

## High‑level flow at each target call.

- Capture stack to locals.
- Run inner prefixes. They can edit captured args and __result, or skip the call.
- Optionally call original using locals.
- Run inner postfixes.
- Write back where needed.
- Restore stack shape so outer IL proceeds unchanged.

See 05-il-emission-algorithm.md.

## Touched code (root‑relative).

/Harmony/Public/Patch.cs
/Harmony/Public/PatchInfo.cs
/Harmony/Public/Patches.cs
/Harmony/Public/PatchClassProcessor.cs
/Harmony/Internal/PatchModels.cs
/Harmony/Internal/PatchFunctions.cs
/Harmony/Internal/MethodCreator.cs
/Harmony/Internal/MethodCreatorTools.cs
/Harmony/Internal/MethodCreatorConfig.cs
/Harmony/Internal/Infix.cs (or new file if preferred)
