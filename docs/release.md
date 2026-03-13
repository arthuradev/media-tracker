# Media Tracker Release Flow

This project now ships with a Windows-first release path for `Media Tracker`.

## Versioning

- The application version lives in [MediaTracker.csproj](/C:/Users/Arthur/Dev/Projetos/Codex/src/MediaTracker/MediaTracker.csproj).
- Update `<Version>`, `<AssemblyVersion>`, `<FileVersion>` and `<InformationalVersion>` before cutting a release.

## Release artifacts

The release script produces:

- a self-contained `win-x64` publish folder
- a `.zip` portable release
- a `.sha256` checksum for the zip
- a `MediaTracker.latest.json` update manifest
- an Inno Setup installer, if `ISCC.exe` is installed locally

All generated outputs go under `artifacts/`, which is already ignored by git.

## Publish profile

The app uses [Release-WinX64.pubxml](/C:/Users/Arthur/Dev/Projetos/Codex/src/MediaTracker/Properties/PublishProfiles/Release-WinX64.pubxml) for the default desktop release:

- `Release`
- `win-x64`
- self-contained
- single-file
- compression enabled
- symbols stripped from publish output

## Release command

From the workspace root:

```powershell
.\scripts\publish-release.ps1 -Version 1.0.0
```

Optional flags:

```powershell
.\scripts\publish-release.ps1 -Version 1.0.0 -SkipInstaller
.\scripts\publish-release.ps1 -Version 1.0.0 -Configuration Release -Runtime win-x64
```

## Installer

The installer script lives at [MediaTracker.iss](/C:/Users/Arthur/Dev/Projetos/Codex/installer/MediaTracker.iss).

It installs per-user to:

```text
%LocalAppData%\Programs\Media Tracker
```

This keeps the first version simple:

- no admin rights required by default
- Start Menu shortcut included
- optional desktop shortcut
- uninstall support via Windows Apps settings

## Update strategy for V1

`V1` now supports a simple in-app update warning:

1. publish a new `zip` and installer
2. upload `MediaTracker.latest.json` to a stable URL
3. point the app to that manifest in `Settings`
4. let the app warn when a newer version is available
5. install the new version over the existing one

Because the app data folder is outside the install folder, reinstalling does not wipe the local database, cache or logs.

The generated manifest includes:

- `version`
- `downloadUrl`
- `portableUrl`
- `notes`
- `publishedAtUtc`

By default, `downloadUrl` points to the installer when one was created. If the installer is skipped or Inno Setup is unavailable, it falls back to the portable zip.

## Notes

- The release script performs a runtime-specific restore before publish, so `win-x64` assets are generated correctly.
- If Inno Setup is not installed, the script still produces the portable `zip` release and checksum.

## Future upgrade path

When Phase 7 moves beyond the initial release, the next natural step is:

- code signing
- hosted release feed
- MSIX/App Installer based updates
- automatic download and install flow
