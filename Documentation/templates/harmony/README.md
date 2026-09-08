# Harmony 3 documentation template

This theme layers Harmony's design over DocFX's `modern` template. DocFX still generates API pages, cross-references, the search index, article outlines, and navigation. Do not hand-copy API entries into the theme.

`layout/_master.tmpl` adapts the DocFX 2.78.5 page shell. `public/main.css` supplies the layout and visual vocabulary. `public/main.js` adds code copying, a member index, and version-menu behavior. `ManagedReference.extension.js` retains section anchors from the previous template. The build pins DocFX so a template update can be checked deliberately.

## Articles and navigation

Edit the guides in `Documentation/articles/` and their order in `articles/toc.yml`. Keep existing filenames and heading anchors when restructuring. If a heading changes, retain its previous identifier with an empty HTML anchor.

The root landing page uses `_harmonyHome: true`. Article and API pages use the same reading layout. API navigation remains generated from the assembly metadata; `api/index.md` is a curated entry point, not a second source of API definitions.

## Visual vocabulary

Use the shared illustrations in `Documentation/includes/` when the same explanation belongs on more than one page:

| Element | Meaning |
| --- | --- |
| Mint box labeled Prefix | Code before the selected method or operation |
| White box | Original method, operation, or execution step |
| Lavender box labeled Postfix | Code after completion or a skip |
| Peach box labeled Finalizer | Handling success or an exception from the enclosed patch sequence |
| Black box labeled Transpiler | Instruction editing while Harmony builds a replacement |
| Gray outer border | The containing method or generated execution |
| Orange inner border | The operation selected by an Infix |
| Arrows | Execution or transformation order, depending on the labeled phase |
| Named state bar spanning steps | Storage shared across suspensions in one execution |

Color always has a text label. Illustrations use semantic HTML with captions and ordered lists; they stack vertically on narrow screens and need no image-generation or diagram runtime. The ordinary path is a simplification: captions must explain skips, exceptions, and any state-lifetime boundary relevant to the example.

For example, inside an article:

```markdown
[!include[Infix operation scope](../includes/infix-scope.md)]
```

Available shared figures:

- `patch-flow.md`: prefixes, original, postfixes, and finalizers.
- `infix-scope.md`: one selected operation inside an outer method.
- `transpiler-phases.md`: generation versus runtime execution.
- `persistent-state.md`: one named value across await/yield suspensions.

Keep new diagrams close to the example they explain. Use the existing `patch-figure`, `patch-flow`, `patch-step`, and scope classes for new compositions. Do not use patch-role colors to imply that an unrelated step is a prefix or finalizer.

## Build and check

From the repository root, with the .NET SDK and Python 3.9 or newer:

```shell
dotnet tool install -g docfx --version 2.78.5
dotnet build -c Debug -f net35 ./Lib.Harmony/Lib.Harmony.csproj
docfx Documentation/docfx.json
python3 Documentation/verify_site.py docs
python3 -m http.server 8080 --directory docs
```

`verify_site.py` checks every generated HTML page's local links, assets, and fragments. It tolerates DocFX's repeated empty overload-group anchors. With `--before /path/to/earlier/build`, it also checks for lost pages, article anchors, API identifiers, code examples, and signatures. Use that option for template migrations; omit it when intentionally changing examples or the API.

For layout changes, check the landing page, an illustrated article, a long API class, and an enum at desktop and mobile widths. Exercise search, the API filter, member links, copy buttons, version switching, and mobile navigation. Copying falls back to selecting the code when the browser denies clipboard access.

Publication from `v3` updates `/v3/`. The default redirect and `/v2/` are managed separately. The approved design proposal remains in Andreas's local `Harmony-v3-docs-design` folder; this template is the maintained implementation.
