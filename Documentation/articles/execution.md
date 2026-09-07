# Execution Flow

Adding a patch does not replace existing patches. Harmony combines them using its [ordering rules](priorities.md) and rebuilds the replacement whenever patches are added or removed.

**Prefixes** run before the original. Returning `false` skips the original and later prefixes that Harmony considers able to affect it. **Postfixes** run after completion or a skip, but not after an exception.

**Transpilers** edit the original IL in sequence while Harmony builds the replacement. They do not run on each call.

Use a **finalizer** for cleanup on success or failure, or to observe, replace, or suppress exceptions.

The pseudocode below shows the replacement's structure.

### Anatomy of a patched method

##### Without Finalizer patches

Harmony calls prefixes, the (possibly transpiled) original, then postfixes.

After a prefix returns `false`, Harmony still runs prefixes whose signatures it classifies as observation-only. See the [prefix skip rules](patching-prefix.md). Postfixes still run after a skip.

An exception stops this sequence and reaches the caller unless a finalizer handles it.

[!code-csharp[example](../examples/execution_without.cs?name=example)]

##### With Finalizer patches

Finalizers add try/catch handling around this sequence:

For simplicity, Prefix and Postfix patches can be considered part of the Original and are not shown here:

[!code-csharp[example](../examples/execution_with.cs?name=example)]
