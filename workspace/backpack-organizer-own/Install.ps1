param([string]$GameDirectory='E:\steam\steamapps\common\Sephiria')
$ErrorActionPreference='Stop'
if (Get-Process -Name Sephiria -ErrorAction SilentlyContinue) { throw '请完全退出游戏后安装。' }
$gameRoot=[IO.Path]::GetFullPath($GameDirectory).TrimEnd('\')
$pluginRoot=Join-Path $gameRoot 'BepInEx\plugins'
$destinationRoot=Join-Path $pluginRoot 'SephiriaBackpackOrganizer'
if (!(Test-Path -LiteralPath $pluginRoot)) { throw '未找到 BepInEx 插件目录。' }
if (Get-ChildItem -LiteralPath $pluginRoot -Recurse -File -Filter 'SephiriaBackpackOrganizer.Beta.dll') { throw '请先禁用 Beta 整理插件，避免两个 F8 同时触发。' }
$sourceDll=Join-Path $PSScriptRoot 'bin5\Release\SephiriaBackpackOrganizer.dll'
if (!(Test-Path -LiteralPath $sourceDll)) { $sourceDll=Join-Path $PSScriptRoot 'SephiriaBackpackOrganizer.dll' }
$modelSource=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'model'))
if (!(Test-Path -LiteralPath $sourceDll) -or !(Test-Path -LiteralPath (Join-Path $modelSource 'worker.cjs')) -or !(Test-Path -LiteralPath (Join-Path $modelSource 'runtime\node.exe'))) { throw '完整安装包缺少 DLL、模型或运行时。' }
$payload=@([pscustomobject]@{Source=$sourceDll;Destination=(Join-Path $destinationRoot 'SephiriaBackpackOrganizer.dll');Hash=(Get-FileHash -LiteralPath $sourceDll).Hash})
foreach ($file in Get-ChildItem -LiteralPath $modelSource -Recurse -File) {
    $relative=$file.FullName.Substring($modelSource.Length+1)
    if ($relative.StartsWith('snapshots\',[StringComparison]::OrdinalIgnoreCase)) { continue }
    $target=[IO.Path]::GetFullPath((Join-Path $destinationRoot ('model\'+$relative)))
    if (!$target.StartsWith($destinationRoot+'\',[StringComparison]::OrdinalIgnoreCase)) { throw '安装路径越界。' }
    $payload += [pscustomobject]@{Source=$file.FullName;Destination=$target;Hash=(Get-FileHash -LiteralPath $file.FullName).Hash}
}
$backup=Join-Path $gameRoot ('OwnOrganizerBackups\'+(Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
$existing=@(Get-ChildItem -LiteralPath $pluginRoot -Recurse -File -Filter '*.dll' | Where-Object { $_.Name -in @('SephiriaBackpackOrganizer.dll','SephiriaBackpackOrganizer.Own.dll') } | ForEach-Object FullName)
$existing+=@($payload | Where-Object { Test-Path -LiteralPath $_.Destination } | ForEach-Object Destination)
$existing=@($existing | Sort-Object -Unique)
New-Item -ItemType Directory -Path $backup -Force | Out-Null
$records=@()
foreach ($path in $existing) {
    $absolute=[IO.Path]::GetFullPath($path)
    if (!$absolute.StartsWith($pluginRoot+'\',[StringComparison]::OrdinalIgnoreCase)) { throw '备份路径越界。' }
    $saved=Join-Path $backup $absolute.Substring($pluginRoot.Length+1)
    New-Item -ItemType Directory -Path (Split-Path $saved -Parent) -Force | Out-Null
    Copy-Item -LiteralPath $absolute -Destination $saved
    $hash=(Get-FileHash -LiteralPath $absolute).Hash
    if ((Get-FileHash -LiteralPath $saved).Hash -ne $hash) { throw '备份哈希不一致，未替换任何文件。' }
    $records += [pscustomobject]@{OriginalPath=$absolute;BackupPath=$saved;Hash=$hash}
}
$manifest=Join-Path $backup 'rollback.json'
$installed=@($payload | ForEach-Object { [pscustomobject]@{Path=$_.Destination;Hash=$_.Hash} })
[pscustomobject]@{Schema=2;Version=([Reflection.AssemblyName]::GetAssemblyName($sourceDll).Version.ToString(3));GameRoot=$gameRoot;InstalledFiles=$installed;Files=$records} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $manifest -Encoding utf8
try {
    foreach ($record in $records) { Remove-Item -LiteralPath $record.OriginalPath }
    foreach ($item in $payload) {
        New-Item -ItemType Directory -Path (Split-Path $item.Destination -Parent) -Force | Out-Null
        Copy-Item -LiteralPath $item.Source -Destination $item.Destination
        if ((Get-FileHash -LiteralPath $item.Destination).Hash -ne $item.Hash) { throw '安装哈希不一致。' }
    }
} catch {
    foreach ($item in $payload) { if (Test-Path -LiteralPath $item.Destination) { Remove-Item -LiteralPath $item.Destination } }
    foreach ($record in $records) { Copy-Item -LiteralPath $record.BackupPath -Destination $record.OriginalPath -Force }
    throw
}
Write-Output "安装完成：$destinationRoot"
Write-Output "校验文件：$($payload.Count)"
Write-Output "回退清单：$manifest"
