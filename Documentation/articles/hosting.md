# Host Harmony in a runtime

<div id="hosting"></div>

Harmony normally installs a patch with a detour. A small jump at the start of the original method leads into a generated copy that holds the patched code. Some runtimes can do better. They can replace the body of a method in place. The original method itself then runs the patched code, and no jump exists.

`MethodBodyPatching` lets such a runtime, or the mod loader that sits on it, take over installation. Harmony keeps everything else: the patch types, their order, Infix, transpilers, and the whole patch API.

![note] Most readers do not need this page. Patch authors see no change in the API. Read on if you maintain a runtime, a game engine, a mod loader, or a profiler and want Harmony to install patches through you.

## Why a host

A host gains four things over the detour:

- The original method runs the patched code. Profilers, debuggers, and stack traces see the original method, with nothing in between.
- The runtime decides when a new body takes effect, for example at the next call. Nothing rewrites native code.
- A generic method gets one patched body for all of its instantiations. The detour cannot reach instantiations that share code, see [generics](patching-edgecases.md#generics).
- The runtime stays in control of its own rewrite pipeline. Harmony becomes one rewriter among others, and the host can rebuild Harmony's output when something underneath it changes.

RimWorld 1.7 ships such a runtime, and its Harmony Mod is the first host. .NET hot reload and the CoreCLR profiler API have the same shape: they take a complete method body with tokens of the method's own module.

## How it works

Without a host, Harmony reads a method's instructions, builds the patched body, generates a callable copy, and detours the original to that copy.

With a host, Harmony asks the host three questions instead:

1. **Do you own this method?** If not, the method keeps the detour.
2. **What does this method look like without patches?** Harmony builds on that body.
3. **Here is the patched body. Install it.** Harmony hands over the complete body as bytes.

Harmony still constructs the body. It applies transpilers, inserts the prefix, postfix, finalizer, and Infix calls, and validates the result. The host only resolves the tokens inside the body and installs the bytes. Unpatching works the same way: Harmony builds the body with the remaining patches, including none, and the host installs it again. Harmony never asks a runtime to revert anything on its own.

## Register a host

A host registers one delegate per question. Do this once, during startup, before the first patch:

[!code-csharp[host](../examples/hosting.cs?name=host)]

Leave a part out to keep Harmony's default for it: no owned methods, the body from reflection, or the detour.

**Eligibility** must be free of side effects. Decline what your runtime cannot rewrite, such as methods without IL or methods with variable arguments. For a generic method, Harmony asks about the open definition, never about an instantiation.

**Body source** returns the method without any installed patches. Reflection is enough while your runtime leaves `GetMethodBody()` alone. If the method itself now holds the patched body, reflection returns that, so read the baseline elsewhere: from your pipeline's input or from the assembly on disk. The returned `MethodBody` uses tokens of the method's module.

**Installer** receives the original, a callable copy of the patched body, and a writer. Call the writer once, on the same thread, and give it your token importer. It returns a complete CLI method body: the header, the instructions, and the exception clauses. Install those bytes. Throw if the installation fails, and Harmony publishes nothing for that patch. Harmony holds its patch lock while the installer runs, so the installer must not wait for another thread that uses Harmony.

The callable copy is null for an open generic definition. Such a body has no copy that could run, so the installer writes the body into the definition itself.

## Import tokens

A method body refers to types, methods, fields, strings, and signatures. Harmony does not know your runtime's tokens, so the writer asks you for each value:

[!code-csharp[runtime](../examples/hosting.cs?name=runtime)]

| Value | Meaning |
| --- | --- |
| `Type` | A type token. Includes array, pointer, and constructed generic types. |
| `MethodBase` | A method or constructor. This can be a `DynamicMethod`. |
| `FieldInfo` | A field. |
| `string` | A string literal. |
| `byte[]` | A standalone signature, used for locals and `calli`. |
| `KeyValuePair<Type, bool>` with `true` | A generic definition instantiated over its own type parameters. Only definition-wide bodies use it. |

Return `0` for a value you cannot import. For a static method, including a `DynamicMethod`, Harmony then offers a stand-in: a generated static method with the same signature that forwards to the callback. The first stand-in lives in a collectible assembly. If your module cannot reference collectible assemblies, return `0` again, and Harmony offers a stand-in that lives for the process. Any other refusal fails the patch with nothing installed.

## What patch authors notice

Prefix, postfix, finalizer, transpiler, Infix, and reverse patches work as before. Three details differ:

- `Patch()` returns the open definition for a definition-wide target, or null for a constructor of an open generic type, instead of a callable copy.
- Stack traces show the original method. `Harmony.GetOriginalMethod` returns it unchanged.
- Patching a generic definition patches every instantiation. Patching one instantiation, such as `Box<string>.Read`, patches only that instantiation, even where the runtime shares code between instantiations.

## Generics under a host

Harmony installs an open generic definition once, for all instantiations. A patch on a closed instantiation installs through its definition. The definition's body carries the patches of every instantiation. Each instantiation's prefix, postfix, and finalizer run under a type check. An instantiation with a transpiler or an Infix patch gets a closed body of its own, which the definition's body enters under the same check. `__originalMethod` in such a callback is the instantiation.

`MethodBodyPatching.Instantiations(definition)` lists the instantiations installed through a definition. A host that compiles bodies at installation time can compile each of them. An instantiation nobody has used yet compiles at its first call, so invalid instructions for it fail there instead of in `Patch()`.

A definition cannot join instantiations that the detour already holds. Harmony reports that as an error, so register the host before mods patch.

## When the body changes underneath

A rewrite pipeline, a profiler, or a loader can change a method's baseline after Harmony installed its body. Call `MethodBodyPatching.Rebuild(method)` when that happens. Harmony reads the new baseline, builds the body again from its patch records, and installs it through the same installer. Reverse patches rebuild from the recipe Harmony kept for them.

## Rules for a host

- Register once, before the first patch. A method that the detour already holds stays with the detour.
- Register only when the runtime supports body replacement. Check the capability first and keep an escape hatch, such as a launch argument that skips registration, so users can bisect a problem.
- Install synchronously and do not wait for other threads inside the installer.
- If a failed installation leaves a method in an unknown state, refuse further updates to that method and report it.
- Keep the baseline separate from the installed body.
- Validate at installation time if you can. A runtime that applies the body at the next call reports invalid instructions at that call.

## Reference hosts

Harmony's test suite contains two hosts that need no special runtime. Both are good starting points:

- `HarmonyTests/Patching/MethodBodyExportHost.cs` writes each body into a module of its own with `System.Reflection.Metadata` and installs it with Harmony's detour.
- `HarmonyTests/Patching/MethodBodyHotReloadHost.cs` installs bodies into the original methods through .NET hot reload. It runs with `DOTNET_MODIFIABLE_ASSEMBLIES=debug` and a Debug build.

The Harmony Mod for RimWorld 1.7 is the shipped host. It registers a pipeline stage, reads the baseline from the pipeline, validates by compiling, and rebuilds when the game's own tools change a body.

[note]: ../images/note.png
