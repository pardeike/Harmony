# Patching

## Finalizer

A finalizer is a method that makes Harmony wrap the original and all other patches in a try/catch block. It can receive a thrown exception and even suppress it or return a different one.

Unlike a postfix, it also runs when a prefix, the original, or a postfix throws.

Finalizers are commonly used to:

- suppress exceptions
- remap exceptions
- run cleanup on success or failure

### Suppressing any exceptions

To suppress all exceptions, return `null` from a finalizer with return type `Exception`. This prevents any exception from being rethrown.

[!code-csharp[example](../examples/patching-finalizer.cs?name=suppress)]

### Observing exceptions

To observe an exception without altering it, use a `void` finalizer with `Exception __exception` as a parameter. The special `__exception` parameter will be `null` if no exception occurred.

[!code-csharp[example](../examples/patching-finalizer.cs?name=observe)]

### Changing and rethrowing exceptions

To remap exceptions, return a new exception from the finalizer. This replaces the original exception with a new one.

[!code-csharp[example](../examples/patching-finalizer.cs?name=rethrow)]

### Running cleanup code

Finalizers are ideal for cleanup or resource management logic that must execute regardless of success or failure - similar to a `finally` block in standard C#.

[!code-csharp[example](../examples/patching-finalizer.cs?name=cleanup)]

Finalizers can receive the same injected arguments as postfixes, plus `__exception`.

[note]: https://raw.githubusercontent.com/pardeike/Harmony/master/Harmony/Documentation/images/note.png
