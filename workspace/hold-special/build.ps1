param([string]$GameRoot = 'D:\GAMES\Steam\steamapps\common\Sephiria', [switch]$Install)
$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_HOME = Join-Path $PSScriptRoot '.build-home'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH = 'false'
dotnet build (Join-Path $PSScriptRoot 'SephiriaHoldSpecial.csproj') -c Release --ignore-failed-sources --nologo "-p:GameRoot=$GameRoot"
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
dotnet run --project (Join-Path $PSScriptRoot 'Tests/RepeatCycle.Tests.csproj') -c Release --verbosity quiet
if ($LASTEXITCODE -ne 0) { throw 'Lifecycle tests failed' }
& (Join-Path $PSScriptRoot 'Tests/Verify-GameContract.ps1') -GameRoot $GameRoot
$package = Join-Path $PSScriptRoot 'package'
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'README.md') -Destination $package -Force
$relative = 'BepInEx/plugins/SephiriaHoldSpecial/SephiriaHoldSpecial.dll'
$dll = Join-Path $package $relative
[ordered]@{
    name = 'SephiriaHoldSpecial'; version = '1.0.1'; date = (Get-Date -Format 'yyyy-MM-dd')
    gameAssemblySHA256 = (Get-FileHash -LiteralPath (Join-Path $GameRoot 'Sephiria_Data/Managed/Assembly-CSharp.dll')).Hash
    runtimeVerified = $false
    files = @([ordered]@{ path = $relative; sha256 = (Get-FileHash -LiteralPath $dll).Hash; bytes = (Get-Item -LiteralPath $dll).Length })
} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $package 'manifest.json') -Encoding utf8
Compress-Archive -Path (Join-Path $package '*') -DestinationPath (Join-Path $PSScriptRoot 'SephiriaHoldSpecial-1.0.1.zip') -Force
if ($Install) {
    if (Get-Process Sephiria -ErrorAction SilentlyContinue) { throw '请退出游戏后安装。' }
    $destination = Join-Path $GameRoot $relative
    New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
    if (Test-Path -LiteralPath $destination) { Copy-Item -LiteralPath $destination -Destination "$destination.previous" -Force }
    Copy-Item -LiteralPath $dll -Destination $destination -Force
    if ((Get-FileHash -LiteralPath $dll).Hash -ne (Get-FileHash -LiteralPath $destination).Hash) { throw 'Installed hash mismatch' }
    Write-Output "Installed and hash verified: $destination"
}
