SEPHIRIA CHEAT LAB 1.0.1
========================

WARNING: This development/testing tool can create items and change progression.
Back up your save before use.

REQUIREMENTS
- Windows PC version of Sephiria.
- BepInEx 5 x64 (not included).
- Tested with Sephiria 1.0.25 / Unity 6000.3.21f1.

INSTALL
1. Install BepInEx 5 x64 into the Sephiria game directory.
2. Extract this archive into the game directory.
3. Confirm this file exists:
   BepInEx\plugins\SephiriaItemLab\SephiriaItemLab.dll

USE
- Enter a single-player run or local-host session.
- Press O in the main scene to open the official Item Box. O is ignored while
  another game control panel is open.
- Press O again or Escape to close it.
- The last outer tab, debug sub-tab, and scroll position are restored on reopen.

FEATURES
- Official item groups, favorites, native tooltips, controller navigation,
  one-click generation, favorite batch generation, and debug-item cleanup.
- Character resources and every supported readable advanced stat.
- Low-overhead infinite HP/MP.
- Weapon reset, replacement, and the official enhancement panel.
- Miracle management with native icons.
- Optional official internal/test items.
- Native zero-cost entries for tablet fusion, artifact rewards, tablet rewards,
  and repeatable enchanting.
- Add or remove one active backpack slot for layout testing.

SAFETY
- All generation and debug writes require a single-player/local-host server with
  at most one network connection.
- Multiplayer clients cannot use write operations.
- Generated items and changes may persist in the run or save.
- Do not use the tool in public or persistent multiplayer environments.

LANGUAGES
- Built-in English and Simplified Chinese; missing languages fall back to English.
- Item names, descriptions, sets, types, rarities, weapons, miracles, and native
  tooltips use Sephiria's current localization.
- Add community translations as Localization\<language-code>.xml using the
  included TRANSLATION_TEMPLATE.xml.

UNINSTALL
Delete BepInEx\plugins\SephiriaItemLab. Generated items are not removed automatically.

PRIVACY AND ASSETS
The plugin does not connect to the internet, collect telemetry, or auto-update.
The archive contains no Sephiria assets; native UI resources are referenced at runtime.

简体中文
========

警告：这是开发与测试工具，会生成物品并可能改变正常进度。使用前请备份存档。

运行要求与安装
- Windows PC 版《赛菲莉娅》与 BepInEx 5 x64；发行包不包含 BepInEx。
- 已在 Sephiria 1.0.25 / Unity 6000.3.21f1 测试。
- 将压缩包解压到游戏目录，确认存在：
  BepInEx\plugins\SephiriaItemLab\SephiriaItemLab.dll

使用
- 进入单人游戏或本地主机对局，在主要场景按 O 打开官方物品箱。
- 其他游戏控制面板打开时不会响应 O；再次按 O 或 Escape 关闭。
- 重新打开时会恢复上次外层标签、调试子标签和滚动位置。

主要功能
- 保留官方物品分组、收藏、原生详情、手柄导航、单件/收藏批量生成与调试物品清理。
- 修改角色资源和全部可读高级属性，并提供低开销无限生命/蓝量。
- 重置/替换武器、打开官方强化界面，以及管理奇迹。
- 可选显示官方内部/测试物品。
- 零费用打开原生石板融合、神器奖励、石板奖励与重复附魔流程。
- 增加或减少一个有效背包格，便于布局测试。

安全限制
- 所有生成与调试写操作仅允许连接数不超过一个的单人/本地主机服务器。
- 多人客户端无法执行写操作；生成物品和修改可能保留在本局或存档中。
- 请勿在公开或持久化多人环境使用本工具。

语言、隐私与资产
- 内置英文和简体中文，缺失语言回退英文；物品、套装、稀有度、武器、奇迹与
  原生详情直接跟随游戏语言。
- 可依据随包模板添加 Localization\<语言代码>.xml，无需重编译 DLL。
- 模组不会联网、收集遥测或自动更新；发行包不包含《赛菲莉娅》资产。

卸载
删除 BepInEx\plugins\SephiriaItemLab；已生成的物品不会被自动移除。
