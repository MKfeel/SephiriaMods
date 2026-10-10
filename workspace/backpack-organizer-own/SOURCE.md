# 3.0.1 模组物品兼容修复源码

本次在现有整理器源码上修改：模组物品不参与自定义战斗收益建模，保留通用布局信息及手动标记。版本号按请求设为 3.0.1。

BuildAwareModel.cs 负责采集和应用时的原生校验；相邻 ../backpack-simulator/ 中的模型负责中性物品定义及标记目标。Build-Package.ps1 将模型同步至 model/ 后生成完整安装包。

编译需要 .NET SDK、.NET Framework 4.7.1 引用程序集，以及本机游戏和 BepInEx 5 的引用 DLL。可用 GameDirectory 指定游戏路径：

    dotnet build .\SephiriaBackpackOrganizer.Own.csproj -c Release -p:GameDirectory='D:\GAMES\Steam\steamapps\common\Sephiria' -p:IntermediateOutputPath=obj5\Release\
    dotnet run --project .\tests\RulesTests.csproj -c Release
    & .\model\runtime\node.exe ..\backpack-simulator\tests\mod-items.test.cjs
    & .\model\runtime\node.exe ..\backpack-simulator\tests\runtime.test.cjs
    & .\Build-Package.ps1

完整包位于 dist/SephiriaBackpackOrganizer-3.0.1.zip，沿用旧版的管理器标准格式：根目录 mod-manifest.json，全部插件文件位于 BepInEx/plugins/SephiriaBackpackOrganizer/。发布 ZIP 位于 packages/com.sephiria.backpack-organizer/，版本索引位于 catalog/mods.json；installed/ 保留历史备份。
