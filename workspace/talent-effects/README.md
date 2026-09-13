# 天赋效果修改 0.2.1

## 效果

- 生存20点：每10点最大生命值提供1%伤害放大，不足10点部分不计。替换原背包减少12格及石板刻印效果。
- 智慧10点：击败迷你Boss时获得1个骰子，击败Boss时获得2个骰子。移除原先神器强制独特、合并升级等 UNIQUE_PAIR 效果；描述完整替换，不再追加到旧描述末尾。

## 实现与验证

生存监听最大生命值的4个原生setter，属性变化后立即更新差额，无定时扫描；激活与洗点按原生天赋生命周期处理。

智慧10点预制体的属性列表替换为 BOSS_REWARD_DICE/2，继续使用原生属性启用和撤销。Boss由原生奖励流程发2个骰子；迷你Boss死亡后，为每位具有BOSSREWARDDICE属性的在线玩家生成1个专属骰子，不要求最后一击。同一怪物实例防重复发放。

描述覆盖普通和UniquePair派生元数据两个入口，统一使用“迷你Boss”。本次修复不会改变其他天赋或物品赋予的独特属性。

Release构建、25项实际源码隔离检查通过。隔离检查不等同于Unity/Harmony或联机运行验收。用户需完整退出并重启游戏验证；运行中的旧实例不能热切换。

```powershell
dotnet build talent-effects/SephiriaTalentEffects.csproj -c Release
dotnet run --project talent-effects/tests/Regression.csproj -c Release
./talent-effects/Verify-Game.ps1
```

DLL：bin/Release/net471/SephiriaTalentEffects.dll。
安装目录：E:\steam\steamapps\common\Sephiria\BepInEx\plugins\SephiriaTalentEffects\。
回滚需退出游戏后恢复部署备份中的DLL。卸载需退出游戏后移走插件DLL；不修改游戏程序集或存档。
