# Publishing a release

First merge a PR whose **build** and **hosted desktop E2E** checks both passed on
its latest commit.

Record the version and release date in [CHANGELOG.md](../CHANGELOG.md): its section
heading starts with the tag, for example `## v0.1.0-alpha.1 — 2026-10-20`, and becomes
the release notes. Check that [README.md](../README.md), its screenshots and
[known limitations](known-limitations.md) describe the version being released.

Tag the merged commit and push the tag, substituting the intended version below:

```powershell
git switch main
git pull --ff-only
git tag v0.1.0-alpha.1
git push origin v0.1.0-alpha.1
```

A `vMAJOR.MINOR.PATCH` tag, optionally with a suffix such as `-alpha.1`, triggers
[the release workflow](../.github/workflows/release.yml). It:

1. Builds the standalone Release exe, embedding the tag version, checks the version
   and runs the isolated Release smoke test.
2. Packages it with [build/Package-Release.ps1](../build/Package-Release.ps1):
   `IDE-tabs-<version>-windows.zip` holds `IDE-tabs.exe`, `README.txt`, `LICENSE.txt` and
   `THIRD_PARTY_NOTICES.txt`; `SHA256SUMS.txt` holds the ZIP's SHA-256, and the release
   notes are the version's changelog section with download instructions.
3. Runs the regression suites in three parallel groups on separate Windows runners,
   and Full desktop E2E against the built exe.
4. Creates a **draft** GitHub Release with the ZIP and `SHA256SUMS.txt` once all of
   that passes. A suffixed tag creates a prerelease draft.

The exe is not code-signed, so Windows SmartScreen warns on first start; the README,
getting started and the release notes say so.

To test the very package a release carries before tagging, build Release and run the
same script locally:

```powershell
dotnet build WindowTabs.sln -c Release -p:Version=0.1.0-alpha.1
powershell -NoProfile -ExecutionPolicy Bypass -File build/Package-Release.ps1 -Version 0.1.0-alpha.1
```

The package lands in `dist/`, which Git ignores. Review the draft on GitHub and publish
it. Publishing does not update installed copies: users download and replace the exe.
