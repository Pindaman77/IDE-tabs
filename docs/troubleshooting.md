# Troubleshooting

## An editor window has no tab, or is not in the workspace

- Click the tray icon and check that the editor is switched on.
- A window you dragged out of the workspace stays out until you drag its tab back onto
  the sidebar, or until IDE-tabs restarts.
- Dialogs, such as **Open Folder**, never get tabs.
- A window on another virtual desktop starts its own workspace there.

## The sidebar is gone

Press **Ctrl+Alt+H**: it hides and shows the sidebar. If the shortcut does nothing,
another app may hold it; click the tray icon and choose a different one.

## Cursor does not fill the screen, or covers the sidebar

IDE-tabs puts the editor back beside the sidebar a moment after it is moved, maximized
or snapped. If it stays wrong, click another project in the sidebar and back, or exit
IDE-tabs from the tray and start it again.

## Alt+Tab shows windows separately

Each editor's workspace has one Alt+Tab entry: Cursor and VS Code each have their own.
Windows taken out of the workspace have their own entries too.

## Windows were left without taskbar buttons

This happens if IDE-tabs stopped unexpectedly. Start IDE-tabs again, or start it and then
exit it from the tray: every window gets its own taskbar button back.

## SmartScreen blocks the download

The alpha is not code-signed. Choose **More info → Run anyway**, after checking the ZIP
against `SHA256SUMS.txt` on the release page.

## Run as administrator?

Not needed and not recommended. Windows blocks some input between administrator and
normal windows, so a Cursor window started as administrator cannot be managed by an
IDE-tabs that is not, and the other way round.

## Reporting a problem

[Open an issue](https://github.com/Pindaman77/IDE-tabs/issues) and paste the
report from **Troubleshooting…** at the bottom of the settings. It leaves out window titles
and paths. If IDE-tabs wrote an `IDE-tabs-crash.log`, attach it after checking it for anything
you would rather not share.
