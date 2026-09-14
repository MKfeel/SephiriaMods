# 自用背包整理 2.5.4

基于本机已安装的 Sephiria Backpack Organizer 2.5.3 开发，保留原作者 Infinite-Heaven 的 MIT 许可证。统一插件 ID：`com.sephiria.backpack-organizer`，DLL：`SephiriaBackpackOrganizer.dll`。不与旧整理插件同时加载。

## 操作

| 操作 | 状态循环 |
| --- | --- |
| 中键 | 无标记 → ↑ → ↑↑ → 无标记 |
| Ctrl + 中键 | 无标记 → ↓ → ↓↓ → 无标记 |
| F8 | 请求整理一次；重复按键不堆积任务 |

↑↑ 优先启用并满级；↑ 尽量提高等级；↓ 保持启用，不争抢等级；↓↓ 尽量低于 0，允许失效，不额外追求更深的负值。换操作组直接切换至另一组第一个状态。箭头用 UI 几何绘制，不依赖游戏字体包含 emoji。

游戏日志收藏来自 `SaveManager.Current.GetBool("Item_Favorite_" + EntityID, false)`。收藏神器入包或首次读取时默认 ↑↑，同种神器的多个实例独立处理。手动标记（包括主动取消）覆盖收藏；取消收藏只移除自动 ↑↑。手动选择在当前背包会话内保留，不跨游戏重启保存；不会修改游戏收藏。

## 整理规则

按以下目标逐层比较，而非把所有目标加成一个可互相抵消的总分：

1. ↑↑ 的启用数量、总缺级、同等缺级时的分配均衡。
2. ↓ 的启用数量。
3. ↑ 的有效等级总和，上限为各自满级。
4. ↓↓ 的负等级达成数量。
5. 原有神器、石板和组合收益；↓ 的多余等级有轻微减分。
6. 同收益时减少位置变化。

未标记神器继续按机制、稀有度和特殊规则优化。克里顿的印章默认优先级 2，修复旧配置漏列时回落到稀有度的问题；显式配置和玩家标记仍可覆盖。原生 `Charm_IncreaseGoldDropRate` 在启用及升级时更新 `ECustomStat.MoneyDrop`，等级收益有价值，不按低收益神器处理。

保留指北针原目标和竖向链。沙漏、雷伊星碎片、白纸不再强制锁住整理前邻居，而是计算实际组合收益。旧 Ctrl + 中键方向绑定已取消。

后台使用智能布局、组合整体移动、局部区域重排、自适应温度搜索，始终保留按上述目标比较的最佳方案。默认搜索预算 0，表示完整执行 8 个起点、每起点最多 18000 次搜索，并进行最多 64 次局部改善。起点包括原布局、智能布局及包含空格的随机布局；局部精修枚举换位与石板换位加旋转。正数预算仍限制搜索耗时，不保证数学全局最优。

## 首次按键与状态变化

取消原版从首次按 F8 起算的 3 秒延迟。请求后检查本地玩家、背包容量、物品/神器/石板映射一致性及拖拽状态，并确认跨帧布局一致；准备完成自动继续，不需要再按一次。等待超过 10 秒结束请求。

搜索期间改标记或背包变化，取消旧计算并重新取快照。应用阶段先等待已发出的批次确认，再接受新标记重新计算。每帧最多 2 次交换、4 次旋转，应用预算 2ms。服务器状态持续不符时停止，不强行覆盖背包。

接受请求时提示“整理中…”，正常完成仅提示“整理完成”。异常仅提示“整理失败”，详细原因写 BepInEx 日志。

## 构建与检查

在工作区根目录执行：

```powershell
$env:APPDATA = Join-Path (Get-Location) '.nuget-appdata'
dotnet build .\backpack-organizer-own\SephiriaBackpackOrganizer.Own.csproj -c Release --configfile .\NuGet.Config
dotnet run --project .\backpack-organizer-own\tests\RulesTests.csproj -c Release
```

编译目标 net471 / BepInEx 5；引用本机 `E:\steam\steamapps\common\Sephiria` 中的游戏依赖。输出 `bin5\Release\SephiriaBackpackOrganizer.dll`。

验证：Release 编译零警告、零错误；共享生产规则的独立检查共 2259 条断言，覆盖标记循环、收藏覆盖、严格分层目标、满级上限、负等级阈值、同分稳定性，以及组合移动的重叠/边界/物品守恒、主背包容量边界及药水栏排除。安装与回退脚本在工作区隔离目录跑通，回退旧 DLL 哈希一致。不将这些检查视为 Unity 游戏内或 Mirror 联机验证，尚未完成新旧版本同背包性能对比。

## 安装与回退

2.5.4 延续本地自用版功能，默认快捷键 F8。退出游戏后，可运行本目录 `Install.ps1`；它会把现有新旧整理 DLL 移到游戏目录下的独立备份目录，再安装本版本并核对哈希。不要将整个源码目录复制进 BepInEx。

```powershell
powershell -ExecutionPolicy Bypass -File .\backpack-organizer-own\Install.ps1
```

默认游戏路径 `E:\steam\steamapps\common\Sephiria`。安装后配置为 `BepInEx\config\com.sephiria.backpack-organizer.cfg`，沿用原插件配置；旧方向绑定和旧评分参数不再使用。

如果使用 ZIP 安装包，解压后直接运行该目录下的 `Install.ps1`，无需构建源码。

脚本输出 `rollback.json` 路径。回退时退出游戏，运行：

```powershell
powershell -ExecutionPolicy Bypass -File .\backpack-organizer-own\Rollback.ps1 -ManifestPath '安装输出的 rollback.json 完整路径'
```

游戏验收重点：四种图标与点击循环；日志收藏入包自动 ↑↑ 和手动取消优先；首次 F8 自动完成；多件 ↑↑ 资源不足；印章；石板和神秘地块；指北针链；主机与客户端；整理中改标记和拖拽。由玩家自行启动游戏验收。

## 来源与文件

- 基线 DLL SHA-256：`A175E003808FCD749E235DCF36C94C8C52EA03559AD88EAD8C1DE2D9B4AB8C50`。
- `Organizer.cs`：从 2.5.3 反编译整理的机制模型、原生 UI 和应用逻辑；反编译局部变量保留原工具命名。
- `MarkRules.cs`、`Marks.cs`、`MarkArrow.cs`：标记目标、收藏同步和图标。
- `HybridSearch.cs`：新搜索；`SortRequests.cs`：请求及就绪检查；`InventoryObserver.cs`：本地入包事件。
- `tests/`：与游戏进程分离的规则检查。

发布至 MKfeel/SephiriaMods，管理器沿用 com.sephiria.backpack-organizer 条目发布 2.5.4，覆盖旧版；安装脚本也会迁移先前 .own 身份的本地开发版。




2.5.4 本地修正：就绪检查和快照校验统一只统计主背包，排除 y=100 的药水栏等外部格位；恢复开始提示，并在日志中记录具体等待原因。

2.5.4 搜索质量修正：恢复 0=完整搜索，取消每 600 步返回同一最优布局；新增 search-tests 隔离程序，链接实际 HybridSearch.cs，用合成评分场景验证已知最优、多起点完成、取消、限时、输入不变和物品守恒，共 18 项检查。合成模型不代替原生等级计算与真实背包验收。

诊断日志：每次整理分配独立编号，默认记录开始/结束的完整分层目标、逐件预测等级与原生显示/效果等级及启用状态、基础格等级和神秘地块倍率、背包与标记修订号、时间预算。差异仅为当前帧观测，不直接判定模型错误。日志中的本次最佳仅指本轮搜索；游戏内提示不变。
