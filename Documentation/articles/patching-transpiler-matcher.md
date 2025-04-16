# Patching

## CodeMatcher

One of the most useful tool for transpiler is the [CodeMatcher](../api/HarmonyLib.CodeMatcher.yml).

`CodeMatcher` is like a cursor that can move through the IL code. Using matching methods, you can find specific set of instructions and then insert, remove or replace instructions. To find matching IL you need to use [CodeMatch](../api/HarmonyLib.CodeMatch.yml) and [Code](../api/HarmonyLib.Code.yml)

### Use case

Here is an example of use, we are in the context of an API providing events to mods. In the game, there is a base class `DamageHandler` which manages damage and death animation. A virtual method `DamageHandler.Apply()` provides basic damage handling. This method calls another method `DamageHandler.Kill()` which is called when the character dies. We want to replace the `Kill()` call with an API method which will invoke an `OnDeath` event. It is not possible to directly patch `Kill()` because this method is used in other API methods and we do not want to trigger the event.

In our case, let's find the call to `Kill()` and replace it with our method `MyDeathHandler()`. `CodeMatcher.ThrowIfInvalid()` will throw an exception if the code does not match. There is also `ReportFailure` which returns a boolink. Using these methods can help maintain code between updates. Indicating where and which patches should have revisions.

[!code-csharp[example](../examples/patching-transpiler-codematcher.cs?name=replacement)]

Using `ThrowIfInvalid` is for an example purposes. There is `ThrowIfNotMatchForward` which summarizes the successive calls of `MatchStartForward` and `ThrowIfInvalid`.

[!code-csharp[example](../examples/patching-transpiler-codematcher.cs?name=replacement_alt)]

Furthermore, in this context, it is very likely that not all patcher methods call `Kill()`. It is possible to check the match validation in the following way. When a Match is a failure, the `CodeMatcher` pointer finds it at the end of the list of instructions. With the `Start()` method this will return the cursor to the start.

[!code-csharp[example](../examples/patching-transpiler-codematcher.cs?name=check_matcher)]

The `Kill()` method might be called more than once. For this it is possible to use `CodeMatcher.Repeat()`, the method will pass the current matcher code to the action. If no Match is successful, it is possible to define an optional action which takes an error message as a parameter, it is called if no match takes place.

[!code-csharp[example](../examples/patching-transpiler-codematcher.cs?name=repeat)]

![note] `Repeat` will not use a `CodeMatcher.Search...()`, only `Match...()` methods can be repeated. If you consider using another method `Match...()` in the "matchAction", clone your `CodeMatcher` into the match action via `CodeMatcher.Clone()`. This is to not replace the old match used by `Repeat`.

[note]: https://raw.githubusercontent.com/pardeike/Harmony/master/Harmony/Documentation/images/note.png
