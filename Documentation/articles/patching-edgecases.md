# Patching

## Edge Cases

Some runtime behavior needs a workaround; some cannot be patched. Here are the common cases.

### Inlining

When the runtime [inlines a method](https://mattwarren.org/2016/03/09/adventures-in-benchmarking-method-inlining), it copies its code into the caller. That call site no longer calls the method, so patching the method does not affect it.

If you control the host, disabling inlining may help. Otherwise, patch a caller that has not been inlined. You may need `TargetMethods()` to cover several callers.

### Calling Base Methods

`base.SomeMethod()` in your patch refers to your patch class's base, not the patched class's base:

[!code-csharp[example](../examples/patching-edgecases.cs?name=example)]

The reason for this is that the resolution of `base.SomeMethod()` happens in your compiler. It will create IL code that targets that specific method. At runtime however, you can't simply use reflections or delegates to call it. They all will be resolved to the overwriting method. The only solution that is known to solve this is to use a `Reverse Patch`, that copies the original to a stub of your own that you then can call. See this [gist](https://gist.github.com/pardeike/45196a8b8ef331f38b14e1a7e5ee1782) for an example and a comparison.

### Generics

Generics can be difficult to patch. In general, expect generic methods and methods of generic classes to be shared between different types of `T` during runtime. This means that by patching one method, the method will be patched for all types of `T`. Depending on the type of generic and your .NET runtime, this can be worked around in a few ways:

*  If the generic includes a value type, such as `int`, in most (but not all) cases, the method will not be shared. Patching a method which uses a value type parameter will patch only that specific method. Conversely, patching a generic with an object type will _not_ patch the value type method, so both may have to be patched.
*  If the method is a non-generic non-static method of a generic class, you can check the generic type using `__instance` (such as `__instance.GetType().DeclaringType.GenericTypeArguments`), and adjust your code's behavior depending on the type.
*  If the method is a generic method of a non-generic class, you may be able to examine the method's arguments, if any argument contains `T`. Also, generic type data will be lost (if `Method<T>` is patched using `Method<string>`, `Method<object>` will become `Method<string>`)
*  If the method is a static non-generic method of a generic class, generic type data will be lost (see above).

### Changing the type returned by a constructor

Constructors initialize an object; they do not choose its type. A C# construction uses IL like:

```
newobj instance void Test::.ctor();
```

And the newobj IL code is described by Microsoft as
> The newobj instruction allocates a new instance of the class associated with ctor and initializes all the fields in the new instance to 0 (of the proper type) or null references as appropriate. It then calls the constructor ctor with the given arguments along with the newly created instance. After the constructor has been called, the now initialized object reference (type O) is pushed on the stack.

By the time the constructor runs, the object already exists. To change what is created, patch the caller's `newobj` operation or replace the value it produces, not the constructor body.

### Static Constructors

Static constructors of a class will run as soon as you touch or instantiate that class. That unfortunately means that when Harmony asks for some basic required information for that class, it will trigger the static constructor before the patch happens.

As a result, you cannot patch static constructors unless you plan to run them again (which often defeats the purpose). It also has the side effect that your patches to other methods in such a class will run the constructor at the wrong moment - causing errors. In that case, you need to time the patching so it happens when it's ok to run the static constructor or when it already has been triggered.

### Native (External) Methods

A native method has no IL body for Harmony to copy, so ordinary prefixes and postfixes do not work. A transpiler-only patch can supply a new body from the empty input. **Beware:** this replaces the native implementation; it does not give you a way to call it.

### MarshalByRefObject

Methods inheriting from `MarshalByRefObject` are kind of special and patching them and information about how the .NET runtime implements the glue code between managed methods and their jitted assembler code does not exist. Thus special methods like certain types of generics and methods inheriting from MarshalByRefObject are difficult or impossible to patch.

### Special Classes

Some framework classes, such as [HttpRequest](https://docs.microsoft.com/en-us/dotnet/api/system.web.httprequest), have runtime-dependent patching quirks. An identity patch (patching with no callbacks or transpilers) on one method has sometimes been needed before patching another in the same class. There is no general workaround; results depend on the runtime, architecture, and class.

### Methods With Dead Code

In some environments (like Mono) the runtime poses strict rules about creating methods that should not contain dead code. This becomes problematic, when patching methods like the following results in a `InvalidProgramException`:

```csharp
public SomeType MyMethod()
{
    throw new NotImplementedException()
}
```

That method has no `RET` IL code in its body and if you try to patch it, Harmony will generate illegal IL. The only solution to this is to create a `Transpiler` that transpiles the method to a correct version by creating valid IL. This is also true for adding a Prefix or Postfix to that method. The way Harmony works, the replacement method needs to be valid to add calls to your patches to it.

### Patching too early: MissingMethodException in Unity

Patching during early Unity startup can throw `MissingMethodException: Attempted to access a missing method` when the target directly or indirectly calls an external UnityEngine method.

In the following example code, patching either `SomeMethod()` or `SomeOtherMethod()` will cause the exception:

[!code-csharp[example](../examples/patching-edgecases.cs?name=early1)]

`UnityEngine.Object.DontDestroyOnLoad()` is an external UnityEngine method:

```csharp
[MethodImpl(MethodImplOptions.InternalCall)]
[GeneratedByOldBindingsGenerator]
public static extern void DontDestroyOnLoad(Object target);
```

Wait until UnityEngine has linked its external methods. One option is to patch after the first scene loads, using `SceneManager.sceneLoaded`:

[!code-csharp[example](../examples/patching-edgecases.cs?name=early2)]
