param([Parameter(Mandatory=$true)][string]$ManifestPath)
$ErrorActionPreference='Stop'
if (Get-Process -Name Sephiria -ErrorAction SilentlyContinue) { throw '请完全退出游戏后回退。' }
$manifest=Get-Content -LiteralPath $ManifestPath -Raw | ConvertFrom-Json
$gameRoot=[IO.Path]::GetFullPath($manifest.GameRoot).TrimEnd('\')
$pluginRoot=Join-Path $gameRoot 'BepInEx\plugins'
$backupRoot=Join-Path $gameRoot 'OwnOrganizerBackups'
$installed=if ($manifest.Schema -eq 2) { @($manifest.InstalledFiles) } else { @([pscustomobject]@{Path=$manifest.InstalledPath;Hash=$manifest.InstalledHash}) }
$paths=@($installed | ForEach-Object Path)
foreach ($entry in $installed) {
    $path=[IO.Path]::GetFullPath($entry.Path)
    if (!$path.StartsWith($pluginRoot+'\',[StringComparison]::OrdinalIgnoreCase)) { throw '安装路径越界。' }
    if ((Test-Path -LiteralPath $path) -and (Get-FileHash -LiteralPath $path).Hash -ne $entry.Hash) { throw "安装文件已被修改，已停止：$path" }
}
foreach ($record in $manifest.Files) {
    $original=[IO.Path]::GetFullPath($record.OriginalPath);$saved=[IO.Path]::GetFullPath($record.BackupPath)
    if (!$original.StartsWith($pluginRoot+'\',[StringComparison]::OrdinalIgnoreCase) -or !$saved.StartsWith($backupRoot+'\',[StringComparison]::OrdinalIgnoreCase)) { throw '回退路径越界。' }
    if ((Get-FileHash -LiteralPath $saved).Hash -ne $record.Hash) { throw '备份哈希不一致，已停止。' }
    if ((Test-Path -LiteralPath $original) -and $paths -notcontains $original) { throw "原路径已有其他文件，已停止：$original" }
}
foreach ($entry in $installed) { if (Test-Path -LiteralPath $entry.Path) { Remove-Item -LiteralPath $entry.Path } }
foreach ($record in $manifest.Files) {
    New-Item -ItemType Directory -Path (Split-Path $record.OriginalPath -Parent) -Force | Out-Null
    Copy-Item -LiteralPath $record.BackupPath -Destination $record.OriginalPath
    if ((Get-FileHash -LiteralPath $record.OriginalPath).Hash -ne $record.Hash) { throw '恢复后校验失败。' }
}
Write-Output '回退完成。'
