# Sephiria Mod Settings

为《赛菲莉娅》的 BepInEx 5 Mod 提供统一的游戏内配置页面。

安装后，原版设置窗口会增加一个 **“模组”** Tab。每个已接入的 Mod 使用独立标题分隔，配置修改后直接写回原 Mod 的 `ConfigEntry`。

## 功能

- 复用原版设置页面、Tab按钮和左右选择控件。
- 按 Mod 分组，每个 Mod 显示独立标题。
- 支持 `bool`、`enum`、`AcceptableValueList<T>` 以及带范围的 `int/float/double`。
- 只显示 Mod 明确标记的配置，不会把内部调试和算法参数全部暴露出来。
- 修改 `ConfigEntryBase.BoxedValue`，正常触发 BepInEx `SettingChanged`。
- 遵守原 ConfigFile 的 `SaveOnConfigSet`，修改时自动保存原 `.cfg`。
- 不需要目标 Mod 对 ModSettings DLL 建立硬依赖。

## 安装

需要 BepInEx 5。将 `SephiriaModSettings.dll` 放入：

```text
Sephiria/BepInEx/plugins/SephiriaModSettings/
```

启动游戏后打开：

```text
设置 → 模组
```

## 性能与热重载

ModSettings不会每帧扫描插件，也不会轮询或反复解析配置文件：

1. BepInEx正常完成全部插件加载。
2. 玩家首次打开设置窗口时，ModSettings扫描一次带元数据标记的 `ConfigEntry` 并创建UI。
3. 玩家改变选项时，仅修改对应 `ConfigEntry`。
4. 目标 Mod通过 BepInEx `SettingChanged` 事件更新自己的缓存或执行一次重载。

推荐目标 Mod不要在每帧执行磁盘读取、插件扫描或UI重建。对于高频代码，应把配置缓存为普通字段：

```csharp
private static ConfigEntry<MyMode> modeEntry;
private static MyMode cachedMode;

private void Awake()
{
    modeEntry = Config.Bind(/* ... */);
    cachedMode = modeEntry.Value;
    modeEntry.SettingChanged += (_, __) => cachedMode = modeEntry.Value;
}
```

这样热重载只在配置变化时工作，高频逻辑只读取普通字段。

## Mod接入方式

ModSettings使用 `ConfigDescription.Tags` 中的字符串元数据。目标 Mod无需引用 `SephiriaModSettings.dll`，未安装 ModSettings 时这些字符串也不会影响正常运行。

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

建议为 ModSettings 添加软依赖，确保加载顺序稳定，但不会强制用户安装：

```csharp
[BepInDependency(
    "com.sephiria.modsettings",
    BepInDependency.DependencyFlags.SoftDependency)]
```

### 支持的标签

| 标签 | 说明 |
|---|---|
| `ModSettings:Show` | 必需。允许该配置显示在模组页面 |
| `ModSettings:ModTitle=背包整理` | Mod分组标题；未提供时使用BepInEx插件名称 |
| `ModSettings:Name=整理模式` | 设置项显示名称；未提供时使用Config Key |
| `ModSettings:Order=10` | Mod内部排序，数字越小越靠前 |
| `ModSettings:Reload=Immediate` | 标记立即生效；也可写 `NextUse` 或 `Restart` |
| `ModSettings:HostOnly` | 标记为房主权威设置，界面名称后显示“（房主）” |
| `ModSettings:Step=50` | 数值范围在UI中的单次调整步长 |
| `ModSettings:Option:EnumValue=显示文本` | 为枚举或列表中的某个值指定文本 |

### 三档枚举示例

```csharp
public enum AutoReloadMode
{
    Original,
    Delayed,
    Disabled
}

ConfigEntry<AutoReloadMode> mode = Config.Bind(
    "Gameplay",
    "AutoReloadMode",
    AutoReloadMode.Original,
    new ConfigDescription(
        "自动上弹模式。",
        null,
        "ModSettings:Show",
        "ModSettings:ModTitle=弩自动上弹",
        "ModSettings:Name=自动上弹模式",
        "ModSettings:Order=10",
        "ModSettings:Reload=Immediate",
        "ModSettings:Option:Original=原版（1.5秒）",
        "ModSettings:Option:Delayed=延长（8秒）",
        "ModSettings:Option:Disabled=关闭自动上弹"));
```

## 热重载语义

ModSettings负责修改配置并触发标准事件，但功能是否能立即生效由目标 Mod 决定：

- `Immediate`：目标 Mod监听事件或在下次逻辑更新时读取缓存，立即生效。
- `NextUse`：例如下次按整理键、下次打开UI或下次生成对象时生效。
- `Restart`：仅启动时应用的补丁、资源或协议配置，需要重启游戏。

## 已知范围

- 仅扫描当前进程中已加载的 BepInEx 5 插件。
- 第一版不提供自由文本输入和快捷键录入控件。
- 多人联机中的服务器权威配置仍由房主决定；ModSettings不会绕过游戏网络权限。
