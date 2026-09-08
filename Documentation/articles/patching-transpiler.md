# Rewrite instructions

<div id="transpiler"></div>

<div id="patching"></div>

A transpiler edits the original method's IL instructions when Harmony builds a replacement. It does not run each time the original is called.

Use a transpiler when other patch types cannot express your change. You can insert calls, remove instructions, or change values and operands. Change as little as possible, and match distinctive instructions rather than fixed offsets so other mods and game updates have room to coexist.

[!include[Build and runtime phases](../includes/transpiler-phases.md)]

The basic API of a transpiler looks like this:

```csharp
static IEnumerable<CodeInstruction> Transpiler(<arguments>)
// or
[HarmonyTranspiler]
static IEnumerable<CodeInstruction> MyTranspiler(<arguments>)

// Arguments are identified by their type and can have any name:
IEnumerable<CodeInstruction> instructions // [REQUIRED]
ILGenerator generator // [OPTIONAL]
MethodBase original // [OPTIONAL]
```

A typical transpiler looks like this:

[!code-csharp[example](../examples/patching-transpiler.cs?name=typical)]

Harmony reruns the transpiler chain whenever it rebuilds the replacement after patches change. A transpiler cannot access a particular call's arguments or locals; it can insert code that accesses them when the method runs.

## Basic Transpiler Tutorial

This tutorial uses a method from an older version of RimWorld. The instruction-editing techniques apply to other .NET applications, but current game code and installation paths may differ.

Writing a transpiler means writing rules for rewriting code. You need to understand C# and the IL stack well enough to keep the result valid.

This tutorial walks through reading a method's IL and removing one section. Section 6 links to references for the instructions used here.

**Tutorial**

The example method is `Dialog_FormCaravan.CheckForErrors()`. The goal is to remove the block that rejects a caravan for exceeding its mass capacity.

**1) Choose a decompiler**

Use a .NET decompiler with C# and IL views, such as [ILSpy](https://github.com/icsharpcode/ILSpy) or [dnSpyEx](https://github.com/dnSpyEx/dnSpy). The original tutorial used [Zhentar's ILSpy build](https://github.com/Zhentar/ILSpy/releases).

**2) Decompile**

Open the game's `Assembly-CSharp.dll`. For the Windows installation used by this example, the path was:
`C:\Program Files (x86)\Steam\steamapps\common\RimWorld\RimWorldWin_Data\Managed\Assembly-CSharp.dll`

**3) The original method**

Find `RimWorld.Dialog_FormCaravan.CheckForErrors` and open its C# view.

**4) Viewing IL Code**

Switch the language view from `C#` to `IL`:

```
.method private hidebysig
	instance bool CheckForErrors (
		class [mscorlib]System.Collections.Generic.List`1<class Verse.Pawn> pawns
	) cil managed
{
	// Method begins at RVA 0xbb290
	// Code size 617 (0x269)
	.maxstack 64
	.locals init (
		[0] class Verse.Pawn,
		[1] int32,
		[2] int32,
		[3] int32,
		[4] int32,
		[5] class RimWorld.Dialog_FormCaravan/'<CheckForErrors>c__AnonStorey3F6',
		[6] class RimWorld.Dialog_FormCaravan/'<CheckForErrors>c__AnonStorey3F8'
	)

	IL_0000: newobj instance void RimWorld.Dialog_FormCaravan/'<CheckForErrors>c__AnonStorey3F6'::.ctor()
	IL_0005: stloc.s 5
	...
```

Switch between IL and C# to compare them. The compiler may rearrange branches or reverse conditions, so source order is not always instruction order.

**5) IL Code Basics**

Each `IL_` row contains an **opcode** (the operation) and sometimes an **operand** (the value it uses). Operands can be numbers, types, fields, methods, labels, and more.

IL works with a stack: values go onto the top and operations take them off. Here is the section containing `reform`, `MassUsage`, and `Message`:

```
IL_0078: ldarg.0
IL_0079: ldfld bool RimWorld.Dialog_FormCaravan::reform
IL_007e: brtrue IL_00ac
IL_0083: ldarg.0
IL_0084: call instance float32 RimWorld.Dialog_FormCaravan::get_MassUsage()
IL_0089: ldarg.0
IL_008a: call instance float32 RimWorld.Dialog_FormCaravan::get_MassCapacity()
IL_008f: ble.un IL_00ac
IL_0094: ldarg.0
IL_0095: call instance void RimWorld.Dialog_FormCaravan::FlashMass()
IL_009a: ldstr "TooBigCaravanMassUsage"
IL_009f: call string Verse.Translator::Translate(string)
IL_00a4: ldc.i4.2
IL_00a5: call void Verse.Messages::Message(string, valuetype Verse.MessageSound)
IL_00aa: ldc.i4.0
IL_00ab: ret
```

That corresponds to your C# code:

```csharp
if (!this.reform && this.MassUsage > this.MassCapacity)
{
	this.FlashMass();
	Messages.Message("TooBigCaravanMassUsage".Translate(), MessageSound.RejectInput);
	return false;
}
```

The instructions evaluate the condition using the stack:

- `ldarg.0` pushes `this`. `ldfld` consumes that instance and pushes its `reform` field value.

- `brtrue` consumes that value and jumps to **IL_00ac** if it is true, skipping the block. The stack is empty again.

- For `this.MassUsage > this.MassCapacity`:
  - `this` onto the stack
  - `MassUsage` call consumes one element and leaves result on stack
  - `this` onto the stack again
  - `MassCapacity` call consumes one element and leaves result on stack
  - `ble.un` consumes both results and jumps to **IL_00ac** when the condition does not hold.

The body follows the same pattern: load values, then consume them with calls and other operations.

**6) IL Code reference**

Overview on Wikipedia:
[Common Intermediate Language](https://en.wikipedia.org/wiki/Common_Intermediate_Language) and [List of CIL instructions](https://en.wikipedia.org/wiki/List_of_CIL_instructions)

Microsoft's [OpCodes reference](https://learn.microsoft.com/en-us/dotnet/api/system.reflection.emit.opcodes) describes each instruction and its stack behavior. Harmony's `CodeInstruction` uses these opcodes.

For the full specification, see [ECMA-335: Common Language Infrastructure](https://ecma-international.org/publications-and-standards/standards/ecma-335/), especially Partition III for instructions.

**7) Method calling**

Instance calls take an extra input: the object to call on. Push it before the method's arguments. Static calls need only their declared arguments.

At the start of the example:

```
IL_0078: ldarg.0
IL_0079: ldfld bool RimWorld.Dialog_FormCaravan::reform
IL_007e: brtrue IL_00ac
```

In this instance method, `ldarg.0` loads `this`, a `Dialog_FormCaravan`.

The `MassUsage` getter consumes that instance and pushes its result:

```
IL_0083: ldarg.0
IL_0084: call instance float32 RimWorld.Dialog_FormCaravan::get_MassUsage()
```

Property accessors are methods named `get_...` or `set_...`. This getter takes no extra arguments. Other instructions can provide the instance too, allowing chains like:

```
ldsfld Foo SomeStaticClass::theFoo
ldfld Bar Foo::theBar
call instance void Bar::Cool()
```

which would be this line in C#:

```csharp
SomeStaticClass.theFoo.theBar.Cool()
```

`ldsfld` pushes the static field `theFoo`; `ldfld` consumes it and pushes `theBar`. The call consumes that instance and returns nothing.

**Keep the stack balanced.** Each instruction needs the right number and types of inputs. Leaving extra values behind, consuming missing values, or joining branches with incompatible stacks produces invalid IL.

A transpiler receives these instructions and returns the edited sequence.

**8) Harmony Transpiler**

The patch class for this example starts with:

```csharp
[HarmonyPatch(typeof(Dialog_FormCaravan))]
[HarmonyPatch("CheckForErrors")]
public static class Dialog_FormCaravan_CheckForErrors_Patch
{
	static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
	{
		// do something
	}
}
```

The name `Transpiler` identifies the patch. It must accept and return `IEnumerable<CodeInstruction>`. Optional `ILGenerator` and `MethodBase` parameters provide the generator and original method.

You can [use `yield`](https://www.kenneth-truyers.net/2016/05/12/yield-return-in-c/) to return instructions one at a time, or edit and return a list.

**9) The patch**

Remove the unwanted block or jump over it. Here it is with the surrounding control flow:

```
IL_0077: ret

IL_0078: ...codes...
IL_007e: brtrue IL_00ac
IL_0083: ...codes...
IL_008f: ble.un IL_00ac
IL_0094: ...codes...
IL_00ab: ret

IL_00ac: ...codes...
```

`IL_0077` ends the previous block. The selected block contains two jumps to `IL_00ac` and ends at `IL_00ab`.

Do not hard-code these offsets: they are byte positions, not instruction indices, and game updates can change them. Instead, use the nearby `ret` instructions to divide the method into sections, then find the selected section by its distinctive message string.

**10) Apply the edit**

Find a section between two `ret` instructions that contains `"TooBigCaravanMassUsage"`. Remove the instructions after the first `ret`, up to and including the second:

[!code-csharp[example](../examples/patching-transpiler.cs?name=caravan)]

Apply this patch class with your Harmony instance, as shown in [Install and apply patches](basics.md#patching-using-annotations).
