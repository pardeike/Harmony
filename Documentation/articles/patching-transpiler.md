# Patching

## Transpiler

A transpiler is not a patch method that is executed at runtime when the Original method is called. Instead, you can see it more as a post-compiler stage that can alter the source code of the original method. Except that at runtime, it's not C# but IL code that you change.

Use this in the advanced case where a normal Prefix or Postfix won't work and where you want to modify the original method in detail. This is usually done by inserting carefully crafted static method calls or by taking out certain parts of the original or changing values or method calls. In general you want to change as little as possible in a way that is flexible and allows others to use transpilers too so don't depend on fixed counts or structures. Keep it dynamic to ensure future compatibility and co-existance with others.

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

A transpiler is executed only once before the original is run. It can therefore not have access to any runtime state. Harmony will run it once when you patch the method and _again_ every time someone else adds a transpiler for the same methods. Transpilers are chained to produce the final output.

## Basic Transpiler Tutorial

_Note: this tutorial uses the game Rimworld as an example but applies equally to any other game too._

Writing transpilers is like writing a book about how to rewrite a cookbook to make it work for the age of microwave even if that cookbook was written before microwave ovens were invented. It requires good knowledge about the language the cookbook is written in and the topics and information models the cookbook author uses.

As such, writing a transpiler is way more complex and low level than writing a simple method that prefixes, postfixes or replaces an existing method. It is required to have good understanding in general C# programming and how to manipulate abstract data structures with respect to references and relative positioning. It also requires to know the language, in this case CIL, so you can manipulate the instructions without it to get into an illegal state.

In this tutorial a typical case is introduced, first with some practical tips on how to get to the CIL of an existing method and how to read the basics of it. Followed by some general links to CIL releated information and tutorials. The links in chapter 6 are almost mandantory and without the understanding of the topics discussed there, you will find transpilers utterly confusing.

**Tutorial**

Rimworld has a method called `Dialog_FormCaravan.CheckForErrors()`. In this tutorial, the goal is to remove a few lines of code in it that we don't want.

**1) Decompilers: ILSpy or dnSpy**

Get yourself the ILSpy that Zhentar has modified so it generates better code:
[https://github.com/Zhentar/ILSpy/releases](https://github.com/Zhentar/ILSpy/releases) (mad props to **Zhentar**!)

Another free alternative that is quite good and in active development is 0xd4d's [dnSpy](https://github.com/0xd4d/dnSpy)

**2) Decompile**

Start the decompiler and open the rimworld DLL. It is located in
`C:\Program Files (x86)\Steam\steamapps\common\RimWorld\RimWorldWin_Data\Managed\Assembly-CSharp.dll`

**3) The original method**

Now search for `CheckForErrors` and find the one from `RimWorld.Dialog_FormCaravan`, double click it. You should see the source in the window with yellow background.

**4) Viewing IL Code**

I guess you knew those steps already, so lets go into uncharted teritories now. Choose `IL` instead of `C#` in the dropdown menu at the top and you should see something like that:

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

Switch between IL and C# so you get a feeling of how the two roughly compare to each other. Most of the time, the structure is quite similar but sometimes the compiler moves things around, mainly the contents of IF statements where it does switch the logic around and places code at the end of the IL code and jumps to it.

**5) IL Code Basics**

Now this looks scary but it is actually a simple code. Beside the mumbo jumbo at the beginning, each row starting with IL\_ is a one-part or two-parts code. The first part is the **operation** and the optional second part the **value** that operation works with. The value can be a lot of different things: int, Type, FieldInfo, MethodInfo, Label etc.

The cool thing is, that the whole IL code system works like a stack, basically a deck of cards where you only deal with the top by adding or removing a card. Whether you add numbers or call subroutines, almost everything happens on the stack. So lets ignore for now that you don't know jack of the different operations and just look at this section that contains the worlds `reform`, `MassUsage` and `Message` in that order (that's how I found it):

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

Let's analyze:

- `this.reform` - we need the field `reform` from `this`. So we load `this` onto the stack (topmost now is `this`). Then we load the field `reform` onto it: The load field operation will first take the topmost element from the stack (the `this` we just put on it) leaving it empty, then do it's operation and put the result back onto the stack. So now, the stacks topmost element is the field `reform`.

- the `!` on this.reform - the compiler thinks that testing the field for `true` and jumping over the code (to a much later line **IL_00ac**) is the way to go here. Bailing out if this.reform is `true`. The thing to easy overlook is again, that the comparison must consume (remove) the topmost value from the stack to compare it to true so now the stack is empty again!

- now for `this.MassUsage > this.MassCapacity` - this is again the same drill:
  - `this` onto the stack
  - `MassUsage` call consumes one element and leaves result on stack
  - `this` onto the stack again
  - `MassCapacity` call consumes one element and leaves result on stack
    Which leaves us at this point with two elements on the stack so we can call the compare which will consume both elements and jump. The compiler again switched the logic and tests for `<=` which is **ble** (branch when less equal) to the same bail out line as before (**IL_00ac**).

Since we now have completed the IF statement, it is time to do some work. But again, this is just stack operations: putting stuff onto the stack and consuming it which may result into another thing put onto it again.

At this point, I would ask two things: Where can I possibly know all the IL code operations and how they work? And: how does method calling work exactly? Which leads us to:

**6) IL Code reference**

Overview on Wikipedia:
[Common Intermediate Language](https://en.wikipedia.org/wiki/Common_Intermediate_Language) and [List of CIL instructions](https://en.wikipedia.org/wiki/List_of_CIL_instructions)

Microsoft page about the class `OpCodes` (part of Harmonys `CodeInstruction`). Has links to each code and the argument types that Harmony reuses since it has to emit them to create the replacement method:
[OpCodes Class](<https://msdn.microsoft.com/en-us/library/system.reflection.emit.opcodes(v=vs.110).aspx>)

Pretty good tutorial on CodeProject:
[Introduction to IL Assembly Language](https://www.codeproject.com/Articles/3778/Introduction-to-IL-Assembly-Language)

And for all the nitty gritty details in pdf format:
[Common Language Infrastructure (CLI) - Partitions I to VI](http://www.ecma-international.org/publications/files/ECMA-ST/ECMA-335.pdf)

**7) Method calling**

I would start with pointing out the difference between static and non-static methods. Since all you have is the stack and your opcodes, there is no big difference. Remember how to do extension methods where you put "this" as the first argument to a method when you define it? It's a hint that instance methods have an invisible first parameter that represents `this`.

Back to the beginning of our code

```
IL_0078: ldarg.0
IL_0079: ldfld bool RimWorld.Dialog_FormCaravan::reform
IL_007e: brtrue IL_00ac
```

We use `LDARG 0` to load the argument number 0 (zero based it is the first) onto the stack. That is `this`. Which is of type `Dialog_FormCaravan`. So after that operation, the topmost element on our stack is the object instance of type `Dialog_FormCaravan`.

So in order to call an instance method, you have to add the object that this method is called on onto the stack (with ST... opcodes), then all the arguments that method takes. Then you call the method with the CALL opcode. That's what happened in our code when we called `MassUsage`

```
IL_0083: ldarg.0
IL_0084: call instance float32 RimWorld.Dialog_FormCaravan::get_MassUsage()
```

It is a property and you already guessed that their accessor methods start internally with "get*" or "set*". The getter of course takes no extra arguments but you still need to load the object instance onto the stack. In this case "this" but it could of course be anything. This allows for cool chaining of calls. A fake example:

```
ldfld Foo SomeStaticClass::theFoo
ldfld Bar Foo::theBar
call instance void Bar::Cool()
```

which would be this line in C#:

```csharp
SomeStaticClass.theFoo.theBar.Cool()
```

Please note that the first ldfld does not need a stack element since it is a static field. The next ldfld just pops/pushes the stack and the last call will consume that element again to call Cool on it. This leaves the stack empty (or in the same state as before).

Which leads to me saying **the single most important** thing to remember with IL code (CIL as Microsoft calls it):

You mess with the stack you mess with the devil. So never shall you leave unnecessary elements on the stack as I will punish you with errors when you try to compile that shit. Same goes of course for underflows where you put an opcode somewhere where there isn't enough (or the wrong) elements on the stack. The interesting thing here is that since CIL is so simple, the compiler can do most of those checks during compile time!

Now, to make some progress, we switch to Harmony and how to deal with all this. Again, it looks complicated but in essence, it is simple data processing. Since all we do is to rewrite code, we have not access to runtime state. That leaves us with a simple black box: codes in, do something, codes out. That's what Harmony calls a Transpiler.

**8) Harmony Transpiler**

The basic patch looks like this. Let's take our example:

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

The name of the method is `Transpiler` so Harmony knows it is a transpiler. The methods signature must be that it returns `IEnumerable<CodeInstruction> instructions` and the arguments must contain at least one that is of type `IEnumerable<CodeInstruction>` and you can also inject the code generator with `ILGenerator generator` and the original method info with `MethodBase method`.

So how does one define what the transpiler does? I strongly recommend reading up on how to use "yield" with methods returning IEnumerable but you can also go old fashion and convert anything to a list and at the end back to an IEnumerable. Here is a nice tutorial on yield: [https://www.kenneth-truyers.net/2016/05/12/yield-return-in-c/](https://www.kenneth-truyers.net/2016/05/12/yield-return-in-c/)

**9) The patch**

Luckily for us, your change is rather simple. We just need to remove that code or make it not execute. We could do this by "nulling it out" with a `OpCodes.Nop` (no operation) or we add a non-conditional jump somewhere. Or we could remove that section of the code all together. Let's have a look again at our code, this time with some extra rows before and after:

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

I just removed all the codes that do not change execution flow. Starting with 0077 which is the end of the code just above `if (!this.reform && this.MassUsage > this.MassCapacity)`. As we saw before, it jumps twice to 00AC and finally ends with the return at 00AB.

This looks simple. We could replace the codes from 0078 - 00AB with NOPs or we could insert an extra jump right between 0077 and 0078.

Now, normally, I would do this in a way that minimizes the risk of it breaking in case RimWorld changes the code of this particular method. It usually involves finding some anchors that are unique in the code and referencing everything from there. I.e. if we would know the start of 0078 in the above example, I would look for the first jump and use the 00AC I find to determine the end. But the main problem with this part of the code is that it isn't that unique. One could go overkill and match each and every opcode to find the correct sequence but that would break with code changes anyway, so I take the easy way out and just check if we can find the RET at position 0077 then insert an unconditional jump to 00AC.

Here, we have a few choices again. The simplest way would be to count the opcodes from the start of the method and just when the count indicates we are at 0077 (those are not the number of opcodes but the actual byte count that operation starts). A better approach is to find a nice pattern and in fact, we have it. If you look at just the RET codes, you will see that they divide the whole code into sections. We could look for a section containing a call to get_MassUsage() and then remove it from the code. Let's do that.

**11) The execution**

Strategy: _Search for RET codes. For every code found, search until the next RET and look for the usage of the string "TooBigCaravanMassUsage". If found, continue to find the following RET and remove everything from right after the first RET to including the second RET_:

[!code-csharp[example](../examples/patching-transpiler.cs?name=caravan)]

There it is. Add this to your code and use the normal Harmony bootstrapping and you have just done your first Harmony Transpiler!
