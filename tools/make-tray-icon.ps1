$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

$assets = Join-Path (Split-Path $PSScriptRoot) "src\Typee.App\Assets"
New-Item -ItemType Directory -Force -Path $assets | Out-Null
$pngPath = Join-Path $assets "tray.png"
$icoPath = Join-Path $assets "tray.ico"

$bmp = New-Object System.Drawing.Bitmap 16, 16
try {
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    try {
        $g.Clear([System.Drawing.Color]::Transparent)
        $fill = [System.Drawing.Color]::FromArgb(255, 32, 120, 220)
        $brush = New-Object System.Drawing.SolidBrush $fill
        try {
            $g.FillRectangle($brush, 0, 0, 16, 16)
        }
        finally {
            $brush.Dispose()
        }

        $white = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::White)
        try {
            $g.FillRectangle($white, 5, 3, 6, 2)
            $g.FillRectangle($white, 7, 3, 2, 10)
        }
        finally {
            $white.Dispose()
        }
    }
    finally {
        $g.Dispose()
    }

    $bmp.Save($pngPath, [System.Drawing.Imaging.ImageFormat]::Png)

    $iconHandle = $bmp.GetHicon()
    $icon = [System.Drawing.Icon]::FromHandle($iconHandle)
    try {
        $fs = [System.IO.File]::Open($icoPath, [System.IO.FileMode]::Create)
        try {
            $icon.Save($fs)
        }
        finally {
            $fs.Dispose()
        }
    }
    finally {
        $icon.Dispose()
    }
}
finally {
    $bmp.Dispose()
}

Write-Host "Wrote $pngPath ($((Get-Item $pngPath).Length) bytes)"
Write-Host "Wrote $icoPath ($((Get-Item $icoPath).Length) bytes)"
