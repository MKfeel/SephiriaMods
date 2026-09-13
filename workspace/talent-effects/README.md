# 天赋效果修改 0.2.0

独立 BepInEx 5 Mod。2026-09-13 已部署 0.2.0，安装文件与构建产物哈希一致。未启动游戏，游戏内效果待验收。

## 效果

- 生存 20 点：每满 10 点最大生命值提供 1% 伤害放大。完整替换原来的背包减少 12 格和石板刻印效果。100 / 250 / 999 最大生命值分别增加 10% / 25% / 99%，沿用原生 AllDamageBonus，与同类伤害放大相加。
- 智慧 10 点：小 Boss 奖励 1 个骰子、大 Boss 奖励 2 个骰子，保留 UNIQUE_PAIR 等其他原有效果。沿用原生按玩家资格发奖的规则，不要求最后一击。

## 实现

生存激活时立即计算，Harmony 挂钩 UnitAvatar 的 NetworkmaxHp、NetworkfinalMaxHp、NetworkisHPCursed、NetworkcursedMaxHp setter，写入后重新读取有效 MaxHp，仅更新差额。无 Update、定时器或对象扫描；洗点和销毁时只移除本天赋贡献。

智慧通过 PassiveDatabase.Initialize 后置补丁，把原生 8 号天赋的 10 点预制体中 BOSS_REWARD_DICE/1 改为 BOSS_REWARD_DICE/2。保留其他属性，原生天赋负责启用、撤销和关键词。大 Boss 的 BossSpawner / SeedBossSpawner 继续按该属性生成奖励，不额外发第二批骰子。

小 Boss 通过 UnitAvatar.Die 的存活到死亡转换触发，弱引用表确保同一怪物实例仅奖励一次。对每位具有 BOSSREWARDDICE 属性的在线玩家，调用原生 PlayerAvatar.RequestCreateDice(1, 1f)，在玩家位置生成专属骰子，沿用原生拾取与网络同步。小 Boss 固定发 1 个，不跟随大 Boss 属性数量变为 2。其他 Mod 若也赋予 BOSSREWARDDICE，同样获得小 Boss 扩展奖励。

生存通过对象名与本地化键双重定位；智慧保留原提示并追加“小 Boss 1 个，大 Boss 2 个”。不改变原生网络预制体的 NetworkBehaviour 结构。

## 构建和验证

在工作区根目录执行：

```powershell
dotnet build talent-effects/SephiriaTalentEffects.csproj -c Release
dotnet run --project talent-effects/tests/Regression.csproj -c Release
./talent-effects/Verify-Game.ps1
```

Release 构建通过，23 项实际插件源码隔离行为检查通过。隔离检查使用模拟 Unity/Mirror 环境，不等同于实际 Harmony 挂钩、游戏运行或联机验收。静态核对原游戏的生命上限 setter、天赋与描述方法、死亡方法、两类 Boss 发奖流程。

当前游戏自身的运行时生命上限字段写入经过上述 4 个 setter。其他 Mod 如果直接写字段或修改 MaxHp getter，需要另行适配；本版未验证与同样修改天赋或生命计算的 Mod 同时运行。

待用户游戏内验收：生存 19→20 点时不扣背包、不授予石板刻印；换装和诅咒变化后伤害放大立即更新；退回 19 点撤销加成；智慧其他原有效果保留，普通怪无奖励、小 Boss 1 个、大 Boss 2 个；洗点与联机下各玩家资格独立生效。

## 本地产物

DLL：bin/Release/net471/SephiriaTalentEffects.dll。已安装到下述游戏插件目录。

手动安装时，完全退出游戏，再将 DLL 放入 E:\steam\steamapps\common\Sephiria\BepInEx\plugins\SephiriaTalentEffects\ 并重新启动。卸载需退出游戏后移走 DLL。不会改写游戏程序集、原始天赋资源或天赋点存档。

