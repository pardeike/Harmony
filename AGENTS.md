# Harmony rules for Codex

* Before you can test, you need to install dotnet using `./scripts/prepare-dotnet.sh`

* If you need to run tests, execute: `./scripts/run-tests.sh`

* To simplify, assume the only supported target is **net9.0 / x64**.

* For complex edits, remember that Harmony runs on many .NET version, operating systems 
  and processor architectures. You cannot test all those but keep that in mind if you 
  change code.

* Respect .editorconfig when doing edits.