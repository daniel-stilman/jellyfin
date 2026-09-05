[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$WebRepository,
    [Parameter(Mandatory)][string]$ServerBuild,
    [Parameter(Mandatory)][string]$WebBuild,
    [Parameter(Mandatory)][string]$OutputDirectory
)
. "$PSScriptRoot/ReaderBuild.Common.ps1"
$serverRepository = Split-Path -Parent $PSScriptRoot
$sources = @{}
foreach ($entry in @{ server = $serverRepository; web = $WebRepository }.GetEnumerator()) {
    $status = git -C $entry.Value status --porcelain
    if ($LASTEXITCODE -ne 0 -or $status) { throw "Commit all source changes before packaging: $($entry.Value)" }
    git -C $entry.Value merge-base --is-ancestor v10.11.11 HEAD
    if ($LASTEXITCODE -ne 0) { throw 'This release workflow requires the v10.11.11 source baseline.' }
    $sources[$entry.Key] = @{
        commit = (git -C $entry.Value rev-parse HEAD).Trim()
        repository = (git -C $entry.Value remote get-url origin).Trim()
    }
}
if (Test-Path -LiteralPath $OutputDirectory) { throw 'Use a new, empty release directory.' }
foreach ($source in @($ServerBuild, $WebBuild, $serverRepository, $WebRepository)) {
    Assert-SeparateDirectories $OutputDirectory $source
}
if ((Get-Content -LiteralPath (Join-Path $WebRepository package.json) -Raw | ConvertFrom-Json).version -ne '10.11.11') {
    throw 'The web release must remain on 10.11.11.'
}
$serverVersion = (Get-Item -LiteralPath (Join-Path $ServerBuild jellyfin.exe)).VersionInfo.ProductVersion
if ($serverVersion -notmatch '^10\.11\.11(?:[+\-]|$)') { throw "Unexpected server version: $serverVersion" }
New-Item -ItemType Directory -Path $OutputDirectory | Out-Null
$payload = Get-ChildPath $OutputDirectory payload
Copy-Tree $ServerBuild $payload
Copy-Tree $WebBuild (Get-ChildPath $payload jellyfin-web)
$manifest = [ordered]@{
    schemaVersion = 1
    release = '10.11.11-readers-' + (Get-Date -Format 'yyyyMMdd-HHmmss')
    baseVersion = '10.11.11'
    builtAt = [DateTime]::UtcNow.ToString('o')
    sources = $sources
    preservedWebFiles = @('jellyfin-web/config.json', 'jellyfin-web/manifest.json', 'jellyfin-web/robots.txt')
    retiredPlugin = 'Custom Comic Pages_1.0.0.0'
    files = @(Get-ChildItem -LiteralPath $payload -File -Recurse | Sort-Object FullName | ForEach-Object {
        @{ path = [IO.Path]::GetRelativePath($payload, $_.FullName).Replace('\', '/'); sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
    })
}
Write-Json (Get-ChildPath $OutputDirectory release.json) $manifest
Assert-ReleaseFiles $payload $manifest
Write-Output "Packaged and hash-verified $($manifest.files.Count) files: $OutputDirectory"
