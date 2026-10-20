# MyTask EXE Installation Guide

## Current availability

This workspace includes the MyTask source project and a GitHub Actions release workflow. The workflow creates a Windows setup installer, a portable executable, a ZIP archive, and SHA-256 checksums when a `v*` release tag is pushed. The current GitHub repository is empty until the source is pushed there; see [Publish a GitHub release](#publish-a-github-release).

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

The target repository is [jackjackque1147-oss/MyTask](https://github.com/jackjackque1147-oss/MyTask). It is currently empty. The development environment used to prepare this guide has no Git client or GitHub write integration, so the initial source push must be done from your computer:

1. Install and open **GitHub Desktop**, then sign in to the GitHub account that owns the repository.
2. In GitHub Desktop, choose **File > Clone repository > URL** and enter `https://github.com/jackjackque1147-oss/MyTask`. Clone the empty repository to a local folder.
3. Copy the project source files into the cloned folder. Include `.github`, `installer`, `Models`, `Services`, and `ViewModels` folders, plus `.gitignore`, `App.xaml`, `App.xaml.cs`, `INSTALLATION.md`, `MainWindow.xaml`, `MainWindow.xaml.cs`, `MyTask.csproj`, and `README.md`.
4. Do **not** copy `bin`, `obj`, `publish`, or `artifacts`. These generated files are ignored by `.gitignore`; the release workflow builds and attaches the application itself.
5. In GitHub Desktop, review the changed-file list, enter a summary such as `Add MyTask desktop application`, choose **Commit to main**, then choose **Push origin**.
6. Open the repository's **Releases** page and choose **Draft a new release**. Enter a new tag such as `v0.1.0`, select `main` as the target, add release notes if desired, then publish the release.
7. The tag triggers the **Build and publish Windows release** GitHub Actions workflow. When it completes, the release will contain the setup installer, portable `.exe`, portable ZIP, and `SHA256SUMS.txt`.

If the workflow cannot create a release asset, open **Settings > Actions > General** in the repository and ensure the workflow has permission to read and write repository contents. The workflow requests only the standard `contents: write` permission needed to attach release files.

To create a later release, push the source changes first, then draft a release using a new version tag such as `v0.1.1`.

## Troubleshooting

- **Release assets are missing:** Check the Actions tab for the tag-triggered workflow run and resolve its build error before retrying.
- **Windows blocks the app:** Verify the source and publisher. Do not disable security protections to run an untrusted copy.
- **The app cannot save files:** Move it from a protected directory such as `Program Files` to a user-writable folder, or use an installer that grants appropriate per-user data locations.
- **Hooks do not work:** Recording is available only while MyTask is open and recording was explicitly started. Check whether Windows or another application is blocking global input hooks.
- **A global hotkey does not work:** Open Settings, assign a shortcut that is not already registered by Windows or another app, and click **Apply hotkeys**. The shortcuts work while MyTask is running; system-tray controls are not implemented in this MVP.
- **Unlimited playback will not stop by itself:** This is expected. Click **Stop playback** or use the configured stop/play hotkey to end it.

## Data and uninstall notes

Scripts, recordings, logs, and configuration are stored separately from the executable under `%LOCALAPPDATA%\MyTask`. Before uninstalling or replacing an installation, back up any user data you want to retain. Follow release-specific documentation if a future release changes these paths.
