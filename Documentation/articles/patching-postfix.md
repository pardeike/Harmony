# Patching

## Postfix

A postfix runs after the original completes or is skipped. It is commonly used to:

- read or change the result of the original method
- access the arguments of the original method
- run even when a prefix skips the original
- read custom state from the prefix

### Reading or changing the result

Use `__result` to read the result, or `ref __result` to change it. Its type must match the original return type or be assignable from it.

[!code-csharp[example](../examples/patching-postfix.cs?name=result)]

### Pass through postfixes

A **pass through** postfix has a non-void return type matching its first parameter's type. Harmony passes the current result to that parameter and uses the returned value as the new result. Other parameters follow normal injection rules.

This is useful for transforming an `IEnumerable<T>` with `yield`, since C# iterator methods cannot have `ref` parameters.

[!code-csharp[example](../examples/patching-postfix.cs?name=passthrough)]

### Reading original arguments

Use names or attributes to access original arguments and private fields. See [Injections](patching-injections.md), or this example:

[!code-csharp[example](../examples/patching-postfix.cs?name=args)]

<a id="postfixes-always-run"></a>

### Postfixes and skipped originals

Skipping the original does not skip postfixes. An exception from a prefix, the original, or an earlier postfix does. Use a [finalizer](patching-finalizer.md) for cleanup that must also run on failure.

### Passing state between prefix and postfix

See [Passing state between prefix and postfix](patching-prefix.md#passing-state-between-prefix-and-postfix).
