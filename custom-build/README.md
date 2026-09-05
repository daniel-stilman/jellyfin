# Reader release workflow

Requires Windows, PowerShell 7, Git, Node 20 or newer and the .NET 9 SDK selected by `global.json`. Keep both repositories on committed `custom/10.11.11-readers` branches. The web dependency installation explicitly uses npm 10.9.3, as required by its lockfile.

```powershell
./custom-build/Build-Readers.ps1 -WebRepository ../jellyfin-web -OutputDirectory ../build-20260905 -DotNet dotnet
```

This runs web tests, types, reader lint and the production build; then server tests, analyzers and a self-contained Windows publish. `New-ReaderRelease.ps1` packages already validated outputs when these steps were run separately. Do not package untested outputs. Every release records both source commits and every managed file's SHA-256. Keep builds, data, logs, credentials and backups outside the repositories.

Run deployment commands in an administrator PowerShell when installing under Program Files. Start the tray under the normal user account with its working directory set to the installation directory.

Prepare while the server is running:

```powershell
./custom-build/Manage-ReaderDeployment.ps1 -Mode Prepare -ReleaseDirectory ../build-20260905/release -BackupDirectory ../backups/20260905
```

Preparation makes an independent installation backup and a sibling staging installation. It replaces the staged web tree while preserving local `config.json`, `manifest.json` and `robots.txt`. FFmpeg, the tray and installer files remain available. A different installed base version is rejected.

Stop Jellyfin and its tray using normal administration controls. Install, start normally, then verify:

```powershell
./custom-build/Manage-ReaderDeployment.ps1 -Mode Install -BackupDirectory ../backups/20260905
# Start Jellyfin normally.
./custom-build/Manage-ReaderDeployment.ps1 -Mode Verify -BackupDirectory ../backups/20260905
```

Install requires both processes stopped. It backs up the entire data directory, including SQLite journals, then swaps installation directories. The old Custom Comic Pages plugin moves to the backup because its routes duplicate the native API. Bookshelf, accounts, libraries, parental restrictions, metadata and reading progress stay in the existing data directory. Allow time for the offline data copy.

Rollback after stopping Jellyfin and the tray:

```powershell
./custom-build/Manage-ReaderDeployment.ps1 -Mode Rollback -BackupDirectory ../backups/20260905
# Start Jellyfin normally.
```

Rollback restores the old binaries, web files and plugin while preserving current user data. The separate full data snapshot is for disaster recovery; restoring it would restore older reading progress and must be an explicit decision.

Optional `Installation`, `DataDirectory` and `ServerUrl` parameters support an isolated rehearsal. Use a new backup directory each time. The scripts do not schedule updates or deploy automatically. For future upstream versions, create a branch from the selected release tag, port changes, update the version gates, test both repositories and rehearse against separate data. Never point a newer test server at production data.

After migration, use these tools instead of the earlier overlay installer. `.reader-fork-release.json` identifies the managed installation.

## Updating only the web reader

When the paired release contains identical server files, `Manage-WebReaderDeployment.ps1` can install its web client while Jellyfin keeps running. Run `Prepare` with a new backup directory, the packaged release directory and installation path; then `Install` and `Verify` with that backup directory. `Rollback` restores the previous web client and release marker. The script refuses server-file differences, preserves local web settings and existing hashed chunks, and leaves the server database alone. Use the full deployment workflow for server changes. See [mobile reader validation](MOBILE-READERS.md).
