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
   `IDE-tabs-<version>-setup.exe` is the installer from [build/IDE-tabs.iss](../build/IDE-tabs.iss);
   `IDE-tabs-<version>-windows.zip` is the portable copy, with `IDE-tabs.exe`, `README.txt`,
   `LICENSE.txt` and `THIRD_PARTY_NOTICES.txt`; `SHA256SUMS.txt` holds the SHA-256 of both,
   and the release notes are the version's changelog section with install instructions.
3. Installs the setup silently, updates it over a running copy and uninstalls it
   ([tests/Run-InstallerSmoke.ps1](../tests/Run-InstallerSmoke.ps1)); every PR build does the same.
4. Runs the regression suites in three parallel groups on separate Windows runners,
   and Full desktop E2E against the built exe.
5. Creates a **draft** GitHub Release with the setup, the ZIP and `SHA256SUMS.txt` once all
   of that passes. A suffixed tag creates a prerelease draft.

The exe and setup are not code-signed, so Windows SmartScreen warns on first start; the
README, getting started and the release notes say so.

## Installer and updates

The setup is built with [Inno Setup 6](https://jrsoftware.org/isinfo.php)
(`winget install JRSoftware.InnoSetup` to package locally). What keeps updates working:

- **One identity.** `AppId` in `build/IDE-tabs.iss` never changes. Every later setup finds
  the earlier install by it, installs over it in the same folder and keeps one Apps entry.
- **Per user, no administrator rights.** It installs to `%LocalAppData%\Programs\IDE-tabs`
  with a Start menu shortcut, an optional desktop shortcut and an uninstaller. Settings stay
  in `%AppData%\IDE-tabs`, so installing, updating and uninstalling never touch them.
- **An update is running the newer setup.** It asks a running IDE-tabs to quit through the
  same named event a second copy uses, so editor windows stay open, replaces the files and
  starts IDE-tabs again, also when silent. A future in-app update only has to download the
  setup, check its signature and run
  `IDE-tabs-<version>-setup.exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART`.
- **Start with Windows follows the copy started last**, so a user moving from the ZIP to the
  setup ends up starting the installed copy. Uninstalling removes that entry only when it
  names the installed copy.
- **Signing is one switch.** With a certificate, pass `-SignCommand` to `Package-Release.ps1`:
  a command line with `$f` for the file, for example
  `signtool sign /fd sha256 /tr <timestamp url> /td sha256 /a $f`. It signs `IDE-tabs.exe`
  before the ZIP and the setup take it, then the setup and its uninstaller. The release
  workflow then needs only the certificate as a secret and that argument.

To test the very package a release carries before tagging, build Release and run the
same script locally:

```powershell
dotnet build WindowTabs.sln -c Release -p:Version=0.1.0-alpha.1
powershell -NoProfile -ExecutionPolicy Bypass -File build/Package-Release.ps1 -Version 0.1.0-alpha.1
```

The package lands in `dist/`, which Git ignores. `tests/Run-InstallerSmoke.ps1 -Setup
dist/IDE-tabs-<version>-setup.exe` tries the setup, but it quits and replaces a running
IDE-tabs, so quit yours first. Review the draft on GitHub and publish it. Publishing does
not update installed copies yet: users run the newer setup.
