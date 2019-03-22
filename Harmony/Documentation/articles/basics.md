# Basics

To instantiate Harmony, you simply call

```csharp
var harmony = HarmonyInstance.Create("com.company.project.product");
```

The id should be in reverse domain notation and must be unique. In order to understand and react on existing patches of others, all patches in Harmony are bound to that id.

This allows other authors to execute their patches before or after a specific patch by using the `HarmonyBefore` and `HarmonyAfter` annotations.

Once you have a Harmony instance, you can delegate the search for patch methods to Harmony by calling

```csharp
harmony.PatchAll(Assembly.GetExecutingAssembly());
```

which will search the give assembly for all classes that are annotated with Harmony annotations. All patches are registered automatically and you're done.

### Manual patching

For more control, you can patch like this:

```csharp
var harmony = HarmonyInstance.Create("com.company.project.product");
var original = typeof(TheClass).GetMethod("TheMethod");
var prefix = typeof(MyPatchClass1).GetMethod("SomeMethod");
var postfix = typeof(MyPatchClass2).GetMethod("SomeMethod");
harmony.Patch(original, new HarmonyMethod(prefix), new HarmonyMethod(postfix));
```

### Checking for existing patches

To get a list of already patched methods, you call

```csharp
var harmony = HarmonyInstance.Create("com.company.project.product"); 
var methods = harmony.GetPatchedMethods();
foreach (var method in methods)
{
	//...
}
```

If you want to know if a specific method is already patched, you can call `HarmonyInstance.IsPatched(MethodInfo)`:

```csharp
var harmony = HarmonyInstance.Create("com.company.project.product"); 
var original = typeof(TheClass).GetMethod("TheMethod");
var info = harmony.IsPatched(original);
if (info == null) return; // not patched
foreach (var patch in info.Prefixes)
{
	Console.WriteLine("index: " + patch.index);
	Console.WriteLine("index: " + patch.owner);
	Console.WriteLine("index: " + patch.patch);
	Console.WriteLine("index: " + patch.priority);
	Console.WriteLine("index: " + patch.before);
	Console.WriteLine("index: " + patch.after);
}
foreach (var patch in info.Postfixes)
{
	//...
}
foreach (var patch in info.Transpilers)
{
	//...
}
// all owners shortcut
Console.WriteLine("all owners: " + info.Owners);
```