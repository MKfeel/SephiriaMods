param([string]$GameDirectory = 'E:\steam\steamapps\common\Sephiria')
$ErrorActionPreference = 'Stop'
Add-Type -Path (Join-Path $GameDirectory 'BepInEx\core\Mono.Cecil.dll')
$assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $GameDirectory 'Sephiria_Data\Managed\Assembly-CSharp.dll'))
$contracts = @{
    HiddenRoomTriggerCollider = @('OnBreakServerside','hiddenRoomIndex','boxCollider')
    EnhancedProceduralFloorGenerator = @('hiddenRoomConnectRoomInstances','hiddenRoomConnectPassageIndexs','hiddenRoomDiggingIndexs','hiddenRoomInstances','hiddenRoomPassageIndexs')
    LibraryFloorGenerator = @('hiddenRoomConnectRoomInstances','hiddenRoomPassageDatas','passageSize','allHiddenPortal_Entrance','roomList','hiddenRoomInstances')
    BreakableProp_HiddenPortal = @('isConnected','passageDir','targetPosition')
    BreakableProp = @('IsBroken')
    FloorData = @('hiddenRoomCount')
    UI_WorldMapStageElement = @('floor')
}
foreach ($typeName in $contracts.Keys) {
    $type = $assembly.MainModule.Types | Where-Object Name -eq $typeName
    if (-not $type) { throw "游戏类型缺失：$typeName" }
    foreach ($field in $contracts[$typeName]) {
        if (-not ($type.Fields | Where-Object Name -eq $field)) { throw "游戏字段缺失：$typeName.$field" }
    }
}
$type = $assembly.MainModule.Types | Where-Object Name -eq 'UI_WorldMapStageElement'
$method = $type.Methods | Where-Object Name -eq 'SetFloor'
if (@($method).Count -ne 1 -or $method.Parameters.Count -ne 2 -or $method.Parameters[0].ParameterType.Name -ne 'FloorData') { throw '路线绑定方法签名变化。' }
$pluginPath = Join-Path $PSScriptRoot 'bin\Release\net471\SephiriaHiddenRoomHints.dll'
$plugin = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($pluginPath)
$type = $plugin.MainModule.Types | Where-Object Name -eq 'HiddenRoomHintPlugin'
$attribute = $type.CustomAttributes | Where-Object {$_.AttributeType.Name -eq 'BepInPlugin'}
if ($attribute.ConstructorArguments[0].Value -ne 'codex.sephiria.hidden-room-hints' -or $attribute.ConstructorArguments[2].Value -ne '0.7.1') { throw '插件 GUID 或版本异常。' }
$portalType = $assembly.MainModule.Types | Where-Object Name -eq 'BreakableProp_HiddenPortal'
foreach ($name in @('isConnected','passageDir','targetPosition')) {
    $field = $portalType.Fields | Where-Object Name -eq $name
    if (-not ($field.CustomAttributes | Where-Object {$_.AttributeType.Name -eq 'SyncVarAttribute'})) { throw "传送状态未同步：$name" }
}
$visual = $plugin.MainModule.Types | Where-Object Name -eq 'EntranceExclamation'
if (-not $visual -or ($plugin.MainModule.Types | Where-Object Name -in @('EntranceBoundsVisual','OpeningVisual','PortalArrow'))) { throw '入口未统一使用感叹号。' }
foreach ($t in @($type,$visual)) {
    foreach ($m in $t.Methods) {
        if ($m.HasBody -and ($m.Body.Instructions | Where-Object { $_.Operand -and $_.Operand.ToString() -match 'BoxCollider2D|ColliderCorners' })) { throw '可视化不应读取碰撞框。' }
    }
}
$prefabs = Get-Content -Raw (Join-Path $PSScriptRoot 'analysis\floor-prefabs.json') | ConvertFrom-Json
$desert = @($prefabs | Where-Object name -like 'Desert*')
if ($desert.Count -lt 1 -or @($desert | Where-Object generator -ne 'LibraryFloorGenerator').Count -gt 0) { throw '沙漠预制体证据与适配不符。' }
Write-Output "PASS: reflected fields, route SetFloor contract, plugin GUID/version; $($desert.Count) desert prefabs use LibraryFloorGenerator."
Write-Output ('DLL SHA256: ' + (Get-FileHash -LiteralPath $pluginPath).Hash)
$assembly.Dispose()
$plugin.Dispose()
