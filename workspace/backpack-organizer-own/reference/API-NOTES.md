# 本机游戏接口核对（2026-09-14）

使用本机 ILSpyCmd 9.1.0.7988 读取当前游戏 `Assembly-CSharp.dll`，不运行游戏。

- `UI_JournalContent_Item.SetFavorite` 使用 `SaveManager.Current.SetBool("Item_Favorite_" + entityID, !flag)`；`IsItemFavorite` 使用相同 key 的 GetBool，默认 false。
- `SaveData.SetBool(string key, bool value)` 是本版本收藏变化 Harmony Postfix 的目标。
- `GridInventory.OnItemAddedForClient` 类型 `Action<NewItemOwnInstance>`，`OnItemRemovedForClient` 类型 `Action<ItemPosition>`；分别由对应通知 RPC 触发。
- `UI_NewInventoryIcon.OnPointerClick(PointerEventData)` 原生处理右键，中键由本插件 Prefix 接管。
- `UI_NewItemPicker.CurrentPickedUp` 和 `UI_NewItemPicker_Controller.CurrentPickedUp` 可用于检查鼠标或手柄抓取。
- `Charm_IncreaseGoldDropRate.goldDropBonusByLevel` 原生初始数组是 20/30/40/50；启用、停用、升级通过 `ECustomStat.MoneyDrop` 更新对应加成。本插件仍以神器运行时 maxLevel 为满级上限，不写死等级数。

临时游戏反编译文件已删除，不包含在安装包中。插件 2.5.3 的完整反编译证据保留在工作区 `docs/backpack-installed-2.5.3-a175e003/`。
