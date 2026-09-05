[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('Prepare', 'Install', 'Verify', 'Rollback')][string]$Mode,
    [Parameter(Mandatory)][string]$BackupDirectory,
    [string]$ReleaseDirectory,
    [string]$Installation = 'C:\Program Files\Jellyfin\Server',
    [string]$ServerUrl = 'http://127.0.0.1:8096'
)
. "$PSScriptRoot/ReaderBuild.Common.ps1"
$planPath = Get-ChildPath $BackupDirectory web-deployment.json

function Assert-BackendUnchanged($Manifest, [string]$Root) {
    foreach ($file in $Manifest.files | Where-Object { -not $_.path.StartsWith('jellyfin-web/') }) {
        $path = Get-ChildPath $Root $file.path
        if (-not (Test-Path -LiteralPath $path) -or (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $file.sha256) {
            throw "A web-only update requires identical server files: $($file.path)"
        }
    }
}

function Assert-WebFiles($Manifest, [string]$Root) {
    foreach ($file in $Manifest.files | Where-Object { $_.path.StartsWith('jellyfin-web/') }) {
        if ($file.path -in $Manifest.preservedWebFiles) { continue }
        $path = Get-ChildPath $Root $file.path.Substring('jellyfin-web/'.Length)
        if (-not (Test-Path -LiteralPath $path) -or (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $file.sha256) {
            throw "Web release hash mismatch: $($file.path)"
        }
    }
}

if ($Mode -eq 'Prepare') {
    if (Test-Path -LiteralPath $BackupDirectory) { throw 'Use a new backup directory.' }
    Assert-SeparateDirectories $BackupDirectory $Installation
    Assert-SeparateDirectories $BackupDirectory $ReleaseDirectory
    $manifest = Get-Content -LiteralPath (Get-ChildPath $ReleaseDirectory release.json) -Raw | ConvertFrom-Json
    Assert-ReleaseFiles (Get-ChildPath $ReleaseDirectory payload) $manifest
    Assert-BackendUnchanged $manifest $Installation
    $stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
    $stage = Get-ChildPath $Installation "jellyfin-web.reader-staged-$stamp"
    $previous = Get-ChildPath $Installation "jellyfin-web.reader-previous-$stamp"
    if ((Test-Path -LiteralPath $stage) -or (Test-Path -LiteralPath $previous)) { throw 'Staging path already exists.' }
    $web = Get-ChildPath $Installation jellyfin-web
    New-Item -ItemType Directory -Path $BackupDirectory | Out-Null
    Copy-Tree $web (Get-ChildPath $BackupDirectory web)
    Copy-Item -LiteralPath (Get-ChildPath $Installation .reader-fork-release.json) -Destination (Get-ChildPath $BackupDirectory previous-release.json)
    # Retain older hashed chunks for clients which already have the previous app open.
    Copy-Tree $web $stage
    Copy-Tree (Get-ChildPath $ReleaseDirectory payload/jellyfin-web) $stage
    foreach ($relative in $manifest.preservedWebFiles) {
        $name = $relative.Substring('jellyfin-web/'.Length)
        $source = Get-ChildPath $web $name
        if (Test-Path -LiteralPath $source) { Copy-Item -LiteralPath $source -Destination (Get-ChildPath $stage $name) -Force }
    }
    Assert-WebFiles $manifest $stage
    Write-Json $planPath ([ordered]@{
        installation = [IO.Path]::GetFullPath($Installation)
        stage = $stage
        previous = $previous
        releaseDirectory = [IO.Path]::GetFullPath($ReleaseDirectory)
        state = 'prepared'
    })
    Write-Output 'Prepared a verified web update and an independent backup.'
    return
}

$plan = Get-Content -LiteralPath $planPath -Raw | ConvertFrom-Json
$Installation = $plan.installation
foreach ($target in @($plan.stage, $plan.previous)) {
    if ((Get-ChildPath $Installation (Split-Path -Leaf $target)) -ne [IO.Path]::GetFullPath($target)) { throw "Unsafe web target: $target" }
}
$manifest = Get-Content -LiteralPath (Get-ChildPath $plan.releaseDirectory release.json) -Raw | ConvertFrom-Json
$web = Get-ChildPath $Installation jellyfin-web
$marker = Get-ChildPath $Installation .reader-fork-release.json
Assert-BackendUnchanged $manifest $Installation

if ($Mode -eq 'Verify') {
    Assert-WebFiles $manifest $web
    $info = Invoke-RestMethod -Uri "$ServerUrl/System/Info/Public" -TimeoutSec 15
    if ($info.Version -ne $manifest.baseVersion) { throw 'Unexpected running server version.' }
    $response = Invoke-WebRequest -Uri "$ServerUrl/web/index.html" -TimeoutSec 15
    if ($response.Content -ne [IO.File]::ReadAllText((Get-ChildPath $web index.html))) { throw 'The server is not serving the installed web entry point.' }
    Write-Output 'Verified installed web hashes, unchanged server files and the served entry point.'
    return
}

if ($Mode -eq 'Install') {
    if ($plan.state -ne 'prepared') { throw 'The update is not prepared.' }
    Assert-WebFiles $manifest $plan.stage
    try {
        Move-Item -LiteralPath $web -Destination $plan.previous
        Move-Item -LiteralPath $plan.stage -Destination $web
        Assert-WebFiles $manifest $web
        Write-Json $marker $manifest
        $plan.state = 'installed'
        Write-Json $planPath $plan
    } catch {
        if ((Test-Path -LiteralPath $web) -and (Test-Path -LiteralPath $plan.previous) -and -not (Test-Path -LiteralPath $plan.stage)) {
            Move-Item -LiteralPath $web -Destination $plan.stage
        }
        if (-not (Test-Path -LiteralPath $web) -and (Test-Path -LiteralPath $plan.previous)) {
            Move-Item -LiteralPath $plan.previous -Destination $web
        }
        Copy-Item -LiteralPath (Get-ChildPath $BackupDirectory previous-release.json) -Destination $marker -Force
        throw
    }
    Write-Output 'Installed the web update. The server process and reading database were not modified.'
    return
}

if ($plan.state -ne 'installed') { throw 'Only an installed web update can be rolled back.' }
if (Test-Path -LiteralPath $plan.stage) { throw 'Rollback staging path is occupied.' }
Move-Item -LiteralPath $web -Destination $plan.stage
try { Move-Item -LiteralPath $plan.previous -Destination $web } catch {
    Move-Item -LiteralPath $plan.stage -Destination $web
    throw
}
Copy-Item -LiteralPath (Get-ChildPath $BackupDirectory previous-release.json) -Destination $marker -Force
$plan.state = 'rolled-back'
Write-Json $planPath $plan
Write-Output 'Restored the previous web client and release marker.'
