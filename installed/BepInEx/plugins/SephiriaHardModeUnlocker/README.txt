SEPHIRIA HARD MODE REWARD UNLOCKER 1.0.0
========================================

Clearing hard-mode difficulty N unlocks every hard-mode reward tier at or
below N. Existing saves also receive any missing tiers up to their recorded
highest clear.

通关困难模式难度 N 后，直接解锁要求难度不高于 N 的全部困难模式奖励。
已有存档也会根据记录中的最高通关难度自动补齐遗漏奖励。

REQUIREMENTS / 运行要求
- Sephiria for Windows. Tested with game version 1.0.25.
  Windows PC 版《赛菲莉娅》，已在游戏 1.0.25 测试。
- BepInEx 5 x64. BepInEx is not included in this archive.
  需要 BepInEx 5 x64，本压缩包不包含 BepInEx。

INSTALL / 安装
Extract the archive into the Sephiria game directory. Confirm this file exists:
将压缩包解压到《赛菲莉娅》游戏目录，并确认存在：

BepInEx\plugins\SephiriaHardModeUnlocker\SephiriaHardModeUnlocker.dll

HOW IT WORKS / 工作方式
- Rewards are granted automatically after a qualifying hard-mode clear.
  符合条件的困难模式通关结算后会自动补齐奖励。
- Existing progress is checked when local player data loads.
  本地玩家数据载入时会自动检查已有最高通关记录。
- The mod uses Sephiria's native reward and multiplayer command path.
  模组使用游戏原生奖励及联机命令流程。
- There is no UI or configuration.
  无需界面或配置。

MULTIPLAYER / 联机
Each player who wants their own local reward progression updated should install
the mod. It does not modify another player's local save.

希望更新自身本地奖励进度的玩家均应安装本模组；模组不会修改其他玩家的
本地存档。

UNINSTALL / 卸载
Delete BepInEx\plugins\SephiriaHardModeUnlocker. Rewards already unlocked are
not removed.

删除 BepInEx\plugins\SephiriaHardModeUnlocker。已经解锁的奖励不会回退。

