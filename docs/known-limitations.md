# Known limitations

IDE-tabs is an early alpha. These are the limits of the current version.

## Platform

- **Windows 11 only.** IDE-tabs is tested on Windows 11. It may run on Windows 10, but
  that is not tested.
- **Cursor and VS Code only.** Other editors built on VS Code, such as Windsurf and
  VSCodium, are left alone until they have been tested.
- **The download is not code-signed.** Windows SmartScreen warns before the first start
  (**More info → Run anyway**). Compare the ZIP with `SHA256SUMS.txt` on the release page
  to check it.

## Workspace

- **Windows does not see the editors as maximized.** Each is sized to the space beside
  the sidebar. Cursor's own maximize button, Windows snapping or full screen (F11) are
  put back beside the sidebar a moment later.
- **One workspace per editor and virtual desktop.** All Cursor windows on a desktop join
  the same workspace; there is no way yet to keep two separate Cursor workspaces.
- **A project taken out of the workspace joins it again** after IDE-tabs restarts.
- **Cursor's own dialogs** (open folder, settings) stay separate windows, as they should.

## Alt+Tab and taskbar

- The workspace's Alt+Tab entry shows the Cursor icon, not a live preview of the project.
- The taskbar has one button for the whole workspace.

## Crash recovery

If IDE-tabs stops unexpectedly, the Cursor windows keep running, but until IDE-tabs is
started again they may be missing their own taskbar buttons. Start IDE-tabs again to
bring the workspace back, or exit it from the tray to give every window its own taskbar
button. A crash report is written to `IDE-tabs-crash.log` next to `IDE-tabs.exe` (or in
`%AppData%\IDE-tabs` if that folder is read-only).

## Running alongside WindowTabs

IDE-tabs is built on WindowTabs and shares its single-instance lock: starting one closes
the other, so they never manage the same windows. Their settings stay separate.
