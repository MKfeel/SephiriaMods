# 赛菲莉娅 Mod 仓库

## Windows 模组管理器

已于 2026-09-13 接入管理器。默认配置为 `MKfeel/SephiriaMods` / `main` / `catalog/mods.json`。

更新页可查看远端版本、单项安装或覆盖；“一键覆盖本地”支持同版本修复，保留配置和启用状态。首批发布 9 个当前模组，包含隐藏房间提示 0.6.0、Item Lab 面板扩展 1.1.0；不会装回旧管理器、Talent Customizer 或独立 SPMod 天赋补丁。

[发布协议与后续维护](catalog/README.md) · [更新索引](catalog/mods.json)

下面保留历史备份说明。日常更新请使用上方管理器发布目录，历史 installed/workspace 不代表当前版本。
## 历史备份

这是 MKfeel 的跨电脑 Mod 备份仓库，包含本地开发资料与游戏模组备份。2026-09-09 创建时为私有仓库；2026-09-12 核对时仓库已公开。

**当前安装状态请看 [Mod 清单（2026-09-12）](MODS.md)**：8 个 BepInEx 插件及已禁用的 SPMod，包含隐藏房间提示 0.6.0 和 Item Lab 扩展 1.1.0。本次仅更新清单和说明；下列 `installed`、`workspace` 与 `manifest.json` 仍为历史备份，版本和插件数量可能与当前清单不同。

## 内容

- `installed/BepInEx/plugins`：旧备份中的 11 个插件 DLL，以及说明、许可证和旧版本备份。
- `installed/BepInEx/config`：旧备份中的 BepInEx 和模组配置。
- `installed/AddOns`：ModManager、SPMod 及所需依赖和配置。
- `workspace`：羁绊神器许愿泉、模组设置页、弩自动装填、天赋兼容、统一模组面板、背包整理性能版与 fork，以及上游对照资料；保留已有模组编译产物和许可证。
- `manifest.json`：每个备份文件的相对路径、来源、大小和 SHA-256。

没有保存游戏本体、游戏存档、Git 历史、工具缓存和编译中间文件。部分已安装模组在此工作区没有源码，仅备份现有文件。`workspace` 内的历史交接说明可能过时；源码 DLL 与 `installed` 中的版本也可能不同。

## 在另一台电脑使用

先安装游戏及适配的 BepInEx 5，然后获取仓库：

```powershell
gh repo clone MKfeel/SephiriaMods
cd SephiriaMods
```

退出游戏，先备份目标电脑原有 `BepInEx/plugins`、`BepInEx/config` 和 `AddOns` 目录，再将仓库 `installed` 内的同名目录合并复制到游戏根目录。确认目标电脑没有在其他子目录安装同一插件的不同版本。旧 DLL 备份保留原文件后缀，不要改成 `.dll`。需要回滚时恢复复制前的备份，并移走此次新增的插件。

初次恢复优先使用 `installed`，开发产物按需单独替换。不要把 `workspace` 整个复制到游戏插件目录。游戏内验证由使用者完成，本次备份只验证文件一致性，不代表所有模组运行正常。

## 后续同步

先 `git pull --ff-only`，再导出指定电脑上的开发目录和游戏模组目录：

```powershell
powershell -ExecutionPolicy Bypass -File .\Export-LocalMods.ps1 -WorkspaceDirectory 'D:\开发\赛菲莉娅mod开发' -GameDirectory 'D:\SteamLibrary\steamapps\common\Sephiria'
git add -f workspace installed manifest.json
git diff --cached --stat
git commit -m "Update local mod backup"
git push
```

导出脚本不会自动删除来源中已移除的旧文件；需要停止同步或删除某模组时，检查仓库差异后手动移除对应备份。导出不会自动提交或上传。

继续开发时阅读 `workspace/README.md` 及各项目说明。原项目中部分引用路径绑定本机游戏目录，需要按目标电脑调整或传入 `-p:GameDirectory=...`；根项目曾有递归收集其他项目源码的问题，备份没有改写构建逻辑，也没有重新构建。
