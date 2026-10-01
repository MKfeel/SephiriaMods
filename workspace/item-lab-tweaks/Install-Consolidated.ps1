param(
    [string]$GameDirectory = 'E:\steam\steamapps\common\Sephiria',
    [switch]$Rollback,
    [string]$BackupDirectory
)
$ErrorActionPreference = 'Stop'
if (Get-Process Sephiria -ErrorAction SilentlyContinue) { throw '请先完全退出游戏，再安装或回退。' }
$game = (Resolve-Path -LiteralPath $GameDirectory).Path
$relativePaths = @(
    'BepInEx/plugins/SephiriaItemLabTweaks/SephiriaItemLabTweaks.dll',
    'BepInEx/plugins/SephiriaItemLabTweaks/README.md',
    'BepInEx/config/com.codex.sephiria.itemlab-tweaks.cfg',
    'BepInEx/plugins/SephiriaTalentCustomizer/SephiriaTalentCustomizer.dll',
    'BepInEx/plugins/SephiriaTalentSPCompat/SephiriaTalentSPCompat.dll',
    'BepInEx/config/com.codex.sephiria.talent-customizer.cfg'
)
function Target($relative) {
    if ($relative -notin $relativePaths) { throw "Unexpected manifest path: $relative" }
    $absolute = [IO.Path]::GetFullPath((Join-Path $game $relative))
    if (-not $absolute.StartsWith($game.TrimEnd('\')+'\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Path outside game directory' }
    $parent = $absolute
    while ($parent -and $parent.Length -ge $game.Length) {
        if ((Test-Path -LiteralPath $parent) -and ((Get-Item -LiteralPath $parent -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw "Reparse point: $parent" }
        $parent = Split-Path $parent
    }
    return $absolute
}
function Restore($directory, $records) {
    foreach ($record in $records) {
        $target = Target $record.Path
        if ($record.Existed) {
            $source = Join-Path $directory $record.Path
            New-Item -ItemType Directory -Force -Path (Split-Path $target) | Out-Null
            Copy-Item -LiteralPath $source -Destination $target -Force
            if ((Get-FileHash -LiteralPath $target).Hash -ne $record.SHA256) { throw "Restore mismatch: $target" }
        } elseif (Test-Path -LiteralPath $target) { Remove-Item -LiteralPath $target -Force }
    }
}

# Validate all rollback inputs before changing any installed file.
$restoreRecords = $null
if ($Rollback) {
    if (-not $BackupDirectory) { throw '请指定 -BackupDirectory。' }
    $BackupDirectory = (Resolve-Path -LiteralPath $BackupDirectory).Path
    $restoreRecords = @(Get-Content -LiteralPath (Join-Path $BackupDirectory 'migration-manifest.json') -Raw | ConvertFrom-Json)
    if ($restoreRecords.Count -ne $relativePaths.Count -or @($restoreRecords.Path | Select-Object -Unique).Count -ne $relativePaths.Count) { throw 'Incomplete rollback manifest' }
    foreach ($record in $restoreRecords) {
        $null = Target $record.Path
        if ($record.Existed -and (Get-FileHash -LiteralPath (Join-Path $BackupDirectory $record.Path)).Hash -ne $record.SHA256) { throw 'Rollback backup hash mismatch' }
    }
} else {
    $sourceDll = Join-Path $PSScriptRoot 'bin/Release/net471/SephiriaItemLabTweaks.dll'
    if (-not (Test-Path -LiteralPath $sourceDll)) { throw '请先构建 Release。' }
    if ([Reflection.AssemblyName]::GetAssemblyName($sourceDll).Version -lt [version]'1.1.0.0') { throw '整合安装要求 1.1.0 或更新版本。' }
    $sourceHash = (Get-FileHash -LiteralPath $sourceDll).Hash
    $null = Get-Item -LiteralPath (Join-Path $PSScriptRoot 'README.md')
}

$backup = Join-Path $PSScriptRoot ('backups/migration-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
New-Item -ItemType Directory -Path $backup | Out-Null
$records = @(foreach ($relative in $relativePaths) {
    $target = Target $relative
    $exists = Test-Path -LiteralPath $target
    $hash = $null
    if ($exists) {
        $destination = Join-Path $backup $relative
        New-Item -ItemType Directory -Force -Path (Split-Path $destination) | Out-Null
        Copy-Item -LiteralPath $target -Destination $destination
        $hash = (Get-FileHash -LiteralPath $target).Hash
        if ((Get-FileHash -LiteralPath $destination).Hash -ne $hash) { throw 'Backup hash mismatch; installation not started.' }
    }
    [pscustomobject]@{ Path=$relative; Existed=$exists; SHA256=$hash }
})
$records | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $backup 'migration-manifest.json') -Encoding utf8
try {
    if ($Rollback) { Restore $BackupDirectory $restoreRecords }
    else {
        $newConfig = Target $relativePaths[2]
        $oldConfig = Target $relativePaths[5]
        New-Item -ItemType Directory -Force -Path (Split-Path $newConfig) | Out-Null
        if (-not (Test-Path -LiteralPath $newConfig)) {
            if (Test-Path -LiteralPath $oldConfig) {
                # Exact copy preserves ExtraPoints and a pending reset, without executing old code.
                Copy-Item -LiteralPath $oldConfig -Destination $newConfig
                if ((Get-FileHash -LiteralPath $newConfig).Hash -ne (Get-FileHash -LiteralPath $oldConfig).Hash) { throw 'Config migration hash mismatch' }
            } else {
                "[Talents]`r`nExtraPoints = 0`r`n`r`n[Internal]`r`nResetOnNextLoad = false" | Set-Content -LiteralPath $newConfig -Encoding utf8
            }
        }
        $targetDll = Target $relativePaths[0]
        New-Item -ItemType Directory -Force -Path (Split-Path $targetDll) | Out-Null
        Copy-Item -LiteralPath $sourceDll -Destination $targetDll -Force
        if ((Get-FileHash -LiteralPath $targetDll).Hash -ne $sourceHash) { throw 'Installed DLL hash mismatch' }
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'README.md') -Destination (Target $relativePaths[1]) -Force
        foreach ($relative in $relativePaths[3..5]) {
            $target = Target $relative
            if (Test-Path -LiteralPath $target) { Remove-Item -LiteralPath $target -Force }
            if (Test-Path -LiteralPath $target) { throw "Removal failed: $target" }
        }
        # Remove only the now-empty, explicitly named legacy plugin directories.
        foreach ($relative in $relativePaths[3..4]) {
            $directory = Split-Path (Target $relative)
            if ((Test-Path -LiteralPath $directory) -and @(Get-ChildItem -LiteralPath $directory -Force).Count -eq 0) { Remove-Item -LiteralPath $directory }
        }
    }
} catch {
    Restore $backup $records
    throw
}
Write-Output "Completed. Backup: $backup"
if (-not $Rollback) { Write-Output "Installed SHA256: $sourceHash" }
