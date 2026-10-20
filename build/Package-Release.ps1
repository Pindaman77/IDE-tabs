# Kept to ASCII: Windows PowerShell reads a script without a byte-order mark as ANSI.
# Packages a built IDE-tabs.exe for a GitHub release: a ZIP with the exe, its license and
# notices and a short readme, SHA256SUMS.txt for it, and release notes taken from CHANGELOG.md.
# The release workflow runs this; run it locally to test the very package a release carries.
param(
    [Parameter(Mandatory)][string]$Version,
    [string]$Executable,
    [string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
$utf8 = New-Object System.Text.UTF8Encoding($false)
# Windows PowerShell leaves $PSScriptRoot empty in parameter defaults.
$repo = Split-Path $PSScriptRoot -Parent
if (-not $Executable) { $Executable = Join-Path $repo 'WtProgram\bin\Release\IDE-tabs.exe' }
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $repo 'dist' }
$name = "IDE-tabs-$Version-windows"
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$stage = Join-Path $OutputDirectory $name
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Path $stage | Out-Null

Copy-Item -LiteralPath $Executable -Destination (Join-Path $stage 'IDE-tabs.exe')
Copy-Item -LiteralPath (Join-Path $repo 'LICENSE') -Destination (Join-Path $stage 'LICENSE.txt')
Copy-Item -LiteralPath (Join-Path $repo 'THIRD_PARTY_NOTICES.md') -Destination (Join-Path $stage 'THIRD_PARTY_NOTICES.txt')
@"
IDE-tabs $Version
One workspace for your Cursor and VS Code projects on Windows 11.

1. Put this folder somewhere of your own, for example %LocalAppData%\Programs\IDE-tabs.
2. Run IDE-tabs.exe. It is not code-signed yet: on the SmartScreen warning, choose
   More info, then Run anyway.
3. Open your projects in Cursor or VS Code, each in its own window.

IDE-tabs runs in the system tray; click its icon for settings. It starts with Windows from
the folder you run it from.

Getting started:   https://github.com/Pindaman77/IDE-tabs/blob/main/docs/getting-started.md
Known limitations: https://github.com/Pindaman77/IDE-tabs/blob/main/docs/known-limitations.md
Report a problem:  https://github.com/Pindaman77/IDE-tabs/issues
"@ | ForEach-Object { [IO.File]::WriteAllText((Join-Path $stage 'README.txt'), $_, $utf8) }

$zip = Join-Path $OutputDirectory "$name.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip
$hash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
"$hash  $name.zip" | Set-Content -LiteralPath (Join-Path $OutputDirectory 'SHA256SUMS.txt') -Encoding ASCII

# The version's changelog section, without its heading: the release has its own title.
$changelog = Get-Content -LiteralPath (Join-Path $repo 'CHANGELOG.md') -Encoding UTF8
$start = [Array]::FindIndex($changelog, [Predicate[string]]{ param($line) $line -like "## v$Version*" })
if ($start -lt 0) { throw "CHANGELOG.md has no section for v$Version" }
$end = [Array]::FindIndex($changelog, $start + 1, [Predicate[string]]{ param($line) $line -like '## *' })
$section = if ($end -lt 0) { $changelog[($start + 1)..($changelog.Length - 1)] } else { $changelog[($start + 1)..($end - 1)] }
@"
$(($section -join "`n").Trim())

### Download

Download ``$name.zip``, unzip it and run ``IDE-tabs.exe``. The download is not code-signed:
Windows SmartScreen asks first: choose **More info**, then **Run anyway**. Check the ZIP against
``SHA256SUMS.txt``: ``Get-FileHash .\$name.zip -Algorithm SHA256``.

Read the [known limitations](https://github.com/Pindaman77/IDE-tabs/blob/main/docs/known-limitations.md)
before relying on this early alpha.
"@ | ForEach-Object { [IO.File]::WriteAllText((Join-Path $OutputDirectory 'release-notes.md'), $_, $utf8) }

Remove-Item $stage -Recurse -Force
Get-ChildItem $OutputDirectory | Select-Object Name, Length | Format-Table -AutoSize | Out-String | Write-Host
