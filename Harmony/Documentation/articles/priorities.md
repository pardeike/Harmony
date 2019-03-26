# Priorities

With Harmony, the order of patches is not linear. A plugin/mod that comes last can still execute first if necessary. For this to work, patches can be annotated with method annotations:

* **[HarmonyPriority(int)]**  
	Sets the priority of this Prefix/Postfix. Defaults to Priority.Normal (400)

* **[HarmonyBefore(string[])]**  
	Indicates that this Prefix/Postfix should be executed before any of the ID's given

* **[HarmonyAfter(string[])]**  
	Indicates that this Prefix/Postfix should be executed after any of the ID's given

Example:

Given the following method:

```csharp
class Foo
{
	static string Bar()
	{
		return "secret";
	}
}
```

and **Plugin 1**

```csharp
var harmony = HarmonyInstance.Create("net.example.plugin1");
harmony.PatchAll(Assembly.GetExecutingAssembly());

[HarmonyPatch(typeof(Foo))]
[HarmonyPatch("Bar")]
class MyPatch
{
	static void Postfix(ref result)
	{
		result = "new secret 1";
	}
}
```

and **Plugin 2**

```csharp
var harmony = HarmonyInstance.Create("net.example.plugin2");
harmony.PatchAll(Assembly.GetExecutingAssembly());

[HarmonyPatch(typeof(Foo))]
[HarmonyPatch("Bar")]
class MyPatch
{
	static void Postfix(ref result)
	{
		result = "new secret 2";
	}
}
```

a call to `Foo.Bar()` would return "new secret 2" because both plugins register their Postfix with the same priority and so the second Postfix overrides the result of the first one. As an author of Plugin 1, you could rewrite your code to

```csharp
var harmony = HarmonyInstance.Create("net.example.plugin1");
harmony.PatchAll(Assembly.GetExecutingAssembly());

[HarmonyPatch(typeof(Foo))]
[HarmonyPatch("Bar")]
class MyPatch
{
	[HarmonyAfter(new string[] { "net.example.plugin2" })]
	static void Postfix(ref result)
	{
		result = "new secret 1";
	}
}
```

and would be executed after net.example.plugin2 which gives you (for a Postfix) the chance to change the result last. Alternatively, you could annotate with [HarmonyPriority(Priority.Low)] to come after plugin1.

All priority annotations are also valid on the class. This will define the priorities for all contained patch methods at the same time.