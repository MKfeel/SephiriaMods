# 赛菲莉娅模组加载管理 0.2.0

由 BepInEx 5 加载的独立窗口，默认 F1 打开，F1 / Escape / 关闭按钮收起。只提供 Mod 加载开关，不再提供配置编辑、JSON 编辑或全部热重载按钮。

## 本次改动

0.1.0 取了设置页第一个按钮的 targetGraphic.sprite，强制当作所有按钮和输入框的 Sliced 底板。这个选择可能是高亮图或箭头，导致截图中的白块和长条。0.2.0 移除该取图方式，按钮改用深色填充、蓝灰色像素边框和浅色文字；窗口保留原版底板，复制其材质与颜色，文字保留原字体的共享材质。输入框随配置编辑功能一并移除。

列表同时显示 BepInEx 和 AddOns，逐项区分“当前进程已加载”和“下次启动是否加载”，发生差异时标记待重启。面板自身不能在面板里禁用。

BepInEx 禁用时将插件文件从 `.dll` 改名为 `.dll.unified-disabled`；恢复时改回，不改 DLL 内容或配置文件。仅展示带 BepInPlugin 元数据的文件，依赖库不显示；同一 DLL 中的多个插件会作为一个整体切换。禁用前检查硬依赖的使用者，恢复前检查前置 GUID 和重复插件。依赖的版本要求仍由 BepInEx 在启动时校验。

AddOns 在 AddOns 与 AddOns_Disabled 之间移动完整目录，保留配置。目录或文件冲突时拒绝覆盖。所有更改立即保存为下次启动状态，不影响当前进程中已加载的代码。Windows 若不允许移动当前占用的文件，操作会报告失败，不会显示为成功。

## 小退到标题能否生效

本版明确要求完全退出并重新启动游戏，不会在返回标题时调用 UnloadAll/LoadAll。

2026-09-09 对本机 SPMod.dll 的 IL 检查发现：OnModUnloaded 取消部分事件并清空字典，但不撤销 Harmony 补丁；OnModLoaded 受静态 _isPatched 标记控制，后续调用会跳过初始化，并且初始化会修改经验表等共享数据。仅调用官方卸载接口不能保证清除它的影响，重复加载也可能留下不完整状态。

BepInEx 普通插件的补丁和静态状态不会因关闭组件或返回标题自动撤销。要实现可靠的小退热切换，需要先为具体 Mod 补齐并验证“卸载补丁、注销事件、还原数据、重新初始化”的生命周期，再加入对应适配。当前不提供未经验证的通用热卸载。

## 安装与回退

退出游戏后运行：

```powershell
pwsh -NoProfile -File .\Install.ps1
```

安装位置：

```text
E:\steam\steamapps\common\Sephiria\BepInEx\plugins\SephiriaUnifiedModPanel\SephiriaUnifiedModPanel.dll
```

旧面板 DLL 不变；新窗口就绪后屏蔽旧面板入口。退出游戏后回退：

```powershell
pwsh -NoProfile -File .\Install.ps1 -Rollback
```

回退停用统一面板，但不会自动撤销用户选择的加载状态。如果手动恢复 BepInEx 插件，将其 `.dll.unified-disabled` 后缀改回 `.dll`；如原 DLL 已存在，先自行处理冲突，不要覆盖。AddOns 恢复时把对应目录移回 AddOns。

## 验证

```powershell
$env:APPDATA = Join-Path (Split-Path $PWD) '.nuget-appdata'
dotnet build .\SephiriaUnifiedModPanel.csproj -c Release --nologo -v:minimal
pwsh -NoProfile -File .\Test-AddonFiles.ps1
pwsh -NoProfile -File .\Test-PluginFiles.ps1
```

文件测试只修改工作区内临时样本，检查禁用/恢复、DLL 哈希不变、依赖保护、自身保护、目录边界和冲突；真实游戏插件目录只用于只读元数据扫描。未自动切换任何实际 Mod 的加载状态。

游戏内仍待用户验收：按钮白块是否消失、文字是否可读、列表滚动、F1 开关、关闭后的输入恢复；选一项禁用，确认当前仍显示已加载，完全重启后确认不再加载，再恢复并重启。不会主动启动或控制游戏，编译和文件测试不代表游戏内验收通过。
2026-09-09：Release 编译 0 警告、0 错误，两组文件测试通过；已安装版本 0.2.0.0，源 DLL 与部署文件哈希一致：81F33BB5501E6EBB9B1B3F1F5A18FBBF5D32D74041636D5D1F8F38B8B95FC5C3。旧版由安装脚本备份。
