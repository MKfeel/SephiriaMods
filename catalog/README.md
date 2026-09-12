# 模组管理器发布目录

管理器设置：仓库 `MKfeel/SephiriaMods`，分支 `main`，索引 `catalog/mods.json`。

`mods.json` 为 schemaVersion 1：以插件 GUID / AddOn entryClass 为稳定身份，包含名称、加载器、说明与版本列表。版本含 HTTPS URL、字节数、SHA-256、更新说明及兼容范围。每个 ZIP 只装一个模组，根部 `mod-manifest.json` 描述全部文件、目标与逐文件 SHA-256。

2026-09-13 首批发布取自当前已安装的 9 个模组，已排除用户配置、旧 DLL 备份及废弃组件。SPMod 包包含依赖 DLL 与 metadata.json；客户端覆盖时保留其本地禁用状态。BepInEx 运行时需由使用者自行安装，不包含游戏本体。

## 覆盖语义

- 更新页展示所有远端模组，包括与本地版本一致的条目。
- “一键覆盖本地”处理仓库中与本地身份匹配的模组，允许同版本修复及显式降级；未收录的本地模组不受影响，未安装项需逐项安装。
- 保留已有配置与启用/禁用状态。下载后校验大小和 SHA-256，预览后以一个事务应用并备份，支持最近一次操作回退。
- `installed/`、`workspace/` 和根部 `manifest.json` 是历史备份，不是自动更新源。

## 后续发布

使用管理器源码中的 Publisher 对单个标准压缩包生成安装包与索引，或使用 `--snapshot-game <游戏目录> --output <空输出目录> --base-url https://raw.githubusercontent.com/MKfeel/SephiriaMods/main` 生成经过静态检查的本地快照。快照命令只读取游戏文件，不上传。新包发布前须人工检查文件归属与资源，不应发布私人配置。

将生成的包加入 `packages/`，把新版本条目合并到 `catalog/mods.json`；避免覆盖既有 URL 对应的字节，包路径包含内容哈希。数字稳定版本中最高版本供管理器选择；修复旧构建应提升版本号。

提交前运行 `pwsh ./scripts/Validate-ModCatalog.ps1`。GitHub Actions 自动检查索引身份、大小、安装包与逐文件哈希。客户端另做静态插件身份、版本、依赖与文件归属检查。文件检查不替代游戏内功能验收。
