# Patching

## Transpiler

This method defines the transpiler that modifies the code of the original method. Use this in the advanced case where you want to modify the original methods IL codes.

```csharp
static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instr, ...)
// or
[HarmonyTranspiler]
static IEnumerable<CodeInstruction> MyTranspiler(IEnumerable<CodeInstruction> instr, ...)
```