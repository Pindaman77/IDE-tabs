# Kept to ASCII: Windows PowerShell reads a script without a byte-order mark as ANSI.
# Installs, updates over a running copy and uninstalls the setup from build/Package-Release.ps1
# for the current user, silently, the way a release user and a future in-app update run it.
# It starts and quits IDE-tabs, so it replaces any copy already running: run it on a CI runner
# or with IDE-tabs quit.
param(
    [Parameter(Mandatory)][string]$Setup,
    [int]$TimeoutSeconds = 60
)
$ErrorActionPreference = 'Stop'
$setupPath = (Resolve-Path -LiteralPath $Setup).Path
$logs = Join-Path $PSScriptRoot 'Debug'
New-Item -ItemType Directory -Path $logs -Force | Out-Null
$dir = Join-Path $env:LOCALAPPDATA ('IDE-tabs-installer-smoke-' + [guid]::NewGuid().ToString('N'))
$exe = Join-Path $dir 'IDE-tabs.exe'
$uninstallKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{81C4FEE0-C952-4B76-AF84-674D6A9AEC5D}_is1'
$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$shortcut = Join-Path ([Environment]::GetFolderPath('Programs')) 'IDE-tabs.lnk'

function Wait-Until([scriptblock]$Condition, [string]$What) {
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    while (-not (& $Condition)) {
        if ([DateTime]::UtcNow -gt $deadline) { throw "Timed out waiting until $What." }
        Start-Sleep -Milliseconds 250
    }
}
function Get-InstalledCopy { @(Get-Process IDE-tabs -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $exe }) }
function Test-Running {
    try { [Threading.Mutex]::OpenExisting('BemoSoftware.WindowTabs').Dispose(); $true } catch { $false }
}
# Start-Process -Wait in Windows PowerShell also waits for the IDE-tabs an update starts, so
# wait for the process itself. Reading Handle first keeps its exit code readable.
function Invoke-Process([string]$Path, [string]$Arguments) {
    $process = Start-Process -FilePath $Path -ArgumentList $Arguments -PassThru
    $null = $process.Handle
    $process.WaitForExit()
    $process.ExitCode
}
function Invoke-Setup([string]$Log) {
    $exitCode = Invoke-Process $setupPath "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /DIR=`"$dir`" /LOG=`"$(Join-Path $logs $Log)`""
    if ($exitCode -ne 0) { throw "$Log`: setup exited with $exitCode" }
}

if (Test-Running) { throw 'IDE-tabs is already running; quit it before this test.' }
try {
    Invoke-Setup 'InstallerSmoke.install.log'
    foreach ($file in 'IDE-tabs.exe', 'LICENSE.txt', 'THIRD_PARTY_NOTICES.txt', 'unins000.exe') {
        if (-not (Test-Path -LiteralPath (Join-Path $dir $file))) { throw "Install left out $file." }
    }
    if (-not (Test-Path -LiteralPath $shortcut)) { throw 'Install made no Start menu shortcut.' }
    $entry = Get-ItemProperty -LiteralPath $uninstallKey
    if ($entry.InstallLocation.TrimEnd('\') -ne $dir) { throw "Uninstall entry names $($entry.InstallLocation)." }
    if (Get-InstalledCopy) { throw 'A silent first install started IDE-tabs.' }

    # An update over a running copy quits it, replaces the exe and starts the new one.
    $before = Start-Process -FilePath $exe -PassThru
    Wait-Until { Test-Running } 'the installed copy runs'
    Invoke-Setup 'InstallerSmoke.update.log'
    Wait-Until { $before.HasExited } 'the update quits the earlier copy'
    Wait-Until { (Get-InstalledCopy | Where-Object Id -ne $before.Id) -and (Test-Running) } 'the update starts IDE-tabs again'
    $registered = (Get-ItemProperty -LiteralPath $runKey -ErrorAction SilentlyContinue).'IDE-tabs'
    $startsWithWindows = $registered -and $registered.Trim('"') -eq $exe

    # Uninstalling quits it too, and leaves neither the folder nor its entries behind.
    $exitCode = Invoke-Process (Join-Path $dir 'unins000.exe') '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART'
    if ($exitCode -ne 0) { throw "Uninstall exited with $exitCode" }
    # The uninstaller hands over to a copy of itself in %TEMP% and returns at once.
    Wait-Until { -not (Test-Path -LiteralPath $uninstallKey) -and -not (Test-Path -LiteralPath $dir) } 'the uninstall finishes'
    if (Get-InstalledCopy) { throw 'Uninstall left IDE-tabs running.' }
    if (Test-Path -LiteralPath $shortcut) { throw 'Uninstall left the Start menu shortcut.' }
    $registered = (Get-ItemProperty -LiteralPath $runKey -ErrorAction SilentlyContinue).'IDE-tabs'
    if ($registered -and $registered.Trim('"') -eq $exe) { throw 'Uninstall left the Start with Windows entry.' }
    Write-Host "Installer smoke passed (Start with Windows entry checked: $startsWithWindows)."
}
finally {
    Get-InstalledCopy | Stop-Process -Force -ErrorAction SilentlyContinue
    if (Test-Path -LiteralPath (Join-Path $dir 'unins000.exe')) {
        $null = Invoke-Process (Join-Path $dir 'unins000.exe') '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART'
    }
}
