param(
    [string]$GameDirectory = 'E:\steam\steamapps\common\Sephiria',
    [switch]$Rollback,
    [string]$BackupDirectory
)
$ErrorActionPreference = 'Stop'
if (-not $Rollback -or ($BackupDirectory -and (Test-Path -LiteralPath (Join-Path $BackupDirectory 'migration-manifest.json')))) {
    & (Join-Path $PSScriptRoot 'Install-Consolidated.ps1') @PSBoundParameters
    return
}
if (Get-Process Sephiria -ErrorAction SilentlyContinue) {
    throw '请先完全退出游戏，再安装或回退。'
}
$target = Join-Path $GameDirectory 'BepInEx\plugins\SephiriaItemLabTweaks\SephiriaItemLabTweaks.dll'
if ($Rollback) {
    if (-not $BackupDirectory) { throw '请用 -BackupDirectory 指定安装时输出的备份目录。' }
    $source = Join-Path $BackupDirectory 'SephiriaItemLabTweaks.dll'
} else {
    $source = Join-Path $PSScriptRoot 'bin\Release\net471\SephiriaItemLabTweaks.dll'
}
if (-not (Test-Path -LiteralPath $source)) { throw "DLL 不存在：$source" }
if (-not (Test-Path -LiteralPath $target)) { throw "未找到原有扩展插件：$target" }
$sourceHash = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
$backup = Join-Path $PSScriptRoot ('backups\' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
New-Item -ItemType Directory -Path $backup | Out-Null
$backupFile = Join-Path $backup 'SephiriaItemLabTweaks.dll'
Copy-Item -LiteralPath $target -Destination $backupFile
if ((Get-FileHash -LiteralPath $target).Hash -ne (Get-FileHash -LiteralPath $backupFile).Hash) {
    throw '原 DLL 备份校验失败，未安装。'
}
Copy-Item -LiteralPath $source -Destination $target -Force
if ((Get-FileHash -LiteralPath $target).Hash -ne $sourceHash) {
    Copy-Item -LiteralPath $backupFile -Destination $target -Force
    throw '安装校验失败，已恢复原 DLL。'
}
Write-Output "Installed: $target"
Write-Output "SHA256: $sourceHash"
Write-Output "Backup: $backup"
