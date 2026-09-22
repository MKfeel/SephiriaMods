# 隐藏房间提示 0.7.3

2026-09-21：移除隐藏房功能全部周期扫描及全局对象查找。0.7.2 的降低扫描频率只减少调用次数，不能消除单次扫描尖峰，已由事件驱动替代。

- 入口通过 CombatBehaviour.Awake 挂接专用生命周期组件，在 OnEnable 登记，OnDisable/OnDestroy 注销；仅处理隐藏墙和传送石。
- 楼层使用原生 FloorGenerator.FloorGenerators 注册表。GenerateSuccess 设置后的 NotifyFloorAllocatedClientside 通知触发未就绪入口的重新解析；补丁挂在通知方法上，不依赖可能被游戏清空的事件订阅列表。
- 客户端入口 OnStartClient、传送石 Connect/DeserializeSyncVars、服务端/客户端破坏回调以及地图创建通知均触发更新。所有通知合并到 LateUpdate 处理；没有计时补漏或启动场景扫描。需在游戏启动时加载，不支持局中热加载补登记。
- 路线节点仅在 SetFloor 和配置变化时更新。每帧读取已知本地玩家的楼层字段以检测切层，并对已登记入口做可见性判断与相机投影；不遍历场景。旧 ScanIntervalSeconds 配置不再使用。

0.7.3 检查：Release 构建零警告/错误，48 项数据模拟回归通过；游戏程序集契约检查覆盖生命周期方法、传送石 Awake 基类调用链、楼层完成通知顺序，以及插件 IL 中不存在自动全局查找。旧测试模式的手动秒杀键仍会查找敌人，与隐藏房显示无关。尚未完成 Unity/Harmony 实机和联机验收。

## 0.7.2 历史修改（扫描策略已被 0.7.3 替代）

2026-09-21 性能优化：稳定场景将四类对象的全局查找从每 0.25 秒一次改为每 2 秒一次；切层立即刷新，生成未完成时仍按配置间隔重试。已有入口继续每 0.25 秒检查状态，破墙/破石后的场景标记在 LateUpdate 隐藏。稳定场景后出现的新对象最多等待约 2 秒加一个状态刷新间隔被发现。

墙入口成功定位后缓存墙图局部坐标；血量改变、墙图替换或生成未就绪时重新读取原生墙格，失败定位继续重试。屏幕外标记只按最终可见状态切换，不再每帧先启用再禁用。地图不在活动层级时跳过图标定位；移除周期性置顶、复用扫描集合，诊断字符串降至每 5 秒构造一次。路线节点保留 SetFloor 回调及低频发现补漏。

验证：Release 构建零警告/错误，48 项数据模拟回归和游戏程序集契约检查通过。全局查找频率在稳定场景下降约 87.5%，这是调用次数变化，不代表帧耗时下降相同比例；实际帧时间、UI 显隐、切层与联机表现仍需游戏内验收。

2026-09-14：隐藏入口统一显示黄色感叹号，覆盖破墙入口与沙漠传送石；场景和完整地图使用同一图形。取消入口轮廓、箭头和入口说明文字。路线选择节点保留“隐藏房”预告。

## 定位方式

沙漠使用 LibraryFloorGenerator 的 Portal 模式：打碎入口石头后出现传送门，不会生成 HiddenRoomTriggerCollider。此前只读取开墙通道，因此漏掉这种入口。

现在读取游戏内存中的 BreakableProp_HiddenPortal：仅标记 isConnected 为真、passageDir 为 Entrance、IsBroken 为假的入口。使用 GetExitPosition() 返回的实际入口坐标，随当前游戏相机投影显示黄色感叹号。房主通过生成器 allHiddenPortal_Entrance 确认归属；客户端通过源房间及同步 targetPosition 所在隐藏房唯一匹配，数据未就绪时重试。破石后清除标记。

破墙入口继续读取生成器的原生通道及墙格数据，以实际开墙区域靠连接房间的一侧定位感叹号。完整地图中，传送石按原生地图坐标换算定位，破墙入口标记在关联房间的入口侧。标记不阻挡点击，仅在当前相机所在楼层和可见范围显示。

本插件只读取入口状态，不调用破石、开墙或传送方法，不修改地图生成。感叹号直接绘制图形，不依赖字体。

## 配置

- Display.ShowWallMarker：场景中的统一黄色感叹号，包含墙入口和传送石。
- Display.ShowMapMarker：完整地图中的黄色感叹号。
- Display.ShowRouteMarker：路线选择节点的“隐藏房”预告，读取 FloorData.hiddenRoomCount；实际生成以进入后的结果为准。
- 原系统消息、屏幕提示及测试配置保留；测试模式当前关闭。

## 构建、检查和安装

在工作区根目录执行：

```powershell
$env:APPDATA = Join-Path (Get-Location) '.nuget-appdata'
dotnet build hidden-room-hints/SephiriaHiddenRoomHints.csproj -c Release --nologo
dotnet run --project hidden-room-hints/tests/Regression.csproj -c Release
& hidden-room-hints/Test-GameContract.ps1
& hidden-room-hints/Install.ps1
```

验证包括构建、48 项数据模拟回归，以及实际游戏程序集的字段、网络同步标记和路线方法签名检查。模拟测试不执行 Unity；沙漠实景位置、地图显示和联机客户端仍需完全重启游戏后验收。

安装位置：E:\steam\steamapps\common\Sephiria\BepInEx\plugins\SephiriaHiddenRoomHints\SephiriaHiddenRoomHints.dll。安装脚本先备份、再替换并核对 SHA-256；记录见 installation.json。保持原插件 GUID 和配置文件。

回退至此前 0.6.0（先完全退出游戏）：

```powershell
& hidden-room-hints/Install.ps1 -Rollback -BackupDirectory 'C:\Users\1\Documents\ChatGPT\赛菲莉娅mod开发\hidden-room-hints\backups\20260914-212140-619'
```

日志中的 Hidden portal stone / Hidden entrance 记录已关联入口；0.7.3 不再定时输出 Hidden entrance scan。

## Python 分析环境

资源分析使用 .tools/Invoke-ProjectPython.ps1，避免调用在受限环境中启动失败的 PATH Anaconda Python；详见 ../.tools/PYTHON_RUNTIME.md。
