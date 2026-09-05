[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$WebRepository,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [string]$DotNet = 'dotnet'
)
. "$PSScriptRoot/ReaderBuild.Common.ps1"
$serverRepository = Split-Path -Parent $PSScriptRoot
if (Test-Path -LiteralPath $OutputDirectory) { throw 'Use a new build output directory.' }
New-Item -ItemType Directory -Path $OutputDirectory | Out-Null
function Invoke-Checked([string]$Command, [string[]]$Arguments) {
    & $Command @Arguments
    if ($LASTEXITCODE -ne 0) { throw "Build step failed: $Command $Arguments" }
}
Push-Location $WebRepository
try {
    Invoke-Checked npx.cmd @('--yes', '--package=npm@10.9.3', 'npm', 'ci', '--no-audit', '--no-fund')
    Invoke-Checked npx.cmd @('vitest', '--watch=false', '--config', 'vite.config.ts')
    Invoke-Checked npx.cmd @('tsc', '--noEmit')
    Invoke-Checked npx.cmd @('eslint', 'src/plugins/bookPlayer', 'src/plugins/comicsPlayer', 'src/plugins/pdfPlayer', 'src/utils/bookPlayer*.ts', 'src/utils/readerNavigation*.ts', 'src/utils/pdfPageCache*.ts')
    Invoke-Checked npx.cmd @('stylelint', 'src/plugins/bookPlayer/style.scss', 'src/plugins/comicsPlayer/style.scss', 'src/plugins/pdfPlayer/style.scss', 'src/styles/reader.scss')
    Invoke-Checked npm.cmd @('run', 'build:production')
    Invoke-Checked npm.cmd @('run', 'escheck')
} finally { Pop-Location }
Push-Location $serverRepository
try {
    Invoke-Checked $DotNet @('test', 'Jellyfin.sln', '--configuration', 'Release')
    Invoke-Checked $DotNet @('build', 'Jellyfin.Server/Jellyfin.Server.csproj', '--configuration', 'Debug')
    Invoke-Checked $DotNet @('publish', 'Jellyfin.Server/Jellyfin.Server.csproj', '--configuration', 'Release', '--runtime', 'win-x64', '--self-contained', 'true', '--output', (Get-ChildPath $OutputDirectory server))
} finally { Pop-Location }
& "$PSScriptRoot/New-ReaderRelease.ps1" -WebRepository $WebRepository -ServerBuild (Get-ChildPath $OutputDirectory server) -WebBuild (Join-Path $WebRepository dist) -OutputDirectory (Get-ChildPath $OutputDirectory release)
