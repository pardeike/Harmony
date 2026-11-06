# CodeMatcher

<div id="patching"></div>

[CodeMatcher](../api/HarmonyLib.CodeMatcher.yml) is a cursor over IL instructions. Use [CodeMatch](../api/HarmonyLib.CodeMatch.yml) and [Code](../api/HarmonyLib.Code.yml) to find a sequence, then insert, remove, or replace instructions.

## Use case

Suppose `DamageHandler.Apply()` calls `Kill()` when a character dies. To invoke your `OnDeath` event there without changing other calls to `Kill()`, find that call and replace it with `MyDeathHandler()`.

`ThrowIfInvalid()` reports a missing match by throwing; `ReportFailure()` reports it through a callback and returns a boolean. These checks help detect broken patches after game updates.

[!code-csharp[example](../examples/patching-transpiler-codematcher.cs?name=replacement)]

`ThrowIfNotMatchForward()` checks for a forward match from a valid cursor position without moving it. Here, `Start()` sets that position and `MatchStartForward()` then moves to the match:

[!code-csharp[example](../examples/patching-transpiler-codematcher.cs?name=replacement_alt)]

If some targets legitimately lack the call, check whether the match succeeded. `Start()` resets the cursor for another search:

[!code-csharp[example](../examples/patching-transpiler-codematcher.cs?name=check_matcher)]

For several matching calls, use `Repeat()`. It passes the current matcher to your action and accepts an optional callback for a missing match:

[!code-csharp[example](../examples/patching-transpiler-codematcher.cs?name=repeat)]

![note] `Repeat()` repeats `Match...()` searches, not `Search...()`. If your action needs another `Match...()`, use a clone so you do not replace the pattern being repeated.

## Match fields and properties

`CodeMatch.LoadsField` and `CodeMatch.StoresField` match field access. `CodeMatch.GetsProperty` and `CodeMatch.SetsProperty` match calls to a property's getter or setter. Each accepts the member itself or a lambda that names it:

```csharp
CodeMatch.LoadsField(() => default(Character).health)
CodeMatch.GetsProperty(() => default(Character).Health)
CodeMatch.SetsProperty(typeof(Character).Property("Health"))
```

The lambda only names the member and never runs. C# compiles a call to an overridden property as a call to the base declaration's accessor. To match it, pass the base type's property or use a lambda.

[note]: ../images/note.png
