param([string]$GameDirectory='E:\steam\steamapps\common\Sephiria', [string]$PreviousDll)
$ErrorActionPreference='Stop'
Add-Type -Path (Join-Path $GameDirectory 'BepInEx/core/Mono.Cecil.dll')
if (-not $PreviousDll) { $PreviousDll=Join-Path $GameDirectory 'BepInEx/plugins/SephiriaItemLabTweaks/SephiriaItemLabTweaks.dll' }
$resolver=[Mono.Cecil.DefaultAssemblyResolver]::new()
$resolver.AddSearchDirectory((Join-Path $GameDirectory 'BepInEx/core'))
$resolver.AddSearchDirectory((Join-Path $GameDirectory 'Sephiria_Data/Managed'))
$parameters=[Mono.Cecil.ReaderParameters]::new()
$parameters.AssemblyResolver=$resolver
$new=[Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $PSScriptRoot 'bin/Release/net471/SephiriaItemLabTweaks.dll'),$parameters)
$old=[Mono.Cecil.AssemblyDefinition]::ReadAssembly($PreviousDll,$parameters)
$spPath=Join-Path $GameDirectory 'AddOns_Disabled/SPMod/SPMod.dll'
if (-not (Test-Path -LiteralPath $spPath)) { $spPath=Join-Path $GameDirectory 'AddOns/SPMod/SPMod.dll' }
$sp=[Mono.Cecil.AssemblyDefinition]::ReadAssembly($spPath)
try {
    $plugin=$new.MainModule.Types | Where-Object Name -eq Plugin
    $dependencies=@($plugin.CustomAttributes | Where-Object { $_.AttributeType.Name -eq 'BepInDependency' } | ForEach-Object { $_.ConstructorArguments[0].Value })
    if ($dependencies.Count -ne 1 -or $dependencies[0] -ne 'com.sephiria.itemlab') { throw 'Unexpected plugin dependencies' }
    $instructions=@($new.MainModule.GetTypes() | ForEach-Object { $_.Methods } | Where-Object HasBody | ForEach-Object { $_.Body.Instructions })
    if ($instructions | Where-Object { [string]$_.Operand -match 'SephiriaTalentCustomizer|talent-customizer|PassiveDatabase|PassiveObject_InventorySlot|addInventory' }) { throw 'Legacy dependency or talent-effect mutation remains' }
    $count=0
    foreach ($name in @('EnchantToggleService','SapphireService','GameAccess','ReflectionUtil')) {
        $nt=$new.MainModule.Types | Where-Object Name -eq $name
        $ot=$old.MainModule.Types | Where-Object Name -eq $name
        foreach ($method in $ot.Methods | Where-Object HasBody) {
            $other=$nt.Methods | Where-Object FullName -eq $method.FullName
            if (-not $other) { throw "Missing method: $($method.FullName)" }
            $before=($method.Body.Instructions | ForEach-Object { "$($_.OpCode) $($_.Operand)" }) -join "`n"
            $after=($other.Body.Instructions | ForEach-Object { "$($_.OpCode) $($_.Operand)" }) -join "`n"
            if ($before -cne $after) { throw "Existing behavior changed: $($method.FullName)" }
            $count++
        }
    }
    $spType=$sp.MainModule.GetTypes() | Where-Object FullName -eq 'SPMod.StarSephiriaPatches/PlayerAvatar_Patches'
    $method=$spType.Methods | Where-Object Name -eq UpdateMaxPassivePoint
    $setters=@($method.Body.Instructions | Where-Object { [string]$_.Operand -match 'PlayerAvatar::set_NetworkmaxPassivePoint' })
    $getters=@($method.Body.Instructions | Where-Object { [string]$_.Operand -match 'PlayerAvatar::get_NetworkmaxPassivePoint' })
    if ($setters.Count -ne 1 -or $getters.Count -ne 1 -or $setters[0].Offset -ge $getters[0].Offset) { throw 'SPMod limit pattern changed' }
    $service=$new.MainModule.Types | Where-Object Name -eq TalentService
    if (@($service.Methods | Where-Object Name -eq CombineSPModLimit).Count -ne 1) { throw 'Integrated helper missing' }
    "PASS: only Item Lab dependency; no legacy effect mutation; $count original methods unchanged; real SPMod setter precedes excess-point check. Static inspection only."
} finally { $new.Dispose(); $old.Dispose(); $sp.Dispose(); $resolver.Dispose() }
