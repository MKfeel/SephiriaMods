# 当前 Mod 清单

核对日期：2026-09-20。依据本机游戏插件元数据与安装路径重新扫描。

当前共 **12 个模组／管理工具：9 个 BepInEx 插件、3 个 AddOn**。仓库统一按默认启用方式提供，不区分本机启用状态；不代表已完成游戏内验收。

## BepInEx 插件

以下安装路径相对游戏的 `BepInEx/plugins/`。

| 模组 | 版本 | 状态 | 当前功能与入口 | 安装路径 |
| --- | --- | --- | --- | --- |
| 羁绊神器许愿泉 | 0.2.0 | 启用 | 许愿泉加入已解锁羁绊神器；费用为 9 | `SephiriaBondArtifactWishes/SephiriaBondArtifactWishes.dll` |
| 背包整理 | 2.5.4 | 启用 | F8；收藏同步、分层整理；沿用原插件 ID | `SephiriaBackpackOrganizer/SephiriaBackpackOrganizer.dll` |
| 弩自动上弹 | 1.1.0 | 启用 | 设置 → 游戏性；支持原版与自动上弹模式 | `SephiriaCrossbowAutoReload/SephiriaCrossbowAutoReload.dll` |
| 困难模式奖励解锁 | 1.0.0 | 启用 | 根据已通关难度补齐不高于该难度的奖励；无配置界面 | `SephiriaHardModeUnlocker/SephiriaHardModeUnlocker.dll` |
| 隐藏房间提示 | 0.7.1 | 启用 | 路线预告；读取已连接的沙漠传送石与原生破墙入口，场景和完整地图统一黄色感叹号；测试模式关闭 | `SephiriaHiddenRoomHints/SephiriaHiddenRoomHints.dll` |
| Item Lab（Cheat Lab） | 1.0.1 | 启用 | O 打开原生物品箱及调试工具；写操作限单人／本地主机条件 | `SephiriaItemLab/SephiriaItemLab.dll` |
| Item Lab 面板扩展 | 1.1.0 | 启用 | 神器满附魔／还原、蓝宝石、角色页额外天赋点；内置 SPMod 天赋兼容 | `SephiriaItemLabTweaks/SephiriaItemLabTweaks.dll` |
| 天赋效果修改 | 0.2.1 | 启用 | 生存20：每10最大生命值增加1%伤害放大；智慧10：迷你Boss1个骰子、Boss2个，移除旧神器效果 | `SephiriaTalentEffects/SephiriaTalentEffects.dll` |
| 统一模组加载面板 | 0.2.0 | 启用 | F1；管理下次启动的加载状态；切换后需完全重启游戏 | `SephiriaUnifiedModPanel/SephiriaUnifiedModPanel.dll` |

## AddOn

| 模组 | 版本 | 状态 | 默认安装路径 |
| --- | --- | --- | --- |
| 根之进（NegativeRoots） | 0.1.5 | 启用 | 游戏目录下 `AddOns/NegativeRoots/` |
| 锻体（BodyForge） | 2.8.24 | 启用 | 游戏目录下 `AddOns/BodyForge/` |
| SPMod（Star's SephiriaMod Mod） | 2.5.3 | 启用 | 游戏目录下 `AddOns/SPMod/` |

ModSettings 1.0.11 为本地未安装开发项目，不计入当前安装数量。Harmony、Mono.Cecil 等依赖库及旧版本 DLL 备份不作为独立模组计数。

## 最近变更

- 2026-09-20：重新扫描当前安装，共 12 个模组；根之进更新为 0.1.5，SPMod 为 2.5.3（禁用），新增锻体 2.8.24（禁用）。9 个 BepInEx 插件版本及启用状态未变。本次同步清单与首页说明。

- 2026-09-16：重新扫描当前安装，新增根之进 0.1.2，SPMod 为 2.4.8（禁用）；9 个 BepInEx 插件均允许加载。本次更新清单与首页说明。

- 2026-09-14：隐藏房间提示 **0.7.1** 已部署与发布，沙漠传送石和破墙入口统一黄色感叹号。源码、安装 DLL 和管理器包同步更新；保留 0.6.0 历史包。48 项模拟检查通过，游戏内待验收。

- 2026-09-14：背包整理 2.5.4 已部署与发布；统一 ID，F8。源码、标准包及 installed 目录同步更新；2.5.3 历史包保留。

- 天赋效果修改 **0.2.1**：移除智慧旧神器独特合并效果，完整替换描述，统一迷你Boss术语；25项隔离检查及部署哈希核对通过。

- 天赋效果修改 **0.2.0** 已部署，DLL SHA-256 与构建产物一致；23 项隔离检查通过，未启动游戏。源码、安装 DLL 与管理器标准包均已加入仓库。

- 隐藏房间提示升级至 **0.6.0**：沙漠实际使用 `LibraryFloorGenerator`，已适配其通道数据；按通道起终点、宽度及原生开墙规则计算实际清除的墙格，绘制边界，删除碰撞体范围框。下一层预告读取 `FloorData.hiddenRoomCount`。构建通过，36 项模拟检查通过，安装 DLL 哈希一致；未执行游戏内视觉验收。
- Item Lab 扩展升级至 **1.1.0**，整合额外天赋点与 SPMod 天赋兼容；旧 Talent Customizer 和独立天赋兼容插件已从游戏移除。
- 旧 AddOns ModManager 和 BepInEx Configuration Manager 已从游戏移除，保留统一模组加载面板。
- SPMod 当前处于禁用目录。

## 清单与仓库备份的区别

**本表记录当前本机安装状态。日常安装和更新以 catalog/mods.json 为准；installed/workspace 与 manifest.json 按各次发布分别维护，可能保留历史文件，不能视为与本表完全一致的安装快照。**

本表版本采用 BepInPlugin／AddOn 元数据，不能仅凭程序集文件版本判断。游戏内显示、输入和联机行为由使用者验证。
