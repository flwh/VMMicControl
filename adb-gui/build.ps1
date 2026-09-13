# Build AdbGui.exe with the built-in .NET Framework compiler (no SDK required)
$ErrorActionPreference = 'Stop'

function New-AdbIcon([string]$Path) {
    Add-Type -AssemblyName System.Drawing
    $size = 64
    $bmp  = New-Object System.Drawing.Bitmap($size, $size)
    $g    = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)

    function Rounded([int]$x, [int]$y, [int]$w, [int]$h, [int]$r) {
        $p = New-Object System.Drawing.Drawing2D.GraphicsPath
        $d = $r * 2
        $p.AddArc($x, $y, $d, $d, 180, 90)
        $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
        $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
        $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
        $p.CloseFigure()
        return $p
    }

    $bg = Rounded 2 2 60 60 14
    $g.FillPath((New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 30, 33, 41))), $bg)
    $g.DrawPath((New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 90, 150, 255), 2.5)), $bg)

    $phone = Rounded 22 11 20 42 5
    $g.FillPath((New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 233, 237, 244))), $phone)
    $screen = Rounded 25 16 14 27 2
    $g.FillPath((New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 90, 150, 255))), $screen)
    $dot = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 46, 204, 113))
    $g.FillEllipse($dot, 29, 46, 6, 6)

    $hIcon = $bmp.GetHicon()
    $icon  = [System.Drawing.Icon]::FromHandle($hIcon)
    $fs    = [System.IO.File]::Create($Path)
    $icon.Save($fs)
    $fs.Close()
    $g.Dispose(); $bmp.Dispose(); $icon.Dispose()
    return $Path
}

$root   = $PSScriptRoot
$outDir = Join-Path $root 'bin'
$csc    = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
if (-not (Test-Path $csc)) { throw "csc.exe not found under $env:WINDIR\Microsoft.NET" }

New-Item -ItemType Directory -Force -Path $outDir | Out-Null

# ---- app icon: reuse adb.ico if present, otherwise draw one ----
$icon = Join-Path $root 'adb.ico'
if (-not (Test-Path $icon)) { $icon = Join-Path $outDir 'adb.ico' }
if (-not (Test-Path $icon)) {
    try { $icon = New-AdbIcon $icon } catch { Write-Host "icon skipped: $($_.Exception.Message)" -ForegroundColor DarkYellow; $icon = $null }
}

$sources = Get-ChildItem (Join-Path $root 'src') -Filter *.cs | Sort-Object Name | ForEach-Object { $_.FullName }
if (-not $sources) { throw "no C# sources found in $root\src" }

$out = Join-Path $outDir 'AdbGui.exe'

$cscArgs = @(
    '/nologo', '/optimize+', '/target:winexe', '/platform:x64', '/codepage:65001',
    '/r:System.dll', '/r:System.Drawing.dll', '/r:System.Windows.Forms.dll',
    ('/out:' + $out)
)
if ($icon) { $cscArgs += ('/win32icon:' + $icon) }

& $csc @cscArgs $sources
if ($LASTEXITCODE -ne 0) { throw "Build failed with exit code $LASTEXITCODE" }

Write-Host "Build OK -> $out" -ForegroundColor Green
