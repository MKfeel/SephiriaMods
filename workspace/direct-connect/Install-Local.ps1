param([string]$GameDirectory = 'E:\steam\steamapps\common\Sephiria')
$ErrorActionPreference = 'Stop'
$source = Join-Path $PSScriptRoot 'bin\Release\netstandard2.1\SephiriaDirectConnect.dll'
$target = Join-Path $GameDirectory 'BepInEx\plugins\SephiriaDirectConnect\SephiriaDirectConnect.dll'
if (Get-Process -Name Sephiria -ErrorAction SilentlyContinue) { throw '请先完全退出游戏。' }
if (!(Test-Path -LiteralPath $source) -or !(Test-Path -LiteralPath $target)) { throw '构建产物或已安装 DLL 不存在。' }
$sourceHash = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
$oldHash = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash
if ($sourceHash -eq $oldHash) { Write-Output '已安装相同构建。'; exit 0 }
if ($oldHash -ne '1FFCBE13E5A6AC796392571841A91DFA7436FF47A56CCC19103BF0474A74BBF6') {
    throw '当前 DLL 已不是检查时的 0.3.9，请先核对版本，避免覆盖其他更新。'
}
$backupFolder = Join-Path $PSScriptRoot ('backups\' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $backupFolder | Out-Null
$backup = Join-Path $backupFolder 'SephiriaDirectConnect.dll'
Copy-Item -LiteralPath $target -Destination $backup
if ((Get-FileHash -LiteralPath $backup -Algorithm SHA256).Hash -ne $oldHash) { throw '备份校验失败。' }
if (Get-Process -Name Sephiria -ErrorAction SilentlyContinue) { throw '游戏已经启动，已保留备份并停止安装。' }
try {
    Copy-Item -LiteralPath $source -Destination $target -Force
    if ((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -ne $sourceHash) { throw '安装校验失败。' }
} catch {
    Copy-Item -LiteralPath $backup -Destination $target -Force
    throw
}
[pscustomobject]@{ Version = '0.4.0'; Target = $target; SHA256 = $sourceHash; Backup = $backup } |
    ConvertTo-Json | Tee-Object -FilePath (Join-Path $PSScriptRoot 'deployment.json')
