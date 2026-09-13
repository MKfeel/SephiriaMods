param([string]$GameDirectory = 'E:\steam\steamapps\common\Sephiria')
$ErrorActionPreference = 'Stop'
Add-Type -Path (Join-Path $GameDirectory 'BepInEx/core/Mono.Cecil.dll')
$assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $GameDirectory 'Sephiria_Data/Managed/Assembly-CSharp.dll'))
try {
    $unit = $assembly.MainModule.Types | Where-Object Name -eq UnitAvatar
    $fields = @('maxHp','finalMaxHp','isHPCursed','cursedMaxHp')
    $setterNames = @($fields | ForEach-Object { "set_Network$_" })
    foreach ($field in $fields) {
        $setter = @($unit.Methods | Where-Object Name -eq "set_Network$field")
        if ($setter.Count -ne 1 -or -not $setter[0].HasBody) { throw "Missing HP setter: $field" }
    }
    $setterCalls = 0
    foreach ($type in $assembly.MainModule.GetTypes()) {
        foreach ($method in $type.Methods | Where-Object HasBody) {
            foreach ($instruction in $method.Body.Instructions) {
                $operand = $instruction.Operand
                if ($instruction.OpCode.Code.ToString() -eq 'Stfld' -and $operand.DeclaringType.FullName -eq 'UnitAvatar' -and $operand.Name -in $fields -and $method.Name -ne '.ctor') {
                    throw "Uncovered direct HP write: $($method.FullName)"
                }
                if ($operand -is [Mono.Cecil.MethodReference] -and $operand.DeclaringType.FullName -eq 'UnitAvatar' -and $operand.Name -in $setterNames) { $setterCalls++ }
            }
        }
    }
    foreach ($typeName in @('PassiveObject_InventorySlot','PassiveObject_StatusInstance')) {
        $type = $assembly.MainModule.Types | Where-Object Name -eq $typeName
        foreach ($name in @('OnEffectEnabled','OnEffectDisabled')) {
            if (@($type.Methods | Where-Object { $_.Name -eq $name -and $_.HasBody }).Count -ne 1) { throw "Missing target: $typeName.$name" }
        }
    }
    foreach ($typeName in @('PassiveObjectMetadata','PassiveObjectMetadata_UniquePair')) {
        $type = $assembly.MainModule.Types | Where-Object Name -eq $typeName
        if (@($type.Methods | Where-Object { $_.Name -eq 'GetEffectStringInner' -and $_.HasBody }).Count -ne 1) { throw "Missing description target: $typeName" }
    }
    $database = $assembly.MainModule.Types | Where-Object Name -eq PassiveDatabase
    if (@($database.Methods | Where-Object { $_.Name -eq 'Initialize' -and $_.HasBody }).Count -ne 1) { throw 'Missing database initialization target' }
    foreach ($typeName in @('BossSpawner','SeedBossSpawner')) {
        $type = $assembly.MainModule.Types | Where-Object Name -eq $typeName
        $method = $type.Methods | Where-Object Name -eq ServerEndBossDramaticDie
        if (-not ($method.Body.Instructions | Where-Object { $_.OpCode.Code.ToString() -eq 'Ldstr' -and $_.Operand -eq 'BOSSREWARDDICE' })) { throw "Native boss reward changed: $typeName" }
    }
    $die = @($unit.Methods | Where-Object { $_.Name -eq 'Die' -and $_.HasBody })
    if ($die.Count -ne 1 -or -not ($die[0].Body.Instructions | Where-Object { [string]$_.Operand -match 'set_NetworkIsDead' })) { throw 'Death transition target changed' }
    "PASS: four HP setters cover $setterCalls native call sites; no non-constructor direct HP writes; native talent, tooltip, death, database and both boss-reward targets verified. Static inspection only."
} finally { $assembly.Dispose() }
