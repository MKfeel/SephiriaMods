# 1.1.0 整合安装记录

2026-09-12 13:28（Asia/Hong_Kong）安装完成。

- 安装 DLL：`E:\steam\steamapps\common\Sephiria\BepInEx\plugins\SephiriaItemLabTweaks\SephiriaItemLabTweaks.dll`。
- SHA-256：`E03170109E89DD4A3B32B990701AB4487F05414A2AC3EAAC81F2CBF04E1ABF1A`，与 Release 产物一致。
- 已删除游戏内 `SephiriaTalentCustomizer`、`SephiriaTalentSPCompat` 两个插件目录及旧天赋配置。
- 新配置：`E:\steam\steamapps\common\Sephiria\BepInEx\config\com.codex.sephiria.itemlab-tweaks.cfg`；保留 `ExtraPoints = 90`、`ResetOnNextLoad = false`。
- SPMod 仍在 `AddOns_Disabled/SPMod`；37 个无关文件的 SHA-256 与整合前清单一致。

Release 编译 0 警告、0 错误；15 项隔离点数逻辑测试通过；实际 DLL 的依赖、旧效果移除及 SPMod 补丁目标通过静态检查；24 个原有功能方法 IL 未变。使用独立临时目录演练过安装迁移及整套回退，90 点、待处理重置标记和回退文件哈希均符合预期，临时样本已清理。

未启动或操作游戏。1.1.0 的实际加载、UI、Mirror 点数更新和 SPMod Harmony 执行仍待用户验收。旧天赋效果修改已不在整合 DLL 内；重启后不再由已删除的 Talent Customizer 应用。

在工作区根目录、游戏退出后执行整套回退：

```powershell
.\item-lab-tweaks\Install.ps1 -Rollback -BackupDirectory '.\item-lab-tweaks\backups\migration-20260912-132809-539'
```

回退恢复扩展 1.0.4、Talent Customizer 1.0.2、独立 SPMod 兼容 1.0.0 和旧配置，并移除安装前不存在的新配置。安装脚本的操作清单及备份 SHA-256 位于该目录的 `migration-manifest.json`。备份不包含游戏存档；安装本身未修改存档。

安装后如需重新比较旧功能的 IL：

```powershell
pwsh -NoProfile -File .\item-lab-tweaks\Test-Integration.ps1 -PreviousDll '.\item-lab-tweaks\backups\migration-20260912-132809-539\BepInEx\plugins\SephiriaItemLabTweaks\SephiriaItemLabTweaks.dll'
```
