# Privacy

IDE-tabs works entirely on your PC. It has no account, no cloud service, no telemetry and
no update check, and it does not send anything over the network.

## What it reads

To manage your Cursor windows, IDE-tabs reads, on your PC only:

- the titles of open windows, to name each tab after its project folder;
- which program each window belongs to, to pick out Cursor and VS Code;
- window positions and sizes, to fit the workspace to your screen.

It does not read the contents of your files, editors, terminals or agent chats.

## What it stores

| File | Contents |
| --- | --- |
| `%AppData%\IDE-tabs\IDE-tabs-settings.json` (or next to `IDE-tabs.exe` for a portable copy) | Your settings, names you gave tabs, and the window titles of groups you made by hand, so they can be put back together |
| `IDE-tabs-crash.log` next to `IDE-tabs.exe` | Technical details of errors: time, version, Windows version and the error itself |

Both stay on your PC. Delete them to remove everything IDE-tabs has stored.

## When something leaves your PC

Only when you choose to:

- **Links** in Settings (Releases, Report issue) open GitHub in your browser.
- **The troubleshooting report** under **Troubleshooting…** in the settings is shown to
  you, with a button to copy it for an issue. It holds your settings and versions, and
  leaves out window titles and file paths; if you turn on **Include window details**, it
  lists each window's program and window class. Read it before you share it.
