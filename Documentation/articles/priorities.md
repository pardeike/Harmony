# Ordering and priorities

<div id="priorities"></div>

Load order is not execution order. A mod loaded last can add a patch that runs first. Use these annotations to control ordering:

- **[HarmonyPriority(int)]**
  Sets this patch's priority. Defaults to `Priority.Normal` (400).

- **[HarmonyBefore(string[])]**
  Runs this patch before patches owned by any of the given Harmony IDs.

- **[HarmonyAfter(string[])]**
  Runs this patch after patches owned by any of the given Harmony IDs.

Example:

Given the following method:

[!code-csharp[example](../examples/priorities.cs?name=foo)]

and **Plugin 1**

[!code-csharp[example](../examples/priorities.cs?name=plugin1)]

and **Plugin 2**

[!code-csharp[example](../examples/priorities.cs?name=plugin2)]

a call to `Foo.Bar()` would return "new secret 2" because both plugins register their Postfix with the same priority and so the second Postfix overrides the result of the first one. As an author of Plugin 1, you could rewrite your code to

[!code-csharp[example](../examples/priorities.cs?name=plugin1b)]

This runs after `net.example.plugin2`, changing the result last. Alternatively, `[HarmonyPriority(Priority.Low)]` puts Plugin 1 after Plugin 2's normal-priority postfix.

All priority annotations are also valid on the class. This will define the priorities for all contained patch methods at the same time.
