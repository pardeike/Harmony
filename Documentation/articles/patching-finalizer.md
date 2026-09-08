# Finalizer

<div id="patching"></div>

A finalizer is a method that makes Harmony wrap the original and all other patches in a try/catch block. It can receive a thrown exception and even suppress it or return a different one.

Unlike a postfix, it also runs when a prefix, the original, or a postfix throws.

Finalizers are commonly used to:

- suppress exceptions
- remap exceptions
- run cleanup on success or failure

See the [runtime flow](patching.md#runtime-flow) for how prefixes, postfixes, and finalizers fit together.

## Suppressing any exceptions

To suppress all exceptions, return `null` from a finalizer with return type `Exception`. This prevents any exception from being rethrown.

[!code-csharp[example](../examples/patching-finalizer.cs?name=suppress)]

## Observing exceptions

To observe an exception without altering it, use a `void` finalizer with `Exception __exception` as a parameter. The special `__exception` parameter will be `null` if no exception occurred.

[!code-csharp[example](../examples/patching-finalizer.cs?name=observe)]

## Changing and rethrowing exceptions

To remap exceptions, return a new exception from the finalizer. This replaces the original exception with a new one.

[!code-csharp[example](../examples/patching-finalizer.cs?name=rethrow)]

## Running cleanup code

Use a finalizer for cleanup that must run on success or failure. A finalizer can run again if one of the finalizers throws, so account for that when releasing resources. See [finalizer execution](execution.md#with-finalizer-patches).

[!code-csharp[example](../examples/patching-finalizer.cs?name=cleanup)]

Finalizers can receive the same injected arguments as postfixes, plus `__exception`.

[note]: ../images/note.png
