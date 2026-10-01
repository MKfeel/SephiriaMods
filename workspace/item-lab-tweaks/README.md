# Item Lab 面板扩展 1.1.0

2026-09-12：额外天赋点与 SPMod 天赋兼容逻辑合并到本扩展。安装时移除 Talent Customizer、独立 SPMod 天赋兼容插件及旧天赋配置，迁移已有额外点数和待处理重置标记。

## 功能与入口

Item Lab → 调试 → 角色 → 背包与资源：额外天赋点输入、保存、重置。范围 0～9999，保存的是额外总量，重复保存不叠加。降低上限后若已有加点超限，调用游戏原生重置；点击重置将额外点数归零并清除现有加点。写操作继续遵守 Item Lab 的单人/本地主机限制。

保留神器满附魔/还原、蓝宝石设置及角色页分组顺序。移除对 Talent Customizer 的依赖和旧设置页入口；不再修改天赋效果，包括原插件将 20 点天赋背包减格从 -12 改成 -6 的补丁。完全重启后该效果由游戏及其他仍启用的模组决定。

新配置：`BepInEx/config/com.codex.sephiria.itemlab-tweaks.cfg`，使用 `Talents/ExtraPoints`。首次整合安装会将旧天赋配置完整复制到此处（首次加载前文件头可能仍是旧名称）；若新配置已存在，以新配置为准。面板读取并保存扩展自身配置，不需要任何独立天赋插件。

## SPMod 兼容

SPMod 可选，无需启用它才能使用额外点数。适配本机 SPMod 2.4.3：在唯一的天赋上限写入点之前，将 SPMod 原上限与本扩展额外点数相加，使其后续超额检查使用合并后的上限；同步额外点数记账，避免重复保存累加。保持本地服务器归属检查。AddOns 加载后尝试安装补丁，另有每秒一次的发现检查，成功或明确失败后停止扫描。

SPMod 自身因服装/难度奖励变化触发的重置仍保留。兼容不等于通用热卸载，必须完全退出重启。本次安装不改变 SPMod 的启用状态。

## 构建、测试和安装

在工作区根目录运行：

```powershell
$env:APPDATA = Join-Path (Get-Location) '.nuget-appdata'
dotnet build .\item-lab-tweaks\SephiriaItemLabTweaks.csproj -c Release
pwsh -NoProfile -File .\item-lab-tweaks\Test-Talents.ps1
pwsh -NoProfile -File .\item-lab-tweaks\Test-Integration.ps1
.\item-lab-tweaks\Install.ps1
```

唯一必需的模组依赖是 Item Lab 主插件（本机 1.0.1），运行框架为 BepInEx 5。安装前必须退出游戏。

`Install.ps1` 默认转交 `Install-Consolidated.ps1`：备份扩展 DLL、安装说明、两个旧天赋插件和新旧配置，验证 SHA-256；迁移配置后安装扩展，删除两个旧插件 DLL 及旧天赋配置，再清理对应空目录。发生写入错误会按备份恢复。安装备份不包含游戏存档，因为脚本不改存档。

按安装输出中的目录整套回退：

```powershell
.\item-lab-tweaks\Install.ps1 -Rollback -BackupDirectory '.\item-lab-tweaks\backups\migration-<时间戳>'
```

整套回退会恢复原扩展、Talent Customizer、独立 SPMod 兼容插件及原配置，并删除安装前不存在的新配置。回退操作自身也会备份当前文件。旧 1.0.3/1.0.4 的单 DLL 备份仍支持原安装脚本的旧回退路径，但整合安装应使用 migration 备份进行整套回退。

## 验证范围

- Release 构建通过，0 警告、0 错误。
- 15 项隔离逻辑测试通过：反复应用/保存、增减点数、超额重置、显式重置、无效输入、会话限制、SPMod 上限及奖励变化、远端角色不受影响、新角色载入和待处理重置。测试使用主机与配置替身，不代表 Unity/Mirror 实机执行。
- 实际 DLL 静态检查通过：仅依赖 Item Lab，无旧天赋效果修改引用；原有附魔、蓝宝石、游戏访问和反射辅助共 24 个方法的 IL 与 1.0.4 一致。
- 实际 SPMod DLL 中的唯一上限 setter 位于超额比较 getter 之前，符合移植补丁的目标条件。未完成实际 Harmony 执行及游戏内验证。

用户重启后验收：Item Lab 内显示迁移后的点数；保存相同值两次不叠加；重新进入角色/读档保留上限；降低点数及重置正确；角色页不重复添加行；原附魔/还原及蓝宝石功能可用。若启用 SPMod，再验证上限为 SPMod 原上限加额外点数，并确认不因旧上限清空加点。
