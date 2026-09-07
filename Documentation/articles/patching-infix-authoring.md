# Infix and instruction edits

Use an Infix when you want prefixes, postfixes, or finalizers around a selected operation with Harmony's argument binding. For a pattern of instructions that must be inserted, replaced, or removed, use an ordinary [transpiler](patching-transpiler.md) with [CodeMatcher](patching-transpiler-matcher.md). Both participate in the existing patch ordering; these recipes introduce no extra processing phases.

Each transpiler sees the instructions passed to it at its position in ordinary transpiler order. Infix selection happens after all those transpilers, before any Infix-generated code is inserted. A transpiler with `Priority.Last` still belongs to the ordinary transpiler pass; it does not run after Infix emission.

The examples below are compiled with the documentation and exercised by tests. `Probe.Tick()`, `Before()`, `After()`, `Replacement()`, `Enter()`, and `Exit()` are static methods taking no arguments and returning void. Each records its own name. `Recipes.Method(name)` finds a method on `Probe`; `Recipes.Call(name)` creates its call instruction.

## Scope of these examples

These small recipes accept bodies without exception regions or instruction prefixes such as `constrained.` and `tail.`. The shared helper makes that boundary explicit:

[!code-csharp[scope](../examples/patching-infix-authoring.cs?name=scope)]

Real transpilers can handle those bodies, but they must preserve the intended exception regions, branch destinations, and instruction prefixes. Moving every exception marker to an inserted instruction is not a general solution. Use an Infix for supported operations when you need Harmony to handle these details.

## Insert before and after a match

This surrounds the first `Tick` call:

[!code-csharp[around](../examples/patching-infix-authoring.cs?name=around)]

Moving the target's labels to `Before` makes branches to that call execute `Before` too. `After` runs only when the call returns normally. A branch that already targets the next instruction continues to bypass this pair. Because the inserted methods consume no arguments and return no value, they leave any existing stack values alone.

## Replace or remove an operation

Replace the first call with another call having the same stack behavior:

[!code-csharp[replace](../examples/patching-infix-authoring.cs?name=replace)]

`Set` changes the existing instruction's opcode and operand while keeping its labels. To remove this particular operation, replace it with `nop`:

[!code-csharp[delete](../examples/patching-infix-authoring.cs?name=delete)]

This works because `Tick()` takes no arguments and returns void. Deleting a call that consumes arguments or produces a result requires an appropriate replacement for those stack effects. The `nop` preserves any branch destination at the old call.

## Require enough matches

This recipe rejects a body with fewer than two `Tick` instructions, then replaces every match:

[!code-csharp[minimum](../examples/patching-infix-authoring.cs?name=minimum)]

The count describes instructions visible to this transpiler, so one call inside a loop still counts once. Later transpilers can still change that body. This is not a final-body match-count option for Infix. A minimum count can catch a changed method shape, but it cannot prove that each match still means what your patch expects after a game update.

## Process at most N matches

This changes the first two matches and leaves further matches alone:

[!code-csharp[first](../examples/patching-infix-authoring.cs?name=first)]

Zero or one match is also accepted. "Process at most two" differs from "reject more than two"; the latter requires counting all matches and checking that count before applying changes.

## Insert at entry and normal exits

An ordinary prefix/postfix is usually the direct way to run code at method entry and exit. A transpiler can also insert instructions there:

[!code-csharp[entry-exit](../examples/patching-infix-authoring.cs?name=entry-exit)]

The entry call precedes the original first instruction without taking its labels, so a loop back to that instruction does not repeat entry logic. Moving a return's labels to the exit call makes branches to the return run exit logic. The void exit call leaves an existing return value on the stack.

This recipe handles normal returns in the accepted bodies. It does not run exit logic after an exception and does not demonstrate inserting calls into exception handlers. Use an ordinary finalizer when exception completion is part of the requirement.

## Install and remove a runtime group

A dedicated Harmony owner ID groups patches for removal, including patches on different outer methods:

[!code-csharp[group](../examples/patching-infix-authoring.cs?name=group)]

Use one owner ID per independently removable group. `RemoveGroup` removes all patches with that owner, including any installed elsewhere with the same ID. Patches owned by other IDs remain. Repeated installation adds registrations; it does not toggle a group or replace its previous installation. Installation across several methods uses ordinary per-method updates, so an error on a later method does not undo successful earlier installations.
