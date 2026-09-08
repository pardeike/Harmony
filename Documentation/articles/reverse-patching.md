# Reverse patch

<div id="patching"></div>

A reverse patch copies the original, or part of it, into your own callable stub. Typical uses are:

- call a private method through a stub with a known signature
- call the unmodified original implementation
- extract part of a method using a transpiler

Once installed, you call the stub directly.

![note] A reverse patch is a frozen copy. Use a delegate or reflection if you want calls to follow later patches instead.

## Defining a reverse patch

Mark your stub with `[HarmonyReversePatch]` and identify the original with patch annotations:

Match the original's signature. A static stub for an instance method takes that instance as its first argument.

![note] An instance stub's `this` must have the type expected by the copied IL. An unrelated patch-class instance will not work.

[!code-csharp[example](../examples/reverse-patching.cs?name=example)]

## Types of reverse patches

The `[HarmonyReversePatch]` attribute has two variants:

```csharp
[HarmonyReversePatch(HarmonyReversePatchType.Original)]
[HarmonyReversePatch(HarmonyReversePatchType.Snapshot)]
```

The default is `Original` so you can write `[HarmonyReversePatch]`.

**Original** gives you the unmodified original method as defined in the dll. No patches or transpilers have touched it.

**Snapshot** includes the ordinary transpilers registered at that moment, but no prefixes, postfixes, finalizers, or Infixes. Later patches do not update your stub.

## Changing the content of the original

A **reverse patch transpiler** edits the IL copied into your stub. You can use it to extract just the part you need.

**Example**
Suppose a long method calculates a checksum. Copy it to a `Checksum(...)` stub and use a transpiler to remove the unrelated code.

The remaining IL must match the stub's inputs and output. If it consumes a string and leaves an integer, the stub could be `static int Checksum(string txt)`.

To define a reverse patch transpiler, put a transpiler **into** your stub:

[!code-csharp[example](../examples/reverse-patching.cs?name=transpiler)]

[note]: ../images/note.png
