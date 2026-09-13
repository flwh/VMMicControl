# Build VMMicControl.exe with the built-in .NET Framework compiler (no SDK required)
$ErrorActionPreference = 'Stop'

$root   = $PSScriptRoot
$outDir = Join-Path $root 'bin'
$csc    = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'

if (-not (Test-Path $csc)) { throw "csc.exe not found: $csc" }

New-Item -ItemType Directory -Force -Path $outDir | Out-Null

$sources = @(
    (Join-Path $root 'src\VoicemeeterRemote.cs'),
    (Join-Path $root 'src\Theme.cs'),
    (Join-Path $root 'src\Hotkeys.cs'),
    (Join-Path $root 'src\HotkeyForm.cs'),
    (Join-Path $root 'src\ChannelCard.cs'),
    (Join-Path $root 'src\MainForm.cs'),
    (Join-Path $root 'src\Program.cs')
)

$out = Join-Path $outDir 'VMMicControl.exe'
$icon = Join-Path $root 'app.ico'

& $csc /nologo /optimize+ /target:winexe /platform:x64 /codepage:65001 /win32icon:$icon /out:$out $sources
if ($LASTEXITCODE -ne 0) { throw "Build failed with exit code $LASTEXITCODE" }

Write-Host "Build OK -> $out" -ForegroundColor Green
