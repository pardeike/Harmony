# Basics

In order to use Harmony to change the original applications functionality, you need to

1) find a way to excute code inside the application or game (Injection or Mod support)  
2) have the 0Harmony.dll file on disk  
3) reference the 0Harmony.dll from your project to use the API  
4) write patches in your code  
5) create a Harmony instance early in your code  
6) use it to apply the patches you wrote
7) compile your code and make sure 0Harmony.dll is available at runtime (package with your release)

## Runtime dependency

Some games or applications already supply Harmony from either a loader or another mod. While this seems easier, it requires that the version of Harmony you compile against is the same as the one available at runtime or else your code will not run (missing dependency). It also ties the release cycle of that solution to your. Harmony can co-exist in multiple versions with itself so it is totally fine that each user packs their own 0Harmony.dll with their mod.

*Note for application/game makers: it seems you can embed multiple versions of Harmony at once which will avoid the issue described above.*

### Manual dll adding

To add Harmony manually to your Visual Studio project, you right-click on ˋReferencesˋ in your solution explorer and choose ˋAdd Referenceˋ to open the Reference Manager. There, browse for 0Harmony.dll and select add it.

### Adding using nuget

To add Harmony manually to your Visual Studio project, you right-click on ˋReferencesˋ in your solution explorer and choose ˋManage NuGet Packagesˋ, then search for "Harmony Library" and install it.

### Import 

Once you reference Harmony correctly, you should be able to import it by adding ˋusing Harmonyˋ to your imports. That gives you code completion so you can discover the API:

```csharp
using Harmony;
```

## Creating a Harmony instance

Most patch operations require a Harmony instance. To instantiate Harmony, you simply call

```csharp
var harmony = HarmonyInstance.Create("com.company.project.product");
```

The id should be in reverse domain notation and must be unique. In order to understand and react on existing patches of others, all patches in Harmony are bound to that id. This allows other authors to execute their patches before or after a specific patch by referring to this id.

### Patching using annotations

If you prefer annotations to organize your patches, you instruct Harmony to search for them by using PatchAll():

```csharp
var assembly = Assembly.GetExecutingAssembly();
harmony.PatchAll(assembly);
```

or

```csharp
harmony.PatchAll(); // implies current assembly
```

which will search the give assembly for all classes that are decorated with Harmony annotations. All patches are registered automatically and you're done.

### Manual patching

For more control, you use ˋPatch()ˋ. It takes an original and a combination of Prefix, Postfix or Transpiler methods, which are optional HarmonyMethod objects (pass null to ˋPatch()ˋ to skip one type of patch):

```csharp
// add null checks to the following lines, they are omitted for clarity
var original = typeof(TheClass).GetMethod("TheMethod");
var prefix = typeof(MyPatchClass1).GetMethod("SomeMethod");
var postfix = typeof(MyPatchClass2).GetMethod("SomeMethod");

harmony.Patch(original, new HarmonyMethod(prefix), new HarmonyMethod(postfix));
```

The use of an extra HarmonyMethod is to allow for you to define extra properties like priority and such together with the method pointer. HarmonyMethod is the common class shared by manual and annotation patching.

A common mistake here is to fail to retrieve a valid reference for original or your patches resulting in a ˋnullˋ value which when passed to HarmonyMethod will throw an error.

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