# Releasing Harmony

The manually dispatched **Release Harmony** workflow reads the configured version from `Directory.Build.props`. It publishes `Lib.Harmony.Ref`, `Lib.Harmony`, and `Lib.Harmony.Thin` to NuGet and attaches the matching three ZIPs to a GitHub release, preserving the Harmony 2.x distribution format. Issue #592 originally also proposed GitHub Package Registry; this workflow uses the existing NuGet and GitHub release destinations.

Every Ubuntu Release build in Platform Tests retains a `release-packages` artifact for 30 days. It contains only the six files for the configured version and a manifest recording their checksums, package frameworks, and source commit. Older tracked packages cannot enter the release bundle. Transient test artifacts are cleaned up independently.

1. Update the version and `docs/release-intro.md`, then push the intended release branch. Wait for Platform Tests and, where present, Framework Compatibility to succeed on that exact commit.
2. In Actions, select **Release Harmony**, **Run workflow**, and the intended branch (for Harmony 3, `v3`). Keep mode **prepare**. The CI run field can be blank or name a successful Platform Tests run for that commit.
3. Review the preparation's summary and `prepared-release` artifact. It contains automatically generated GitHub notes preceded by `docs/release-intro.md`, the three packages, the three ZIPs, and the manifest. Preparation verifies NuGet trusted publishing access without uploading, restores the actual packages into fresh consumer projects, compiles Ref, and executes patch/unpatch smoke tests for Fat and Thin on .NET 10/x64. For Harmony 3 this includes an Infix execution and scope check. Nothing is published in this mode.
4. Run the workflow again on the same branch and commit with mode **publish** and the successful preparation run ID. Publishing verifies the preparation, CI gates, package contents, source commit, and notes before using NuGet trusted publishing. It uploads Ref first, then Fat and Thin, waits for each package to be available, and compares the public package contents. NuGet's added repository signature is excluded from content comparison.
5. Once all three packages are verified, the workflow creates the configured version tag and a GitHub draft, uploads and verifies the three ZIPs, then publishes the release. Prereleases are marked as such and do not replace the latest stable release.

NuGet publishing is not transactional. If an upload, indexing, or GitHub operation fails, rerun **publish** with the same preparation ID. Identical existing packages and assets are accepted; different contents, tags pointing elsewhere, and conflicting release text cause failure. The workflow never overwrites a published package, moves a tag, or replaces an asset. If CI artifacts expire, run CI again and prepare a fresh release before publishing.

The workflow must also be present on the default branch (`master`) for GitHub to show the manual Run workflow control. Register it there without merging Harmony 3 into the stable branch. The workflow and helper scripts should remain identical on the branches that use them.

## NuGet authentication

The NuGet account `pardeike` must have a trusted publishing policy for repository owner `pardeike`, repository `Harmony`, workflow file `release.yml`, and environment `release`. Scope it to pushing new versions of the existing IDs `Lib.Harmony`, `Lib.Harmony.Thin`, and `Lib.Harmony.Ref`. The GitHub `release` environment must exist. `NuGet/login@v1` exchanges the workflow's OIDC token for a short-lived credential; no long-lived API key is needed in repository secrets.

## Automation verification

Run `python3 scripts/release_test.py` for failure/recovery checks. Package and source validation runs as part of the real CI build and manual preparation. Ordinary source test work continues to use `./scripts/test.sh`.
