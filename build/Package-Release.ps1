# Kept to ASCII: Windows PowerShell reads a script without a byte-order mark as ANSI.
# Packages a built IDE-tabs.exe for a GitHub release: a per-user setup (build/IDE-tabs.iss), a
# portable ZIP with the exe, its license and notices and a short readme, SHA256SUMS.txt for both,
# and release notes taken from CHANGELOG.md. The release workflow runs this; run it locally to
# test the very package a release carries. It needs Inno Setup 6 (winget install JRSoftware.InnoSetup).
# -SignCommand signs the exe, the setup and its uninstaller once there is a certificate: a command
# line with $f where the file goes, e.g. 'signtool sign /fd sha256 /tr <timestamp url> /td sha256 /a $f'.
param(
    [Parameter(Mandatory)][string]$Version,
    [string]$Executable,
    [string]$OutputDirectory,
    [string]$InnoSetupCompiler,
    [string]$SignCommand
)
$ErrorActionPreference = 'Stop'
$utf8 = New-Object System.Text.UTF8Encoding($false)
# Windows PowerShell leaves $PSScriptRoot empty in parameter defaults.
$repo = Split-Path $PSScriptRoot -Parent
if (-not $Executable) { $Executable = Join-Path $repo 'WtProgram\bin\Release\IDE-tabs.exe' }
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $repo 'dist' }
$name = "IDE-tabs-$Version-windows"
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
# ISCC resolves a relative path against the script's folder, not the current one.
$OutputDirectory = (Resolve-Path -LiteralPath $OutputDirectory).Path
$stage = Join-Path $OutputDirectory $name
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Path $stage | Out-Null

$stagedExe = Join-Path $stage 'IDE-tabs.exe'
Copy-Item -LiteralPath $Executable -Destination $stagedExe
if ($SignCommand) {
    # Run as Inno Setup runs it for the setup; /s keeps a quoted tool path intact.
    $command = $SignCommand.Replace('$f', '"' + $stagedExe + '"')
    $signing = Start-Process cmd.exe -ArgumentList ('/s /c "' + $command + '"') -NoNewWindow -Wait -PassThru
    if ($signing.ExitCode -ne 0) { throw "Signing IDE-tabs.exe failed: $($signing.ExitCode)" }
}
Copy-Item -LiteralPath (Join-Path $repo 'LICENSE') -Destination (Join-Path $stage 'LICENSE.txt')
Copy-Item -LiteralPath (Join-Path $repo 'THIRD_PARTY_NOTICES.md') -Destination (Join-Path $stage 'THIRD_PARTY_NOTICES.txt')
@"
IDE-tabs $Version
One workspace for your Cursor and VS Code projects on Windows 11.

This is the portable copy. IDE-tabs-$Version-setup.exe on the release page installs it for you,
with a Start menu shortcut, updates in place and an uninstaller.

1. Put this folder somewhere of your own, for example %LocalAppData%\Programs\IDE-tabs.
2. Run IDE-tabs.exe. On a SmartScreen warning, choose More info, then Run anyway.
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

if (-not $InnoSetupCompiler) {
    $InnoSetupCompiler = @(
        (Get-Command ISCC.exe -ErrorAction SilentlyContinue | ForEach-Object Source)
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
    ) | Where-Object { $_ -and (Test-Path -LiteralPath $_) } | Select-Object -First 1
    if (-not $InnoSetupCompiler) { throw 'Inno Setup 6 is not installed: winget install JRSoftware.InnoSetup' }
}
$setupName = "IDE-tabs-$Version-setup.exe"
$setupArgs = @("/DAppVersion=$Version", "/DFileVersion=$(($Version -split '-')[0])", "/DSourceExe=$stagedExe",
               "/DOutputDir=$OutputDirectory")
# Inno Setup reads $q as a quote: Windows PowerShell passes quotes inside an argument badly.
if ($SignCommand) { $setupArgs += @('/DSign', ('/Ssign=' + $SignCommand.Replace('"', '$q'))) }
& $InnoSetupCompiler /Q @setupArgs (Join-Path $PSScriptRoot 'IDE-tabs.iss')
if ($LASTEXITCODE -ne 0) { throw "Inno Setup failed: $LASTEXITCODE" }

$sums = foreach ($file in @($setupName, "$name.zip")) {
    $hash = (Get-FileHash -LiteralPath (Join-Path $OutputDirectory $file) -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $file"
}
$sums | Set-Content -LiteralPath (Join-Path $OutputDirectory 'SHA256SUMS.txt') -Encoding ASCII

# The version's changelog section, without its heading: the release has its own title.
$changelog = Get-Content -LiteralPath (Join-Path $repo 'CHANGELOG.md') -Encoding UTF8
$start = [Array]::FindIndex($changelog, [Predicate[string]]{ param($line) $line -like "## v$Version*" })
if ($start -lt 0) { throw "CHANGELOG.md has no section for v$Version" }
$end = [Array]::FindIndex($changelog, $start + 1, [Predicate[string]]{ param($line) $line -like '## *' })
$section = if ($end -lt 0) { $changelog[($start + 1)..($changelog.Length - 1)] } else { $changelog[($start + 1)..($end - 1)] }
@"
$(($section -join "`n").Trim())

**Install:** download and run ``$setupName``. It installs for your account only, without administrator rights, and an update installs over it the same way. Until IDE-tabs is code-signed, Windows may ask first: **More info, then Run anyway**. For a portable copy, unzip ``$name.zip`` instead.

See [known limitations](https://github.com/Pindaman77/IDE-tabs/blob/main/docs/known-limitations.md)
"@ | ForEach-Object { [IO.File]::WriteAllText((Join-Path $OutputDirectory 'release-notes.md'), $_, $utf8) }

Remove-Item $stage -Recurse -Force
Get-ChildItem $OutputDirectory | Select-Object Name, Length | Format-Table -AutoSize | Out-String | Write-Host
