# Host Harmony in a runtime

<div id="hosting"></div>

Harmony normally installs a patch with a detour. A jump at the original method's entry leads into a generated copy containing the patched code. A runtime that can replace method bodies can install the patched code into the original method instead.

`MethodBodyPatching` connects that runtime to Harmony's patch construction. Harmony still applies transpilers, inserts callbacks, sorts patches, and validates bindings. The host supplies the baseline, imports metadata tokens, and installs the result.

![note] Read this page if you maintain a runtime, game engine, mod loader, or profiler. Patch authors should also read the [generic callback rules](#generic-callbacks) when targeting a host-owned generic method.

## What a host controls

The host chooses when a replacement takes effect and how it interacts with other runtime rewrites. Harmony does not write a native detour for that installation. The original method runs the patched body, although generated helpers can add calls and stack frames.

A host can install one body for every instantiation of a generic definition. Patches on individual instantiations run under type guards in that body. This requires both host eligibility and a registered installer. Declined targets keep Harmony's ordinary detour behavior, including its [generic limitations](patching-edgecases.md#generics).

## Register a host

Register each required part once during startup, before any target is accepted:

[!code-csharp[host](../examples/hosting.cs?name=host)]

The example needs a runtime implementation and a baseline reader. The reader must use the pipeline input or an original assembly image, separate from installed bodies. The runtime implementation must create real metadata tokens and install bytes. The example does not provide those runtime-specific operations.

An omitted part keeps its default: no owned methods, a reflection baseline, or the detour. Passing null registers nothing and does not remove an existing delegate. A second non-null registration of the same part throws.

Once Harmony pins any target as host-owned, adding another part throws `InvalidOperationException`:

```text
Method-body patching parts cannot be registered after a host-owned target has been pinned.
```

A pin is Harmony's retained choice of installer for a target. Targets already pinned to the detour do not prevent registration, but keep their installer. Register all parts before patching to avoid a partially configured host.

### Eligibility

Keep the eligibility check free of side effects. Decline methods your runtime cannot rewrite, such as methods without IL or methods with unsupported calling conventions.

With an installer, Harmony asks about a closed generic target's fully open definition. It does not ask about that closed instantiation. A partially open target is different: Harmony asks about it and throws if the host accepts it. The exception starts with `Definition-wide installation requires the fully open generic definition: `.

Without an installer, Harmony can ask about a closed instantiation directly. Generic targets then keep ordinary detour behavior. An exception from eligibility fails the attempt without creating a pin.

### Baseline reader

Return a fresh `MethodBody` snapshot without Harmony's installed patches. Its tokens must resolve in the target module and generic context. Harmony can request a constructed view of an owned definition, including a closed instantiation. Return the locals and tokens for that requested view.

Reflection is suitable only while `GetMethodBody()` returns the baseline. If runtime replacement changes reflection's body, read your pipeline input or original assembly image instead. A rebuild must read the latest baseline, not a cached installed body.

### Installer

The installer receives the original, a callable replacement, and a body writer. For a definition-wide installation, the original is the definition and the callable replacement is null.

To export bytes, call the writer once on the installation thread. Supply your token importer. The writer returns a complete CLI method body with its header, instructions, local signature token, and exception sections. A second call, another thread, or a call after installation returns throws.

Harmony's `net35` and `netstandard2.0` builds cannot export bodies, even when loaded on a newer runtime. Calling the writer throws `NotSupportedException` with `Exporting a method body requires a modern Harmony target framework.` This restriction concerns export. An installer can use a non-null callable replacement without invoking the writer.

Install synchronously. If installation fails, throw. Harmony publishes neither the candidate patch records nor their prepared persistent state. Once the registered installer starts, Harmony retains the target's pin and any family pins, even if the installer throws. Construction failures before installation release provisional pins. A failed default-detour attempt also releases its provisional pin.

If failure leaves native state unknown, quarantine that target and refuse further updates. Declining eligibility later cannot move a pinned target to another installer.

### Locking and re-entry

Eligibility and installation run under Harmony's shared patch lock. Baseline reads during patch construction also run under it. Standalone instruction or body reads can invoke the baseline reader outside that lock, concurrently with installation. Keep the reader safe for those concurrent calls.

The synchronous writer and importer run inside installation. A callback holding Harmony's lock must not wait for another thread that uses Harmony. Avoid acquiring a runtime lock before calling Harmony if installation needs that same lock.

The installer must not call patch, unpatch, reverse-patch, or rebuild operations. Harmony rejects re-entry into the same target or generic family with `InvalidOperationException`. Its message starts with `Cannot re-enter Harmony while installing a body for the same target or generic family: `. `Instantiations` remains available during installation.

## Import tokens

The runtime must implement these operations with its own metadata and installation APIs:

[!code-csharp[runtime](../examples/hosting.cs?name=runtime)]

The writer passes the following values to the importer. Return a token valid in the installation module, or `0` if the value or a required dependency cannot bind.

| Value | Meaning |
| --- | --- |
| `Type` | A type token, including arrays, pointers, and constructed generic types. |
| `MethodBase` | A method or constructor, possibly a `DynamicMethod`. |
| `FieldInfo` | A field. |
| `string` | A string literal. |
| `byte[]` | A standalone signature for locals or `calli`, already containing imported tokens. |
| `KeyValuePair<Type, bool>` with `true` | A generic definition instantiated over its own type parameters. |
| `KeyValuePair<MemberInfo, bool>` with `true` | A member on its self-instantiated declaring type, or a generic method instantiated over its own parameters. |

The two markers occur in definition-wide bodies. A bare member retains its ordinary metadata meaning. For the member marker, use the definition's signature. If its declaring type is a generic definition, emit a MemberRef with that type's self-instantiated TypeSpec as its parent. For a generic method, emit a MethodSpec too. A generic method definition uses its own parameters. An already constructed method keeps its arguments.

Check all referenced dependencies, including signature types and constructed generic arguments. Checking only the imported member's assembly is insufficient. The example rejects directly collectible members before token creation. Its runtime implementation must also reject dependencies its module cannot reference.

### Refused calls and stand-ins

For a refused call to a static method without open generic parameters, Harmony offers a stand-in with the same signature. The stand-in forwards through a delegate. This includes calls to `DynamicMethod` callbacks.

The first stand-in is collectible and remains retained while its callback remains live. The host owns the callback's lifetime when it imports the callback directly. If your module cannot reference a collectible assembly, return `0` for the stand-in too. Harmony then offers a permanent stand-in that remains for the process lifetime.

Permanent stand-ins for generated closed helpers reuse one delegate field per original, helper site, and signature. A successful installation updates that field. A failed installation keeps the previous delegate. Older bodies calling the same stand-in also see the latest successfully installed delegate.

Ordinary reflection callbacks use their module, metadata token, declaring type, and method arguments as their identity. Unrelated DynamicMethods retain separate permanent stand-ins. Thus closed-helper growth is bounded by distinct original/site/signature combinations, not by rebuild count. New dynamic callbacks still add permanent entries. Unpatching does not unload permanent assemblies.

Generated open generic helpers cannot use these delegates. If the importer refuses such a helper, Harmony offers a permanent assembly containing its generic context. These assemblies can accumulate across rebuilds. A permanent helper cannot reference a collectible dependency. Harmony rejects that case and names the dependency. Generating generic helpers requires a runtime with collectible `AssemblyLoadContext` support. On .NET Framework and Mono, `Patch()` refuses an Infix patch on a generic method definition: it throws a `HarmonyException` whose inner `NotSupportedException` names this requirement, before installation and without recording the patch.

Other open generic callbacks must bind directly. Harmony cannot make a delegate stand-in for them. A refused `ldtoken`, `ldftn`, or `ldvirtftn` method operand also fails export. Substituting a forwarding method would change its identity. Other unsupported refusals fail export as well.

A host using assembly-name resolution must resolve `HarmonyDynamicMethod_*` stand-ins and `HarmonyGenerated_*` permanent helpers. It must also resolve callback assemblies and all other body dependencies. Preserve exact loaded identities when assembly names overlap.

### Access checks

Runtime access checks still apply to calls in an installed body. A hot reload or ReJIT host must account for private callbacks and Harmony's internal `GenericFamily` guard and conversion helpers. Public callbacks alone do not solve access to those internal helpers.

The module host in the tests emits `IgnoresAccessChecksTo` attributes for its references. The hot reload tests run in Harmony's friend assembly and use callbacks accessible from their targets. Those tests do not prove that an arbitrary application module can call private callbacks or internal helpers. The host runtime or target module needs an appropriate mechanism to ignore those access checks.

## What patch authors notice

The patch API retains its method signatures. Host installation changes these results and limits:

- For a definition-wide method, `Patch()` returns the open definition.
- For a method installed through its generic family, `Patch()` returns the requested instantiation.
- For a constructor installed through a generic family, `Patch()` returns null, whether the constructor is open or closed.
- For other targets, `Patch()` returns the generated callable replacement.
- Frames in an installed body name the original method. `Harmony.GetOriginalMethod` returns that method unchanged.
- Reverse patches into host-owned generic stand-ins are unsupported, including closed instantiations. Harmony rejects them before pinning, with `Reverse patches into host-owned generic targets are not supported: ` followed by the target description.

## Generics under a host

With an installer and an accepted definition, Harmony installs one body for the definition's entire family. Prefixes, postfixes, and finalizers registered on a closed instantiation run under exact type guards.

An instantiation with a transpiler or an Infix patch gets a closed helper body. Harmony builds it from the definition's patches and the instantiation's patches. The definition body calls the helper under that instantiation's guard. The definition's transpilers therefore also run on closed IL and must tolerate substituted types and operands.

The helper call adds one stack frame: caller, definition body, helper. Stack inspection and caller-sensitive code can observe it. Harmony maps the helper back to the instantiation for its stack-frame helpers. `__originalMethod` in instantiation callbacks identifies that instantiation. `__state` stays separate per patch class and registration target, including inside a closed helper.

Within each patch category, the existing priority and before/after sorter merges both records. Record indices can interleave, so definition patches do not always precede instantiation patches. When priority and index are equal, the definition's patch runs first, whichever member started the rebuild.

Removing an instantiation's last transpiler or Infix patch returns it to the guarded family body. Removing persistent Infix state also removes its retained slots and disposal hook.

`MethodBodyPatching.Instantiations(definition)` lists pinned closed instantiations, including the one being installed. A host can compile them after writing the definition. An unused instantiation may compile only at its first call. Invalid instructions for that instantiation can therefore fail after `Patch()` returns.

If a sibling already uses the detour, a new closed target keeps the detour too. An accepted definition cannot join that detour-held family. Harmony reports a conflict instead.

### Generic callbacks

For a generic prefix, postfix, or finalizer, register its generic method definition. It must declare exactly as many generic parameters as the target has in total. Harmony binds declaring-type parameters first, then method parameters. On a closed target, it uses that instantiation's arguments. `MakeGenericMethod` also enforces the callback's constraints.

For any target installed through a definition, Harmony rejects a directly constructed generic callback or a callback on a constructed generic patch class. Shared patch records store module/token identity and cannot preserve those fixed arguments. Use an existing patch factory if a fixed callback instantiation is intentional. Harmony reruns the factory on rebuild and retains its concrete output.

Definition-wide bindings must match the open source or accept every type guaranteed by its constraints. Representable `object` bindings remain supported. A closed binding such as `string value` cannot consume an unconstrained definition-wide `T value`. Unchecked reference binding does not bypass this open-type guarantee.

Instantiation callbacks validate their bindings and passthrough results against closed types. Reference bindings retain the original address. Pointer and byref-like bridges retain their storage. Generic family bridges currently reject function-pointer signatures before installation.

Transpilers remain patch-time functions. Infix callbacks retain their existing nongeneric contract. Generated Infix helpers can carry the outer method's generic context.

## When the baseline changes

If an upstream rewrite changes the baseline, call `MethodBodyPatching.Rebuild(method)` outside installation. Harmony reads the new baseline and rebuilds from its published patch records. Rebuilding a family definition also works when only its instantiations have patch records.

Reverse patches rebuild from their active recipe, including stand-ins using the default detour. A successful ordinary installation supersedes that recipe. A failed installation preserves it. If another participating Harmony copy installs a new reverse patch, the former copy refuses to rebuild that foreign recipe.

A retained pin without patch records or an active recipe cannot rebuild. Harmony throws `InvalidOperationException` starting with `Harmony has no rebuild information for `.

## Reference hosts

The repository contains two test hosts with bounded coverage:

- `HarmonyTests/Patching/MethodBodyExportHost.cs` writes exported bytes into a fresh module with `System.Reflection.Metadata`, then detours the original to it.
- `HarmonyTests/Patching/MethodBodyHotReloadHost.cs` installs bytes into original methods through .NET hot reload. It requires a Debug build and `DOTNET_MODIFIABLE_ASSEMBLIES=debug` before the test process starts.

Their metadata emitter covers the tested shapes. It is not a complete runtime adapter. Review signature modifiers, function pointers, collectible dependencies, assembly resolution, and runtime access checks before adapting it.

`HostingDocumentation` compiles the example and executes its exported bodies through the module emitter. It checks baseline replacement, rebuilding, unpatching, and permanent stand-in selection. It does not prove installation into original methods. The separate hot reload fixture supplies that evidence for its tested cases.

[note]: ../images/note.png
