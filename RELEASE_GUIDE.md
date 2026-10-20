# MyTask Release Guide

This guide covers the source files, local build, and GitHub release steps for publishing a future MyTask version. Releases are built by the repository's Windows GitHub Actions workflow; do not commit generated executables or installer files.

## Important files

| File or folder | Purpose |
| --- | --- |
| `MyTask.csproj` | .NET target, NuGet dependencies, and executable icon resource |
| `App.xaml`, `App.xaml.cs` | WPF application startup and shared styles |
| `MainWindow.xaml`, `MainWindow.xaml.cs` | Main application interface and window interactions |
| `Models/` | Automation event, script, and settings data types |
| `Services/` | Input hooks, macro playback, path handling, global hotkeys, and pause support |
| `ViewModels/MainViewModel.cs` | Application state, commands, recording/playback operations, and persistence |
| `Assets/MyTask.ico` | Original MyTask mouse icon for the window and executable |
| `installer/MyTask.iss` | Inno Setup configuration for the per-user Windows installer |
| `.github/workflows/release.yml` | Builds the self-contained app and installer, then attaches assets to a GitHub Release |
| `README.md`, `INSTALLATION.md`, `RELEASE_GUIDE.md` | User, install, and maintainer documentation |

Keep the checked-in release source, installer configuration, and workflow together. The workflow reads the `.ico` from `Assets/` and compiles the installer from `installer/`.

## Build and verify locally

Use Windows with the .NET 9 SDK. From the repository root in PowerShell:

```powershell
dotnet restore .\MyTask.csproj
dotnet build .\MyTask.csproj --configuration Release
```

For a local self-contained x64 executable:

```powershell
dotnet publish .\MyTask.csproj `
  --configuration Release `
  --runtime win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  --output .\publish\win-x64
```

Run and test `.\publish\win-x64\MyTask.exe`, including its window/executable icon, global recording, hotkey settings, script saving, and macro playback. The release workflow independently builds the publish output and installer on a clean Windows runner.

## Publish a new version

1. Make and test source changes on `main`. Update documentation whenever user-visible behavior or installation steps change.
2. Confirm the Git working tree contains only intentional changes:

   ```powershell
   git status --short
   ```

3. Commit and push the source changes before creating the release tag:

   ```powershell
   git add --all
   git commit -m "Describe the release change"
   git push origin main
   ```

4. Choose a **new** version tag; never move or reuse a published tag. For example, after `v0.1.0`, use `v0.1.1`:

   ```powershell
   git tag -a v0.1.1 -m "MyTask v0.1.1"
   git push origin v0.1.1
   ```

5. Open the repository's **Actions** tab. The **Build and publish Windows release** workflow runs for tags that start with `v`. Confirm every job step succeeds.
6. Open [GitHub Releases](https://github.com/jackjackque1147-oss/MyTask/releases). The workflow creates a release with a per-user installer, portable executable, portable ZIP, and `SHA256SUMS.txt`.
7. Test the downloaded installer on Windows and check the checksum before sharing the release link.

The version number is taken from the pushed tag, with the initial `v` removed for the installer version. GitHub Actions installs the required Inno Setup compiler; no local installer build is needed for a normal release.

## Generated files (do not commit)

The root `.gitignore` excludes `bin/`, `obj/`, `publish/`, and `artifacts/`. These contain generated build output and installers. The `.ico` under `Assets/` is intentional source and must be committed.

## Release artifact contents

- `MyTask-Setup-vX.Y.Z.exe` — per-user installer; no administrator elevation is required.
- `MyTask.exe` — self-contained Windows x64 executable.
- `MyTask-portable-win-x64-vX.Y.Z.zip` — portable executable archive.
- `SHA256SUMS.txt` — SHA-256 hashes of the release artifacts.

The artifacts are unsigned development releases. Configure code signing separately before presenting them as publisher-verified production software.
