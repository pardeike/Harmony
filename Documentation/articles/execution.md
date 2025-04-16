# Execution Flow

Patching a method does not override any previous patches that other users of Harmony apply to the same method. Instead, **Prefix**, **Postfix**, **Transpiler** and **Finalizer** patches are executed around and inside code from the original method in an adaptive and prioritised way.

**Prefix** patches can return a boolean that, if false, skips prefixes that alter the result of the original and skips the execution of the original method too. In contrast, **Postfix** patches are executed all the time except when the the original or any patch to it throws an exception.

All the original code is inserted into the replacement method together with all calls to all patches. The original code can be changed by **Transpiler** patches that are applied to the code in sequence before its inserted.

If you need guaranteed execution after the original or want to catch exceptions or alter thrown exceptions, you use a **Finalizer** patch.

The overall structure of the replacement method (original after patching) can be explained best by looking at a pseudo code example of the method that will replace the original anytime someone adds or removes a patch:

### Anatomy of a patched method

##### Without Finalizer patches

The basic logic of a Harmony replacement method consists of calling all Prefix methods first, then calling the (possibly transpiled) original and then all Postfix methods.

To skip the Original, a prefix can return `false` to skip the Original and all other Prefix methods that come after it and that have some effect on the Original.

Prefix methods that return `void` and have no `ref` arguments are considered side effect free and are always run, regardless of their position. Postfix methods are always executed.

Exceptions thrown in a Prefix, a Postfix or in the modified Original method will not be caught by default and will reach the caller of the Original method. If you want to handle exceptions, you need to use Finalizer patches.

[!code-csharp[example](../examples/execution_without.cs?name=example)]

##### With Finalizer patches

Normally, Harmony does not introduce the overhead of try/catch to the replacement method. When you start adding Finalizer patches the overall logic becomes a lot more complicated which is illustrated in the following pseudeo code example.

For simplicity, Prefix and Postfix patches can be considered part of the Original and are not shown here:

[!code-csharp[example](../examples/execution_with.cs?name=example)]
