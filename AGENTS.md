# Harmony rules for Codex

* Relevant drafts and information are located in `./drafts/` and is named according to the topic.

* For simple edits, limit testing to net9 and x64.

* Use the latest C# language features, like `var`, shorter array syntax etc. Longer than usual lines (~ 140 chars are ok)

* During editing C# files, don't bother with formatting. Instead, run `dotnet format` at the end to format the code - it will respect the .editorconfig file.
