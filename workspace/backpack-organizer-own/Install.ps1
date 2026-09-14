param([string]$GameDirectory = 'E:\steam\steamapps\common\Sephiria')
$ErrorActionPreference = 'Stop'
if (Get-Process -Name Sephiria -ErrorAction SilentlyContinue) { throw '请完全退出游戏后安装。' }
$gameRoot = [IO.Path]::GetFullPath($GameDirectory).TrimEnd('\')
$pluginRoot = Join-Path $gameRoot 'BepInEx\plugins'
if (!(Test-Path -LiteralPath $pluginRoot)) { throw '未找到 BepInEx 插件目录。' }
$source = Join-Path $PSScriptRoot 'bin5\Release\SephiriaBackpackOrganizer.dll'
if (!(Test-Path -LiteralPath $source)) { $source = Join-Path $PSScriptRoot 'SephiriaBackpackOrganizer.dll' }
if (!(Test-Path -LiteralPath $source)) { throw '未找到已构建 DLL。' }
$expected = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
$backup = Join-Path $gameRoot ('OwnOrganizerBackups\' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
$destination = Join-Path $pluginRoot 'SephiriaBackpackOrganizer\SephiriaBackpackOrganizer.dll'
$existing = @(Get-ChildItem -LiteralPath $pluginRoot -Recurse -File -Filter '*.dll' | Where-Object { $_.Name -in @('SephiriaBackpackOrganizer.dll','SephiriaBackpackOrganizer.Own.dll') })
New-Item -ItemType Directory -Path $backup -Force | Out-Null
$records = @()
foreach ($file in $existing) {
    $absolute = [IO.Path]::GetFullPath($file.FullName)
    if (!$absolute.StartsWith($pluginRoot + '\', [StringComparison]::OrdinalIgnoreCase)) { throw '插件路径越界。' }
    $saved = Join-Path $backup ($absolute.Substring($pluginRoot.Length + 1))
    New-Item -ItemType Directory -Path (Split-Path -Parent $saved) -Force | Out-Null
    Copy-Item -LiteralPath $absolute -Destination $saved
    $hash = (Get-FileHash -LiteralPath $absolute).Hash
    if ((Get-FileHash -LiteralPath $saved).Hash -ne $hash) { throw '备份校验失败，未禁用旧插件。' }
    $records += [pscustomobject]@{ OriginalPath=$absolute; BackupPath=$saved; Hash=$hash }
}
$manifest = Join-Path $backup 'rollback.json'
[pscustomobject]@{ GameRoot=$gameRoot; InstalledPath=$destination; InstalledHash=$expected; Files=@($records) } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $manifest -Encoding UTF8
try {
    foreach ($record in $records) { Remove-Item -LiteralPath $record.OriginalPath }
    New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
    Copy-Item -LiteralPath $source -Destination $destination
    if ((Get-FileHash -LiteralPath $destination).Hash -ne $expected) { throw '安装校验失败。' }
} catch {
    if (Test-Path -LiteralPath $destination) { Remove-Item -LiteralPath $destination }
    foreach ($record in $records) { Copy-Item -LiteralPath $record.BackupPath -Destination $record.OriginalPath -Force }
    throw
}
Write-Output "安装完成：$destination"
Write-Output "回退清单：$manifest"
