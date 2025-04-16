# Utilities

### AccessTools

To simplify reflections, Harmony has a helper class called AccessTools. Here are the most commonly used methods:

```csharp
public static BindingFlags all = ....
public static Type TypeByName(string name)
public static FieldInfo Field(Type type, string name)
public static PropertyInfo Property(Type type, string name)
public static MethodInfo Method(Type type, string name, Type[] parameters = null, Type[] generics = null)
public static ConstructorInfo Constructor(Type type, Type[] parameters = null)
public static Type Inner(Type type, string name)
public static Type FirstInner(Type type, Func<Type, bool> predicate)
```

Any of these methods use the **all** BindingFlags definition and thus work on anything regardless if it is public, private, static or else.

### Traverse

In order to access fields, properties and methods from classes via reflection, Harmony contains a utility called Traverse. Think of it as LINQ for classes. Here are the main methods:

```csharp
// starting from a type or instance
public static Traverse Create(Type type)
public static Traverse Create<T>()
public static Traverse CreateWithType(string name)

// digging deeper
public Traverse Type(string name)
public Traverse Field(string name)
public Traverse Property(string name, object[] index = null)
public Traverse Method(string name, params object[] arguments)
public Traverse Method(string name, Type[] paramTypes, object[] arguments = null)

// calling getter or method
public object GetValue()
public T GetValue<T>()
public object GetValue(params object[] arguments)
public T GetValue<T>(params object[] arguments)
public override string ToString()

// calling setter
public Traverse SetValue(object value)

// iterating
public static void IterateFields(object source, Action<Traverse> action)
public static void IterateFields(object source, object target, Action<Traverse, Traverse> action)
public static void IterateProperties(object source, Action<Traverse> action)
public static void IterateProperties(object source, object target, Action<Traverse, Traverse> action)
```

Example:

[!code-csharp[example](../examples/utilities.cs?name=example)]

Although most fields, properties and methods in that class hierarchy are private, Traverse can easily access anything. It has build-in null protection and propagates null as a result if any of the intermediates would encounter null. It works with static types and caches lookups which makes it pretty fast.

### FileLog

For simple and quick logging, Harmony uses a tool class FileLog. It has three methods:

```csharp
public static void Log(string str)
// Creates a new log file called "harmony.log.txt" on the computers Desktop (if it not already exists) and appends *str* to it. 

public static void Reset()
// Deletes the log file.

public static unsafe void LogBytes(long ptr, int len)
// Same as Log(string str) but logs a hex dump and md5 hash.
```