# Patching

## Common injected values

Each patch method (except a transpiler) can get all the arguments of the original method as well as the instance if the original method is not static and the return value.  

You only need to define the parameters you want to access.

### __instance

Patches can use an argument called **`__instance`** to access the instance value if original method is not static. This is similar to the C# keyword `this` when used in the original method.

### __result

Patches can use an argument called **`__result`** to access the returned value. The type must match the return type of the original or be assignable from it. For prefixes, as the original method hasn't run yet, the value of `__result` is the default for that type. For most reference types, that would be `null`. If you wish to **alter** the `__result`, you need to define it **by reference** like `ref string name`.

### __resultRef

Patches can use an argument called **`__resultRef`** to alter the "**ref return**" reference itself. The type must be `RefResult<T>` by reference, where `T` must match the return type of the original, without `ref` modifier. For example `ref RefResult<string> __resultRef`.

### __state

Patches can use an argument called **`__state`** to store information in the prefix method that can be accessed again in the postfix method. Think of it as a local variable. It can be any type and you are responsible to initialize its value in the prefix. **Note:** It only works if both patches are defined in the same class.

### ___fields

Argument names starting with **three** underscores like **`___someField`** can be used to read/write private fields that have that name minus the underscores. To write to field you need to use the **`ref`** keyword like `ref string ___name`.

### __args

To access all arguments at once, you can let Harmony inject **`object[] __args`** that will contain all arguments in the order they appear. Editing the contents of that array (no ref needed) will automatically update the values of the corresponding arguments.  

**Note:** This way of manipulation comes with some small overhead so if possible use normal argument injection

### method arguments

To access or change one or several of the original methods arguments, simply repeat them with the same name in your patch. Some restrictions are placed on the types and names of arguments in the patched method:

- The type of an injected argument must be assignable from the original argument (or just use `object`)
- The name of a given argument (that is to be matched to the argument of the original method) must either be the same name or of the form **`__n`**, where `n` is the zero-based index of the argument in the orignal method (you can also use argument annotations to map to custom names).

### __originalMethod

To allow patches to identify on which method they are attached to, you can inject the original methods MethodBase by using an argument called **`__originalMethod`**.

![note] **You cannot call the original method with that**. The value is only for conditional code in your patch that can selectively run if the patch is applied to multiple methods. The original does not exist after patching and this will point to the patched version.

### __runOriginal

To learn if the original is/was skipped you can inject **`bool __runOriginal`**. This is a readonly injection to understand if the original will be run (in a Prefix) or was run (in a Postfix).

### Transpilers

In transpilers, arguments are only matched by their type so you can choose any argument name you like.

An argument of type **`IEnumerable<CodeInstruction>`** is required and will be used to pass the IL codes to the transpiler
An argument of type **`ILGenerator`** will be set to the current IL code generator
An argument of type **`MethodBase`** will be set to the current original method being patched

[note]: https://raw.githubusercontent.com/pardeike/Harmony/master/Harmony/Documentation/images/note.png
