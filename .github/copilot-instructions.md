For editing C# code changes:
- prefer `var` in variable declarations.
- use the latest C# version features.
- do not add obvious comments.
- when adding new methods, provide docfx documentation.
- respect .editorconfig (for example, tab instead of spaces).

For editing .github/actions or .github/workflows:
- make sure to understand the multi-platform nature of the Harmony tests
- avoid running bash or powershell scripts
- use the latest and valid GitHub Actions syntax and features
- make sure no unnecessary warnings or errors are introduced
- prefer smaller surgical changes over large refactors unless instructed to do so
