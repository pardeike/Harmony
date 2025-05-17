# Harmony rules for Codex

* This is the only AGENTS.md file in the repository

* Relevant drafts and information are located in `./drafts/` and is named according to the topic.

// currently the proxy does not allow downloads, so no dotnet binary is available
// no dotnet binary means no testing for now
//
// * Before you can test, you need to install dotnet using `./scripts/prepare-dotnet.sh`.
//
// * If you need to run tests, execute: `./scripts/run-tests.sh`.
//
//* To simplify, assume the only supported target is **net9.0 / x64**.

* For complex edits, remember that Harmony runs on many .NET version, operating systems 
  and processor architectures. You cannot test all those but keep that in mind if you 
  change code.

* Respect .editorconfig when doing edits, especially for using whitespace

* Whitespace rules are TAB, CRLF endings and longer than usual lines (~ 140 chars are ok)