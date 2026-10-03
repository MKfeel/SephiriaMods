# 3.0.5 源码与发布文件

`*.cs` 与 `SephiriaBackpackOrganizer.Own.csproj` 是 F8 原生桥接和整理器源码；`model/` 包含本版使用的模型、数据、worker 及 Node.js 22.20.0。相邻 `../backpack-simulator/` 保存与 Mod 一致的模型源文件，供 `Build-Package.ps1` 使用。

编译需要 Windows、.NET SDK、.NET Framework 4.7.1 引用程序集，以及本机 Sephiria / BepInEx 5。项目中的 `HintPath` 默认指向 `E:\steam\steamapps\common\Sephiria`，其他位置需相应修改。游戏本体和引用 DLL 由本机游戏安装提供。

```powershell
dotnet build .\SephiriaBackpackOrganizer.Own.csproj -c Release -p:IntermediateOutputPath=obj5\Release\
New-Item -ItemType Directory -Path build-runtime -Force | Out-Null
Copy-Item .\model\runtime\node.exe .\build-runtime\node.exe
Copy-Item .\model\runtime\NODE-LICENSE.txt .\build-runtime\LICENSE
& .\Build-Package.ps1 -NodePath (Join-Path $PWD 'build-runtime\node.exe')
dotnet run --project .\tests\RulesTests.csproj -c Release
& .\model\runtime\node.exe ..\backpack-simulator\tests\runtime.test.cjs
```

以上命令复用随包 Node.js，并把打包输入放在单独的 `build-runtime/`，打包完成后可删除该目录。生成的独立安装包位于 `dist/`。管理器标准包使用仓库根目录 `packages/com.sephiria.backpack-organizer/`，可以直接导入管理器；手动安装则把其中 `BepInEx/` 合并至游戏根目录，保留完整 `model/`。

`tests/validation-corpus-3.0.5.json` 记录 12 份背包三轮对照；`validation-package-3.0.5.json` 记录完整 worker 回放，`validation-install-3.0.5.json` 记录隔离安装回退。原始游戏快照及历史对照包保留在开发工作区，汇总中的相对引擎路径说明当时的对照版本。构建、静态检查和离线回放不代表 3.0.5 已通过游戏内验收。
