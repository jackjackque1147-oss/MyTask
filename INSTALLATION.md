# MyTask EXE Installation Guide

## Current availability

The `v0.1.0` release is published on [GitHub Releases](https://github.com/jackjackque1147-oss/MyTask/releases/latest). Its assets include the per-user Windows installer, portable executable, portable ZIP, and SHA-256 checksums. The repository also contains a GitHub Actions workflow that builds these assets for future `v*` release tags.

## Run the build in this workspace

A self-contained x64 build is available at:

```text
publish\win-x64\MyTask.exe
```

Run it from File Explorer, or from PowerShell in the project folder:

```powershell
Start-Process .\publish\win-x64\MyTask.exe
```

This is a locally built, unsigned development executable, not an official signed release. Windows may show a publisher warning. The published executable includes the .NET runtime; no separate .NET installation is required to run it.

## Installing a published build

When a release is available on the repository's GitHub Releases page:

1. Download `MyTask-Setup-vX.Y.Z.exe` from the release assets.
2. Optionally verify its SHA-256 hash against `SHA256SUMS.txt`.
3. Run the setup. It installs per-user under `%LOCALAPPDATA%\Programs\MyTask` and does not require administrator access.
4. Alternatively, download the portable ZIP and extract it to a folder you control.
5. To uninstall, use **Settings > Apps > Installed apps** or the Start menu's MyTask uninstall shortcut. Your scripts, recordings, logs, and settings in `%LOCALAPPDATA%\MyTask` are kept.

The Inno Setup installer and portable build are unsigned. Windows may show a publisher warning; verify that the download is from the expected repository before running it.

## Build from source

The WPF application targets .NET 9 and builds on Windows. Install the .NET 9 SDK, open PowerShell in the project folder, then run:

```powershell
dotnet restore .\MyTask.csproj
dotnet build .\MyTask.csproj --configuration Release
```

To create a self-contained x64 publish output:

```powershell
dotnet publish .\MyTask.csproj `
  --configuration Release `
  --runtime win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  --output .\publish\win-x64
```

The executable is `publish\win-x64\MyTask.exe`. The self-contained output includes the .NET runtime but remains Windows-specific. Test it on a clean Windows machine before distribution. The project uses CommunityToolkit.Mvvm and restores it from NuGet.

The publish command creates only the executable. The tagged GitHub Actions workflow compiles the Inno Setup installer, creates the portable ZIP, and calculates SHA-256 checksums. Code signing and update delivery still need to be configured before distributing production builds.

## Publish a GitHub release

The source repository is [jackjackque1147-oss/MyTask](https://github.com/jackjackque1147-oss/MyTask). To publish a later release:

1. Commit and push your source changes to `main`.
2. On GitHub, open **Releases > Draft a new release** and create a new version tag, for example `v0.1.1`, targeting `main`.
3. Publish the release. The tag starts the **Build and publish Windows release** workflow.
4. Check the repository's **Actions** tab. When the workflow succeeds, the release assets will include the setup installer, portable `.exe`, portable ZIP, and `SHA256SUMS.txt`.

If the workflow cannot create a release asset, open **Settings > Actions > General** in the repository and ensure the workflow has permission to read and write repository contents. The workflow requests only the standard `contents: write` permission needed to attach release files.

## Troubleshooting

- **Release assets are missing:** Check the Actions tab for the tag-triggered workflow run and resolve its build error before retrying.
- **Windows blocks the app:** Verify the source and publisher. Do not disable security protections to run an untrusted copy.
- **The app cannot save files:** Move it from a protected directory such as `Program Files` to a user-writable folder, or use an installer that grants appropriate per-user data locations.
- **Hooks do not work:** Recording is available only while MyTask is open and recording was explicitly started. Check whether Windows or another application is blocking global input hooks.
- **A global hotkey does not work:** Open Settings, assign a shortcut that is not already registered by Windows or another app, and click **Apply hotkeys**. The shortcuts work while MyTask is running; system-tray controls are not implemented in this MVP.
- **Unlimited playback will not stop by itself:** This is expected. Click **Stop playback** or use the configured stop/play hotkey to end it.

## Data and uninstall notes

Scripts, recordings, logs, and configuration are stored separately from the executable under `%LOCALAPPDATA%\MyTask`. Before uninstalling or replacing an installation, back up any user data you want to retain. Follow release-specific documentation if a future release changes these paths.
