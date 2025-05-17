# Patching

## Prefix

A prefix is a method that is executed before the original method. It is commonly used to:

- access and edit the arguments of the original method
- set the result of the original method
- skip the original method and prefixes that alter its input/result
- set custom state that can be recalled in the postfix

![note] The first prefix that returns false will skip all remaining prefixes unless they have no side effects (no return value, no ref arguments) and will skip the original too. Postfixes and Finalizers are not affected.

### Reading and changing arguments

[!code-csharp[example](../examples/patching-prefix.cs?name=args)]

### Changing the result and skipping the original

To change the result of the original, use `__result` as an argument of your prefix. It must match the return type or be assignable from it. Changing the result of the original does not make sense if you let the original run so skipping the original is necessary too.

To stop executing prefixes and skip the original, let the prefix return a bool that returns false. To let the original run after all prefixes, return a bool that returns true.

![note] It is not recommended to skip the original unless you want to completely change the way it works. If you only want a small change or a side effect, using a postfix or a transpiler is always preferred since it allows for multiple users changing the original without each implementation fighting over how the original should behave.

[!code-csharp[example](../examples/patching-prefix.cs?name=skip)]

Here is another example showing the difference between what the original method returns and what the Prefix returns. it illustrate that the boolean return value of the Prefix only determines if the original gets executed or not.

[!code-csharp[example](../examples/patching-prefix.cs?name=skip_maybe)]

### Passing state between prefix and postfix

If you want to share state between your prefix and the corresponding postfix, you can use `__state` (with the `ref` or `out` keyword). If you need more than one value you can create your own type and pass it instead.

![note] This only works if Prefix and Postfix are defined in the same class since Harmony internally uses the declaring type as a key to store the information.

[!code-csharp[example](../examples/patching-prefix.cs?name=state)]

[note]: https://raw.githubusercontent.com/pardeike/Harmony/master/Harmony/Documentation/images/note.png
