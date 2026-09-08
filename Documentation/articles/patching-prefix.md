# Prefix

<div id="patching"></div>

A prefix is a method that is executed before the original method. It is commonly used to:

- access and edit the arguments of the original method
- set the result of the original method
- skip the original method and prefixes that alter its input/result
- set custom state that can be recalled in the postfix

![note] Returning `false` skips the original and later prefixes that Harmony considers able to affect it. A prefix returning `bool`, or taking writable or reference-type arguments, normally falls in that group. The injections `__instance`, `__originalMethod`, and `__state` are exceptions to the argument check. Other prefixes still run, as do postfixes and finalizers. This is a signature check, not an analysis of what your code does.

[!include[Patch execution](../includes/patch-flow.md)]

## Reading and changing arguments

[!code-csharp[example](../examples/patching-prefix.cs?name=args)]

## Changing the result and skipping the original

Use `ref __result` to supply a result, then skip the original so it does not overwrite your value. Its type must match the original return type or be assignable from it.

Return `false` to skip the original. Return `true` to let later prefixes decide whether it runs.

![note] Skip the original when you mean to replace its behavior. For small changes, a postfix or transpiler often works better alongside other mods.

[!code-csharp[example](../examples/patching-prefix.cs?name=skip)]

The prefix's boolean return controls execution; it is not the original method's result:

[!code-csharp[example](../examples/patching-prefix.cs?name=skip_maybe)]

## Passing state between prefix and postfix

Set `__state` using `ref` or `out` in a prefix, then read it in a postfix. Use your own type to group several values.

![note] Both patches must be in the same class: Harmony uses that class to identify their shared state.

[!code-csharp[example](../examples/patching-prefix.cs?name=state)]

[note]: ../images/note.png
