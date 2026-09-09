# 赛菲莉娅 Mod 开发工作区

这是《赛菲莉娅》（Sephiria）Mod 开发工作区的总交接文档，面向接手工作的 agent。目标是让新 agent 在不询问历史背景的情况下直接定位源码、构建、部署、诊断和继续开发。

> 资料快照：2026-08-28（Windows，Asia/Hong_Kong）。
>
> 重要：本文同时记录“源码状态”和“游戏目录中已部署的运行时状态”。两者存在版本差异时，不能把已部署 DLL 当成当前源码的产物；必须先区分来源再调试。

## 先读这几条

1. 先读本文件，再读与任务相关的源码、`.csproj`、`HANDOFF_MODSETTINGS_UI.md` 和对应子目录 README。
2. 用户负责启动游戏和进游戏验证。agent 不要主动启动、控制或操作游戏；可以在用户验证后只读 `LogOutput.log`。
3. 不要修改游戏程序集或原版资源。所有功能应通过 BepInEx 5 + Harmony 补丁实现。
4. 不要执行 `git reset --hard`、`git checkout --`、批量删除、覆盖不明 DLL 或清空配置。工作区有大量未跟踪目录，均可能是用户的开发材料。
5. 背包整理项目禁止上传 GitHub。只有用户明确确认后，才创建/上传 ModSettings 和弩自动上弹仓库；UI 未稳定前不要发布。
6. 修改前先运行 `git status --short --branch`，保留无关改动；不要把 `bin/`、`obj/`、`dist/` 或游戏目录文件误加入提交。
7. 遇到 UI 问题先做运行时日志诊断，不要根据截图盲改布局、缩放原版按钮或批量重挂 Image。

## 项目目标和范围

当前工作区包含一个已经发布过的许愿泉 Mod，以及三个正在整理/开发的相关 Mod：

| 模块 | 源码目录 | GUID | 当前源码版本 | 作用 | 当前状态 |
|---|---|---|---|---|---|
| 羁绊神器许愿泉 | 根目录 | `com.codex.sephiria.bondartifactwishes` | `0.2.0` | 让许愿泉显示并选择已解锁的羁绊神器 | 已有实现，已适配游戏 `1.0.30` 的记录 |
| ModSettings | `mod-settings/` | `com.sephiria.modsettings` | `1.0.11` | 在原版设置中增加“模组”Tab，显示其他 Mod 标记的配置 | 源码已有多次 UI 尝试；视觉/选中态仍须用户实测确认 |
| 弩自动上弹 | `crossbow-auto-reload/` | `com.sephiria.crossbow-auto-reload` | `1.1.0` | 配置脱战自动上弹：1.5 秒、8 秒、关闭 | 三档逻辑和独立设置行已实现；计划接入 ModSettings |
| 背包整理（BepInEx 5 构建） | `backpack-organizer-performance/` | `com.sephiria.backpack-organizer` | `2.5.1` | F8 触发原版/增强背包整理，离线评分后安全应用 | 源码与游戏目录中的部署版本不一致；禁止上传 |

另外两个目录不是当前发布目标：

- `SephiriaBackpackOrganizer-fork/`：背包整理的 fork/reference，含 BepInEx 6 和 BepInEx 5 项目、文档及手动提权代码；用于对照，不要默认它就是当前源码。
- `_compare-upstream-backpack-2.4.9/`：上游背包整理 `2.4.9` 的比较/逆向快照；只读参考，不要把它当作本工作区的发布目录。

根仓库当前 Git 状态快照：`main` 位于 tag `v0.2.0`，远端为 `origin/main`；根目录以外的多个模块目前是未跟踪目录。未跟踪不等于可以删除或忽略。

## 运行环境与关键路径

### 游戏和框架

- 游戏：Steam 版《赛菲莉娅》（Steam AppID `2436940`）。
- 当前开发基线：Windows、Unity Mono、BepInEx 5、Mirror 联机。
- 根目录许愿泉 README 记录的已适配游戏版本为 `1.0.30`。
- 逆向笔记中还记录过 `Unity 6000.3.21f1` 和游戏程序集 `v0.10.x`；这属于程序集分析记录，不要把它直接等同于游戏显示版本。
- 游戏只使用新版 Unity Input System；背包热键必须优先使用 `UnityEngine.InputSystem.Keyboard.current`，旧输入只作为回退。

### 本机路径

工作区：

```text
C:\Users\1\Documents\ChatGPT\赛菲莉娅mod开发
```

游戏目录和编译引用：

```text
E:\steam\steamapps\common\Sephiria
E:\steam\steamapps\common\Sephiria\Sephiria_Data\Managed
E:\steam\steamapps\common\Sephiria\BepInEx\core
```

重要运行时文件：

```text
E:\steam\steamapps\common\Sephiria\Sephiria_Data\Managed\Assembly-CSharp.dll
E:\steam\steamapps\common\Sephiria\Sephiria_Data\Managed\Mirror.dll
E:\steam\steamapps\common\Sephiria\BepInEx\core\BepInEx.dll
E:\steam\steamapps\common\Sephiria\BepInEx\core\0Harmony.dll
E:\steam\steamapps\common\Sephiria\BepInEx\LogOutput.log
```

插件安装目录：

```text
E:\steam\steamapps\common\Sephiria\BepInEx\plugins
```

配置目录：

```text
E:\steam\steamapps\common\Sephiria\BepInEx\config
```

当前三个相关插件推荐的一插件一文件夹路径：

```text
E:\steam\steamapps\common\Sephiria\BepInEx\plugins\SephiriaBondArtifactWishes\SephiriaBondArtifactWishes.dll
E:\steam\steamapps\common\Sephiria\BepInEx\plugins\SephiriaModSettings\SephiriaModSettings.dll
E:\steam\steamapps\common\Sephiria\BepInEx\plugins\SephiriaCrossbowAutoReload\SephiriaCrossbowAutoReload.dll
E:\steam\steamapps\common\Sephiria\BepInEx\plugins\SephiriaBackpackOrganizer\SephiriaBackpackOrganizer.dll
```

工作区内的 UnityPy 和分析脚本位于 `.tools/`。其中 `.tools/unitypy/` 有部分由提升权限安装的文件，普通读取可能出现拒绝访问；不要为了读取它们改权限或删除目录。

## 源码、部署和版本差异

### 源码版本是构建时的依据

| 模块 | 源码证据 | 本机游戏目录观察到的运行时状态 |
|---|---|---|
| 许愿泉 | 根 `SephiriaBondArtifactWishes.csproj` / `Plugin.cs` 为 `0.2.0` | DLL Assembly/File version `0.2.0.0`，文件存在 |
| ModSettings | `mod-settings/SephiriaModSettings.csproj` / `Plugin.cs` 为 `1.0.11` | `SephiriaModSettings` 文件夹和 DLL 当前未发现；不能按旧 handoff 认为 `1.0.6` 已部署 |
| 弩自动上弹 | `crossbow-auto-reload/SephiriaCrossbowAutoReload.csproj` / `Plugin.cs` 为 `1.1.0` | DLL Assembly/File version `1.1.0.0`，已部署；配置当前为 `Disabled` |
| 背包整理 | `backpack-organizer-performance/SephiriaBackpackOrganizer.BepInEx5.csproj` / `Plugin.cs` 为 `2.5.1` | DLL Assembly/File version `2.6.1.0`，配置文件头也写 `v2.6.1`；旧 handoff 写过 `2.5.3`。这是工作区外/旧构建，不能假设与源码一致 |

因此接手调试时要先回答：问题出在“当前源码构建”还是“游戏目录中较新的旧 DLL”。如果需要验证源码，先备份目标 DLL，再由用户决定何时让游戏加载新 DLL；不要直接把源码版本写成已部署版本。

当前游戏目录里还存在其他不属于本工作区的插件，例如 `SephiriaHardModeUnlocker`、`SephiriaHiddenRoomHints`、`SephiriaItemLab`。不要改动或删除它们。

## 构建

### 前置条件

- .NET SDK 8 或更高；本机核对版本为 `8.0.405`。
- Windows/.NET Framework 4.7.1 targeting 支持，因为 ModSettings、弩和背包 BepInEx 5 项目目标为 `net471`。
- 游戏已安装 BepInEx 5，并且下列 DLL 存在：BepInEx、0Harmony、Assembly-CSharp、Mirror 以及项目需要的 Unity DLL。
- 根许愿泉项目目标为 `netstandard2.1`；其他当前项目多数目标为 `net471`。
- 工作区没有统一 `.sln`，按项目分别构建。

### 构建命令

在工作区根目录执行：

```powershell
dotnet build .\SephiriaBondArtifactWishes.csproj -c Release
dotnet build .\mod-settings\SephiriaModSettings.csproj -c Release
dotnet build .\crossbow-auto-reload\SephiriaCrossbowAutoReload.csproj -c Release
dotnet build .\backpack-organizer-performance\SephiriaBackpackOrganizer.BepInEx5.csproj -c Release
```

根项目支持通过 MSBuild 参数覆盖游戏目录：

```powershell
dotnet build .\SephiriaBondArtifactWishes.csproj -c Release -p:GameDirectory="D:\Games\Sephiria"
```

注意：`mod-settings/`、`crossbow-auto-reload/` 和当前 `backpack-organizer-performance/SephiriaBackpackOrganizer.BepInEx5.csproj` 的引用路径仍直接写着 `E:\steam\steamapps\common\Sephiria`；换机器或换盘时必须修改项目引用，不能只给它们传 `GameDirectory` 参数。

构建产物：

```text
dist/SephiriaBondArtifactWishes.dll
mod-settings/bin/Release/SephiriaModSettings.dll
crossbow-auto-reload/bin/Release/SephiriaCrossbowAutoReload.dll
backpack-organizer-performance/bin5/Release/SephiriaBackpackOrganizer.dll
```

各项目引用的游戏/BepInEx DLL 均为 `Private=false`，发布目录不应包含游戏程序集。根 `.gitignore` 已忽略 `bin/`、`obj/`、`dist/`、`*.user`；不要手动把这些输出加入版本控制。

构建后至少检查：

```powershell
git status --short
Get-Item .\dist\SephiriaBondArtifactWishes.dll
Get-Item .\mod-settings\bin\Release\SephiriaModSettings.dll
Get-Item .\crossbow-auto-reload\bin\Release\SephiriaCrossbowAutoReload.dll
Get-Item .\backpack-organizer-performance\bin5\Release\SephiriaBackpackOrganizer.dll
```

## 部署、备份和回滚

部署前应先停止游戏，并只操作明确的目标文件。建议使用带标签的同目录备份，例如：

```text
SephiriaCrossbowAutoReload.dll.bak-before-<reason>
SephiriaBackpackOrganizer.dll.bak-before-<reason>
```

部署规则：

- DLL 放入对应的插件子文件夹，不要把多个 Mod 的 DLL 混在一起。
- 覆盖前先复制备份；不要覆盖 `.cfg`，除非明确设计了配置迁移。
- BepInEx 配置由插件首次加载生成，配置路径以 GUID 为准。
- 配置中的旧键通常会被 BepInEx 保留；不要因为一个旧键就删除整个配置文件。
- 回滚应恢复同一个 DLL 的明确备份，并在用户重新启动游戏后读日志确认实际加载版本。
- 任何“已部署/已修复”的结论都必须有目标 DLL 时间/版本或 `LogOutput.log` 证据。

## Mod 1：羁绊神器许愿泉

### 功能和行为

源码在根目录：

```text
Plugin.cs
WishingFountainPatches.cs
WishCost.cs
BondFilterPatches.cs
SephiriaBondArtifactWishes.csproj
```

功能：

- 在 `UI_DimensionPocketPanel.Open` 前，将玩家已解锁、`ItemEntity.isDual == true` 且类型为 `Charm` 的羁绊神器加入许愿泉候选。
- 用 `ItemMetadata(-1, itemId, 1)` 添加候选，并按已有 `entityID` 去重。
- 只有消耗不超过当前许愿容量的羁绊神器才加入候选。
- 在稀有度过滤栏加入本地化的羁绊按钮，标签使用 `ItemRarity_Dual`，颜色为 `RGBA(204,66,171,255)`。
- 过滤逻辑支持羁绊、收藏模式和分类标签组合。
- 羁绊神器消耗统一由配置控制，默认 `9` 点，允许范围 `0..999`。
- 成本同步覆盖许愿界面、预设面板以及房主/服务器开局发放逻辑。
- 费用图标按每行最多 6 个点显示；默认 9 点为上排 3、下排 6，左对齐，避免挤到相邻图标。
- 不改游戏程序集、不改原版许愿存档格式。

### 配置

配置文件：

```text
BepInEx/config/com.codex.sephiria.bondartifactwishes.cfg
```

默认内容的核心项：

```ini
[WishingFountain]
BondArtifactCost = 9
```

修改后重启游戏。若成本高于玩家当前许愿容量，该候选不会显示。

### Harmony 补丁点和维护注意

当前成本/可见性 transpiler 会尝试补丁：

- `UI_DimensionPocketPanel.OnOpened`
- `UI_DimensionPocketPanel.HandleItemClick`
- `UI_DimensionPocketPanel.UpdateCapacityText`
- `UI_DimensionPocketPanel.UpdateItemSelectable`
- `UI_PresetPanel.UpdateDimensionPocketInfo`
- `PlayerSpawner.AddDimensionPocketItemsOnServer`

关键替换：

- 原版 `ItemEntity.rarity -> GetCapacity` 改为 `WishCost.GetItemCost`。
- 原版 `rarity -> SetCost` 改为 `WishCost.SetIconCost`。
- 原版 `cannotBeReward` 改为 `WishCost.IsRewardBlocked`，羁绊神器不被错误过滤。
- 许愿泉可见性判断中 Eternal 羁绊通过 `GetRarityForVisibility` 映射为 Common，以进入原版列表。

游戏更新后必须看日志中的 `Patched ... cost=..., icon=..., filter=...` 和 warning；如果 cost 替换为 0，不要继续假设功能正常。

### 联机规则

- 许愿选择发生在客户端，但开局物品由房主/服务器发放。
- 房主必须安装本 Mod，否则服务器仍按原版成本和候选计算。
- 建议所有玩家安装相同版本并使用相同 `BondArtifactCost`。
- 解锁列表按玩家自身生效，不要把一个玩家的羁绊解锁当成全队状态。

## Mod 2：Sephiria ModSettings

源码：

```text
mod-settings/Plugin.cs
mod-settings/SephiriaModSettings.csproj
mod-settings/README.md
```

### 目标和当前实现

目标是在原版“设置”窗口增加“模组”Tab；各 Mod 通过 `ConfigDescription.Tags` 标记要显示的配置，ModSettings 不需要被目标 Mod 硬引用。

当前源码版本为 `1.0.11`，入口在 `UI_OptionsPanel.OnOpened`：

- 首次创建“模组”Tab 和内容页。
- 扫描当前进程已加载的 BepInEx 5 插件及其 `ConfigEntry`。
- 只收集带 `ModSettings:Show` 的设置。
- 按插件分组，按 `ModSettings:Order`、Section、Key 排序。
- 支持 `bool`、枚举、`AcceptableValueList<T>`、带范围的 `int/float/double`。
- 通过 `ConfigEntryBase.BoxedValue` 修改值，遵守原 `ConfigFile.SaveOnConfigSet`，触发 BepInEx 的 `SettingChanged`。
- 目标 Mod 可监听 `SettingChanged`，把值更新到缓存；高频逻辑不应每帧读取配置文件。
- 当前布局采用原版设置行模板的绝对位置，不再给固定坐标行强加 `VerticalLayoutGroup + ContentSizeFitter`。
- 当前 Tab 按钮克隆使用实际屏幕 X 选择最右侧原版按钮，并重建克隆按钮的 `onClick`。
- `OptionsTabDiagnosticPatch` 会在选择 Tab 时输出视觉诊断。

### ModSettings 元数据协议

标签放在 `ConfigDescription.Tags`，不需要添加对 `SephiriaModSettings.dll` 的引用：

| 标签 | 必须/可选 | 含义 |
|---|---|---|
| `ModSettings:Show` | 必须 | 允许该配置出现在“模组”页面 |
| `ModSettings:ModTitle=背包整理` | 可选 | Mod 分组标题；默认使用 BepInEx 插件名 |
| `ModSettings:Name=整理模式` | 可选 | UI 显示名；默认使用 Config Key |
| `ModSettings:Order=10` | 可选 | 同一 Mod 内排序，数字越小越靠前 |
| `ModSettings:Reload=Immediate` | 可选 | 文档语义标记；也可用 `NextUse`、`Restart` |
| `ModSettings:HostOnly` | 可选 | 名称后显示“（房主）”；表示网络权威由房主决定 |
| `ModSettings:Step=50` | 可选 | 数值范围控件的步长 |
| `ModSettings:Option:枚举值=显示文本` | 可选 | 枚举/列表值的本地化显示文本 |

最小示例：

```csharp
ConfigEntry<bool> enabled = Config.Bind(
    "Gameplay",
    "Enabled",
    true,
    new ConfigDescription(
        "是否启用功能。",
        null,
        "ModSettings:Show",
        "ModSettings:ModTitle=示例 Mod",
        "ModSettings:Name=启用功能",
        "ModSettings:Order=10",
        "ModSettings:Reload=Immediate"));
```

如果需要加载顺序但不希望强制用户安装，可使用软依赖：

```csharp
[BepInDependency(
    "com.sephiria.modsettings",
    BepInDependency.DependencyFlags.SoftDependency)]
```

### 支持范围和限制

- 不支持自由文本输入；字符串配置继续直接编辑 `.cfg`。
- 当前不支持 `KeyboardShortcut` 控件；背包热键仍应直接改配置。
- 数值范围会按步长生成选项，单次最多约 201 个值；范围太大必须提供合理的 `ModSettings:Step`。
- `Reload` 只是 UI/协议语义，不会自动让目标 Mod 热重载；目标 Mod 必须自己监听事件或在下次使用时读取缓存。
- 只扫描当前进程已经加载的插件；未加载或未安装的 Mod 不会出现。
- ModSettings 不绕过 Mirror 的服务器权限，房主设置仍由房主决定。

### 当前未解决的 UI 问题

在 `HANDOFF_MODSETTINGS_UI.md` 记录的最后一次有效反馈中，下列问题仍不能视为解决：

- 点击“模组”后，高亮可能仍出现在“游戏性”位置。
- 顶部 Tab 条可能整体横向偏移/溢出，第一项左侧被挤出。
- 不能因为当前源码版本变成 `1.0.11`，就把这些问题当成已经经过游戏验证；ModSettings DLL 在当前游戏目录也未发现。

### 已确认的原版 UI 资源数据

资源分析脚本位于 `.tools/inspect_unity_options.py`、`.tools/dump_options_hierarchy.py`。`level1` 和 `level2` 都有设置界面；以下数字主要来自 `level2`：

- `OptionPanel`：GameObject path ID `4543`。
- `TabArea` 宽 `433`。
- `TabLabel` 实际宽约 `382`，资源 `sizeDelta.x = -51`，为横向拉伸。
- 原版 5 个按钮均约 `74 x 20`，中心 X 为 `0, 77, 154, 231, 308`，原生间距 `3`。
- 5 栏总宽：`5*74 + 4*3 = 382`。
- `Base` 宽 `489`；六个原生宽按钮理论总宽 `6*74 + 5*3 = 459`，可以放下。
- 原版按钮 Image 在资源中为 stretch（anchor 0..1，sizeDelta 0）。
- 原版按钮 GameObject path ID：GamePlay `3463`、Sound `4633`、Display `3755`、Keyboard `3131`、Gamepad `5469`。

布局必须使用运行时 Canvas/UI 单位，不要使用屏幕像素；不能简单修改 `TabLabel.sizeDelta` 后假设整体仍居中。

### UI 诊断的正确下一步

先做只加日志、不改布局的诊断版。用户只需启动游戏并打开设置一次，agent 随后读取本机日志；不要要求用户手动复制日志，也不要主动启动游戏。

诊断至少记录：

- `tab.tabButtons` 每个索引、按钮名/GameObject 名、InstanceID。
- 每个按钮 `RectTransform.anchoredPosition`、尺寸、父节点和实际 X 顺序。
- `UI_TabButton.butttonImage` 私有字段实际指向的 GameObject 名、InstanceID、父路径。
- backing Image 的父路径、坐标、sprite、Image 类型。
- 按钮下所有 TMP 文本。
- 点击“模组”后 `tab.CurrentSelectedTab`、EventSystem 当前选中对象和每个按钮 `IsActivated`。

重点验证：

1. `tab.tabButtons` 数组顺序可能不等于屏幕从左到右顺序，模板必须按实际 X 选择。
2. 克隆出来的 `UI_TabButton.butttonImage` 可能仍指向原版“游戏性”Image；必须依据日志重新绑定到克隆按钮自己的根 Image，不能继续猜 `GetComponentInChildren<Image>()`。

如果要完整克隆按钮，优先验证以下思路的运行时引用是否正确，再决定是否保留：

```csharp
GameObject cloneObject = UnityEngine.Object.Instantiate(
    sourceButton.gameObject,
    sourceButton.transform.parent);
UI_TabButton newButton = cloneObject.GetComponent<UI_TabButton>();
```

### 已失败的 UI 方案，禁止重复

1. 把 6 个按钮压缩到原 382 宽，宽度约 `61.17`：九宫格/切片高亮边框无法缩小，出现跨 Tab 错位。
2. 给 `HorizontalLayoutGroup` 设置 `preferredWidth=0`、`flexibleWidth=1`：6 个按钮被挤到中间。
3. 强行重挂/拉伸全部按钮 Image：没有解决序列化引用问题，还破坏原版按钮高亮。
4. 给克隆内容强加 `VerticalLayoutGroup + ContentSizeFitter`：原版设置行是固定 `RectTransform` 坐标，导致所有行重叠；当前应按模板尺寸绝对排列。
5. 只凭资源层级猜 `UI_TabButton.butttonImage`：资源层级不能证明运行时字段引用。

### ModSettings 验收清单

- [ ] 设置窗口能看到“模组”Tab，并能切回所有原版 Tab。
- [ ] 点击“模组”后高亮就在“模组”按钮上，不在“游戏性”。
- [ ] 顶部第一项和最后一项完整可见，无横向溢出或整体偏移。
- [ ] Mod 标题是纯文本，没有左右箭头或配置行底板。
- [ ] 多个 Mod 分组顺序正确，行间距正常，内容可滚动。
- [ ] bool、enum、范围 int/float/double 可切换，当前值显示正确。
- [ ] 切换语言后标题/标签不显示旧语言残留。
- [ ] 关闭并重新打开设置，值不会重复创建、重复监听或错位。
- [ ] 外部编辑 `.cfg` 触发 `SettingChanged` 后，已打开的行能刷新。
- [ ] 未安装 ModSettings 时，目标 Mod 仍可正常运行。
- [ ] 不同分辨率/CanvasScaler 下重复确认布局。
- [ ] 日志无 `NullReferenceException`、Harmony patch failure 或重复 Tab。

## Mod 3：弩自动上弹

源码：

```text
crossbow-auto-reload/Plugin.cs
crossbow-auto-reload/SephiriaCrossbowAutoReload.csproj
crossbow-auto-reload/README.md
```

### 当前功能

枚举 `AutoReloadMode`：

| 值 | 行为 |
|---|---|
| `Original` | 原版脱战 `1.5` 秒自动上弹 |
| `Delayed` | 脱战 `8` 秒后自动上弹 |
| `Disabled` | 不因脱战自动上弹 |

手动换弹、弹匣打空后的换弹、换弹动画、弹药强化机制不应受影响。

### 实现约束

- 配置文件：`BepInEx/config/com.sephiria.crossbow-auto-reload.cfg`。
- 配置项：`[Gameplay] AutoReloadMode`，默认 `Original`。
- `SettingChanged` 时把模式转换为普通静态字段 `AutoReloadDelaySeconds`：`1.5f`、`8f` 或 `float.PositiveInfinity`。
- `WeaponSimple_Crossbow.Update` transpiler 只替换紧跟在 `autoReloatTimer` 字段读取后的 `1.5f` 阈值。
- 如果找不到精确阈值，记录 error 并不修改原逻辑，避免误改其他换弹代码。
- 每帧只读取缓存的 `float`，不得每帧读取 `ConfigEntry` 或磁盘。
- 当前设置 UI 仍通过 `UI_OptionsPanel.OnOpened` 克隆原版“游戏性”设置行，三档选择器在底部显示。

当前游戏目录中弩配置还残留：

```ini
EnableOutOfCombatAutoReload = false
```

这不是当前源码绑定的配置项，属于旧配置残留；不要重新在源码中引入，也不要仅为清理它而删除整个 cfg。

### 计划中的 ModSettings 接入

目标是把 `AutoReloadMode` 标记为 ModSettings 可见，并提供：

```text
ModSettings:Show
ModSettings:ModTitle=弩自动上弹
ModSettings:Name=自动上弹模式
ModSettings:Order=10
ModSettings:Reload=Immediate
ModSettings:Option:Original=原版（1.5秒）
ModSettings:Option:Delayed=延长（8秒）
ModSettings:Option:Disabled=关闭自动上弹
```

当前弩源码尚未包含这些标签，也未包含 `BepInDependency`；因此“接入 ModSettings”尚未完成。建议最终行为是：ModSettings 已安装时只显示通用 ModSettings 行，未安装时保留独立设置行作为 fallback，避免重复显示；实施前检查 `UI_OptionsPanel.OnOpened` 的重复注入和加载顺序。

### 联机规则和验收

- 弩换弹由房主/服务器判定，联机时房主设置统一控制。
- 三档都要分别验证脱战计时。
- 手动换弹和弹匣耗尽换弹不能被 Disabled 误禁用。
- 修改设置后无需重启即可影响下一次判定。
- ModSettings 和独立 fallback 不应同时出现两行。

## Mod 4：背包整理

源码当前主目录：

```text
backpack-organizer-performance/Plugin.cs
backpack-organizer-performance/InventorySorter.cs
backpack-organizer-performance/ManualPriority.cs
backpack-organizer-performance/SephiriaBackpackOrganizer.BepInEx5.csproj
```

### 版本和构建分支注意

- 当前工作区目标是 BepInEx 5 / Unity Mono 构建，项目定义 `BEPINEX5`，输出到 `bin5/Release/`。
- `Plugin.cs` 通过条件编译同时保留 BepInEx 6 类型注释/分支，但不要因此把当前 BepInEx 5 csproj 改成 BepInEx 6。
- `SephiriaBackpackOrganizer-fork/` 和 `_compare-upstream-backpack-2.4.9/` 是参考项目；它们的 BepInEx 6 README、版本号或安装包不能覆盖当前 BepInEx 5 工作区规则。
- 源码版本为 `2.5.1`，游戏目录 DLL 为 `2.6.1.0`；修复问题前必须确认实际运行的是哪一个。

### 用户功能

- 默认按 `F8` 整理背包。
- `SortMode=Vanilla` 使用游戏内置自动排列；`Enhanced` 使用后台增强搜索。
- 增强搜索在主线程构建快照后，后台只对纯数据副本评分/搜索；最终交换、旋转和 Mirror/Unity 操作回到主线程。
- 可设置进入会话后自动整理一次；默认关闭。
- 有背包初始化保护，默认进入会话后等待 3 秒；期间按 F8 应提示稍后再试，不能冒险写入未就绪背包。
- 联机客户端也可以请求整理；应用阶段按交换/旋转序列逐步执行并等待服务器确认。
- 中键点击护符可切换手动提权，显示 `P1/P2...`；P1 为最后一次提权、权重最高的护符。
- 自检模式会先随机打乱再整理，仅主机可用，用于比较整理前后评分。

### 算法/评分必须保持的事实

以下是 `SephiriaBackpackOrganizer-fork/analysis/REVERSE-ENGINEERING-NOTES.md` 和当前实现记录的核心事实。修改 `InventorySorter.cs` 时，不要把它们简化成“按稀有度排序”：

#### 背包数据与权限

- `GridInventory` 是 Mirror `NetworkBehaviour`。
- 主要数据：`inventoryMatrix`、`charms`、`stoneTablets`，格子默认宽 6、最高 7 行，实际容量由 `CurrentInventoryStorage` 决定。
- `NewItemOwnInstance` 有 `InstanceID / Quantity / XIdx / YIdx / EntityID / Charm / StoneTablet`。
- `ItemEntity` 是物品定义，包含 `id / type / rarity / categories / cost` 等。
- `GridInventory.Permission` 会移除/重放石板效果、刷新护符启用状态和套装效果；布局写入必须包在 `using (new GridInventory.Permission(inv))` 内。
- 背景线程不能调用 Unity、`DungeonManager`、游戏对象、Mirror 或任何非线程安全 API；只能使用主线程预先构建的快照。

#### 石板、等级和位置条件

- 石板支持 `IncreaseConstLevel`、`MultiplyConstLevel`、`Disable`、`IgnoreCriteria` 等效果，并允许旋转。
- `conditionQuery` 不满足时整块石板效果可能不生效；离线模型必须复刻条件判定，不能只展开效果网格。
- 负等级石板应尽量把负效果推出背包有效区域、放在非护符下方或其他低损位置；越界等级对实际物品天然无害，但模型仍需和游戏评分逻辑一致。
- `IgnoreCriteria` 会让豁免格上的受限护符跳过位置条件。
- 护符条件包括：顶行、底行、左右边、内侧、外侧、两侧为空、两侧有护符、八邻域满、靠近魔法书、满 HP 等。
- `SideEnd.IsActivePosition` 存在游戏 bug；以 `GetCriteria` 语义和真实 `EvaluateCurrentAutoArrangeScore` 为准，不要按错误的辅助方法猜测。
- 游戏评分的主要量级是有效等级 `×10000`、启用数量 `×1000`，并包含禁用/负等级/溢出惩罚；离线评分必须以真实评分为最终兜底。

#### 主要协同机制

- 行星望远镜 `Charm_PlanetModule`：周围 8 格的行星类护符会获得真实战斗增强；默认 `PlanetBonus=40000`，可排除“谱子「银河」”和红色行星观察日志等不应聚簇的行星。
- 和谐之晶：周围 8 格的护符有效等级越高越好，默认 `HarmonyLevelBonus=2000`。
- 奉献徽章：同一横排的同伴藏品越多越好，默认 `DedicationCompanionBonus=3000`；同伴通过 `ICompanionCharm` 识别。
- 发光沙漏：放在 CD 最长魔法书左边，默认 `HourglassBonus=6000`；右边魔法书 CD 恢复按等级为 `+30/+60/+100%` 的游戏效果。
- 雷伊星碎片：方向与沙漏相反，放在耗蓝最高魔法书右侧，默认 `RayShardBonus=4000`。
- 永恒蚀：冰霜武具多于太阳剑时偏右三列，反之偏左三列；数量相等或没有时不限。
- 对立之秤：冰川多于余烬时偏最右列，少于时偏最左列，相等时只能最左或最右列。
- 白纸：在两件同类连击神器中间补 1 个类别；优先数量最大、尚未达到最高档的连击，例如坚固 `9/10`。
- 指北针：整理前已经配对的每枚针必须绑定同一个物品实例；连续竖向指北针形成链并一起移动。整理前未配对的针才允许重新寻找伤害类护符/指北针。
- 凯尔萨德尼钥匙：根据不依赖钥匙自己和白纸复制的现有数量，在坚固、余烬、冰川、魔法科技中选择最多类别；实际周期行顺序为 `STURDY / EMBER / GLACIER / MAGITECH`。
- 金色指北针已绑定的目标可强制 P1；`CompassTargetForcedHigh` 默认开启。
- 闪烁的眼睛和蜥蜴板甲默认是低等级价值物品：优先保证启用，等级分按 `LowLevelValueFactor=0.1` 打折。
- `MinLevelItems` 默认要求谱子「银河」达到等级 2；缺少目标等级必须有明显惩罚，避免仅因稀有度或聚簇把它牺牲掉。
- 多用途腰带/木箱：启用后尽量把神器放满最上行；默认 `BeltRowBonus=2500`。
- 心之重担等负面藏品应塞入最差的负等级格，默认 `NegativeCellPenalty=20000`。
- 神秘标签：神秘护符至少 2 个时通常有 1 个 `×2` 地块，至少 5 个时共 4 个；默认 `MysticMultiplier=2`。神秘位置可能在刚凑齐数量的第一次整理前尚未生成，第二次 F8 才可见。
- 如意宝珠 `Charm_Chintamani` 本身是 MYSTIC；启用后受致命伤会保命、临时 `+3` 后消失。放在神秘 `×2` 地块可让临时增益等效放大；它是低优先级“预备+3”物品，不应抢走其他重要护符位置。

#### 物品识别和配置 key

- 默认使用物品 `LocalizedString` key，例如 `Item_MindBurden_Name`、`Item_ColdLock_Name`。
- 当前实现的匹配应支持 `aName.key` 和护符类名，大小写不敏感；不要把玩家看到的中文名硬编码进算法。
- 打开详细诊断时，`LogItemIdentification` 会打印护符 `key|类名`，新增物品配置前先用日志确认 key。
- 常用 key：
  - 指北针：`Item_UpCharmDamage_Name`（蓝针为 `Item_UpCharmDamage_Uncommon_Name`）。
  - 闪烁的眼睛：`Item_ShadowEye_Name`。
  - 蜥蜴板甲：`Item_PlateArmor_Name`。
  - 故障探测针：`Item_FaultfinderNeedle_Name`。
  - 多用途腰带：`Item_Belt_Name`，类识别也覆盖 `Charm_WoodenBox`。
  - 谱子「银河」：`Item_SuperPlanet_Name`。
  - 和谐之晶：`Item_NearLevelDamage_Name`。
  - 雷伊星碎片：`Item_DoubleMagic_Name`。
  - 负担：`Item_MindBurden_Name`。
  - 如意宝珠：`Item_Chintamani_Name`。

### 背包当前配置速查

真实默认值以 `backpack-organizer-performance/Plugin.cs` 为准；以下表格用于接手时定位配置，不替代源码。

| 分区/键 | 默认值 | 说明 |
|---|---:|---|
| `General/Hotkey` | `F8` | 触发整理；当前 ModSettings 不支持快捷键控件 |
| `General/SortMode` | `Enhanced` | `Vanilla` 或 `Enhanced` |
| `General/ShowNotifications` | `true` | 游戏内完成/失败提示 |
| `General/AutoSortOnSessionStart` | `false` | 进会话自动整理一次 |
| `General/SessionStableDelay` | `3` | 背包初始化等待秒数，范围 `0..30` |
| `General/RowLockedItems` | 空 | 逗号分隔的 LocalizedString key；凯尔萨德尼钥匙无需手填 |
| `Vanilla/MaxIterations` | `30` | 原版排列迭代次数，范围 `1..500` |
| `Vanilla/AllowTabletRotation` | `true` | 是否允许旋转石板 |
| `Enhanced/Iterations` | `3000` | 模拟退火迭代数，范围 `10..50000` |
| `Enhanced/Restarts` | `3` | 随机重启数，范围 `1..10` |
| `Enhanced/Temperature` | `800` | 初始温度，范围 `1..50000` |
| `Enhanced/SearchRounds` | `4` | 独立搜索轮数，范围 `1..10` |
| `Enhanced/SearchTimeBudgetMs` | `0` | 搜索主线程预算；0 为不限制，范围 `0..1000` |
| `Apply/SwapsPerFrame` | `2` | 每帧交换次数，范围 `1..10` |
| `Apply/RotationClicksPerFrame` | `4` | 每帧旋转点击次数，范围 `1..12` |
| `Apply/FrameBudgetMs` | `2` | 每帧应用预算，范围 `0.25..10` |
| `Apply/NetworkAckTimeoutMs` | `2000` | 联机等待确认，范围 `250..10000` |
| `ManualPriority/Enabled` | `true` | 开启中键提权 |
| `ManualPriority/ShowBadge` | `true` | 显示 P1/P2 标记 |
| `ManualPriority/Strength` | `6000` | P1 额外等级分，范围 `1..100000` |
| `Debug/VerboseDiagnostics` | `false` | 输出物品、网格和机制分析 |
| `Debug/SelfTest` | `false` | 仅主机可用的随机打乱自检 |
| `Smart/EnableSmartStart` | `true` | 启用石板/受限护符智能初始布局 |
| `Smart/EnableRandomStarts` | `true` | 启用多起点搜索 |
| `Smart/CriteriaWeight` | `150` | 位置条件引导权重，范围 `0..5000` |
| `Smart/NegativeWeight` | `80` | 负等级格引导惩罚，范围 `0..2000` |
| `Smart/CriteriaMoveChance` | `0.15` | 受限护符定向移动概率，范围 `0..1` |
| `Priority/Enable` | `true` | 启用优先级系统 |
| `Priority/Common` | `4` | 普通优先级，1 最高，范围 `1..4` |
| `Priority/Uncommon` | `3` | 高级优先级 |
| `Priority/Rare` | `2` | 稀有优先级 |
| `Priority/Legend` | `1` | 传说优先级 |
| `Priority/Eternal` | `1` | 羁绊/永恒优先级 |
| `Priority/FixedHighPriorityItems` | 见 cfg | 强制 P1 的 key 列表 |
| `Priority/ForcedPriorityItems` | 见 cfg | `key:优先级`，覆盖稀有度映射 |
| `Priority/IgnoreCellPreferredItems` | `Item_ColdLock_Name` | 优先使用豁免格的 key |
| `Priority/Weight1..4` | `1.5/1.25/1.1/1` | 各优先级等级分权重，范围 `0.5..3` |
| `Priority/CompassTargetForcedHigh` | `true` | 金色针绑定目标强制 P1 |
| `Priority/LowLevelValueItems` | 两个 key | 等级价值低，默认降为 P4 |
| `Priority/LowLevelValueFactor` | `0.1` | 低价值等级分比例，范围 `0..1` |
| `Priority/MinLevelItems` | `Item_SuperPlanet_Name=2` | 最低目标等级列表 |
| `Synergy/PlanetBonus` | `40000` | 望远镜周围行星奖励，范围 `0..200000` |
| `Synergy/PlanetClusterExcludedItems` | 两个 key | 不参与望远镜聚簇的行星 |
| `Synergy/HarmonyCrystalItems` | `Item_NearLevelDamage_Name` | 和谐之晶识别 key |
| `Synergy/HarmonyLevelBonus` | `2000` | 和谐之晶周围等级奖励 |
| `Synergy/DedicationBadgeItems` | `Item_CompanionChaos_Name` | 奉献徽章 key |
| `Synergy/DedicationCompanionBonus` | `3000` | 同排同伴奖励 |
| `Synergy/HourglassItems` | 空 | 沙漏自定义 key；默认类识别已覆盖 |
| `Synergy/HourglassBonus` | `6000` | 沙漏-魔法书奖励 |
| `Synergy/RayShardItems` | `Item_DoubleMagic_Name` | 雷伊星碎片 key |
| `Synergy/RayShardBonus` | `4000` | 碎片-魔法书奖励 |
| `Synergy/EclipseItems` | 空 | 永恒蚀自定义 key |
| `Synergy/OpposingScaleItems` | 空 | 对立之秤自定义 key |
| `Synergy/WhitePaperComboBonus` | `5000` | 白纸连击补位奖励 |
| `Synergy/CompassBonus` | `12000` | 指北针维持原目标奖励；0 不解除绑定 |
| `Synergy/CompassUnpairedFactor` | `0.1` | 未配对指北针等级分比例 |
| `Synergy/BeltItems` | `Item_Belt_Name` | 腰带/木箱 key |
| `Synergy/BeltRowBonus` | `2500` | 第一行每件神器奖励 |
| `Burden/NegativeCellPenalty` | `20000` | 负面藏品未入负格惩罚 |
| `Burden/ItemKeys` | `Item_MindBurden_Name` | 负面藏品 key 列表 |
| `Mystic/Enable` | `true` | 神秘 ×2 地块联动 |
| `Mystic/Category` | `Mystic` | 神秘分类 key |
| `Mystic/Multiplier` | `2` | 神秘地块倍率，范围 `1..10` |

字符串、`KeyboardShortcut` 和调试项不应盲目暴露到 ModSettings；通用 UI 当前只支持布尔、枚举、受限列表和数值范围。若要接入，先为每项决定 `Immediate/NextUse/Restart` 语义，并添加合理 `Order/Step`。

### 背包安全和性能约束

- `Plugin.Update` 只做热键检测、状态轮询和 `sorter.Poll()`；不要在其中新增磁盘读取、插件扫描或全背包反射扫描。
- 增强搜索由 `Task.Run` 执行，但搜索期间不能接触 Unity 对象或 Mirror 状态。
- 最终应用必须在 Unity 主线程；交换和旋转按帧预算执行，不能为了“更快”一次性清空再重建背包。
- 联机每批操作要等服务器确认；超时、会话结束、背包快照变化时应安全取消，不要继续写入旧布局。
- 最终布局必须和原始布局比较；如果真实游戏评分没有变好，应回退到原始布局。
- 不要在未验证 `GridInventory.Permission` 的情况下重写 `inventoryMatrix`、`charms`、`stoneTablets`。
- 配置变化若通过 ModSettings 热重载，应使用事件更新缓存或在下次整理读取；不要在每帧读 `ConfigEntry.Value` 来替代设计。

## ModSettings 与其他 Mod 的整合计划

这是当前开发主线之一，当前源码还没有完成整合：

1. 给弩 `AutoReloadMode` 添加完整 ModSettings 元数据；保留无 ModSettings 时的独立 UI fallback，避免用户失去配置入口。
2. 给背包中真正适合游戏内调整的 bool/enum/range 配置添加元数据。字符串 key、热键和超长列表继续通过 `.cfg` 修改，除非先扩展控件协议。
3. 网络权威配置加 `ModSettings:HostOnly`，但 UI 不能伪造客户端权限。
4. 算法/应用类配置优先标 `NextUse`；弩三档是 `Immediate`。需要启动时才安全读取的补丁/协议才标 `Restart`。
5. 对范围配置设置 `ModSettings:Step`，防止一次创建上百个选择项。
6. 接入后分别测试“安装 ModSettings”和“不安装 ModSettings”两条路径，防止硬依赖、重复 UI、重复事件监听。
7. 在 ModSettings UI 通过用户实测确认稳定前，不创建 GitHub 仓库、不发布 release。

## 逆向分析和游戏 API 速查

完整笔记：

```text
SephiriaBackpackOrganizer-fork/analysis/REVERSE-ENGINEERING-NOTES.md
_compare-upstream-backpack-2.4.9/analysis/REVERSE-ENGINEERING-NOTES.md
```

常用类和成员：

| 类型/成员 | 用途 |
|---|---|
| `ItemEntity` | 物品定义：id、类型、稀有度、分类、LocalizedString 等 |
| `ItemDatabase.FindItemById` | 通过 entity ID 查物品定义 |
| `NewItemOwnInstance` | 背包中的物品实例，含 InstanceID 和坐标 |
| `GridInventory` | 背包容器、格子、等级矩阵和 Mirror 权限 |
| `GridInventory.Permission` | 安全刷新石板/护符/套装效果的权限周期 |
| `Charm_Basic.DisplayedLevel` | 当前格位显示等级 |
| `StoneTablet.ParseQuery` | 离线展开石板效果和条件 |
| `EvaluateCurrentAutoArrangeScore` | 游戏权威背包评分 |
| `NetworkClient.localPlayer` | 本地玩家入口 |
| `PlayerAvatar.Inventory` | 本地玩家主背包 |
| `UI_OptionsPanel` / `UI_Tab` | 原版设置页和 Tab |
| `UI_HorizontalSelectionBox` | 原版左右选择控件 |
| `LocalizationManager` | 游戏语言和 Mod 文本注册 |
| `WeaponSimple_Crossbow.autoReloatTimer` | 弩脱战自动上弹计时器字段（拼写按游戏程序集保留） |

输入、网络、UI 和游戏对象调用都默认视为主线程约束；只有明确确认是纯数据快照的部分才可放入后台线程。

## 测试和问题报告流程

### 不启动游戏的静态验证

适合 agent 自动执行：

- 构建相关 `.csproj`，确认无编译错误。
- `rg -n` 检查 GUID、版本、配置键、Harmony patch 目标和 ModSettings 标签。
- 反射读取生成 DLL 的 Assembly/File version。
- 只读检查游戏目录目标 DLL、cfg、插件目录和日志时间戳。
- 检查输出目录没有复制 `Assembly-CSharp.dll`、BepInEx 或 Unity 大程序集。

### 用户进游戏后的验证

#### 许愿泉

- [ ] 普通神器原版流程不变。
- [ ] 已解锁羁绊神器出现在候选，未解锁的不出现。
- [ ] 容量不足时羁绊神器不出现。
- [ ] 羁绊过滤按钮本地化、可与收藏/分类过滤组合。
- [ ] 9 点费用显示为上 3 下 6；修改成本后界面、预设和实际发放一致。
- [ ] 房主安装与未安装时分别验证联机结果。

#### ModSettings

按本文件“ModSettings 验收清单”执行，特别记录高亮实际位置、顶部 Tab 是否偏移，以及设置窗口重开后的重复问题。

#### 弩

- [ ] `Original` 脱战 1.5 秒自动上弹。
- [ ] `Delayed` 脱战 8 秒自动上弹。
- [ ] `Disabled` 不触发脱战自动上弹。
- [ ] 手动换弹、弹匣打空换弹仍然工作。
- [ ] 设置修改后立即影响后续判定。
- [ ] 联机由房主设置控制。

#### 背包

- [ ] F8 在背包未初始化时只提示等待，不丢物。
- [ ] Vanilla/Enhanced 都能运行；不同容量、石板旋转、负等级和受限护符均验证。
- [ ] 搜索完成后最终布局没有丢物、复制物品或越界。
- [ ] 联机客户端能安全执行交换/旋转，超时/退会话会取消。
- [ ] 中键提权、P1 标记、会话切换清理正常。
- [ ] 自检仅主机可用，且日志能给出整理前后评分。
- [ ] 开启 `VerboseDiagnostics` 时日志可识别物品 key；关闭时不产生不必要开销。

### 日志和报告必须带上

发生问题时收集：

- 游戏显示版本、BepInEx 版本、运行后实际加载的插件版本。
- `BepInEx/LogOutput.log`，尤其是插件加载、Harmony warning/error、设置页诊断和背包取消原因。
- 相关 `.cfg` 中实际生效的配置（注意不要只看源码默认值）。
- 目标 DLL 的文件时间和 Assembly/File version。
- UI 问题要带运行时日志；截图只能说明现象，不能替代 `tabButtons`、Image 引用和 RectTransform 诊断。

## GitHub、发布和许可证边界

- GitHub 用户记录为 `MKfeel`。
- 计划仓库：`SephiriaModSettings`、`SephiriaCrossbowAutoReload`。
- 之前记录“尚未完成仓库创建/上传”；本工作区没有 `gh` 命令，不能因此自行改用其他发布流程。
- `backpack-organizer-performance/` 和背包 fork/reference 禁止上传。
- UI 稳定前不要发布任何 release；版本号必须在源码、csproj、DLL 和 README 状态一致后再更新。
- ModSettings 和弩子目录各有 MIT LICENSE；发布时按目标仓库保留 LICENSE。根项目是否发布、根 README 如何拆分需用户明确决定。
- 不要把游戏 DLL、用户配置、日志、备份 DLL 或 `.tools/unitypy` 打进发布包。

## 接手后的推荐顺序

1. 运行 `git status --short --branch`，确认当前工作区没有新的用户改动。
2. 根据任务先读对应源文件；如果任务是 ModSettings UI，额外读 `HANDOFF_MODSETTINGS_UI.md` 和逆向脚本。
3. 记录源码版本、目标 DLL 版本和日志时间，先解决“源码/部署不一致”问题。
4. 若继续 ModSettings UI，先做只增加日志的运行时诊断，定位 `UI_TabButton.butttonImage`、按钮数组顺序和 RectTransform；不要先改布局。
5. 若接入 ModSettings，先给弩和背包设计可见配置清单，再加标签和 soft dependency，最后处理重复 UI/fallback。
6. 每次改动只构建相关项目；不启动游戏。把 DLL 和日志读取准备好后交给用户验证。
7. 根据用户反馈迭代，确认所有验收项通过后，才更新版本、准备发布或进行 Git 操作。

## 相关文件索引

```text
README.md                                      本总交接文档
HANDOFF_MODSETTINGS_UI.md                     ModSettings UI 历史状态、失败方案和下一步
Plugin.cs                                      羁绊神器许愿泉入口
WishingFountainPatches.cs                      许愿候选、成本和服务器发放补丁
WishCost.cs                                    羁绊成本与费用点显示
BondFilterPatches.cs                            羁绊过滤按钮
mod-settings/Plugin.cs                         ModSettings 扫描、协议和 UI
mod-settings/README.md                         ModSettings 接入说明
crossbow-auto-reload/Plugin.cs                 弩配置、UI fallback 和阈值 transpiler
crossbow-auto-reload/README.md                 弩用户说明
backpack-organizer-performance/Plugin.cs       背包入口、配置、输入、会话状态
backpack-organizer-performance/InventorySorter.cs 快照、离线评分、搜索和安全应用
backpack-organizer-performance/ManualPriority.cs 中键提权和 P1/P2 标记
backpack-organizer-performance/com.sephiria.backpack-organizer.cfg 源码对应的配置样例
SephiriaBackpackOrganizer-fork/analysis/REVERSE-ENGINEERING-NOTES.md 逆向/API/机制笔记
_compare-upstream-backpack-2.4.9/              上游背包项目比较快照
.tools/                                        UnityPy 与资源分析脚本
```

当源码、旧 handoff、部署 DLL、cfg 或日志互相矛盾时，把矛盾本身记录下来，再以“当前源码 + 实际运行时日志/程序集”为依据推进，不要用猜测覆盖状态。
