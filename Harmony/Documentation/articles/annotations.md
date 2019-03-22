# Annotations

When you use the **PatchAll(Assembly assembly)** call, Harmony will search through all classes and methods inside the given **assembly** looking for specific Harmony annotations.

### Example

A typical patch consists of a class with annotations that looks like this:

```csharp
[HarmonyPatch(typeof(SomeTypeHere))]
[HarmonyPatch("SomeMethodName")]
class MyPatchClass
{
	static void Postfix(...)
	{
		//...
	}
}
```

This will annotate the class with enough information to identify the method to patch. Usually, you will have one class for each method that you want to patch. Inside that class, you define a combination of **Prefix**, **Postfix** or **Transpiler** methods. Harmony will find them by their name. If you annotate those methods you can even have different names.

### Annotation types

To indicate that a class contains patch methods it needs to be annotated with at lease one of the following class annotations:
	
* **[HarmonyPatch(Type, Type[])]**
	Defines the type that contains the method to be patched (optional Type[] for generics)

* **[HarmonyPatch(String)]**
	Defines the method to be patched by name

* **[HarmonyPatch(String, PropertyMethod)]**
	Defines the property to be patched by name

* **[HarmonyPatch(Type[])]**
	Defines the parameters of the method to be patched (only necessary if multiple methods with the same name exist)

Additionally to repeating the basic annotations, the following shortcut can be used:

* **[HarmonyPatch(Type, String, Type[])]**
	Defines the type and method to be patched in a single annotation

### Combining annotations

The combination of those annotations defines the target method. Examples:

To patch method **String.ToUpper()** :

```csharp
[HarmonyPatch(typeof(String))]
[HarmonyPatch("ToUpper")]
```

To patch the setter for a property **Account** in class **MyClass** :

```csharp
[HarmonyPatch(typeof(MyClass))]
[HarmonyPatch("Account", PropertyMethod.Setter)]
```

To patch method **String.IndexOf(char, int)** :

```csharp
[HarmonyPatch(typeof(String))]
[HarmonyPatch("IndexOf")]
[HarmonyPatch(new Type[] { typeof(char), typeof(int) })]

//or

[HarmonyPatch(typeof(String), "IndexOf", new Type[] { typeof(char), typeof(int) })]
```

Patch classes can be public or private, static or not. Patch methods can be public or private but must be static since the patched original method does not have any reference to an instance of your patch class. If you use the manual way to specify the patch methods, your patch methods can even be DynamicMethod's.

### Constructors

To patch constructors, do not use the method name ".ctor". Instead, omit the method name completely and only specify the argument types. Example:

```cshapr
[HarmonyPatch(typeof(TestClass))]
[HarmonyPatch(new Type[] { })]
```

### Generic Methods

To patch methods with generic signatures, you need to patch specific versions of the method. It is not possible to patch an open generic method. Example: AddItem(**T** item) cannot be patched directly but you can define one patch for i.e. AddItem(**string** item) and one for AddItem(**int** item). Pro tip: to patch a large number of variations, create your patches dynamically.