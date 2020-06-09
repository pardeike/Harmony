# Annotations

Instead of writing a lot of reflection code you can use annotations to define your original and patch methods in a declarative way. Harmony uses annotations in a hierarchical way on classes and methods in those classes to determine which original methods you want to patch with which patch methods and with which properties like priorities and such.

To simplify things, each original method you want to patch is usually represented by a "patch class", that is, a class that has at least one harmony patch annotation `[HarmonyPatch]`.

When you call harmony.**PatchAll()**, Harmony will search through all classes and methods inside the given assembly looking for specific Harmony annotations.

A typical patch consists of a class with annotations that looks like this:

[!code-csharp[example](../examples/annotations_basic.cs?name=example)]

This example annotates the class with enough information to identify the method to patch. Inside that class, you define a combination of **Prefix**, **Postfix**, **Finalizer** or **Transpiler** methods. Harmony will find them by their name and if you annotate those methods you can even have different names.

### Patch classes

**Patch classes** can be public, private, static or not. **Patch methods** can be public or private but **must be static** since the patched original method does not have any reference to an instance of your patch class. If you use the manual way to specify the patch methods, your patch methods can even be DynamicMethod's.

##### Limitations

The only limitation is that annotations are not ordered (even if they appear so). At runtime, the order of methdos or multiple annotations on something is undefined. The consequence of this is that you cannot rely on order when you define multiple annotations that theoretically could overwrite each other like with normal inheritance. This normally isn't a problem unless you annotate multiple Prefix methods in a class and expect the order of the prefixes to be as in the source code (use priority annotations in this case).

### Annotation alternatives

To indicate that a class contains patch methods it needs to be annotated with at lease one annotations.

#### Basic annotations

Basic annotations need to be combined to define all aspects of your original method:

**Empty annotation**

```csharp
// The empty annotation marks the class as a patch class. Harmony will consider the class and its methods.
[HarmonyPatch]
```

**Class/Type annotation**

```csharp
// Use the type annotation to define the class/type that contains your original method/property/constructor
[HarmonyPatch(Type declaringType)]
```

**Name annotation**

```csharp
// Use the string annotation to define the name of the method or property
[HarmonyPatch(string methodName)]

// or for methods with overloads add an optional argument type array:
[HarmonyPatch(string methodName, params Type[] argumentTypes)]
```

**Method Type annotation**

```csharp
// Defines the type (Method, Getter, Setter, Constructor) to be patched
[HarmonyPatch(MethodType methodType)]
```

**Arguments annotation**

```csharp
// For overloads this defines the argument types of the method/constructor
[HarmonyPatch(Type[] argumentTypes)]

// Since annotations cannot contain code and you cannot use .MakeByRefType(), the second
// form allows for a ArgumentType array defining the type of each argument type
// Normal, Ref, Out or Pointer. Both arrays need to have the same number of elements:
[HarmonyPatch(Type[] argumentTypes, ArgumentType[] argumentVariations)]
```

#### Combination annotations

Beside combining the basic annotations you can also pick from the many combination annotations to express things more compact:

```csharp
[HarmonyPatch(Type, string)]
[HarmonyPatch(Type declaringType, Type[] argumentTypes)]
[HarmonyPatch(Type declaringType, string methodName)]
[HarmonyPatch(Type declaringType, string methodName, params Type[] argumentTypes)]
[HarmonyPatch(Type declaringType, string methodName, Type[] argumentTypes, ArgumentType[] argumentVariations)]
[HarmonyPatch(Type declaringType, MethodType methodType)]
[HarmonyPatch(Type declaringType, MethodType methodType, params Type[] argumentTypes)]
[HarmonyPatch(Type declaringType, MethodType methodType, Type[] argumentTypes, ArgumentType[] argumentVariations)]
[HarmonyPatch(string methodName, Type[] argumentTypes, ArgumentType[] argumentVariations)]
[HarmonyPatch(string methodName, MethodType methodType)]
[HarmonyPatch(MethodType methodType, params Type[] argumentTypes)]
[HarmonyPatch(MethodType methodType, Type[] argumentTypes, ArgumentType[] argumentVariations)]
```

#### Examples

To patch method **String.ToUpper()** :

```csharp
[HarmonyPatch(typeof(String))]
[HarmonyPatch("ToUpper")]
```

To patch the setter for a property **Account** in class **MyClass** :

```csharp
[HarmonyPatch(typeof(MyClass))]
[HarmonyPatch("Account", MethodType.Setter)]
```

To patch method **String.IndexOf(char, int)** :

```csharp
[HarmonyPatch(typeof(String))]
[HarmonyPatch("IndexOf")]
[HarmonyPatch(new Type[] { typeof(char), typeof(int) })]

//or

[HarmonyPatch(typeof(String), "IndexOf", new Type[] { typeof(char), typeof(int) })]
```

#### Constructors

To patch constructors, you use the annotations that contain a `MethodType` argument and set it to `MethodType.Constructor`:

```csharp
// default constructor:
[HarmonyPatch(typeof(TestClass), MethodType.Constructor)]
// or with an overload:
[HarmonyPatch(typeof(TestClass), MethodType.Constructor, new Type[] { typeof(int) })]
// same with multiple rows:
[HarmonyPatch(typeof(TestClass))]
[HarmonyPatch(MethodType.Constructor)]
[HarmonyPatch(new Type[] { typeof(int) })]
```

#### Getters/Setters

To patch a property you use the annotations that contain a `MethodType` argument and set it to `MethodType.Getter` or `MethodType.Setter`:

```csharp
// in one row:
[HarmonyPatch(typeof(TestClass), "GameInstance", MethodType.Getter)]
// in two rows:
[HarmonyPatch(typeof(TestClass))]
[HarmonyPatch("GameInstance", MethodType.Getter)]
```

#### Generic Methods

To patch methods with generic signatures, you need to patch specific versions of the method. It is not possible to patch an open generic method. Example: AddItem(**T** item) cannot be patched directly but you can define one patch for i.e. AddItem(**string** item) and one for AddItem(**int** item):

```csharp
[HarmonyPatch(typeof(TestClass<string>), "AddItem")]
```

#### Patching multiple methods

To simplify multiple patches while still using annotations, you can combine annotations with `TargetMethod()` and `TargetMethods()`:

[!code-csharp[example](../examples/annotations_multiple.cs?name=example)]

### Combining annotations

The combination of those annotations defines the target method. Annotations are **inherited** from class to method so you can use `[HarmonyPatch(Type)]` on the class and `[HarmonyPatch(String)]` on one of its methods to combine both.

[!code-csharp[example](../examples/annotations_combining.cs?name=example)]
