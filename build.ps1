# DownloadDock 构建脚本：用系统自带 csc（.NET Framework 4.x）编译单文件 exe
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$src  = Join-Path $root 'src'
$out  = Join-Path $root 'DownloadDock.exe'
$ico  = Join-Path $root 'app.ico'
$manifest = Join-Path $root 'app.manifest'

$csc = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$fw  = Split-Path -Parent $csc
$wpf = Join-Path $fw 'WPF'
if (-not (Test-Path $csc)) { throw "csc.exe not found: $csc" }

$refs = @(
    (Join-Path $wpf 'PresentationFramework.dll'),
    (Join-Path $wpf 'PresentationCore.dll'),
    (Join-Path $wpf 'WindowsBase.dll'),
    (Join-Path $fw  'System.Xaml.dll'),
    (Join-Path $fw  'System.dll'),
    (Join-Path $fw  'System.Core.dll'),
    (Join-Path $fw  'System.Drawing.dll'),
    (Join-Path $fw  'System.Windows.Forms.dll')
)
foreach ($r in $refs) { if (-not (Test-Path $r)) { throw "missing reference: $r" } }

# ---------- 生成 app.ico：半透明圆形 + 白色下载箭头（经典 BMP-in-ICO，旧版 csc 可读） ----------
Add-Type -AssemblyName System.Drawing | Out-Null
$size = 64
$bmp = New-Object System.Drawing.Bitmap $size, $size
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$bg = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(210, 18, 18, 22))
$g.FillEllipse($bg, 2, 2, 60, 60)
$ring = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(120, 255, 255, 255)), 2
$g.DrawEllipse($ring, 2, 2, 60, 60)
$pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(240, 255, 255, 255)), 5
$pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
$pen.EndCap   = [System.Drawing.Drawing2D.LineCap]::Round
$g.DrawLine($pen, 32, 15, 32, 39)   # 竖线
$g.DrawLine($pen, 23, 30, 32, 39)   # 左箭头
$g.DrawLine($pen, 41, 30, 32, 39)   # 右箭头
$g.DrawLine($pen, 21, 48, 43, 48)   # 底部托盘线
$g.Dispose()

# 32bpp BGRA 像素（GDI+ 默认自底向上，正好是 ICO 要求的行序）
$rect = New-Object System.Drawing.Rectangle 0, 0, $size, $size
$bd = $bmp.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$stride = $bd.Stride
$rowBytes = $size * 4
$pix = New-Object byte[] ($rowBytes * $size)
[System.Runtime.InteropServices.Marshal]::Copy($bd.Scan0, $pix, 0, $pix.Length)
$bmp.UnlockBits($bd)
$bmp.Dispose()
if ($stride -ne $rowBytes) {
    $compact = New-Object byte[] ($rowBytes * $size)
    for ($y = 0; $y -lt $size; $y++) {
        [Array]::Copy($pix, ($y * $stride), $compact, ($y * $rowBytes), $rowBytes)
    }
    $pix = $compact
}
$mask = New-Object byte[] (($size / 8) * $size)   # AND 掩码全 0：不透明由 alpha 决定
$imgSize = 40 + $pix.Length + $mask.Length

$fs = [System.IO.File]::Create($ico)
$bw = New-Object System.IO.BinaryWriter($fs)
$bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]1)          # ICONDIR
$bw.Write([byte]$size); $bw.Write([byte]$size); $bw.Write([byte]0); $bw.Write([byte]0)
$bw.Write([uint16]1); $bw.Write([uint16]32)
$bw.Write([uint32]$imgSize); $bw.Write([uint32]22)
$bw.Write([uint32]40); $bw.Write([int32]$size); $bw.Write([int32]($size * 2))  # BITMAPINFOHEADER
$bw.Write([uint16]1); $bw.Write([uint16]32)
$bw.Write([uint32]0); $bw.Write([uint32]$imgSize)
$bw.Write([int32]0); $bw.Write([int32]0); $bw.Write([uint32]0); $bw.Write([uint32]0)
$bw.Write($pix)
$bw.Write($mask)
$bw.Dispose(); $fs.Dispose()
Write-Host "icon generated: $ico"

# ---------- 编译 ----------
$argList = @(
    '/nologo','/target:winexe','/platform:anycpu','/optimize+',
    '/codepage:65001','/warn:4',
    "/win32manifest:$manifest",
    "/win32icon:$ico",
    "/out:$out"
)
foreach ($r in $refs) { $argList += "/r:$r" }
$argList += (Get-ChildItem $src -Filter *.cs | ForEach-Object { $_.FullName })

& $csc @argList
if ($LASTEXITCODE -ne 0) { throw "compile failed with exit code $LASTEXITCODE" }
Write-Host "OK: $out"
