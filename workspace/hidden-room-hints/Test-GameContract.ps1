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
if ($attribute.ConstructorArguments[0].Value -ne 'codex.sephiria.hidden-room-hints' -or $attribute.ConstructorArguments[2].Value -ne '0.7.3') { throw '插件 GUID 或版本异常。' }
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
# Validate every event hook against the installed game, including inherited lifecycle entry points.
$hooks = @{
    CombatBehaviour = @('Awake','OnStartClient')
    HorayModAPI = @('NotifyFloorAllocatedClientside')
    FloorGenerator = @('OnDestroy','CreateMapUI')
    HiddenRoomTriggerCollider = @('ApplyDamage')
    BreakableProp_HiddenPortal = @('Connect','DeserializeSyncVars','HandleBreakServerSide','HandleBreakClientSide')
}
foreach ($name in $hooks.Keys) {
    $gameType = $assembly.MainModule.Types | Where-Object Name -eq $name
    foreach ($methodName in $hooks[$name]) {
        $methods = @($gameType.Methods | Where-Object Name -eq $methodName)
        if ($methods.Count -ne 1 -or -not $methods[0].HasBody) { throw "生命周期方法缺失或重载变化：$name.$methodName" }
    }
}
foreach ($pair in @(@('BreakableProp_HiddenPortal','BreakableProp'), @('BreakableProp','CombatBehaviour'))) {
    $gameType = $assembly.MainModule.Types | Where-Object Name -eq $pair[0]
    $awake = $gameType.Methods | Where-Object Name -eq 'Awake'
    if (-not ($awake.Body.Instructions | Where-Object { $_.Operand -and $_.Operand.ToString().Contains(($pair[1] + '::Awake()')) })) {
        throw "入口 Awake 不再调用基类：$($pair[0])"
    }
}
$floorType = $assembly.MainModule.Types | Where-Object Name -eq 'FloorGenerator'
if (-not ($floorType.Fields | Where-Object { $_.Name -eq 'FloorGenerators' -and $_.IsStatic -and $_.IsPublic })) { throw '原生楼层注册表缺失' }
$generate = $floorType.NestedTypes | Where-Object Name -like '<Generate>d__*'
$moveNext = $generate.Methods | Where-Object Name -eq 'MoveNext'
$readyOffset = -1; $notifyOffset = -1
foreach ($instruction in $moveNext.Body.Instructions) {
    if ($instruction.Operand -and $instruction.Operand.ToString().Contains('::set_GenerateSuccess(')) { $readyOffset = $instruction.Offset }
    if ($instruction.Operand -and $instruction.Operand.ToString().Contains('::NotifyFloorAllocatedClientside(')) { $notifyOffset = $instruction.Offset }
}
if ($readyOffset -lt 0 -or $notifyOffset -le $readyOffset) { throw '楼层完成通知不再位于 GenerateSuccess 设置之后' }
function Assert-NoDiscovery($types) {
    foreach ($pluginType in $types) {
        foreach ($method in $pluginType.Methods) {
            # Legacy test hotkey is an explicit action, not hidden-room discovery.
            if ($method.Name -eq 'InstantKillAllEnemies' -or -not $method.HasBody) { continue }
            if ($method.Body.Instructions | Where-Object { $_.Operand -and $_.Operand.ToString() -match '::FindObjects|::FindObjectOfType|::FindFirstObjectByType|::FindAnyObjectByType' }) {
                throw "隐藏房功能残留全局查找：$($pluginType.Name).$($method.Name)"
            }
        }
        Assert-NoDiscovery $pluginType.NestedTypes
    }
}
Assert-NoDiscovery $plugin.MainModule.Types
$late = $type.Methods | Where-Object Name -eq 'LateUpdate'
if (-not ($late.Body.Instructions | Where-Object { $_.Operand -and $_.Operand.ToString().Contains('::ProcessEvents()') })) { throw 'LateUpdate 未消费生命周期事件' }
Write-Output 'PASS: lifecycle hooks, portal base Awake chain, native floor registry, generation-ready ordering, event consumer; no automatic global discovery calls in plugin IL.'
$desert = @($prefabs | Where-Object name -like 'Desert*')
if ($desert.Count -lt 1 -or @($desert | Where-Object generator -ne 'LibraryFloorGenerator').Count -gt 0) { throw '沙漠预制体证据与适配不符。' }
Write-Output "PASS: reflected fields, route SetFloor contract, plugin GUID/version; $($desert.Count) desert prefabs use LibraryFloorGenerator."
Write-Output ('DLL SHA256: ' + (Get-FileHash -LiteralPath $pluginPath).Hash)
$assembly.Dispose()
$plugin.Dispose()
