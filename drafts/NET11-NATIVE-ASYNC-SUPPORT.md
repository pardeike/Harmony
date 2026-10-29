**Yes. There is a credible path to supporting it on .NET 11. It requires coordinated work in Harmony and MonoMod.Core: emit the async body correctly, intercept both calling conventions, and preserve the code needed by suspended invocations.**

The encouraging finding is that **.NET 11 already supports creating native async methods through `MethodBuilder`**. Harmony does not need to wait for .NET 12 to start implementing this. The difficult work is in the detour layer.

I checked your current `v3` branch at `5266eea`, MonoMod’s implementation, and the .NET 11 runtime source. Your [NET11-SUPPORT draft](https://github.com/pardeike/Harmony/blob/5266eeaddd975ef19a54d3528dd14ed14ef16261/drafts/NET11-SUPPORT.md) already records the failing postfix experiment and the rejection guard. That guard is justified by what I found below.

## 1. What actually changes

### The method still has IL, but its return semantics change

A native async method carries `MethodImplAttributes.Async`, value `0x2000`. Its IL contains calls to `AsyncHelpers.Await` and related helpers. There is no new `await` opcode.

The surprising part is the difference between its **declared return type** and what its **IL returns**:

| Declared signature | Value on the evaluation stack at `ret` |
|---|---|
| `Task` or `ValueTask` | Nothing |
| `Task<T>` or `ValueTask<T>` | A `T` |

Thus, copying a native async `Task<int>` body into an ordinary `Task<int>` replacement produces incompatible IL: the body returns an `int`. Await helper calls also require an async method context. This is specified directly in the runtime async design. [Source](https://github.com/dotnet/runtime/blob/main/docs/design/specs/runtime-async.md)

Harmony therefore needs to distinguish the **public return type** from the **async body’s result type** wherever it constructs or analyses that body.

### `DynamicMethod` cannot represent it yet

Microsoft’s issue [dotnet/runtime#118074](https://github.com/dotnet/runtime/issues/118074) is still open and assigned to .NET 12. `DynamicMethod` has fixed implementation flags; it cannot currently be marked `Async`. [Source](https://github.com/dotnet/runtime/issues/118074)

But the runtime’s own **`TypeBuilder_DefineMethod` test** creates a native async method using:

- `AssemblyBuilder` and `TypeBuilder`;
- a `MethodBuilder` returning `Task`;
- `SetImplementationFlags(MethodImplAttributes.Async)`;
- IL calling `AsyncHelpers.Await`.

It then invokes that method with completed and suspending tasks. That provides a concrete emission route on .NET 11. [Runtime test](https://github.com/dotnet/runtime/blob/e67cf04f8f58b0391f8b7a7e1df862b863aa9ed4/src/tests/async/reflection/reflection.cs#L146)

Your existing [`MethodBodyGenerator`](https://github.com/pardeike/Harmony/blob/5266eeaddd975ef19a54d3528dd14ed14ef16261/Harmony/Internal/MethodBodyGenerator.cs) and [`GeneratedAssemblyLoader`](https://github.com/pardeike/Harmony/blob/5266eeaddd975ef19a54d3528dd14ed14ef16261/Harmony/Internal/GeneratedAssemblyLoader.cs) give Harmony a useful starting point. Native async would become another explicit reason to select assembly generation.

## 2. The larger problem: one managed method, two native calling paths

The CLR creates paired representations for eligible `Task`/`ValueTask` methods:

| Representation | Native behaviour |
|---|---|
| Ordinary entry | Returns the declared `Task`/`ValueTask` |
| Async entry | Returns the logical result, with additional continuation input/output |

Optimized native async callers can call the async entry directly, bypassing the ordinary Task-returning entry. The relationship is documented in [`MethodDesc`’s async representation](https://github.com/dotnet/runtime/blob/e67cf04f8f58b0391f8b7a7e1df862b863aa9ed4/src/coreclr/vm/method.hpp#L85).

The async ABI includes a hidden managed `Continuation` argument and a separate continuation return channel. A null input means a fresh invocation; a non-null input means resumption. Consequently, the original async entry needs a destination with the matching async ABI. [CLR ABI](https://github.com/dotnet/runtime/blob/e67cf04f8f58b0391f8b7a7e1df862b863aa9ed4/docs/design/coreclr/botr/clr-abi.md#async)

MonoMod already documents these problems in [`RuntimeAsyncStubTest.cs`](https://github.com/MonoMod/MonoMod/blob/cf017b10175fb87bb194ce125b521dd2e4070d26/src/MonoMod.UnitTest/RuntimeDetour/RuntimeAsyncStubTest.cs). Its currently skipped tests describe:

- a hook intercepting the reflected Task-returning entry;
- a compiled async caller bypassing that hook;
- an original trampoline producing the wrong return representation.

Those are upstream documented cases, rather than tests I executed here.

### There is also a gap worth checking in ordinary Task-method support

**This affects eligible methods without `MethodImpl.Async` too.**

.NET 11 can JIT an async variant directly from the IL of an ordinary Task-returning method. It no longer necessarily forwards that variant through the ordinary entry. Microsoft describes this explicitly in the runtime improvements. [Source](https://github.com/dotnet/core/blob/main/release-notes/11.0/preview/preview6/runtime.md)

Your current [`Ordinary_task_methods_remain_supported` test](https://github.com/pardeike/Harmony/blob/5266eeaddd975ef19a54d3528dd14ed14ef16261/HarmonyTests/Patching/RuntimeAsync.cs) checks ordinary direct calls. **I would add a caller compiled with native async to that test immediately.** Its success through that second path is currently unverified by the test.

The `0x2000` check is useful for selecting how to copy a body. Detour coverage needs a broader classification.

## 3. The easily missed problem: patching while an invocation is suspended

This is the strongest reason to make the detour work an early feasibility test.

The runtime associates each continuation with a **specific native code version**. Its resumption stub calls that saved code address directly. The source explicitly explains that resumption must return to the same version because the continuation belongs to it. [Resumption-stub implementation](https://github.com/dotnet/runtime/blob/e67cf04f8f58b0391f8b7a7e1df862b863aa9ed4/src/coreclr/vm/jitinterface.cpp#L15382)

Consider:

1. Original method starts and suspends.
2. Harmony overwrites its native entry.
3. The original invocation resumes.
4. Its saved continuation reaches the replacement.

The replacement may expect an entirely different continuation layout.

**The invariant has to be: fresh invocations use the replacement; existing continuations resume the code version that created them.**

A plausible implementation is an async entry gate:

| Hidden continuation argument | Destination |
|---|---|
| Null: fresh call | Replacement’s async entry |
| Non-null: resumption | Preserved original code-version trampoline |

That is a proposed design, not a validated fix. It needs correct argument placement, GC/unwind behaviour and code lifetime handling.

There is an architecture-specific wrinkle for your Macs: MonoMod’s current [`Arm64Arch`](https://github.com/MonoMod/MonoMod/blob/cf017b10175fb87bb194ce125b521dd2e4070d26/src/MonoMod.Core/Platforms/Architectures/Arm64Arch.cs) has no alternate-entry trampoline factory. A solution based on preserving original native entry points would therefore need additional ARM64 work.

## 4. The Harmony implementation I would choose

I would preserve the existing patch contract using **two logical layers**.

### A. Emit a genuine native async copy of the original body

This method would:

- retain `MethodImpl.Async`;
- retain the declared `Task`/`ValueTask` signature;
- preserve native async IL return semantics;
- receive the transpiler and applicable Infix transformations;
- be emitted into an assembly.

The runtime would handle suspension and continuation layout for this new method.

### B. Wrap that body with Harmony’s ordinary patch semantics

Schematically, omitting the existing skip-original and finalizer scaffolding:

```csharp
Task<int> Patched(int value)
{
    Prefix(...);

    // Ordinary call to the emitted native async method.
    Task<int> result = NativeAsyncBodyCopy(value);

    Postfix(ref result, ...);
    return result;
}
```

That preserves the behaviour patch authors already rely on:

- `__result` remains `Task<int>`.
- A prefix can skip the original and supply a task.
- A postfix can replace the task.
- An ordinary caller receives that replacement task.
- Postfixes run when the task is returned.
- Finalizers retain their existing invocation scope.

Putting ordinary prefix/postfix/finalizer machinery directly into the native async body would change the result to logical `int` and move trailing callbacks to operation completion. That could be a useful explicit feature, but it needs its own semantics.

For dispatch, I would initially make the **wrapper itself an ordinary metadata-backed method**, then use the runtime-generated async variant of that wrapper for native async callers. The runtime already supports generating async variants for ordinary Task-returning methods. This also gives it responsibility for the context behaviour involved in awaiting the wrapper’s returned task. [Runtime representation](https://github.com/dotnet/runtime/blob/e67cf04f8f58b0391f8b7a7e1df862b863aa9ed4/src/coreclr/vm/method.hpp)

This is the design I would prototype. It may materialize tasks that an unpatched native async call could avoid; that is a reasonable initial compatibility cost.

## 5. What needs changing where

| Area | Required work |
|---|---|
| **Harmony body construction** | Separate the copied async body from the ordinary patch wrapper. Track declared return type and logical body result independently. |
| **Harmony emission** | Preserve the Async implementation flag and deliberately select assembly-backed generation for native async bodies and wrappers needing paired entries. |
| **Harmony target resolution** | Define native async body targeting explicitly. There is no compiler-generated `MoveNext` to return from existing state-machine resolvers. |
| **Harmony Infix integration** | Handle state across runtime suspension and audit assumptions about generated fields, builder calls and byref locals. |
| **MonoMod runtime adapter** | Locate and distinguish ordinary/async variants, including already compiled variants. |
| **MonoMod detours** | Select ABI-compatible destinations and handle initial entry versus resumption. |
| **MonoMod compilation tracking** | Keep variants and native code versions distinct through tiering, repatching and unpatching. |
| **Lifetime management** | Keep code and callback dependencies valid while older invocations remain suspended. |

There is a particularly concrete MonoMod change here. Its JIT notification already carries the raw `RuntimeMethodHandle`, but [`PlatformTripleDetourFactory`](https://github.com/MonoMod/MonoMod/blob/cf017b10175fb87bb194ce125b521dd2e4070d26/src/MonoMod.Core/Platforms/PlatformTripleDetourFactory.cs#L375) chooses related detours using the normalized `MethodBase`, ignoring that raw handle.

The CLR deliberately maps async variants back to the ordinary method when producing reflection objects. [Runtime reflection handling](https://github.com/dotnet/runtime/blob/e67cf04f8f58b0391f8b7a7e1df862b863aa9ed4/src/coreclr/vm/runtimehandles.cpp)

**That distinction must survive compilation notifications.** Otherwise an async compilation can be mistaken for another ordinary compilation and redirected to the wrong ABI. This is a source-derived hazard; I have not reproduced that particular failure.

### Infix deserves a separate pass

Native async makes the logical body directly available, which is promising for transpilers and Infixes. The current state-machine-specific machinery needs adaptation, though: [`PersistentStatePlan`](https://github.com/pardeike/Harmony/blob/5266eeaddd975ef19a54d3528dd14ed14ef16261/Harmony/Internal/PersistentStatePlan.cs) identifies generated state machines and rewrites their suspension bookkeeping.

The native async rules allow appropriate locals to survive suspension, but byref/ref-like/pinned locals cannot be treated that way. Suspension is also prohibited inside exception-handler blocks. Those restrictions need to be respected by injected IL. [Source](https://github.com/dotnet/runtime/blob/main/docs/design/specs/runtime-async.md)

Existing transpilers that depend on a particular compiler-generated `MoveNext` layout will still need adaptation.

## 6. The smallest useful implementation sequence

I would use these as successive architectural gates.

### 1. Prove body emission independently

Copy one native async `Task<int>` method into an emitted assembly. Verify synchronous completion, real suspension, a result and an exception.

Use a `TaskCompletionSource` to control suspension deterministically. This isolates IL/emission problems from detouring.

### 2. Prove both calling paths

Redirect the original to a known replacement, and call it from:

- an ordinary caller;
- a compiler-generated async caller;
- a native async caller.

Test cold and already warmed callers/targets. Repeat with an **ordinary Task-returning target lacking the Async flag**.

This establishes whether MonoMod can find and intercept the representations that matter.

### 3. Prove suspension survives patch changes

The decisive test:

1. Start the original and hold it at an await.
2. Install the patch.
3. Verify a new call uses the replacement.
4. Release the original invocation.
5. Verify it completes correctly through its original code.

Then repeat with repatching and unpatching while replacement invocations are suspended. Passing an awaitless async test does not establish this property.

### 4. Integrate Harmony semantics and expand coverage

Verify exact task replacement, skip-original, callback timing and exception behaviour. Then extend across:

- `Task`, `Task<T>`, `ValueTask`, `ValueTask<T>`;
- instance/generic methods and large logical struct results;
- virtual/interface calls;
- cancellation, `AsyncLocal`, synchronization contexts and `ConfigureAwait`;
- tiered compilation, ReadyToRun and code collection;
- x64 and ARM64.

These cases exercise different ABI and lifecycle assumptions, so they belong in the acceptance criteria.

## My assessment

**The emission problem has an established solution on .NET 11. The main engineering uncertainty is safe, complete native dispatch through MonoMod.** Harmony’s existing assembly fallback and patch construction code should be reusable once that foundation works.

I would start with the ordinary-Task/native-caller regression test and a small MonoMod prototype covering both variants plus **suspend → patch → resume**. Those results will tell you much more about the remaining scope than removing Harmony’s rejection guard.

Waiting for .NET 12’s `DynamicMethod` support may simplify emission later; it does not resolve the two calling paths or continuation-version problem. Also, compiling an application with runtime async disabled cannot remove native async from the already compiled .NET 11 runtime libraries. [Source](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-11/runtime)

**Verification boundary:** this is a source-level feasibility assessment, supported by your recorded reproduction and upstream code/tests. I have not executed a new prototype here because this environment has no .NET SDK/runtime.
