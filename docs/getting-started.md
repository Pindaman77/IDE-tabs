# Getting started

IDE-tabs is an early alpha for **Windows 11**, supporting **Cursor** and **VS Code**. It
needs no account.

## Install

1. Download `IDE-tabs-<version>-setup.exe` from the latest prerelease on
   [Releases](https://github.com/Pindaman77/IDE-tabs/releases). To check the download,
   compare its SHA-256 with `SHA256SUMS.txt` on the same page:
   `Get-FileHash .\IDE-tabs-*-setup.exe -Algorithm SHA256`.
2. Run it. The download is not code-signed yet, so Windows SmartScreen shows
   **Windows protected your PC**: choose **More info → Run anyway**.
3. The setup installs IDE-tabs for your account only, without administrator rights, to
   `%LocalAppData%\Programs\IDE-tabs`, adds it to the Start menu and starts it.

For a portable copy, download `IDE-tabs-<version>-windows.zip` instead, unzip it to a folder
of your own and run `IDE-tabs.exe`.

IDE-tabs starts in the system tray. On Windows 11 its icon may be under the **^** arrow
next to the clock; drag it onto the taskbar to keep it in view.

## First run

There is nothing to set up:

- Only Cursor and VS Code windows get tabs. Other apps are left alone.
- Each editor gets a workspace of its own; Cursor and VS Code are never mixed.
- The project sidebar sits on the left, 180 pixels wide, and stays visible.
- Each workspace has one taskbar button and one entry in the native Alt+Tab.

Open your projects, each in its own window (**File → New Window**, then open a folder).
Every window joins its editor's workspace by itself, including Cursor's Agents window.

## Daily use

- **Switch project:** click it in the sidebar. The editors keep their own shortcuts:
  IDE-tabs takes only **Ctrl+Alt+H**.
- **Go to another app and back:** Alt+Tab returns to the project you selected.
- **More room:** click the chevron at the top of the sidebar to collapse it to project
  initials, or press **Ctrl+Alt+H** to hide it. Do the same to bring it back.
- **Take a project out:** drag its tab onto the desktop. It becomes a normal window
  again, at its earlier size, until you drag it back onto the sidebar.
- **Rename a tab:** right-click it → **Rename**. Your name is kept.

Cursor itself is unchanged: its editor, agent chats and terminals keep running in each
window.

## Start with Windows

IDE-tabs starts with Windows from the folder you last ran it from. To stop that, click the
tray icon and turn off **Start with Windows**.

## Settings

Click the tray icon for IDE-tabs' one page of settings: turn Cursor or VS Code off,
change the sidebar shortcut, and choose whether it starts with Windows. They save to
`%AppData%\IDE-tabs\IDE-tabs-settings.json`; for a portable copy, put an
`IDE-tabs-settings.json` next to `IDE-tabs.exe` and IDE-tabs uses that one instead.

## Update

Run the newer setup. It quits the running IDE-tabs, installs over it and starts it again;
your editor windows stay open and your settings are kept. Moving from the ZIP to the setup
works the same way: afterwards Windows starts the installed copy, and you can delete the
unzipped folder.

For the portable copy, quit IDE-tabs from the tray (right-click → **Quit IDE-tabs**),
replace `IDE-tabs.exe` with the new one and start it again.

## Uninstall

Open **Settings → Apps → Installed apps**, find **IDE-tabs** and choose **Uninstall**. It
quits IDE-tabs, and your editor windows stay open and get their own taskbar buttons again.
Your settings stay in `%AppData%\IDE-tabs`; delete that folder to remove them too.

For the portable copy:

1. Click the tray icon and turn off **Start with Windows**.
2. Quit IDE-tabs from the tray.
3. Delete the folder with `IDE-tabs.exe`, and `%AppData%\IDE-tabs` to remove your settings.
