# Basics

To use Harmony:

1. find a way to run code inside the application (a loader or mod support)
2. get 0Harmony.dll
3. reference it from your project
4. write patches in your code
5. create a Harmony instance early in your code
6. apply your patches
7. compile and make sure Harmony is available at runtime

## Runtime dependency

Some loaders or games already supply Harmony. Compile against APIs available in the version they load. Harmony supports multiple versions in one process, but the runtime's assembly-loading rules determine which DLL your code uses; bundling one does not guarantee it will be loaded.

### Manual dll adding

In Visual Studio, right-click `References`, choose `Add Reference`, and browse to 0Harmony.dll.

### Adding using nuget

In Visual Studio, right-click `References`, choose `Manage NuGet Packages`, and install `Lib.Harmony`.

### Import

Import the namespace to use Harmony's API:

[!code-csharp[example](../examples/basics.cs?name=import)]

## Creating a Harmony instance

Most patch operations need a Harmony instance:

[!code-csharp[example](../examples/basics.cs?name=create)]

Use a unique ID, preferably in reverse-domain notation. It identifies your patches and lets other authors order theirs before or after yours.

### Debug Log

Set `Harmony.DEBUG = true` to log patching details and generated IL. `FileLog` writes to `harmony.log.txt` on your Desktop by default:

[!code-csharp[example](../examples/basics.cs?name=debug)]

You can also use Harmony's file logger in your own code:

[!code-csharp[example](../examples/basics.cs?name=log)]

#### Controlling FileLog with Environment Variables

FileLog can be configured using environment variables:

- **`HARMONY_NO_LOG`**: Set to any non-empty value to disable file logging entirely.
- **`HARMONY_LOG_FILE`**: Set to a custom file path to change where the log file is written.

Example:
```bash
# Disable logging
export HARMONY_NO_LOG=1

# Or use a custom log path
export HARMONY_LOG_FILE=/path/to/harmony.log.txt
```

### Patching using annotations

Use `PatchAll()` to find and apply annotated patch classes in an assembly:

[!code-csharp[example](../examples/basics.cs?name=patch_annotation)]

For groups applied at different times, mark classes with `[HarmonyPatchCategory]`. Use `PatchCategory()` for a selected group and `PatchAllUncategorized()` for classes without a category. `PatchAll()` ignores categories and applies them all.

### Manual patching

For manual control, `Patch()` takes the original and optional prefix, postfix, transpiler, and finalizer methods, each wrapped in a `HarmonyMethod`:

[!code-csharp[example](../examples/basics.cs?name=patch_manual)]

`HarmonyMethod` holds the patch method and settings such as priority. Manual and annotation patching share these settings.

[!code-csharp[example](../examples/basics.cs?name=patch_method)]

Check that reflection found the original and patch methods: passing a missing (`null`) method causes an error. [AccessTools](utilities.md#accesstools) can simplify these lookups.

### Checking for existing patches

`GetAllPatchedMethods()` lists all patched methods in the current AppDomain:

[!code-csharp[example](../examples/basics.cs?name=patch_getall)]

`GetPatchedMethods()` lists methods patched by your Harmony instance:

[!code-csharp[example](../examples/basics.cs?name=patch_get)]

`GetPatchInfo()` describes everyone's patches on a method:

[!code-csharp[example](../examples/basics.cs?name=patch_info)]

To detect another mod, look up one of its types by name. To check whether a Harmony ID has registered patches, use `HasAnyPatches()`:

[!code-csharp[example](../examples/basics.cs?name=patch_has)]

To see Harmony versions used by assemblies with active patches:

[!code-csharp[example](../examples/basics.cs?name=version)]

### Unpatching

Unpatching removes selected registrations and rebuilds the method from its original IL and remaining patches. Removing every patch leaves a Harmony replacement with the original behavior.

You can remove all patches belonging to one Harmony ID, or everyone's patches:

[!code-csharp[example](../examples/basics.cs?name=unpatch)]

Or remove specific patches:

[!code-csharp[example](../examples/basics.cs?name=unpatch_one)]
