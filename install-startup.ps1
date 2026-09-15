# 开机自启：在 shell:startup 创建/移除 DownloadDock 快捷方式
# 用法:  pwsh -File install-startup.ps1           （安装）
#        pwsh -File install-startup.ps1 -Remove   （卸载）
param([switch]$Remove)

$exe = Join-Path $PSScriptRoot 'DownloadDock.exe'
$link = Join-Path ([Environment]::GetFolderPath('Startup')) 'DownloadDock.lnk'

if ($Remove) {
    if (Test-Path $link) { Remove-Item $link -Force; Write-Host "removed: $link" }
    else { Write-Host "not installed: $link" }
    return
}

if (-not (Test-Path $exe)) { throw "exe not found: $exe" }
$ws = New-Object -ComObject WScript.Shell
$sc = $ws.CreateShortcut($link)
$sc.TargetPath = $exe
$sc.WorkingDirectory = Split-Path -Parent $exe
$sc.Description = 'DownloadDock 下载浮窗'
$sc.Save()
Write-Host "installed: $link"
