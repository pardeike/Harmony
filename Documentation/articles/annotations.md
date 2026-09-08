# Annotations and targets

<div id="annotations"></div>

Annotations describe the original method, your patch methods, and settings such as priority. Usually, each original gets a "patch class" marked with `[HarmonyPatch]`.

`PatchAll()` finds and applies every annotated patch class in an assembly. To apply selected groups, mark classes with `[HarmonyPatchCategory]` and call `PatchCategory()`. `PatchAllUncategorized()` applies classes without a category. `PatchAll()` ignores categories.

A typical patch consists of a class with annotations that looks like this:

[!code-csharp[example](../examples/annotations_basic.cs?name=example)]

The class annotations identify the original. Inside it, define **Prefix**, **Postfix**, **Finalizer**, or **Transpiler** methods. Harmony recognizes these names; use method annotations if you prefer other names.

### Patch classes

**Patch classes** can be public, private, static or not. **Patch methods** can be public or private but **must be static**. Ordinary patches can also use a static [factory method](patching.md#patch-methods) that returns a `DynamicMethod`; Infix patches cannot.

##### Limitations

Declaration order is not execution order. Do not rely on the order of methods or annotations, or on conflicting annotations overwriting each other. Use [priorities](priorities.md) to order patches.

### Annotation alternatives

Mark a patch class with at least one `[HarmonyPatch]` annotation.

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
// form allows for an ArgumentType array defining the kind of each argument
// Normal, Ref, Out or Pointer. Both arrays need to have the same number of elements:
[HarmonyPatch(Type[] argumentTypes, ArgumentType[] argumentVariations)]
```

**Category annotation**

```csharp
// Use with Harmony.PatchCategory()
[HarmonyPatchCategory(string category)]
```

#### Combination annotations

These overloads combine the basic annotations more compactly:

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

To patch method **String.ToUpper()**:

```csharp
[HarmonyPatch(typeof(String))]
[HarmonyPatch("ToUpper")]
```

To patch the setter for a property **Account** in class **MyClass**:

```csharp
[HarmonyPatch(typeof(MyClass))]
[HarmonyPatch("Account", MethodType.Setter)]
```

To patch method **String.IndexOf(char, int)**:

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

Ordinary patches need a constructed generic target, not an open definition. For example, target `TestClass<string>.AddItem` rather than `TestClass<T>.AddItem`. See [generic sharing limits](patching-edgecases.md#generics).

```csharp
[HarmonyPatch(typeof(TestClass<string>), "AddItem")]
```

#### Patching multiple methods

To simplify multiple patches while still using annotations, you can combine annotations with `TargetMethod()` and `TargetMethods()`:

[!code-csharp[example](../examples/annotations_multiple.cs?name=example)]

### Combining annotations

Harmony combines the class and method annotations to identify the target. For example, put `[HarmonyPatch(Type)]` on the class and `[HarmonyPatch(string)]` on a patch method to supply the declaring type and method name separately.

[!code-csharp[example](../examples/annotations_combining.cs?name=example)]
