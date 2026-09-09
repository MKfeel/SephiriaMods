$ErrorActionPreference = 'Stop'
$core = 'E:\steam\steamapps\common\Sephiria\BepInEx\core'
Add-Type -Path (Join-Path $core 'Mono.Cecil.dll')
$references = @(Get-ChildItem (Join-Path $PSHOME 'ref') -Filter '*.dll' | Select-Object -ExpandProperty FullName) + @([Newtonsoft.Json.Linq.JObject].Assembly.Location, (Join-Path $core 'Mono.Cecil.dll'))
Add-Type -Path @((Join-Path $PSScriptRoot 'AddonFiles.cs'), (Join-Path $PSScriptRoot 'PluginFiles.cs')) -ReferencedAssemblies $references -CompilerOptions '/nowarn:1701'
$testRoot = Join-Path $PSScriptRoot ('.test-' + [guid]::NewGuid().ToString('N'))
$plugins = Join-Path $testRoot 'plugins'
function Assert-True($condition, $message) { if (-not $condition) { throw $message } }
function Assert-Rejected([scriptblock]$action) {
    $rejected = $false
    try { & $action } catch { $rejected = $true }
    Assert-True $rejected 'Expected rejection'
}
$bep = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $core 'BepInEx.dll'))
$pluginCtor = ($bep.MainModule.Types | Where-Object FullName -eq 'BepInEx.BepInPlugin').Methods | Where-Object Name -eq '.ctor'
$dependencyCtor = ($bep.MainModule.Types | Where-Object FullName -eq 'BepInEx.BepInDependency').Methods | Where-Object { $_.Name -eq '.ctor' -and $_.Parameters[1].ParameterType.FullName -eq 'System.String' }
function New-TestPlugin($name, $guid, $dependency) {
    $asm = [Mono.Cecil.AssemblyDefinition]::CreateAssembly([Mono.Cecil.AssemblyNameDefinition]::new($name, [version]'1.0.0'), $name, [Mono.Cecil.ModuleKind]::Dll)
    try {
        $m = $asm.MainModule
        $t = [Mono.Cecil.TypeDefinition]::new('Test', $name, [Mono.Cecil.TypeAttributes]::Public, $m.TypeSystem.Object)
        $m.Types.Add($t)
        $attr = [Mono.Cecil.CustomAttribute]::new($m.ImportReference($pluginCtor))
        foreach ($value in @($guid, $name, '1.0.0')) { $attr.ConstructorArguments.Add([Mono.Cecil.CustomAttributeArgument]::new($m.TypeSystem.String, $value)) }
        $t.CustomAttributes.Add($attr)
        if ($dependency) {
            $dep = [Mono.Cecil.CustomAttribute]::new($m.ImportReference($dependencyCtor))
            foreach ($value in @($dependency, '1.0.0')) { $dep.ConstructorArguments.Add([Mono.Cecil.CustomAttributeArgument]::new($m.TypeSystem.String, $value)) }
            $t.CustomAttributes.Add($dep)
        }
        $asm.Write((Join-Path $plugins ($name + '.dll')))
    } finally { $asm.Dispose() }
}
function Item($guid) { [SephiriaUnifiedModPanel.PluginFiles]::Scan($plugins, $null) | Where-Object { $_.Guids.Contains($guid) } }
try {
    New-Item -ItemType Directory -Path $plugins, (Join-Path $testRoot 'core') -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $core 'BepInEx.dll') -Destination (Join-Path $testRoot 'core/BepInEx.dll')
    New-TestPlugin 'Base' 'test.base' $null
    New-TestPlugin 'Child' 'test.child' 'test.base'
    New-TestPlugin 'Manager' 'com.sephiria.unifiedmodpanel' $null
    $hash = (Get-FileHash (Join-Path $plugins 'Child.dll')).Hash
    Assert-True ((Item 'test.child').HardDependencies.Contains('test.base')) 'Dependency metadata missing'
    Assert-Rejected { [SephiriaUnifiedModPanel.PluginFiles]::SetEnabled((Item 'test.base'), $false, $plugins) }
    Assert-Rejected { [SephiriaUnifiedModPanel.PluginFiles]::SetEnabled((Item 'com.sephiria.unifiedmodpanel'), $false, $plugins) }
    [SephiriaUnifiedModPanel.PluginFiles]::SetEnabled((Item 'test.child'), $false, $plugins)
    Assert-True (-not (Test-Path (Join-Path $plugins 'Child.dll'))) 'Disabled DLL still discoverable'
    Assert-True (-not (Item 'test.child').Enabled) 'Disabled row lost'
    [SephiriaUnifiedModPanel.PluginFiles]::SetEnabled((Item 'test.base'), $false, $plugins)
    Assert-Rejected { [SephiriaUnifiedModPanel.PluginFiles]::SetEnabled((Item 'test.child'), $true, $plugins) }
    [SephiriaUnifiedModPanel.PluginFiles]::SetEnabled((Item 'test.base'), $true, $plugins)
    [SephiriaUnifiedModPanel.PluginFiles]::SetEnabled((Item 'test.child'), $true, $plugins)
    Assert-True ((Get-FileHash (Join-Path $plugins 'Child.dll')).Hash -eq $hash) 'DLL content changed'
    [IO.File]::WriteAllText((Join-Path $plugins 'Child.dll.unified-disabled'), 'collision')
    Assert-Rejected { [SephiriaUnifiedModPanel.PluginFiles]::SetEnabled((Item 'test.child'), $false, $plugins) }
    Assert-True (Test-Path (Join-Path $plugins 'Child.dll')) 'Collision changed source'
    $live = [SephiriaUnifiedModPanel.PluginFiles]::Scan('E:\steam\steamapps\common\Sephiria\BepInEx\plugins', { param($message) throw $message })
    Assert-True ($live.Count -gt 0) 'Installed plugin scan is empty'
    Write-Output ('PASS: metadata, dependencies, self protection, disable/enable, unchanged DLL hash, collision; read-only installed scan: ' + $live.Count + ' plugin files.')
} finally {
    $bep.Dispose()
    $resolved = [IO.Path]::GetFullPath($testRoot)
    $allowed = [IO.Path]::GetFullPath($PSScriptRoot).TrimEnd('\') + '\.test-'
    if (-not $resolved.StartsWith($allowed, [StringComparison]::OrdinalIgnoreCase)) { throw 'Unexpected cleanup path' }
    if (Test-Path -LiteralPath $resolved) { Remove-Item -LiteralPath $resolved -Recurse -Force }
}
