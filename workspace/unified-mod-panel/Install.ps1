param(
    [string]$GameDirectory = 'E:\steam\steamapps\common\Sephiria',
    [switch]$Rollback
)
$ErrorActionPreference = 'Stop'
if (Get-Process Sephiria -ErrorAction SilentlyContinue) { throw '请先退出游戏，再安装或回退。' }
$game = [IO.Path]::GetFullPath($GameDirectory).TrimEnd('\')
if (-not (Test-Path -LiteralPath (Join-Path $game 'BepInEx/core/BepInEx.dll'))) { throw '未找到 BepInEx。' }
$folder = Join-Path $game 'BepInEx/plugins/SephiriaUnifiedModPanel'
$target = Join-Path $folder 'SephiriaUnifiedModPanel.dll'
if ($Rollback) {
    if (Test-Path -LiteralPath $target) {
        $resolved = [IO.Path]::GetFullPath($target)
        if ($resolved -ne (Join-Path $game 'BepInEx/plugins/SephiriaUnifiedModPanel/SephiriaUnifiedModPanel.dll')) { throw '回退路径不符合预期。' }
        Move-Item -LiteralPath $target -Destination ($target + '.disabled-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
    }
    Write-Output '统一面板已停用。下次启动恢复旧 F1/F9 面板，原 DLL 和配置均保留。'
    exit
}
$source = Join-Path $PSScriptRoot 'bin/Release/SephiriaUnifiedModPanel.dll'
if (-not (Test-Path -LiteralPath $source)) { throw '请先构建 Release 版本。' }
New-Item -ItemType Directory -Path $folder -Force | Out-Null
if (Test-Path -LiteralPath $target) {
    Copy-Item -LiteralPath $target -Destination ($target + '.backup-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
}
Copy-Item -LiteralPath $source -Destination $target -Force
if ((Get-FileHash -LiteralPath $source).Hash -ne (Get-FileHash -LiteralPath $target).Hash) { throw '安装后哈希校验失败。' }
Write-Output "已安装并校验：$target"
