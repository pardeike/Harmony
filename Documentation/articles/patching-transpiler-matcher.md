# Patching

## CodeMatcher

[CodeMatcher](../api/HarmonyLib.CodeMatcher.yml) is a cursor over IL instructions. Use [CodeMatch](../api/HarmonyLib.CodeMatch.yml) and [Code](../api/HarmonyLib.Code.yml) to find a sequence, then insert, remove, or replace instructions.

### Use case

Suppose `DamageHandler.Apply()` calls `Kill()` when a character dies. We want that call to invoke our `OnDeath` event, without changing calls to `Kill()` elsewhere.

Find the call to `Kill()` and replace it with `MyDeathHandler()`. `ThrowIfInvalid()` reports a missing match by throwing; `ReportFailure()` reports it through a callback and returns a boolean. These checks help detect broken patches after game updates.

[!code-csharp[example](../examples/patching-transpiler-codematcher.cs?name=replacement)]

`ThrowIfNotMatchForward()` checks for a forward match from a valid cursor position without moving it. Here, `Start()` sets that position and `MatchStartForward()` then moves to the match:

[!code-csharp[example](../examples/patching-transpiler-codematcher.cs?name=replacement_alt)]

If some targets legitimately lack the call, check whether the match succeeded. `Start()` resets the cursor for another search:

[!code-csharp[example](../examples/patching-transpiler-codematcher.cs?name=check_matcher)]

For several matching calls, use `Repeat()`. It passes the current matcher to your action and accepts an optional callback for a missing match:

[!code-csharp[example](../examples/patching-transpiler-codematcher.cs?name=repeat)]

![note] `Repeat()` repeats `Match...()` searches, not `Search...()`. If your action needs another `Match...()`, use a clone so you do not replace the pattern being repeated.

[note]: https://raw.githubusercontent.com/pardeike/Harmony/master/Harmony/Documentation/images/note.png
