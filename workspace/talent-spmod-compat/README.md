# SPMod 与 Talent Customizer 兼容补丁 1.0.0

适配本机 SPMod 2.4.3、Sephiria Talent Customizer 1.0.2、BepInEx 5。

SPMod 的 `SPMod.StarSephiriaPatches+PlayerAvatar_Patches.UpdateMaxPassivePoint` 在更新中将上限写为 `50 + SPMod奖励`，随后重置超过上限的加点。这会覆盖 Talent Customizer 的额外点数，且不受 `ModifyPassive` 开关控制。

补丁在该方法写入上限前加上 Talent Customizer 的额外点数，沿用原插件的本地房主判断和点数记账，保留 SPMod 自身奖励与重置规则。AddOns 加载结束时安装补丁，另有 Update 检查作为后备。匹配不到唯一写入点时记录错误，不猜测修改位置。

## 安装与回退

构建：`dotnet build talent-spmod-compat/SephiriaTalentSPCompat.csproj -c Release`。

将 `bin/Release/net471/SephiriaTalentSPCompat.dll` 放到 `E:\steam\steamapps\common\Sephiria\BepInEx\plugins\SephiriaTalentSPCompat\`，保留两个原 Mod。完全退出后重新启动游戏，不使用热重载验收。

回退：退出游戏后移走新增的 `SephiriaTalentSPCompat.dll`。本补丁不修改两个原 Mod、配置或存档文件。

## 验证情况

- Release 构建成功，0 警告、0 错误。
- Mono.Cecil 静态检查实际 SPMod DLL：唯一上限 setter 位于 IL_00d3，超额比较 getter 位于 IL_011c，插入点先于超额重置判断。
- 当前 PowerShell/.NET 环境不能运行游戏附带的旧版 Harmony（读取方法体及 AccessTools 初始化失败），因此未完成离线 Harmony 执行测试，不能视为游戏运行验证。
- 未启动游戏。请用户验证：日志出现“SPMod 天赋兼容已启用”；额外 90 点时上限为 140 加 SPMod 奖励；重复保存 90 不叠加；切换场景、重新进档后仍保留；超出旧上限的加点不再因此被清空。
- SPMod 自身在服装/难度奖励变化时仍可能主动重置天赋，此行为未修改。已被原冲突清空的加点需要重新分配。

## 输入 DLL SHA-256

- SPMod: `1A6CD5594D646516FBE5C159269A391D0C68860F5A600005F288B0B4A6F4C15D`
- Talent Customizer: `CA596712205EE9A0B9E214118270F8449E5F20042EF96CCB64B2E7DC3070B7CB`
