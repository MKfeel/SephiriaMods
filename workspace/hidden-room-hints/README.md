# 隐藏房间提示 0.7.1

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

管理器通过仓库 catalog/mods.json 安装 0.7.1。手动安装时，将标准包中的 BepInEx 目录合并到游戏目录，先退出游戏并备份原 DLL；回退可使用仓库保留的 0.6.0 标准包。不要覆盖个人配置。

日志中的 Hidden portal stone 记录已关联传送石，Hidden entrance scan 记录扫描到的对象、连接状态和关联数量，便于诊断漏标。

## 资源分析

analysis/inspect_floor_assets.py 需要单独安装 UnityPy，并按本机路径调整游戏资源目录；构建插件不需要 Python。
