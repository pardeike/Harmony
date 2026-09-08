# CodeInstruction

<div id="patching"></div>

A [CodeInstruction](../api/HarmonyLib.CodeInstruction.yml) represents one IL instruction, including its opcode, operand, labels, and exception boundaries.

`CodeInstruction` wraps the .NET [Emit API](https://docs.microsoft.com/en-us/dotnet/api/system.reflection.emit) so transpilers can share and edit instructions. Some details differ: jumps use labels, for example, not numeric offsets such as "four instructions forward".

A transpiler receives and returns `IEnumerable<CodeInstruction>`. An optional [ILGenerator](https://docs.microsoft.com/en-us/dotnet/api/system.reflection.emit.ilgenerator) lets you define labels and local variables.

Most arguments from [Emit()](https://docs.microsoft.com/en-us/dotnet/api/system.reflection.emit.ilgenerator.emit) are used in the same way:

- Emit takes an [OpCode](https://docs.microsoft.com/en-us/dotnet/api/system.reflection.emit.opcode) and so does CodeInstruction
- Operands are mostly the same:
  - `Type`
  - `FieldInfo`
  - `MethodInfo`
  - `ConstructorInfo`
  - `Int64`, `Int32`, `Int16`, `Single`, `Double`, `String`, or `Byte`

The main restrictions are:

- Operands of jumps cannot be numeric, use `Label` instead
- `SignatureHelper` support is experimental at best
- You should avoid using indices when referring to local variables

Do **not** call `ILGenerator.Emit()`: return instructions and let Harmony emit them. Use the generator to declare locals and labels; describe [exception boundaries](#trycatch-boundaries) on the instructions themselves.

Prefer reusing existing operands for labels and locals. Find a distinctive instruction and take its operand instead of hard-coding an index.

## Indirect calls

For a `calli` instruction read from a method body, Harmony supplies an [InlineSignature](../api/HarmonyLib.InlineSignature.yml) operand. Transpilers can inspect its parameter types, return type, calling convention, and instance flags without accessing Harmony internals.

Use its `PopCount` and `PushCount` properties when calculating evaluation-stack depth:

```csharp
if (instruction.opcode == OpCodes.Calli && instruction.operand is InlineSignature signature)
{
    stackDepth -= signature.PopCount;
    stackDepth += signature.PushCount;
}
```

`PopCount` includes the function pointer, every listed parameter, and an implicit instance when `HasThis` is true. When `ExplicitThis` is also true, the first listed parameter already represents that instance, so it is not counted twice. `PushCount` is zero for `void` and one for any returned value. These are counts of evaluation-stack values, not bytes or native calling-convention registers. See the [calli stack behavior](https://learn.microsoft.com/en-us/dotnet/api/system.reflection.emit.opcodes.calli) and [instance-signature flags](https://learn.microsoft.com/en-us/dotnet/api/system.reflection.callingconventions).

Parameter and return entries are `Type` objects, nested `InlineSignature` objects for function pointers, or `InlineSignature.ModifierType` objects for optional/required type modifiers. Modifiers do not change the stack count; returning a function pointer pushes one value even if that function itself returns `void`. The counts follow changes to the mutable signature.

The existing calling-convention representation uses `CallingConvention.Winapi` for the default managed convention; its other named values denote their corresponding unmanaged conventions. Stack counting does not depend on this encoding. The computed counts describe a valid signature; they do not validate whether the runtime can emit an arbitrary signature assembled by a transpiler.

Making this model public does not expand the reader's existing signature support. Generic type/method parameter entries, varargs sentinels, and pointer/by-reference/array wrappers around a nested function-pointer signature are not currently supported by the reader.

## Local variables

Existing local operands can be numeric indices or `LocalBuilder` objects. Handle both. To add a local, call [ILGenerator.DeclareLocal](https://docs.microsoft.com/en-us/dotnet/api/system.reflection.emit.ilgenerator.declarelocal); to reuse one, copy its existing operand.

## Labels

Jumps use a [Label](https://docs.microsoft.com/en-us/dotnet/api/system.reflection.emit.ilgenerator.definelabel) operand. The destination instruction holds that label in its `labels` list. To add a jump, call `ILGenerator.DefineLabel()`, attach the label to the destination, and use it as the jump's operand.

## Try/catch boundaries

An instruction's `blocks` list marks exception boundaries, including filters. Harmony builds the exception metadata from these markers. When moving or inserting code, preserve boundary order and keep handler-entry labels on their handler instructions.

## Convenience methods

[CodeInstruction extension methods](../api/HarmonyLib.CodeInstructionExtensions.yml) help create, find, and compare instructions and their operands.

## Pitfalls

Removing an instruction can orphan its labels or exception boundaries. Copying one can duplicate them. Both can produce invalid IL.

When inserting before an instruction, decide whether jumps should reach your new code or the old instruction. Move labels accordingly. Treat exception boundaries separately: moving one changes which code is inside the handler or protected region.

Set `Harmony.DEBUG = true` or use `[HarmonyDebug]` to inspect generated instructions, labels, and exception boundaries in the log.
