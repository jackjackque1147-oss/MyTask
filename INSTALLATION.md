# MyTask User Installation Guide

## Download

The latest version is available on [GitHub Releases](https://github.com/jackjackque1147-oss/MyTask/releases/latest). The release includes:

- **Setup installer:** `MyTask-Setup-vX.Y.Z.exe` — recommended for most users.
- **Portable executable:** `MyTask.exe` — run it directly without installing.
- **Portable ZIP:** extract it to a folder you choose, then run `MyTask.exe`.
- **Checksums:** `SHA256SUMS.txt` — verify a downloaded file's SHA-256 hash.

The Windows app is x64 and includes the .NET runtime; you do not need to install .NET separately.

## Install MyTask

1. Download `MyTask-Setup-vX.Y.Z.exe` from the release page.
2. If desired, compare its SHA-256 hash to the entry in `SHA256SUMS.txt`.
3. Run the installer. It installs for your Windows user and does not require administrator access.
4. Use the Start menu shortcut, or select the optional desktop shortcut.

The installer and portable app are currently unsigned. Windows may show a publisher warning. Before continuing, verify you downloaded the file from the official repository release page. Do not bypass warnings for files from other sources.

## Troubleshooting

- **The app is missing:** Open the repository's [Releases page](https://github.com/jackjackque1147-oss/MyTask/releases/latest) and download one of its release assets.
- **Windows blocks the app:** Verify the source and publisher. Do not disable security protections to run an untrusted copy.
- **The app cannot save files:** MyTask stores its scripts, recordings, logs, and settings under `%LOCALAPPDATA%\MyTask`; make sure your Windows user can write there.
- **Hooks do not work:** Recording is available only while MyTask is open and recording was explicitly started. Check whether Windows or another application is blocking global input hooks.
- **A global hotkey does not work:** Open Settings, assign a shortcut that is not already registered by Windows or another app, and click **Apply hotkeys**. The shortcuts work while MyTask is running; system-tray controls are not implemented in this MVP.
- **Unlimited playback will not stop by itself:** This is expected. Click **Stop playback** or use the configured stop/play hotkey to end it.

## Uninstall and data

Uninstall MyTask from **Settings > Apps > Installed apps** or from its Start menu uninstall shortcut. Uninstalling the app does not remove your personal data. Scripts, recordings, logs, and settings are kept under `%LOCALAPPDATA%\MyTask`; back them up before removing them manually.
