param(
    [string]$GameDirectory = 'E:\steam\steamapps\common\Sephiria',
    [switch]$Rollback,
    [string]$BackupDirectory
)
$ErrorActionPreference = 'Stop'
if (Get-Process Sephiria -ErrorAction SilentlyContinue) { throw '请先完全退出游戏，再安装或回退。' }
$target = Join-Path $GameDirectory 'BepInEx\plugins\SephiriaHiddenRoomHints\SephiriaHiddenRoomHints.dll'
$config = Join-Path $GameDirectory 'BepInEx\config\codex.sephiria.hidden-room-hints.cfg'
if (-not (Test-Path -LiteralPath $target -PathType Leaf)) { throw "原插件不存在：$target" }
if ($Rollback) {
    if (-not $BackupDirectory) { throw '回退需要 -BackupDirectory。' }
    $source = Join-Path $BackupDirectory 'SephiriaHiddenRoomHints.dll'
} else {
    $source = Join-Path $PSScriptRoot 'bin\Release\net471\SephiriaHiddenRoomHints.dll'
}
if (-not (Test-Path -LiteralPath $source -PathType Leaf)) { throw "安装源不存在：$source" }
$hash = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
$oldHash = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash
$backup = Join-Path $PSScriptRoot ('backups\' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
New-Item -ItemType Directory -Path $backup | Out-Null
$savedDll = Join-Path $backup 'SephiriaHiddenRoomHints.dll'
Copy-Item -LiteralPath $target -Destination $savedDll
if ((Get-FileHash -LiteralPath $savedDll).Hash -ne $oldHash) { throw '备份校验失败，未安装。' }
if (Test-Path -LiteralPath $config) { Copy-Item -LiteralPath $config -Destination $backup }
try {
    Copy-Item -LiteralPath $source -Destination $target -Force
    if ((Get-FileHash -LiteralPath $target).Hash -ne $hash) { throw '安装哈希校验失败。' }
} catch {
    Copy-Item -LiteralPath $savedDll -Destination $target -Force
    throw
}
$record = [ordered]@{
    Date = (Get-Date).ToString('o'); Target = $target; Source = (Resolve-Path -LiteralPath $source).Path
    SHA256 = $hash; PreviousSHA256 = $oldHash; BackupDirectory = $backup; Rollback = [bool]$Rollback
}
$record | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'installation.json') -Encoding utf8
$record | ConvertTo-Json
