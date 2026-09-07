# Infix and instruction edits

Use an Infix for prefixes, postfixes or finalizers around a selected operation. To insert, replace or remove instruction patterns, use a [transpiler](patching-transpiler.md) with [CodeMatcher](patching-transpiler-matcher.md).

Transpilers run in their usual order. Infix selects from their finished output, before inserting its own code. Even `Priority.Last` transpilers run before Infix.

These examples are compiled and tested. `Probe.Tick()`, `Before()`, `After()`, `Replacement()`, `Enter()` and `Exit()` are static, take no arguments and return void. Each records its name. `Recipes.Method(name)` finds a `Probe` method; `Recipes.Call(name)` creates its call instruction.

## Scope of these examples

These recipes reject exception regions and instruction prefixes such as `constrained.` and `tail.`:

[!code-csharp[scope](../examples/patching-infix-authoring.cs?name=scope)]

Handling those bodies requires preserving exception regions, branch destinations and prefixes. Moving every exception marker onto an inserted instruction is not enough. Use Infix for supported operations if you want Harmony to handle this.

## Insert before and after a match

Surround the first `Tick` call:

[!code-csharp[around](../examples/patching-infix-authoring.cs?name=around)]

Moving the call's labels to `Before` makes incoming branches run it too. `After` runs only on normal return. Branches to the next instruction still bypass the pair. The inserted void, zero-argument calls leave existing stack values alone.

## Replace or remove an operation

Replace the first call with one that has the same stack behavior:

[!code-csharp[replace](../examples/patching-infix-authoring.cs?name=replace)]

`Set` replaces the opcode and operand but keeps labels. To remove this call, use `nop`:

[!code-csharp[delete](../examples/patching-infix-authoring.cs?name=delete)]

This works for the void, zero-argument `Tick()`. Other calls need a replacement that accounts for their arguments and result. The `nop` keeps the old branch destination.

## Require enough matches

Reject fewer than two `Tick` instructions, then replace every match:

[!code-csharp[minimum](../examples/patching-infix-authoring.cs?name=minimum)]

This counts instructions at this transpiler's turn, not final Infix matches. A call in a loop counts once; later transpilers can change the body. Enough matches does not prove they still mean the same thing after an update.

## Process at most N matches

Change the first two matches and leave the rest:

[!code-csharp[first](../examples/patching-infix-authoring.cs?name=first)]

Zero or one match is accepted. To *reject* more than two, count all matches before changing anything.

## Insert at entry and normal exits

Usually, use an ordinary prefix/postfix for entry and exit. A transpiler can do it too:

[!code-csharp[entry-exit](../examples/patching-infix-authoring.cs?name=entry-exit)]

The entry call leaves labels on the original first instruction, so loops back to it do not repeat entry logic. Return labels move to the exit call so incoming branches run it. The void exit call preserves any return value on the stack.

This covers normal returns only, not exceptions or exception handlers. Use an ordinary finalizer for exception completion.

## Install and remove a runtime group

Give each independently removable group its own Harmony owner ID:

[!code-csharp[group](../examples/patching-infix-authoring.cs?name=group)]

`RemoveGroup` removes every patch with that owner, even on other methods, and leaves other owners alone. Repeated installation adds registrations. It does not toggle or replace the group. Updates are per method; a later failure does not undo earlier installations.
