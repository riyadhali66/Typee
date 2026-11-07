# Downloads the pinned harper-ls Windows x64 release asset.
# Full checksum verification and LICENSE copy land in Phase 6.
# Phase 1 stub: ensures the script exists and is CI-callable without failing the build.

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent $PSScriptRoot
$VersionFile = Join-Path $PSScriptRoot "harper.version"
$Tag = (Get-Content -Path $VersionFile -Raw).Trim()
$OutDir = Join-Path $RepoRoot "src\Typee.App\runtimes\harper"

Write-Host "Harper pin: $Tag"
Write-Host "Output directory: $OutDir"

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

$Marker = Join-Path $OutDir "README.txt"
if (-not (Test-Path $Marker)) {
    @"
harper-ls.exe will be downloaded here by fetch-harper.ps1 (Phase 6).
Pinned tag: $Tag
Source: https://github.com/Automattic/harper/releases
"@ | Set-Content -Path $Marker -Encoding UTF8
}

Write-Host "Phase 1: skipped binary download (implemented in Phase 6)."
exit 0
