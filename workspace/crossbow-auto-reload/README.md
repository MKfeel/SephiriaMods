# Sephiria Crossbow Auto Reload

为《赛菲莉娅》的弩增加可配置的脱战自动上弹模式。

## 模式

- **原版（1.5秒）**：保持游戏原本行为。
- **延长（8秒）**：脱离战斗 8 秒后才自动上弹。
- **关闭自动上弹**：不再因脱离战斗自动上弹。

手动换弹、弹匣打空后的换弹、换弹动画和弹药强化机制不受影响。

## 安装

需要 BepInEx 5。将 `SephiriaCrossbowAutoReload.dll` 放入：

```text
Sephiria/BepInEx/plugins/SephiriaCrossbowAutoReload/
```

进入游戏后可在：

```text
设置 → 游戏性 → 弩自动上弹
```

页面最底部直接切换三种模式。也可以编辑：

```text
BepInEx/config/com.sephiria.crossbow-auto-reload.cfg
```

## 热重载与性能

切换模式立即生效。配置变化时，Mod把模式转换成一个缓存的时间阈值；弩每帧只读取一个普通静态 `float`，不会扫描配置文件或反复访问 `ConfigEntry`。

## 联机说明

弩换弹由房主/服务器判定，因此多人联机时由房主的设置统一控制。
