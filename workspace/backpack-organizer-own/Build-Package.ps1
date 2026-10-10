param([string]$NodePath=(Join-Path $PSScriptRoot 'model\runtime\node.exe'))
$ErrorActionPreference='Stop'
$projectRoot=[IO.Path]::GetFullPath($PSScriptRoot)
$labRoot=Join-Path (Split-Path $projectRoot -Parent) 'backpack-simulator'
$modelRoot=Join-Path $projectRoot 'model'
$dllPath=Join-Path $projectRoot 'bin5\Release\SephiriaBackpackOrganizer.dll'
$version=[Reflection.AssemblyName]::GetAssemblyName($dllPath).Version.ToString(3)
$sourceVersion=([xml](Get-Content -Raw -LiteralPath (Join-Path $projectRoot 'SephiriaBackpackOrganizer.Own.csproj'))).Project.PropertyGroup.Version
if ($sourceVersion -ne $version) { throw 'DLL 与项目版本不一致，请先构建。' }
if (!(Test-Path -LiteralPath $NodePath)) { throw '找不到打包用 Node.js；安装包将自带运行时。' }
$nodeLicense=Join-Path (Split-Path $NodePath -Parent) 'LICENSE'
if (!(Test-Path -LiteralPath $nodeLicense)) { $nodeLicense=Join-Path (Split-Path $NodePath -Parent) 'NODE-LICENSE.txt' }
if (!(Test-Path -LiteralPath $nodeLicense)) { throw '找不到 Node.js 许可文件。' }
New-Item -ItemType Directory -Force -Path (Join-Path $modelRoot 'data'),(Join-Path $modelRoot 'runtime') | Out-Null
foreach ($name in @('native-query.js','builds.js','mechanics.js','model.js','runtime-model.js','resonance-model.js')) {
    Copy-Item -LiteralPath (Join-Path $labRoot $name) -Destination (Join-Path $modelRoot $name) -Force
}
foreach ($name in @('catalog.json','weapons.json','statuses.json')) {
    Copy-Item -LiteralPath (Join-Path $labRoot ('data\'+$name)) -Destination (Join-Path $modelRoot ('data\'+$name)) -Force
}
foreach ($runtime in @(@($NodePath,'node.exe'),@($nodeLicense,'NODE-LICENSE.txt'))) {
    $target=Join-Path $modelRoot ('runtime\'+$runtime[1])
    if ([IO.Path]::GetFullPath($runtime[0]) -ne [IO.Path]::GetFullPath($target)) {
        Copy-Item -LiteralPath $runtime[0] -Destination $target -Force
    }
}
$distRoot=[IO.Path]::GetFullPath((Join-Path $projectRoot 'dist'))
$packageName='SephiriaBackpackOrganizer-'+$version
$packageRoot=[IO.Path]::GetFullPath((Join-Path $distRoot $packageName))
if (!$packageRoot.StartsWith($distRoot+'\',[StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetFileName($packageRoot) -cne $packageName) { throw '打包路径越界。' }
if (Test-Path -LiteralPath $packageRoot) { Remove-Item -LiteralPath $packageRoot -Recurse -Force }
$pluginRoot=Join-Path $packageRoot 'BepInEx\plugins\SephiriaBackpackOrganizer'
New-Item -ItemType Directory -Force -Path $pluginRoot | Out-Null
Copy-Item -LiteralPath $dllPath,(Join-Path $projectRoot 'LICENSE') -Destination $pluginRoot
Copy-Item -LiteralPath (Join-Path $projectRoot 'README-3.0.md') -Destination (Join-Path $pluginRoot 'README.md')
$payload=@(Get-ChildItem -LiteralPath $modelRoot -Recurse -File | Where-Object { !$_.FullName.StartsWith((Join-Path $modelRoot 'snapshots')+'\',[StringComparison]::OrdinalIgnoreCase) })
foreach ($file in $payload) {
    $relative=$file.FullName.Substring($modelRoot.Length+1)
    $target=Join-Path $pluginRoot ('model\'+$relative)
    New-Item -ItemType Directory -Path (Split-Path $target -Parent) -Force | Out-Null
    Copy-Item -LiteralPath $file.FullName -Destination $target
}
$files=@(Get-ChildItem -LiteralPath $pluginRoot -Recurse -File | Sort-Object FullName | ForEach-Object {
    $relative=$_.FullName.Substring($packageRoot.Length+1).Replace('\','/')
    [ordered]@{source=$relative;target=$relative;sha256=(Get-FileHash -LiteralPath $_.FullName).Hash;config=$false}
})
[ordered]@{schemaVersion=1;mods=@([ordered]@{id='com.sephiria.backpack-organizer';version=$version;files=$files})} |
    ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $packageRoot 'mod-manifest.json') -Encoding utf8
$archive=Join-Path $distRoot ($packageName+'.zip')
if (Test-Path -LiteralPath $archive) { Remove-Item -LiteralPath $archive }
[IO.Compression.ZipFile]::CreateFromDirectory($packageRoot,$archive,[IO.Compression.CompressionLevel]::Optimal,$false)
Get-FileHash -LiteralPath $archive
