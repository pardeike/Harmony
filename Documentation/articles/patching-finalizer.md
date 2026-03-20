# Patching

## Finalizer

A finalizer is a method that makes Harmony wrap the original and all other patches in a try/catch block. It can receive a thrown exception and even suppress it or return a different one.

It is a very good candidate for code that has to run regardless of what happens. Its counterpart is a Prefix with no side effects (void return type and no ref/out arguments). These are never skipped and thus serve as a way to run code guaranteed at the start of a method.

Finalizers are commonly used to:

- suppress exceptions
- remap exceptions
- make sure your code is always executed

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

Beside their handling of exceptions they can receive the same arguments as Postfixes.

[note]: https://raw.githubusercontent.com/pardeike/Harmony/master/Harmony/Documentation/images/note.png
