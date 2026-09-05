Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-ChildPath([string]$Root, [string]$Relative) {
    $base = [IO.Path]::GetFullPath($Root).TrimEnd('\') + '\'
    $path = [IO.Path]::GetFullPath((Join-Path $base $Relative))
    if ([IO.Path]::IsPathRooted($Relative) -or -not $path.StartsWith($base, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Path escapes its declared directory: $Relative"
    }
    return $path
}

function Copy-Tree([string]$Source, [string]$Destination) {
    if (-not (Test-Path -LiteralPath $Source -PathType Container)) { throw "Missing source: $Source" }
    & robocopy.exe $Source $Destination /E /IS /IT /COPY:DAT /DCOPY:DAT /XJ /R:1 /W:1 /NFL /NDL /NJH /NJS | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "Copy failed ($LASTEXITCODE): $Source" }
}

function Write-Json([string]$Path, $Value) {
    [IO.File]::WriteAllText($Path, ($Value | ConvertTo-Json -Depth 12) + [Environment]::NewLine)
}

function Assert-ReleaseFiles([string]$Root, $Manifest, [switch]$Installed) {
    foreach ($file in $Manifest.files) {
        if ($Installed -and $file.path -in $Manifest.preservedWebFiles) { continue }
        $path = Get-ChildPath $Root $file.path
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing release file: $path" }
        if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $file.sha256) {
            throw "Release file hash mismatch: $path"
        }
    }
}

function Assert-InstallationStopped([string]$Installation) {
    $prefix = [IO.Path]::GetFullPath($Installation).TrimEnd('\') + '\'
    $running = @(Get-CimInstance Win32_Process | Where-Object {
        $_.ExecutablePath -and $_.ExecutablePath.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)
    })
    if ($running.Count) { throw 'Stop this Jellyfin server and its tray before installing or rolling back.' }
}

function Assert-SeparateDirectories([string]$First, [string]$Second) {
    $a = [IO.Path]::GetFullPath($First).TrimEnd('\') + '\'
    $b = [IO.Path]::GetFullPath($Second).TrimEnd('\') + '\'
    if ($a.StartsWith($b, [StringComparison]::OrdinalIgnoreCase) -or $b.StartsWith($a, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Directories must not contain one another: $First and $Second"
    }
}
