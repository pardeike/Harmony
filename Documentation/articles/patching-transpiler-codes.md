# Patching

## CodeInstruction

The workhorse of a transpiler is the type [CodeInstruction](../api/HarmonyLib.CodeInstruction.yml).

`CodeInstruction` is an abstraction around the .NET [Emit namespace](https://docs.microsoft.com/en-us/dotnet/api/system.reflection.emit). The idea is to abstract most of the specific things you can do in order to help multiple mods that patch/manipulate the same method to coexist. As such, specific instructions like "jump 4 instructions forward" are not possible and instead there are a couple of concepts that differ from the arguments that you know from the ordinary Emit arguments.

Every transpiler will receive a list of `CodeInstruction` and is expected to return a modified list of `CodeInstruction`. In addition, you can inject an [ILGenerator](https://docs.microsoft.com/en-us/dotnet/api/system.reflection.emit.ilgenerator) into the transpiler to create one or more `Label` and instances of `LocalBuilder` which represent local variables.

Most arguments from [Emit()](https://docs.microsoft.com/en-us/dotnet/api/system.reflection.emit.ilgenerator.emit) are used in the same way:

- Emit takes an [OpCode](https://docs.microsoft.com/en-us/dotnet/api/system.reflection.emit.opcode) and so does CodeInstruction
- Operands are mostly the same:
  -  Type
  -  FieldInfo,
  -  MethodInfo, 
  -  ConstructorInfo, 
  -  Int64/Int32/Int16/Single/Double/String/Byte

Some things though will be restricted:

- Operands of jumps cannot be numeric, use `Label` instead
- `SignatureHelper` support is experimental at best
- You should avoid using indices when referring to local variables

Do **not** use `ILGenerator.Emit()`. While it does create IL code, Harmony is an abstraction over it. Harmony takes the returned `IEnumerable<CodeInstruction>` and after some manipulation, calls `Emit()` itself. `ILGenerator` should only be used for its methods to define blocks and labels. See [Labels](#labels) and [Try/catch boundaries](#trycatch-boundaries) for more information.

In general, it is advised to reuse and to copy existing operands for things like labels and local variables. Search for a significant and unique location in the existing codes and grab the operand from there. This will allow you to refer to local variables and labels in a change-resistant way.

#### Indirect calls

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

#### Local variables

The instructions that your transpiler will receive will contain existing local variables and Harmony will not alter the original operands of instructions. This means that you need to be prepared to deal with instructions that refer to local variables either with a number (like "2nd local variable") or with a `LocalBuilder` object. A LocalBuilder object is an opaque representation of a local variable and you can create a new one using the [DeclareLocal](https://docs.microsoft.com/en-us/dotnet/api/system.reflection.emit.ilgenerator.declarelocal) method for [ILGenerator](https://docs.microsoft.com/en-us/dotnet/api/system.reflection.emit.ilgenerator) or copy the operand of an existing instruction.

#### Labels

All original labels (or those generated by a previous transpiling) are represented by a [Label](https://docs.microsoft.com/en-us/dotnet/api/system.reflection.emit.ilgenerator.definelabel) object. They are used in operands of a `CodeInstruction` or in the `labels` field (of type `Label[]`) of it. When you want to create a jump, you specify the jump Opcode and the label as the operand. Then you append the label to the destinations labels.

To create a new label to jump to, use `ILGenerator.DefineLabel()` and put that label into the `labels` field of the *target* `CodeInstruction`. Use that label as the argument for a jump opcode as described above.

#### Try/catch boundaries

When constructing methods with instructions, you need to specify the exception block boundaries. Harmony will automatically create the necessary metadata from them. Use the `blocks` field of an instruction (of type `ExceptionBlock[]`) to mark the different types of boundaries, including exception filters. Preserve the original boundary ordering and keep handler-entry labels on their handler instructions when moving or inserting code.

#### Convenience methods

To create, search and compare instructions, Harmony defines a number of extension methods on `CodeInstruction`. Those methods make it easier to compare operands (which are defined as type `object`) with specific values as well as to find specific instructions easier.

You will find more information about those methods in the [API documentation](../api/HarmonyLib.CodeInstructionExtensions.yml).

#### Pitfalls

A common error is to remove instructions that contain labels or exception blocks without dealing with the corresponding pair. You end up with a dynamic method compile error because an instruction will want to jump to a label that is not assigned to any other instruction. Another common case is if you copy an existing instruction and thus copy the labels and blocks fields with it. You end up with multiple defined labels/block boundaries. Makes you you clear those or use proper duplication.

To avoid such cases, it is important to keep an eye on the `labels` and `blocks` fields. CIL is quite sensitive to undefined or duplicate labels. Normally it is enough to move the contents of those fields around if you insert new instructions. One of the typical cases is inserting a new instruction at a place and pushing the old one one index up thus moving the labels on that instruction with it. Moving the labels/blocks from the old instruction to the newly inserted one usually solves the problem.

In general, it is useful to turn the Harmony debug log output on (set `Harmony.DEBUG = true` or use `[HarmonyDebug]`). The log will contain all your generated instructions, labels and blocks so you can verify the error more informed.
