param([string]$GameRoot = 'D:\GAMES\Steam\steamapps\common\Sephiria')
$ErrorActionPreference = 'Stop'
Add-Type -Path (Join-Path $GameRoot 'BepInEx/core/Mono.Cecil.dll')
$path = Join-Path $GameRoot 'Sephiria_Data/Managed/Assembly-CSharp.dll'
$asm = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($path)
try {
    function Get-GameType([string]$Name) {
        $t = $asm.MainModule.Types | Where-Object Name -eq $Name
        if (!$t) { throw "Missing type: $Name" }
        return $t
    }
    foreach ($pair in @(
        @('PlayerInputController','HandleOnSubFire',1), @('PlayerInputController','HandleOnFire',1),
        @('PlayerInputController','Update',0), @('PlayerInputController','OnDisable',0),
        @('PlayerInputController','OnApplicationFocus',1), @('PlayerInputController','GetAimedPosition',0),
        @('PlayerInputController','ValidateScreenFader_PlayerMove',0),
        @('WeaponSimple_GreatSword','UserCode_RpcCreateCompleteChargeFx',0),
        @('WeaponSimple_GreatSword','UserCode_RpcCreateChargingFx',0),
        @('WeaponSimple_GreatSword','UserCode_RpcDestroyChargingFx',0))) {
        $m = @((Get-GameType $pair[0]).Methods | Where-Object Name -eq $pair[1])
        if ($m.Count -ne 1 -or $m[0].Parameters.Count -ne $pair[2]) { throw "Method mismatch: $($pair -join '.')" }
        Write-Output "PASS method $($pair[0]).$($pair[1])"
    }
    foreach ($pair in @(
        @('PlayerInputController','avatar','PlayerAvatar'),
        @('PlayerInputController','weaponController','WeaponControllerSimple'),
        @('PlayerInputController','integratedActionController','IntegratedActionController'),
        @('PlayerInputController','isAnyUIHovered','System.Boolean'),
        @('WeaponSimple_GreatSword','specialAttackToTransform','System.Boolean'),
        @('WeaponSimple','addons','WeaponAddon[]'),
        @('WeaponSimple_GreatSword','sweepRequest','System.Boolean'),
        @('WeaponSimple_SwordAndShield','isSweepUsing','System.Boolean'),
        @('WeaponSimple_Katana_New','smashReadyStateEnabled','System.Boolean'),
        @('WeaponSimple_Katana_New','smashAttackRequest','System.Boolean'))) {
        $f = (Get-GameType $pair[0]).Fields | Where-Object Name -eq $pair[1]
        if (!$f -or $f.FieldType.FullName -ne $pair[2]) { throw "Field mismatch: $($pair -join '.')" }
        Write-Output "PASS field $($pair[0]).$($pair[1])"
    }
    $expected = @('WeaponSimple_Crossbow','WeaponSimple_Dagger','WeaponSimple_GreatSword','WeaponSimple_Katana','WeaponSimple_Katana_New','WeaponSimple_QuartterStaff','WeaponSimple_SwordAndShield')
    $actual = @($asm.MainModule.Types | Where-Object { $_.BaseType.Name -eq 'WeaponSimple' -and ($_.Methods | Where-Object Name -eq 'SubAttackButtonDown') } | ForEach-Object Name)
    if (Compare-Object $expected $actual) { throw "Weapon subtype set changed: $($actual -join ', ')" }
    Write-Output 'PASS all 7 native special-attack overrides classified; remaining weapon types left native'
    foreach ($pair in @(
        @('PlayerInputController','HandleOnSubFire','IntegratedActionController::Cast'),
        @('IntegratedActionController','ActuallyCast','PlayerAvatar::SubAttackButtonDown'),
        @('PlayerAvatar','SubAttackButtonDown','UnitAvatar::get_CanMove'),
        @('WeaponSimple_GreatSword','SubAttackButtonUp','WeaponSimple_GreatSword::sweepRequest'),
        @('WeaponSimple_GreatSword','Update','WeaponSimple_GreatSword::sweepTimer'),
        @('WeaponAddonGreatsword_BoneBlood','OnEnableAddon','WeaponSimple_GreatSword::OnTransformChangedServerside'),
        @('WeaponSimple_SwordAndShield','AttackButtonDown','WeaponSimple_SwordAndShield::isSweepUsing'),
        @('WeaponSimple_Crossbow','SubAttackButtonDown','WeaponSimple_Crossbow::specialAttackType'),
        @('WeaponSimple_Katana','SubAttackButtonDown','WeaponSimple_Katana::sheathActionType'))) {
        $m = (Get-GameType $pair[0]).Methods | Where-Object Name -eq $pair[1]
        if (!(($m.Body.Instructions | ForEach-Object ToString) -match [regex]::Escape($pair[2]))) { throw "Native path changed: $($pair -join ' / ')" }
        Write-Output "PASS native path $($pair[0]).$($pair[1])"
    }
    $pluginPath = Join-Path $PSScriptRoot '../bin/Release/SephiriaHoldSpecial.dll'
    $plugin = [Mono.Cecil.AssemblyDefinition]::ReadAssembly([IO.Path]::GetFullPath($pluginPath))
    try {
        $code = @($plugin.MainModule.Types | ForEach-Object Methods | Where-Object HasBody | ForEach-Object { $_.Body.Instructions })
        $gameWrites = @($code | Where-Object { $_.OpCode.Name -eq 'stfld' -and $_.Operand.DeclaringType.Scope.Name -eq 'Assembly-CSharp' })
        if ($gameWrites.Count) { throw "Unexpected direct writes to game fields: $gameWrites" }
        $forbidden = @($code | Where-Object { $_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.Name -match '^(UseMp|CreateSpecialAttackProjectile|SetAnimation|set_Network)' })
        if ($forbidden.Count) { throw "Unexpected game-mechanic bypass: $forbidden" }
        Write-Output 'PASS plugin only requests native inputs; no game-field writes/resource/animation overrides'
        $session = $plugin.MainModule.Types | Where-Object Name -eq 'HoldSession'
        foreach ($name in @('SpecialInput','Tick','Stop','SendSpecial')) {
            $method = $session.Methods | Where-Object Name -eq $name
            if (!(($method.Body.Instructions | ForEach-Object ToString) -match 'HoldSession::RequiresManualSpecial')) {
                throw "Missing manual-special exclusion: $name"
            }
        }
        Write-Output 'PASS manual-special exclusion covers arming, repeat, cleanup release and input dispatch'
    } finally { $plugin.Dispose() }
    Write-Output "GameAssembly SHA256=$((Get-FileHash -LiteralPath $path).Hash)"
} finally { $asm.Dispose() }
