# Patching

## Common injected values

Each prefix and postfix can get all the arguments of the original method as well as the instance (if original method is not static) and the return value. Patches can define only those parameters they want to access.

### Instance

Patches can use an argument named `__instance` to access the instance value if original method is not static. This is similar to the C# keyword `this` where used in the original method.

### Result

Patches can use an argument named `__result` to access the returned value. The type `T` of argument must match the return type of the original or be assignable from it. For prefixes, as the original method hasn't run yet, the value of `__result` is default(T). For most reference types, that would be `null`. If you wish to alter the `__result`, you need to pass it by `ref`.

### State

Patches can use an argument named `__state` to store information in the prefix method that can be accessed again in the postfix method. Think of it as a local variable. It can be any type and you are responsible to initialize its value in the prefix.

### (Private) Fields

Argument names starting with three underscores, for example `___someField`, can be used to read and (with `ref`) write private fields on the instance that has the corresponding name (minus the underscores).

### Argument types

Arguments from the original must use the exact same name and type as the original method but using `object` is ok too.

### The original

To allow patch reusing, one can inject the original method by using an argument named `__originalMethod`.

### Special arguments

In transpilers, arguments are only matched by their type so you can choose any argument name you like.

An argument of type `IEnumerable<CodeInstruction>` is required and will be used to pass the IL codes to the transpiler
An argument of type `ILGenerator` will be set to the current IL code generator
An argument of type `MethodBase` will be set to the current original method being patched