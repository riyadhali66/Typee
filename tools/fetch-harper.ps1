# Downloads the pinned harper-ls Windows x64 release asset, verifies SHA256,
# and copies harper-ls.exe + LICENSE into src/Typee.App/runtimes/harper/
# (also mirrored into build output via the App csproj). Never commit binaries.

$ErrorActionPreference = "Stop"

$RepoRoot = Split-Path -Parent $PSScriptRoot
$VersionFile = Join-Path $PSScriptRoot "harper.version"
$ShaFile = Join-Path $PSScriptRoot "harper.sha256"
$Tag = (Get-Content -Path $VersionFile -Raw).Trim()
if ([string]::IsNullOrWhiteSpace($Tag)) {
    throw "tools/harper.version is empty."
}

$AssetName = "harper-ls-x86_64-pc-windows-msvc.zip"
$Url = "https://github.com/Automattic/harper/releases/download/$Tag/$AssetName"
$OutDir = Join-Path $RepoRoot "src\Typee.App\runtimes\harper"
$TempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("typee-harper-" + [Guid]::NewGuid().ToString("N"))
$ZipPath = Join-Path $TempRoot $AssetName
$MaxAttempts = 3

Write-Host "Harper pin: $Tag"
Write-Host "Asset: $Url"
Write-Host "Output directory: $OutDir"

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
New-Item -ItemType Directory -Force -Path $TempRoot | Out-Null

function Get-ExpectedSha256 {
    if (Test-Path $ShaFile) {
        $raw = (Get-Content -Path $ShaFile -Raw).Trim()
        if ($raw -match '^[A-Fa-f0-9]{64}') {
            return $Matches[0].ToLowerInvariant()
        }
    }

    return $null
}

function Invoke-DownloadWithRetry {
    param([string]$Uri, [string]$Destination)

    $attempt = 0
    while ($true) {
        $attempt++
        try {
            Write-Host "Download attempt $attempt/$MaxAttempts..."
            Invoke-WebRequest -Uri $Uri -OutFile $Destination -UseBasicParsing
            return
        }
        catch {
            Write-Warning "Download failed: $($_.Exception.Message)"
            if ($attempt -ge $MaxAttempts) {
                throw
            }

            Start-Sleep -Seconds (2 * $attempt)
        }
    }
}

try {
    Invoke-DownloadWithRetry -Uri $Url -Destination $ZipPath

    $actualHash = (Get-FileHash -Path $ZipPath -Algorithm SHA256).Hash.ToLowerInvariant()
    Write-Host "Downloaded SHA256: $actualHash"

    $expected = Get-ExpectedSha256
    if ($null -eq $expected) {
        # First pin / local bootstrap: record the verified download hash for CI.
        Set-Content -Path $ShaFile -Value $actualHash -Encoding ascii
        Write-Host "Wrote tools/harper.sha256 (bootstrap)."
        $expected = $actualHash
    }

    if ($actualHash -ne $expected) {
        throw "SHA256 mismatch for $AssetName. Expected $expected, got $actualHash."
    }

    Write-Host "SHA256 verified."

    $ExtractDir = Join-Path $TempRoot "extract"
    New-Item -ItemType Directory -Force -Path $ExtractDir | Out-Null
    Expand-Archive -Path $ZipPath -DestinationPath $ExtractDir -Force

    $exe = Get-ChildItem -Path $ExtractDir -Recurse -Filter "harper-ls.exe" | Select-Object -First 1
    if ($null -eq $exe) {
        throw "harper-ls.exe not found inside $AssetName."
    }

    Copy-Item -Path $exe.FullName -Destination (Join-Path $OutDir "harper-ls.exe") -Force

    $license = Get-ChildItem -Path $ExtractDir -Recurse -Filter "LICENSE*" | Select-Object -First 1
    $licenseDest = Join-Path $OutDir "LICENSE"
    if ($null -ne $license) {
        Copy-Item -Path $license.FullName -Destination $licenseDest -Force
    }
    else {
        # Zip may omit LICENSE; fetch the release-tag license from the Harper repo.
        $licenseUrl = "https://raw.githubusercontent.com/Automattic/harper/$Tag/LICENSE"
        Write-Host "LICENSE not in zip; fetching $licenseUrl"
        Invoke-WebRequest -Uri $licenseUrl -OutFile $licenseDest -UseBasicParsing
    }

    $readme = @"
Official harper-ls binary for Typee (pin $Tag).
Downloaded by tools/fetch-harper.ps1 — do not commit binaries.
Source: https://github.com/Automattic/harper/releases/tag/$Tag
"@
    Set-Content -Path (Join-Path $OutDir "README.txt") -Value $readme -Encoding UTF8

    Write-Host "Installed:"
    Get-ChildItem $OutDir | ForEach-Object { Write-Host "  $($_.Name)" }
    exit 0
}
catch {
    Write-Error "fetch-harper.ps1 failed after retries: $($_.Exception.Message)"
    Write-Host "[!] Harper binary not installed. Typee will use the built-in fallback grammar checker."
    exit 1
}
finally {
    if (Test-Path $TempRoot) {
        Remove-Item -Path $TempRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}
