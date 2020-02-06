# Priorities

With Harmony, the order of patches is not linear. A plugin/mod that comes last can still add patches that execute first. For this to work, patches need to be annotated with method annotations:

- **[HarmonyPriority(int)]**
  Sets the priority of this Prefix/Postfix. Defaults to Priority.Normal (400)

- **[HarmonyBefore(string[])]**
  Indicates that this Prefix/Postfix should be executed before any of the ID's given

- **[HarmonyAfter(string[])]**
  Indicates that this Prefix/Postfix should be executed after any of the ID's given

Example:

Given the following method:

[!code-csharp[example](../examples/priorities.cs?name=foo)]

and **Plugin 1**

[!code-csharp[example](../examples/priorities.cs?name=plugin1)]

and **Plugin 2**

[!code-csharp[example](../examples/priorities.cs?name=plugin2)]

a call to `Foo.Bar()` would return "new secret 2" because both plugins register their Postfix with the same priority and so the second Postfix overrides the result of the first one. As an author of Plugin 1, you could rewrite your code to

[!code-csharp[example](../examples/priorities.cs?name=plugin1b)]

and would be executed after net.example.plugin2 which gives you (for a Postfix) the chance to change the result last. Alternatively, you could annotate with [HarmonyPriority(Priority.Low)] to come after plugin1.

All priority annotations are also valid on the class. This will define the priorities for all contained patch methods at the same time.
