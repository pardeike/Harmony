# Refactor plan: reuse injection logic cleanly

1) Model
- Add enum InjectionScope { Outer, Inner }.
- Extend the injected‑parameter resolver to accept { scope, callSiteContext? }.

2) Binding
- Map names to sources per scope:
- Inner: __instance, inner arg names, __result, __args?.
- Outer: existing mapping. Trigger via o_ prefix.
- Outer locals: __var_<index>.
- Synthetic: __var_<name> declared once per outer method and reused.

3) Emission
- Do not fork the emitter.
- Add small hooks:
  - Provide “value loader” and “address loader” delegates per source so the existing emitter can push either ldloc or ldloca or outer ldarg[a] etc.
  - For inner __result, bypass outer result plumbing and use the call‑site local.

4) Minimal changes by file
- PatchModels.cs: add inner patch types and InnerMethod.
- PatchClassProcessor.cs: detect [HarmonyInfixPrefix/Postfix], parse [HarmonyInfixTarget], attach InnerMethod.
- PatchInfo.cs: store inner prefix/postfix arrays. Remove patch support covers them too.
- PatchFunctions.cs: filter/sort inner patches per call occurrence.
- MethodCreatorTools.cs: extend parameter resolver with InjectionScope and __var_* handling.
- MethodCreator.cs: implement AddInfixes(...) pass that replaces calls per algorithm.
- Infix.cs: optional thin helpers for per‑site context.

5) No duplication
- The only special‑case logic lives in:
  - call‑site stack capture,
  - inner __result handling,
  - absorption of call‑only prefixes.
