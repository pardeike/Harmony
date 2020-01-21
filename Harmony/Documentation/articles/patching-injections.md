# Patching

## Common injected values

Each patch method (except a transpiler) can get all the arguments of the original method as well as the instance if the original method is not static and the return value. Patches only need to define the parameters they want to access.

### Instance

Patches can use an argument named `__instance` to access the instance value if original method is not static. This is similar to the C# keyword `this` when used in the original method.

### Result

Patches can use an argument named `__result` to access the returned value. The type `T` of argument must match the return type of the original or be assignable from it. For prefixes, as the original method hasn't run yet, the value of `__result` is default(T). For most reference types, that would be `null`. If you wish to alter the `__result`, you need to define it by reference like `ref string name`.

### State

Patches can use an argument named `__state` to store information in the prefix method that can be accessed again in the postfix method. Think of it as a local variable. It can be any type and you are responsible to initialize its value in the prefix. It only works if both patches are defined in the same class.

### (Private) Fields

Argument names starting with three underscores, for example `___someField`, can be used to read and (with `ref`) write private fields on the instance that has the corresponding name (minus the underscores).

### Original Method Argument Matching

In order for the original method arguments to be properly matched to the patched method, some restrictions are placed on the types and names of arguments in the patched method:

#### - Argument Types

The type of a given argument (that is to be matched to the argument of the original method) must either be the same type or be `object`.

#### - Argument Names

The name of a given argument (that is to be matched to the argument of the original method) must either be the same name or of the form `__n`, where `n` is the zero-based index of the argument in the orignal method (you can use argument annotations to map to custom names).

### - The original

To allow patches to identify on which method they are attachted you can inject the original methods MethodBase by using an argument named `__originalMethod`.

![note] **You cannot call the original method with that**. The value is only for conditional code in your patch that can selectively run if the patch is applied to multiple methods. The original does not exist after patching and this will point to the patched version.

### - Special arguments

In transpilers, arguments are only matched by their type so you can choose any argument name you like.

An argument of type `IEnumerable<CodeInstruction>` is required and will be used to pass the IL codes to the transpiler
An argument of type `ILGenerator` will be set to the current IL code generator
An argument of type `MethodBase` will be set to the current original method being patched

[note]: https://raw.githubusercontent.com/pardeike/Harmony/master/Harmony/Documentation/images/note.png
