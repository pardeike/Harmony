# Harmony rules for Codex

* Relevant drafts and information are located in `./drafts/` and is named according to the topic.

* The Infix design reference starts at `drafts/INFIX-NEW-IMPL-V3.md`. Its linked operation-target addendum and `drafts/INFIX-FEATURE-COMPLETION.md` define the implemented, unreleased extensions. Read them as one specification, with the completion document overriding earlier exclusions only where stated. Executed checks and remaining runtime boundaries are recorded in `docs/infix/README.md`.

* For simple edits, limit testing to net9 and x64.

* Use the latest C# language features, like `var`, shorter array syntax etc. Longer than usual lines (~ 140 chars are ok)

* During editing C# files, don't bother with formatting. Instead, run `dotnet format` at the end to format the code - it will respect the .editorconfig file.
