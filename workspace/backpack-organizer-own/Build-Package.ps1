param([string]$NodePath='F:\nodejs\node.exe')
$ErrorActionPreference='Stop'
$projectRoot=[IO.Path]::GetFullPath($PSScriptRoot)
$labRoot=Join-Path (Split-Path $projectRoot -Parent) 'backpack-simulator'
$modelRoot=Join-Path $projectRoot 'model'
$dllPath=Join-Path $projectRoot 'bin5\Release\SephiriaBackpackOrganizer.dll'
$version=[Reflection.AssemblyName]::GetAssemblyName($dllPath).Version.ToString(3)
$sourceVersion=([xml](Get-Content -Raw -LiteralPath (Join-Path $projectRoot 'SephiriaBackpackOrganizer.Own.csproj'))).Project.PropertyGroup.Version
if ($sourceVersion -ne $version) { throw 'DLL 与项目版本不一致，请先构建。' }
$packageRoot=Join-Path $projectRoot ('dist\SephiriaBackpackOrganizer-'+$version)
if (!(Test-Path -LiteralPath $NodePath)) { throw '找不到打包用 Node.js；安装包将自带运行时。' }
New-Item -ItemType Directory -Force -Path (Join-Path $modelRoot 'data'),(Join-Path $modelRoot 'runtime'),$packageRoot | Out-Null
foreach ($name in @('native-query.js','builds.js','mechanics.js','model.js','runtime-model.js','resonance-model.js')) {
    Copy-Item -LiteralPath (Join-Path $labRoot $name) -Destination (Join-Path $modelRoot $name) -Force
}
foreach ($name in @('catalog.json','weapons.json','statuses.json')) {
    Copy-Item -LiteralPath (Join-Path $labRoot ('data\'+$name)) -Destination (Join-Path $modelRoot ('data\'+$name)) -Force
}
Copy-Item -LiteralPath $NodePath -Destination (Join-Path $modelRoot 'runtime\node.exe') -Force
Copy-Item -LiteralPath (Join-Path (Split-Path $NodePath -Parent) 'LICENSE') -Destination (Join-Path $modelRoot 'runtime\NODE-LICENSE.txt') -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'bin5\Release\SephiriaBackpackOrganizer.dll') -Destination $packageRoot -Force
foreach ($name in @('Install.ps1','Rollback.ps1','README-3.0.md','LICENSE')) {
    Copy-Item -LiteralPath (Join-Path $projectRoot $name) -Destination $packageRoot -Force
}
$payload=@(Get-ChildItem -LiteralPath $modelRoot -Recurse -File | Where-Object { !$_.FullName.StartsWith((Join-Path $modelRoot 'snapshots')+'\',[StringComparison]::OrdinalIgnoreCase) })
foreach ($file in $payload) {
    $relative=$file.FullName.Substring($modelRoot.Length+1)
    $target=Join-Path $packageRoot ('model\'+$relative)
    New-Item -ItemType Directory -Path (Split-Path $target -Parent) -Force | Out-Null
    Copy-Item -LiteralPath $file.FullName -Destination $target -Force
}
$hashes=@(Get-ChildItem -LiteralPath $packageRoot -Recurse -File | Where-Object { $_.Name -ne 'manifest.json' } | ForEach-Object {
    [pscustomobject]@{Path=$_.FullName.Substring($packageRoot.Length+1);Bytes=$_.Length;Sha256=(Get-FileHash -LiteralPath $_.FullName).Hash}
})
[pscustomobject]@{Version=$version;NodeVersion=(& $NodePath --version);Files=$hashes} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $packageRoot 'manifest.json') -Encoding utf8
$archive=Join-Path $projectRoot ('dist\SephiriaBackpackOrganizer-'+$version+'.zip')
Compress-Archive -Path (Join-Path $packageRoot '*') -DestinationPath $archive -Force
Get-FileHash -LiteralPath $archive
