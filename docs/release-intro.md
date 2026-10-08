Harmony 3.0.0-rc.1 is the first public release candidate for Harmony 3.

The main new feature is **Infix**: apply prefixes, postfixes, and finalizers around selected operations inside a method, including calls, property access, field reads and writes, construction, and literal loads. Infix also supports selected compiler-generated iterator and async bodies, captured variables, and patch-owned state across suspensions.

See the [Harmony 3 documentation](https://harmony.pardeike.net/v3/) and [what is new](https://harmony.pardeike.net/v3/articles/new.html) for examples and supported behavior.

All three variants are available as NuGet packages at version `3.0.0-rc.1`:

- **Lib.Harmony**: Fat, with MonoMod merged into `0Harmony.dll`.
- **Lib.Harmony.Thin**: Thin, with MonoMod supplied as a NuGet dependency.
- **Lib.Harmony.Ref**: reference assemblies for compilation against .NET Standard 2.0.

The matching Fat, Thin, and Ref ZIP downloads are attached below. This is a prerelease; Harmony 2.4.2 remains the stable release.

Mixed Harmony 2/3 loading and patches from different engines on the same target still have the limitations documented in the [compatibility notes](https://github.com/pardeike/Harmony/blob/v3/HarmonyTests.Compatibility/README.md#known-boundaries-retained-by-the-probes).
