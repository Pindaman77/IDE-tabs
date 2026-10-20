# Passive evidence for a slow desktop: counts window move, reorder and foreground events per
# window, and IDE-tabs CPU time, in each interval. Hooks events out of context; changes no windows.
# Run it while the desktop is slow, before restarting anything:
#   powershell -NoProfile -ExecutionPolicy Bypass -File tests/Measure-WindowEvents.ps1 -DurationSeconds 60
param(
    [ValidateRange(1, 86400)][int]$DurationSeconds = 60,
    [ValidateRange(1, 3600)][int]$IntervalSeconds = 5,
    [ValidateRange(1, 1000)][int]$Top = 12,
    [string]$OutputPath = ''
)
$ErrorActionPreference = 'Stop'
# The message loop below uses WinForms, which Add-Type in PowerShell 7 cannot reference.
if ($PSVersionTable.PSEdition -ne 'Desktop') { throw 'Run this with Windows PowerShell: powershell -File tests/Measure-WindowEvents.ps1' }
if (-not $OutputPath) {
    $OutputPath = Join-Path $PSScriptRoot ('Debug/window-events/' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '.txt')
}
$OutputPath = [IO.Path]::GetFullPath($OutputPath)
New-Item -ItemType Directory -Path (Split-Path $OutputPath) -Force | Out-Null

Add-Type -ReferencedAssemblies System.Windows.Forms @'
using System;
using System.Text;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;
public static class WindowEventRate {
    public delegate void Proc(IntPtr hook, uint ev, IntPtr hwnd, int obj, int child, uint thread, uint time);
    [DllImport("user32.dll")] static extern IntPtr SetWinEventHook(uint min, uint max, IntPtr mod, Proc p, uint pid, uint tid, uint flags);
    [DllImport("user32.dll")] static extern bool UnhookWinEvent(IntPtr h);
    [DllImport("user32.dll")] static extern int GetWindowThreadProcessId(IntPtr h, out int pid);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern int InternalGetWindowText(IntPtr h, StringBuilder s, int n);
    static readonly Proc keep = OnEvent;
    static readonly Dictionary<int,string> names = new Dictionary<int,string>();
    public static readonly Dictionary<string,int> Counts = new Dictionary<string,int>();
    static string NameOf(int pid) {
        string name;
        if (!names.TryGetValue(pid, out name)) {
            try { name = System.Diagnostics.Process.GetProcessById(pid).ProcessName; } catch { name = "?"; }
            names[pid] = name;
        }
        return name;
    }
    static void OnEvent(IntPtr hook, uint ev, IntPtr hwnd, int obj, int child, uint thread, uint time) {
        if (obj != 0 || hwnd == IntPtr.Zero) return;
        int pid; GetWindowThreadProcessId(hwnd, out pid);
        // InternalGetWindowText sends no message, so a hung window cannot stall the count.
        var text = new StringBuilder(80); InternalGetWindowText(hwnd, text, 80);
        string kind = ev == 0x800B ? "LOCATION" : ev == 0x8004 ? "REORDER" : "FOREGROUND";
        string key = kind + " " + NameOf(pid) + " " + hwnd + " '" + text + "'";
        int n; Counts.TryGetValue(key, out n); Counts[key] = n + 1;
    }
    public static void Run(int seconds, int every, Action<int> report) {
        var hooks = new List<IntPtr> {
            SetWinEventHook(0x800B, 0x800B, IntPtr.Zero, keep, 0, 0, 0),
            SetWinEventHook(0x8004, 0x8004, IntPtr.Zero, keep, 0, 0, 0),
            SetWinEventHook(0x0003, 0x0003, IntPtr.Zero, keep, 0, 0, 0) };
        try {
            var timer = new Timer { Interval = every * 1000 };
            int elapsed = 0;
            timer.Tick += (s, e) => {
                elapsed += every;
                report(elapsed);
                Counts.Clear();
                if (elapsed >= seconds) { timer.Stop(); Application.ExitThread(); }
            };
            timer.Start();
            Application.Run();
        } finally {
            foreach (var h in hooks) UnhookWinEvent(h);
        }
    }
}
'@

$script:lastCpu = @{}
$report = [Action[int]]{
    param($elapsed)
    $cpu = @{}
    Get-Process IDE-tabs -ErrorAction SilentlyContinue | ForEach-Object { $cpu[$_.Id] = $_.TotalProcessorTime.TotalSeconds }
    $usage = if ($cpu.Count -eq 0) { 'not running' } else {
        ($cpu.Keys | ForEach-Object {
            $delta = if ($script:lastCpu.ContainsKey($_)) { '{0:N2}s' -f ($cpu[$_] - $script:lastCpu[$_]) } else { 'n/a' }
            "pid $_ cpu $delta"
        }) -join ', '
    }
    $script:lastCpu = $cpu
    $total = 0; foreach ($value in [WindowEventRate]::Counts.Values) { $total += $value }
    $lines = @("--- t=${elapsed}s  events=$total  IDE-tabs: $usage")
    $lines += [WindowEventRate]::Counts.GetEnumerator() | Sort-Object Value -Descending | Select-Object -First $Top |
        ForEach-Object { '{0,7}  {1}' -f $_.Value, $_.Key }
    foreach ($line in $lines) { Write-Host $line; Add-Content -LiteralPath $OutputPath -Value $line }
}
$report.Invoke(0)
[WindowEventRate]::Run($DurationSeconds, $IntervalSeconds, $report)
Write-Host "Saved to $OutputPath"
