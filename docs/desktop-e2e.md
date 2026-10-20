# Desktop end-to-end tests

The desktop E2E suite drives the shipped Release `IDE-tabs.exe` with real mouse and keyboard
input on the interactive desktop. It needs no test hooks in the app: a separate x86 WinForms
driver opens three windows titled like VS Code's (`notes.md - E2E Project 1 - Visual Studio Code`),
and the runner stages that driver as `Code.exe`, so IDE-tabs takes its windows for VS Code's.

Run it on an unlocked desktop with .NET Framework 4.8 and the .NET SDK. Quit IDE-tabs and close
Cursor and VS Code first: the runner refuses to start otherwise, because IDE-tabs would take real
editor windows into the test workspace. Leave the mouse and keyboard alone while it runs, and do
not run it alongside the regression suites.

```powershell
# Compile and stage without opening windows or sending input.
powershell -NoProfile -ExecutionPolicy Bypass -File tests/Run-DesktopE2E.ps1 -BuildOnly

# Run it.
powershell -NoProfile -ExecutionPolicy Bypass -File tests/Run-DesktopE2E.ps1 -Interactive -Profile Quick
powershell -NoProfile -ExecutionPolicy Bypass -File tests/Run-DesktopE2E.ps1 -Interactive -Profile Full
```

| Profile | Workload | Timeout |
| --- | --- | --- |
| Quick | One startup/quit cycle; 60 + 30 + 30 checked switches | 3 minutes |
| Full (default) | Two cycles with restart; 600 + 30 + 30 checked switches per cycle | 10 minutes |
| Soak | Two cycles, at least 30 minutes of switching plus seeded random actions | 40 minutes |

Each cycle checks that:

1. Startup gathers the three editor windows into one workspace, on its own: one full-height
   column at the left of the work area, the editor in front filling the rest, and one taskbar
   button (so one native Alt+Tab entry) for all three.
2. Clicking each row of the column brings a different editor forward.
3. Ctrl+1 and Ctrl+Alt+Right, which belong to the editors, do not switch projects.
4. Ctrl+Alt+H hides the column and the editor takes its space; again shows it. The chevron
   narrows the column to icons and widens the editor; again expands it.
5. Switching by row click puts the expected
   editor in front, fitted beside the column, every time. Resource growth stays in budget.
6. Maximizing the front editor gives it back its workspace frame; minimizing minimizes the
   workspace and hides the column, and restoring brings both back.
7. Closing an editor keeps the others in the workspace, and a new editor window joins it.
8. **Quit IDE-tabs** in the tray menu exits with code zero, leaves every editor open, unowned and
   visible, gives the editor in front its own placement back, and keeps IDE Mode in the settings.

The next cycle restarts IDE-tabs with the surviving windows. Soak adds 30 seeded random actions
per batch; `-Seed` and `-RandomBatchesPerCycle` replay a failure.

The staging directory `tests/Debug/desktop-e2e-<id>` keeps `result.json`, `manifest.json` (with
the binary's SHA-256), stdout/stderr, the settings and crash log, and on failure `failure.txt`
and `windows.json`. Any timeout, wrong foreground, crash log entry or incomplete cycle fails the
run; nothing is retried. A switch may take up to 5 seconds and a layout change up to 20, so
a busy desktop slows the run rather than failing it.

## Virtual desktops

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tests/Run-DesktopE2E.ps1 -Interactive -VirtualDesktops
```

A separate workload creates two temporary virtual desktops with two editor windows each and
checks that each desktop gets a workspace and a taskbar button of its own, that a new window
joins its own desktop's workspace, and that quitting keeps every window on its desktop with its
taskbar button back. It removes the temporary desktops afterwards, also after a failure.

## GitHub Actions

`.github/workflows/desktop-e2e-hosted.yml` runs Full on GitHub's `windows-2022` VM for every pull
request (the **Desktop E2E (Full)** check), Soak daily on the default branch, and Full again
in the release workflow on the very exe the release ships. `desktop-e2e.yml` runs the same suite
on demand on a self-hosted runner labelled `windowtabs-desktop`.

## Other manual tools

`tests/Run-DesktopLatency.ps1` and `tests/Run-RealAppScenario.ps1` come from WindowTabs and test
tabs on browsers and other apps, which IDE Mode leaves alone; they are kept for reference and
not used for IDE-tabs.
