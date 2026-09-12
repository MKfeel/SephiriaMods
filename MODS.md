# 当前 Mod 清单

核对日期：2026-09-12。依据本机游戏插件元数据、安装路径和配置整理。

当前共 **9 个模组／管理工具：8 个 BepInEx 插件、1 个 AddOn**。8 个插件在磁盘上允许加载，SPMod 已禁用。“启用”表示下次启动允许加载，不代表已完成游戏内验收。

## BepInEx 插件

以下安装路径相对游戏的 `BepInEx/plugins/`。

| 模组 | 版本 | 状态 | 当前功能与入口 | 安装路径 |
| --- | --- | --- | --- | --- |
| 羁绊神器许愿泉 | 0.2.0 | 启用 | 许愿泉加入已解锁羁绊神器；费用为 9 | `SephiriaBondArtifactWishes/SephiriaBondArtifactWishes.dll` |
| 背包整理 | 2.5.3 | 启用 | F8；Enhanced 模式；手动优先级、方向绑定开启 | `SephiriaBackpackOrganizer/SephiriaBackpackOrganizer.dll` |
| 弩自动上弹 | 1.1.0 | 启用 | 设置 → 游戏性；当前 Original，原版 1.5 秒 | `SephiriaCrossbowAutoReload/SephiriaCrossbowAutoReload.dll` |
| 困难模式奖励解锁 | 1.0.0 | 启用 | 根据已通关难度补齐不高于该难度的奖励；无配置界面 | `SephiriaHardModeUnlocker/SephiriaHardModeUnlocker.dll` |
| 隐藏房间提示 | 0.6.0 | 启用 | 路线节点提前标记隐藏房；支持沙漠生成器；按原生开墙规则绘制实际墙格边界；系统消息、地图、墙标记、屏幕提示开启，测试模式关闭 | `SephiriaHiddenRoomHints/SephiriaHiddenRoomHints.dll` |
| Item Lab（Cheat Lab） | 1.0.1 | 启用 | O 打开原生物品箱及调试工具；写操作限单人／本地主机条件 | `SephiriaItemLab/SephiriaItemLab.dll` |
| Item Lab 面板扩展 | 1.1.0 | 启用 | 神器满附魔／还原、蓝宝石、角色页额外天赋点；当前额外 90 点；内置 SPMod 天赋兼容 | `SephiriaItemLabTweaks/SephiriaItemLabTweaks.dll` |
| 统一模组加载面板 | 0.2.0 | 启用 | F1；管理下次启动的加载状态；切换后需完全重启游戏 | `SephiriaUnifiedModPanel/SephiriaUnifiedModPanel.dll` |

## AddOn

| 模组 | 版本 | 状态 | 当前安装路径 |
| --- | --- | --- | --- |
| SPMod（Star's SephiriaMod Mod） | 2.4.3 | **禁用** | 游戏目录下 `AddOns_Disabled/SPMod/` |

ModSettings 1.0.11 为本地未安装开发项目，不计入当前安装数量。Harmony、Mono.Cecil 等依赖库及旧版本 DLL 备份不作为独立模组计数。

## 最近变更

- 隐藏房间提示升级至 **0.6.0**：沙漠实际使用 `LibraryFloorGenerator`，已适配其通道数据；按通道起终点、宽度及原生开墙规则计算实际清除的墙格，绘制边界，删除碰撞体范围框。下一层预告读取 `FloorData.hiddenRoomCount`。构建通过，36 项模拟检查通过，安装 DLL 哈希一致；未执行游戏内视觉验收。
- Item Lab 扩展升级至 **1.1.0**，整合额外天赋点与 SPMod 天赋兼容；旧 Talent Customizer 和独立天赋兼容插件已从游戏移除。
- 旧 AddOns ModManager 和 BepInEx Configuration Manager 已从游戏移除，保留统一模组加载面板。
- SPMod 当前处于禁用目录。

## 清单与仓库备份的区别

**本次更新上传的是当前清单，仓库 `installed/`、`workspace/` 和 `manifest.json` 仍是此前的备份快照。** 其中可能保留已移除的插件、旧版本 DLL，以及旧的 SPMod 启用路径，不能把这些文件视为与本表一致的最新安装包。

本表版本采用 BepInPlugin／AddOn 元数据，不能仅凭程序集文件版本判断。游戏内显示、输入和联机行为由使用者验证。
