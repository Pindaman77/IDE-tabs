# Changelog

## v0.1.0-alpha.1 — unreleased

The first IDE-tabs release: an early alpha for Windows 11, Cursor and VS Code.

### Workspace

- Every Cursor window joins one workspace and every VS Code window another, including
  windows already open when IDE-tabs starts, ones opened later, empty windows and
  Cursor's Agents window. The two editors are never mixed, not even by dragging a tab.
- The project in front fills its monitor beside a full-height project sidebar; the
  others wait behind it at the same size. Maximizing, snapping or dragging an editor by
  its title bar puts it back beside the sidebar.
- The workspace has one taskbar button and one native Alt+Tab entry, named after the
  project in front. Alt+Tab returns to the project you selected.
- Dragging a tab onto the desktop takes its window out of the workspace, at its earlier
  size or maximized where it is dropped; dragging it back returns it.

### Sidebar

- Each tab is named after its project folder from the window title. Names you give tabs
  are kept, and two windows of one project show their full titles.
- Expanded (180 px) with project names, or collapsed (48 px) to two-letter project
  initials with the chevron at its top.
- **Ctrl+Alt+H** hides the sidebar so the editor fills the screen, and brings it back as
  it was. The shortcut can be changed in the settings.

### App

- One page of settings, from the tray icon: Cursor and VS Code on or off (a supported
  editor starts on, and one turned off stays off after updates), the sidebar shortcut
  and Start with Windows.
- Ctrl+1–9, Ctrl+Alt+arrows and Shift+scroll stay with the editors; IDE-tabs takes only
  Ctrl+Alt+H.
- Its own icon, settings (`%AppData%\IDE-tabs`) and Start with Windows entry. It never
  reads WindowTabs' settings.
- Works entirely on your PC: no account, no telemetry, no network access.

IDE-tabs is built on WindowTabs v2026.10.17-beta.1. For WindowTabs' own history, see its
[changelog](https://github.com/leafOfTree/WindowTabs/blob/main/CHANGELOG.md).
