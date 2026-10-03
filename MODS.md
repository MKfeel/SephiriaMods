# 当前 Mod 清单

核对日期：2026-10-01。依据本机游戏插件与 AddOn 元数据静态扫描。

2026-10-04 增量核对并更新背包整理 **3.0.5** 的源码、完整安装文件和管理器安装包；其他条目仍以 2026-10-01 的扫描为准。

当前安装共 **14 个 Mod：10 个 BepInEx 插件、4 个 AddOn**，本机均处于允许下次启动加载的磁盘状态。仓库安装文件默认启用；静态扫描不代表游戏内验收。

## BepInEx 插件

| 模组 | 版本 | 状态 | 当前功能与入口 | 安装路径 |
| --- | --- | --- | --- | --- |
| 背包整理 | 3.0.5 | 启用 | F8；武器/流派识别、收益整理、共鸣石成长准备、同分无损提升；完整模型与 Node.js 随包提供 | `SephiriaBackpackOrganizer/SephiriaBackpackOrganizer.dll` |
| 羁绊神器许愿泉 | 0.2.0 | 启用 | 许愿泉加入已解锁羁绊神器；费用为 9 | `SephiriaBondArtifactWishes/SephiriaBondArtifactWishes.dll` |
| 弩自动上弹 | 1.1.0 | 启用 | 设置 → 游戏性；支持原版与自动上弹模式 | `SephiriaCrossbowAutoReload/SephiriaCrossbowAutoReload.dll` |
| IP 直连 | 0.4.0（本地维护版） | 启用 | 联机石板 IP 创建／加入；超时、取消、认证与进场判断、旧连接清理 | `SephiriaDirectConnect/SephiriaDirectConnect.dll` |
| 困难模式奖励解锁 | 1.0.0 | 启用 | 根据已通关难度补齐不高于该难度的奖励；无配置界面 | `SephiriaHardModeUnlocker/SephiriaHardModeUnlocker.dll` |
| 隐藏房间提示 | 0.7.3 | 启用 | 路线预告；读取已连接的沙漠传送石与原生破墙入口，场景和完整地图统一黄色感叹号；测试模式关闭 | `SephiriaHiddenRoomHints/SephiriaHiddenRoomHints.dll` |
| 按住特殊攻击 | 1.0.1 | 启用 | 按武器机制连续释放；剑盾双键横扫；放血／重新组装保留手动操作 | `SephiriaHoldSpecial/SephiriaHoldSpecial.dll` |
| Item Lab（Cheat Lab） | 1.0.1 | 启用 | O 打开原生物品箱及调试工具；写操作限单人／本地主机条件 | `SephiriaItemLab/SephiriaItemLab.dll` |
| Item Lab 面板扩展 | 1.1.0 | 启用 | 神器满附魔／还原、蓝宝石、角色页额外天赋点；内置 SPMod 天赋兼容 | `SephiriaItemLabTweaks/SephiriaItemLabTweaks.dll` |
| 天赋效果修改 | 0.2.1 | 启用 | 生存20：每10最大生命值增加1%伤害放大；智慧10：迷你Boss1个骰子、Boss2个，移除旧神器效果 | `SephiriaTalentEffects/SephiriaTalentEffects.dll` |

## AddOn

| 模组 | 版本 | 状态 | 默认安装路径 |
| --- | --- | --- | --- |
| 锻体 | 2.8.37 | 启用 | `AddOns/BodyForge/` |
| BowWeapon | 1.0.0 | 启用 | `AddOns/BowWeapon/` |
| 根之进 | 1.1.0 | 启用 | `AddOns/NegativeRoots/` |
| Star's SephiriaMod Mod | 2.6.1 | 启用 | `AddOns/SPMod/` |

## 本次同步

- 更新锻体 2.8.37、根之进 1.1.0、SPMod 2.6.1，新增 BowWeapon 1.0.0（含依赖与全部 120 张纹理）。
- 同步当前 Mod 的安装文件和本地可用源码；不上传本机连接地址、运行日志或调试转储。没有本地源码的 Mod 保留安装文件。
- 统一模组加载面板已不在本机安装目录；管理器保留其历史发布。因此 catalog/mods.json 共 15 个可安装条目，当前本机清单为 14 个。
- 相同版本、相同文件内容沿用已发布包，并保留包内的说明和许可证；旧版本包继续保留。

## 使用与验证边界

日常安装和更新使用 catalog/mods.json 与 packages/。installed/ 和 workspace/ 保留历史备份，不能将整个 installed/ 视为当前安装快照；按本清单选取目录恢复，避免装回历史组件。

MOD_INVENTORY.json 记录本次 14 个 Mod 的身份、版本和发布文件哈希；manifest.json 记录仓库 installed/workspace 文件哈希。游戏运行、输入与联机行为仍需游戏内验证。
