param([Parameter(Mandatory=$true)][string]$ManifestPath)
$ErrorActionPreference = 'Stop'
if (Get-Process -Name Sephiria -ErrorAction SilentlyContinue) { throw '请完全退出游戏后回退。' }
$manifest = Get-Content -LiteralPath $ManifestPath -Raw | ConvertFrom-Json
$gameRoot = [IO.Path]::GetFullPath($manifest.GameRoot).TrimEnd('\')
$pluginRoot = Join-Path $gameRoot 'BepInEx\plugins'
$backupRoot = Join-Path $gameRoot 'OwnOrganizerBackups'
$installed = [IO.Path]::GetFullPath($manifest.InstalledPath)
if (!$installed.StartsWith($pluginRoot + '\', [StringComparison]::OrdinalIgnoreCase)) { throw '安装路径越界。' }
foreach ($record in $manifest.Files) {
    $original = [IO.Path]::GetFullPath($record.OriginalPath)
    $saved = [IO.Path]::GetFullPath($record.BackupPath)
    if (!$original.StartsWith($pluginRoot + '\', [StringComparison]::OrdinalIgnoreCase) -or !$saved.StartsWith($backupRoot + '\', [StringComparison]::OrdinalIgnoreCase)) { throw '回退路径越界。' }
    if ((Get-FileHash -LiteralPath $saved).Hash -ne $record.Hash) { throw '备份哈希不一致，已停止。' }
    if ((Test-Path -LiteralPath $original) -and $original -ne $installed) { throw "原路径已有其他文件，已停止：$original" }
}
if (Test-Path -LiteralPath $installed) {
    if ((Get-FileHash -LiteralPath $installed).Hash -ne $manifest.InstalledHash) { throw '当前 DLL 已被其他操作修改，已停止。' }
    Remove-Item -LiteralPath $installed
}
foreach ($record in $manifest.Files) {
    New-Item -ItemType Directory -Path (Split-Path -Parent $record.OriginalPath) -Force | Out-Null
    Copy-Item -LiteralPath $record.BackupPath -Destination $record.OriginalPath
    if ((Get-FileHash -LiteralPath $record.OriginalPath).Hash -ne $record.Hash) { throw '恢复后校验失败。' }
}
Write-Output '回退完成。'
