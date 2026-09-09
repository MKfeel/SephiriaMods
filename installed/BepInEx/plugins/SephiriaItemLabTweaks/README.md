# Sephiria Item Lab Tweaks

Version 1.0.2

Requires Sephiria Cheat Lab / Item Lab 1.0.1 and BepInEx 5.

Adds two controls to Item Lab's Character debug page:

- Max-enchant every enchantable artifact in the main backpack. Press again to restore the exact original enchant levels captured by the first press.
- Set the currently displayed Sapphire total and save the corresponding persistent value.

State-changing actions are restricted to a single-player game or local host with at most one connection. The enchant snapshot is kept in memory only and is cleared when the plugin unloads or the game session changes.

## 简体中文

需要 Sephiria Cheat Lab / Item Lab 1.0.1 和 BepInEx 5。

在 Item Lab 的“角色”调试页中添加：

- 将主背包内全部可附魔神器提升到满级；再次点击时精确恢复首次点击前的附魔等级。
- 直接设置当前显示的蓝宝石总数，并保存对应的持久蓝宝石数据。

所有写操作只允许在单人游戏或连接数不超过一个的本地主机中执行。附魔还原快照只保存在内存中；插件卸载或游戏会话变化后会清除。
