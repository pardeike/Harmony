# Reflection and logging

<div id="utilities"></div>

### AccessTools

`AccessTools` simplifies reflection. Common members include:

```csharp
public static readonly BindingFlags all
public static Type TypeByName(string name)
public static FieldInfo Field(Type type, string name)
public static PropertyInfo Property(Type type, string name)
public static MethodInfo Method(Type type, string name, Type[] parameters = null, Type[] generics = null)
public static ConstructorInfo Constructor(Type type, Type[] parameters = null)
public static Type Inner(Type type, string name)
public static Type FirstInner(Type type, Func<Type, bool> predicate)
```

These lookups include public, private, static, and instance members.

### Traverse

`Traverse` chains reflection lookups for fields, properties, and methods:

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

`Traverse` accesses private members, handles static members, and caches lookups. If an intermediate lookup encounters `null`, it propagates `null`.

### FileLog

`FileLog` provides simple file logging. Common methods include:

```csharp
public static void Log(string str)
// Appends str to the log, creating it if needed. The default is harmony.log.txt on the desktop.

public static void Reset()
// Deletes the log file.

public static unsafe void LogBytes(long ptr, int len)
// Same as Log(string str) but logs a hex dump and MD5 hash.
```

#### Environment Variables

See [Controlling FileLog with Environment Variables](basics.md#controlling-filelog-with-environment-variables) for all Harmony environment variables, including `HARMONY_DEBUG`, `HARMONY_NO_LOG`, and `HARMONY_LOG_FILE`.
