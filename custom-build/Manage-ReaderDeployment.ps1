[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('Prepare', 'Install', 'Verify', 'Rollback')][string]$Mode,
    [Parameter(Mandatory)][string]$BackupDirectory,
    [string]$ReleaseDirectory,
    [string]$Installation = 'C:\Program Files\Jellyfin\Server',
    [string]$DataDirectory = 'C:\ProgramData\Jellyfin\Server',
    [string]$ServerUrl = 'http://127.0.0.1:8096'
)
. "$PSScriptRoot/ReaderBuild.Common.ps1"
$planPath = Get-ChildPath $BackupDirectory deployment.json
if ($Mode -eq 'Prepare') {
    if (Test-Path -LiteralPath $BackupDirectory) { throw 'Use a new backup directory.' }
    foreach ($source in @($Installation, $DataDirectory, $ReleaseDirectory)) {
        Assert-SeparateDirectories $BackupDirectory $source
    }
    $manifest = Get-Content -LiteralPath (Get-ChildPath $ReleaseDirectory release.json) -Raw | ConvertFrom-Json
    $payload = Get-ChildPath $ReleaseDirectory payload
    Assert-ReleaseFiles $payload $manifest
    $version = (Get-Item -LiteralPath (Get-ChildPath $Installation jellyfin.exe)).VersionInfo.ProductVersion
    if ($version -notmatch '^10\.11\.11(?:[+\-]|$)') { throw "Refusing a cross-version deployment: $version" }
    $parent = Split-Path -Parent ([IO.Path]::GetFullPath($Installation))
    $leaf = Split-Path -Leaf $Installation
    $suffix = Get-Date -Format 'yyyyMMdd-HHmmss'
    $stage = Get-ChildPath $parent "$leaf.reader-staged-$suffix"
    $previous = Get-ChildPath $parent "$leaf.reader-previous-$suffix"
    if ((Test-Path -LiteralPath $stage) -or (Test-Path -LiteralPath $previous)) { throw 'A staging or rollback directory already exists.' }
    New-Item -ItemType Directory -Path $BackupDirectory | Out-Null
    Copy-Tree $Installation (Get-ChildPath $BackupDirectory installation)
    Copy-Tree $Installation $stage
    # Only this newly created staging directory is pruned. The live web tree is untouched.
    $stagedWeb = Get-ChildPath $stage jellyfin-web
    Remove-Item -LiteralPath $stagedWeb -Recurse -Force
    Copy-Tree $payload $stage
    foreach ($relative in $manifest.preservedWebFiles) {
        $original = Get-ChildPath $Installation $relative
        if (Test-Path -LiteralPath $original) { Copy-Item -LiteralPath $original -Destination (Get-ChildPath $stage $relative) -Force }
    }
    Write-Json (Get-ChildPath $stage .reader-fork-release.json) $manifest
    Assert-ReleaseFiles $stage $manifest -Installed
    $plan = [ordered]@{
        installation = [IO.Path]::GetFullPath($Installation)
        dataDirectory = [IO.Path]::GetFullPath($DataDirectory)
        stage = $stage
        previous = $previous
        releaseDirectory = [IO.Path]::GetFullPath($ReleaseDirectory)
        release = $manifest.release
        state = 'prepared'
    }
    Write-Json $planPath $plan
    Write-Output 'Prepared a verified installation and an independent installation backup. Stop Jellyfin and its tray, then run Install.'
    return
}
$plan = Get-Content -LiteralPath $planPath -Raw | ConvertFrom-Json
$Installation = $plan.installation
$DataDirectory = $plan.dataDirectory
$manifest = Get-Content -LiteralPath (Get-ChildPath $plan.releaseDirectory release.json) -Raw | ConvertFrom-Json
$parent = Split-Path -Parent $Installation
# Resolve all rename targets as direct children of the recorded installation parent.
foreach ($target in @($Installation, $plan.stage, $plan.previous)) {
    $validated = Get-ChildPath $parent (Split-Path -Leaf $target)
    if ($validated -ne [IO.Path]::GetFullPath($target)) { throw "Unsafe installation target: $target" }
}
$plugin = Get-ChildPath (Get-ChildPath $DataDirectory plugins) $manifest.retiredPlugin
$retiredPlugin = Get-ChildPath $BackupDirectory disabled-comic-plugin
if ($Mode -eq 'Verify') {
    Assert-ReleaseFiles $Installation $manifest -Installed
    if (Test-Path -LiteralPath $plugin) { throw 'The retired plugin would duplicate the native comic API.' }
    $info = Invoke-RestMethod -Uri "$ServerUrl/System/Info/Public" -TimeoutSec 15
    if ($info.Version -ne $manifest.baseVersion) { throw 'The running server version does not match the release.' }
    Write-Output "Verified $($manifest.release): installed hashes, retired plugin and running server version."
    return
}
Assert-InstallationStopped $Installation
if ($Mode -eq 'Install') {
    if ($plan.state -ne 'prepared') { throw "Cannot install a deployment in state $($plan.state)." }
    Assert-ReleaseFiles $plan.stage $manifest -Installed
    # Copy the entire data directory while the process is stopped, including SQLite journals.
    Copy-Tree $DataDirectory (Get-ChildPath $BackupDirectory data)
    try {
        if (Test-Path -LiteralPath $plugin) { Move-Item -LiteralPath $plugin -Destination $retiredPlugin }
        Move-Item -LiteralPath $Installation -Destination $plan.previous
        Move-Item -LiteralPath $plan.stage -Destination $Installation
        Assert-ReleaseFiles $Installation $manifest -Installed
        $plan.state = 'installed'
        Write-Json $planPath $plan
    } catch {
        if ((Test-Path -LiteralPath $Installation) -and (Test-Path -LiteralPath $plan.previous) -and -not (Test-Path -LiteralPath $plan.stage)) {
            Move-Item -LiteralPath $Installation -Destination $plan.stage
        }
        if (-not (Test-Path -LiteralPath $Installation) -and (Test-Path -LiteralPath $plan.previous)) {
            Move-Item -LiteralPath $plan.previous -Destination $Installation
        }
        if ((Test-Path -LiteralPath $retiredPlugin) -and -not (Test-Path -LiteralPath $plugin)) {
            Move-Item -LiteralPath $retiredPlugin -Destination $plugin
        }
        throw
    }
    Write-Output 'Installed. Start Jellyfin using its usual tray or service, then run Verify.'
    return
}
if ($plan.state -ne 'installed') { throw "Cannot roll back a deployment in state $($plan.state)." }
if (-not (Test-Path -LiteralPath $plan.previous)) { throw 'The original installation directory is missing.' }
if (Test-Path -LiteralPath $plan.stage) { throw 'The staging location is occupied; refusing to overwrite it.' }
if ((Test-Path -LiteralPath $retiredPlugin) -and (Test-Path -LiteralPath $plugin)) { throw 'The retired plugin location is occupied.' }
Move-Item -LiteralPath $Installation -Destination $plan.stage
try {
    Move-Item -LiteralPath $plan.previous -Destination $Installation
    if (Test-Path -LiteralPath $retiredPlugin) { Move-Item -LiteralPath $retiredPlugin -Destination $plugin }
} catch {
    if (-not (Test-Path -LiteralPath $Installation)) { Move-Item -LiteralPath $plan.stage -Destination $Installation }
    throw
}
$plan.state = 'rolled-back'
Write-Json $planPath $plan
Write-Output 'Restored the original installation and comic plugin. Current reading progress and other data were preserved. Start Jellyfin normally.'
