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

To add Harmony manually to your Visual Studio project, you right-click on `References` in your solution explorer and choose `Add Reference` to open the Reference Manager. There, browse for 0Harmony.dll and select add it.

### Adding using nuget

To add Harmony manually to your Visual Studio project, you right-click on `References` in your solution explorer and choose `Manage NuGet Packages`, then search for "Harmony Library" and install it.

### Import 

Once you reference Harmony correctly, you should be able to import it by adding Harmony to your imports. That gives you code completion so you can discover the API:

```csharp
using Harmony;
```

## Creating a Harmony instance

Most patch operations require a Harmony instance. To instantiate Harmony, you simply call

```csharp
var harmony = HarmonyInstance.Create("com.company.project.product");
```

The id should be in reverse domain notation and must be unique. In order to understand and react on existing patches of others, all patches in Harmony are bound to that id. This allows other authors to execute their patches before or after a specific patch by referring to this id.

### Debug Log

If you want to know more about the patching or the IL code Harmony produces, you can enable the debug log. Harmony offers and uses a class called `FileLog` that will create a log file on your systems Desktop called "harmony.log.txt".

You can set Harmony's global DEBUG flag to true, which will make Harmony log out many details that can help you while debugging your usage of Harmony:

```csharp
HarmonyInstance.DEBUG = true;
```

You can also use Harmony's file logger in your own code:

```csharp
FileLog.Log("something");
// or buffered:
FileLog.LogBuffered("A");
FileLog.LogBuffered("B");
FileLog.FlushBuffer(); // don't forget to flush
```

### Patching using annotations

If you prefer annotations to organize your patches, you instruct Harmony to search for them by using `PatchAll()`:

```csharp
var assembly = Assembly.GetExecutingAssembly();
harmony.PatchAll(assembly);
```

or

```csharp
harmony.PatchAll(); // implies current assembly
```

which will search the given assembly for all classes that are decorated with Harmony annotations. All patches are registered automatically and Harmony will do the rest.

### Manual patching

For more control, you use `Patch()`. It takes an original and a combination of Prefix, Postfix or Transpiler methods, which are optional HarmonyMethod objects (pass null to `Patch()` to skip one type of patch):

```csharp
// add null checks to the following lines, they are omitted for clarity
var original = typeof(TheClass).GetMethod("TheMethod");
var prefix = typeof(MyPatchClass1).GetMethod("SomeMethod");
var postfix = typeof(MyPatchClass2).GetMethod("SomeMethod");

harmony.Patch(original, new HarmonyMethod(prefix), new HarmonyMethod(postfix));
```

The use of an extra HarmonyMethod is to allow for you to define extra properties like priority and such together with the method pointer. HarmonyMethod is the common class shared by manual and annotation patching.

A common mistake here is to fail to retrieve a valid reference for original or your patches resulting in a `null` value which when passed to HarmonyMethod will throw an error. You can use standard `System.Reflection` to get the MethodInfo of the original and your HarmonyMethods. See the Utilities section for the various ways Harmony can make Reflection easier.

### Checking for existing patches

To get a list of all patched methods in the current appdomain (yours and others), call GetAllPatchedMethods:

```csharp
var originalMethods = HarmonyInstance.GetAllPatchedMethods();
foreach (var method in originalMethods)
{
	//...
}
```

If you are only interested in your own patched methods, use GetPatchedMethods:

```csharp
var myOriginalMethods = harmony.GetPatchedMethods();
foreach (var method in myOriginalMethods)
{
	//...
}
```

If you want to know more about all existing patches (yours or others) on a specific original method, you can call GetPatchInfo:

```csharp
// get the MethodBase of the original
var original = typeof(TheClass).GetMethod("TheMethod");

// retrieve all patches
var patches = HarmonyInstance.GetPatchInfo(original);
if (patches == null) return; // not patched

// get a summary of all different Harmony ids involved
FileLog.Log("all owners: " + patches.Owners);

// get info about all Prefixes/Postfixes/Transpilers
foreach (var patch in patches.Prefixes)
{
	FileLog.Log("index: " + patch.index);
	FileLog.Log("owner: " + patch.owner);
	FileLog.Log("patch method: " + patch.patch);
	FileLog.Log("priority: " + patch.priority);
	FileLog.Log("before: " + patch.before);
	FileLog.Log("after: " + patch.after);
}
```

Sometimes it is necessary to test if another mod is loaded. This is best done by resolving one of their types by name. However, if you want to know if a specific Harmony has applied any patches so far, you can use HasAnyPatches:

```csharp
if(HarmonyInstance.HasAnyPatches("com.some.product"))
{
	//...
}
```

Finally, to retrieve an overview of which assemblies use which version of Harmony you can use (based on actice patches only)

```csharp
var dict = HarmonyInstance.VersionInfo(out var myVersion);
FileLog.Log("My version: " + myVersion);
foreach (var entry in dict)
{
	var id = entry.Key;
	var version = entry.Value;
	FileLog.Log("Mod " + id + " uses Harmony version " + version);
}
```
