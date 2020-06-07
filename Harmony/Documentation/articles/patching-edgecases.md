# Patching

## Edge Cases

Patching at runtime is very flexible. But it comes with its downsides. This section describes edge cases that need workarounds, are hard to solve or sometimes impossible.

### Inlining

This [Article](https://mattwarren.org/2016/03/09/adventures-in-benchmarking-method-inlining) describes the details pretty good. An inlined method is no longer a method and is not called in the normal way. As a result, Harmony cannot patch these methods and your patches will simply be non-functional.

The solution is highly depended on your situation. If you have control over the host application you could run it in debug mode but that would come with a large speed penalty. Beside that, you can only resort to some clever redesign of your patch and find a spot in the calling chain higher up that is not inlined. This sometimes requires mass-patching all occurances of all methods that call the inlined method and patching there (`TargetMethods()` is your friend).

### Calling Base Methods

When the class you want to patch overrides a method in its base class, calling the base implementation with `base.SomeMethod()` does not work as you expected when you call it from your patch code.

[!code-csharp[example](../examples/patching-edgecases.cs?name=example)]

The reason for this is that the resolution of `base.SomeMethod()` happens in your compiler. It will create IL code that targets that specific method. At runtime however, you can't simply use reflections or delegates to call it. They all will be resolved to the overwriting method. The only solution that is known to solve this is to use a `Reverse Patch` that copies the original to a stub of your own that you then can call. See this [gist](https://gist.github.com/pardeike/45196a8b8ef331f38b14e1a7e5ee1782) for an example and a comparison.

### Generics

Methods that contains generics like `Add` in `List<T>.Add(...)` or methods defined in classes that are generic can be patched but you might get unexpected results. Usually you need to patch specific implementations separately, patching each type of T on its own.

Other times, patching such a method can make the runtime call your patch for types of T that you do not expect. The solution for that is to inject `__instance` and check its type and run your patch code conditionally.

### Static Constructors

Static constructors of a class will run as soon as you touch or instantiate that class. That unfortunately means that when Harmony asks for some basic required information for that class, it will trigger the static constructor before the patch happens.

As a result, you cannot patch static constructors unless you plan to run them again (which often defeats the purpose). It also as the side effect that your patches to other methods in such a class will run the constructor at the wrong moment - causing errors. In that case, you need to time the patching so it happens when its ok to run the static constructor or when it already has been triggered.

### Native (External) Methods

A method that has only an external implementation (like a native Unity method) can normally not be patched. Harmony requires access to the original IL code to build the replacement. Thus adding Prefix or Postfix to it does not work. This leaves only one possibility: using a transpiler to create your own implementation. 

As a result, you can patch native methods with a transpiler-only patch that ignores the (empty) input and returns a new implementation that will replace the original. **Beware:** after patching, the original implemenation is lost and you cannot call it anymore, making this approach less useful.

### MarshalByRefObject

Methods inherting from `MarshalByRefObject` are kind of special and patching them and information about how the .NET runtime implements the glue code between managed methods and their jitted assembler code does not exist. Thus special methods like certain types of generics and methods inheriting from MarshalByRefObject are difficult or impossible to patchable.

### Special Classes

Related to the problem with marshalled classes, .NET contains classes like [HttpRequest](https://docs.microsoft.com/en-us/dotnet/api/system.web.httprequest) that exhibit strange side effects when patching methods in them. Sometimes, its necessary to patch some methods with identity patches (no prefix, postfix or transpiler, but still patched) to make patches on other methods in the same class work. Details are sparse and it really depends on your architecture, your .NET version, the runtime environment and the class you are patching. There is no simple solution but sometimes, experimenting gives results.

### Methods With Dead Code

In some environments (like Mono) the runtime poses strict rules about creating methods that should not contain dead code. This becomes problematic when patching methods like the following result in a `InvalidProgramException`:

```csharp
public SomeType MyMethod()
{
    throw new NotImplementedException()
}
```

That method has no `RET` IL code in its body and if you try to patch it, Harmony will generate illegal IL. The only solution to this is to create a `Transpiler` that transpiles the method to a correctly version by creating valid IL. This is also true for adding a Prefix or Postfix to that method. The way Harmony works, the replacement method needs to be valid to add calls to your patches to it.