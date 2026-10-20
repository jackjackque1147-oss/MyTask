# MyTask

MyTask is a Windows desktop automation application inspired by AutoHotkey. This initial .NET 9 WPF build provides global input recording, editable macro events, playback, and a local script library in a dark-themed interface.

> **Project status:** This is an early MVP. System-tray controls, syntax highlighting, variable inspection/debugging, conditional blocks, and system-command actions are not implemented yet.

## Available features

- **Execution dashboard:** Play/stop controls, loop progress, CPU and memory monitor, and input-hook status.
- **Recorder studio:** Explicitly start/stop global keyboard and mouse recording; edit, duplicate, delete, or add waits, key presses, and clicks.
- **Playback:** Replay events with recorded delays, keyboard/mouse actions, a three-second target-switch countdown, and finite or unlimited looping. Playback can be paused/resumed or stopped with global hotkeys.
- **Global hotkeys:** Configure and persist shortcuts for toggling recording, toggling playback, stopping all actions, and pausing/resuming playback.
- **Script library:** Save JSON recordings and `.mytask` scripts, browse local scripts, and load a script by double-clicking it.
- **Modern dark UI:** Indigo, purple, and cyan accents.

## Technology

- C# and .NET 9
- WPF desktop UI
- MVVM with CommunityToolkit.Mvvm
- Windows low-level keyboard/mouse hooks and `SendInput`

## Data directories

User data is stored in `%LOCALAPPDATA%\MyTask` so the application can write data without requiring installation-folder permissions:

```text
%LOCALAPPDATA%\MyTask\
├── Scripts/     # User-authored scripts
├── Recorded/    # Saved raw recordings
├── Logs/        # Error log
└── Config/      # Application settings and keybindings
```

## Installation

Download the latest Windows installer from [GitHub Releases](https://github.com/jackjackque1147-oss/MyTask/releases/latest). For portable downloads, source-build steps, and upgrade/uninstall guidance, see [INSTALLATION.md](INSTALLATION.md).

## Safety and permissions

Recording is opt-in and begins only after pressing Record. While active, MyTask captures keyboard and mouse input system-wide, including input in other applications; captured data remains in memory until you save it. Playback sends input to the currently active application. After starting playback, use the three-second countdown to switch to the intended target. Do not record passwords or other sensitive input. Pausing or stopping playback releases keys and mouse buttons that remain held by the macro.

The default global shortcuts are `Ctrl+Alt+F9` (toggle recording), `Ctrl+Alt+F10` (toggle playback), `Ctrl+Alt+F11` (stop all), and `Ctrl+Alt+F12` (pause/resume). Change them from **Settings** if another application already uses a shortcut.

## Contributing

Build requirements and commands are in [INSTALLATION.md](INSTALLATION.md). There is not yet an automated test suite.
