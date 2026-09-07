---
uid: HarmonyLib
summary: *content
---

Harmony patches existing .NET methods at runtime. Create a [Harmony](xref:HarmonyLib.Harmony) instance with your own owner ID, then use patch attributes or a [PatchProcessor](xref:HarmonyLib.PatchProcessor) to install patches.

Prefixes, postfixes and finalizers apply to a whole method. [HarmonyInfix](xref:HarmonyLib.HarmonyInfix) applies those same roles to selected operations inside a method; [InnerMethod](xref:HarmonyLib.InnerMethod) and [InnerTarget](xref:HarmonyLib.InnerTarget) describe targets for manual registration. Transpilers edit instructions directly, with [CodeMatcher](xref:HarmonyLib.CodeMatcher) for matching and editing instruction sequences and [InlineSignature](xref:HarmonyLib.InlineSignature) for inspecting indirect calls.

The types below form the API reference. For working examples and the rules shared by these APIs, start with the [patching guide](../articles/patching.md) or the [Infix guide](../articles/patching-infix.md).
