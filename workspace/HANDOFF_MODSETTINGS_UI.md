# Sephiria ModSettings UI 开发 Handoff

## 用户当前目标

为 Sephiria（BepInEx 5）开发 `Sephiria Mod Settings`：

1. 在原版“设置”中添加“模组”Tab。
2. 不同 Mod 的设置以纯文本标题分隔。
3. 设置变更热重载，不能每帧扫描配置。
4. 弩自动上弹提供三档：原版 1.5 秒、延长 8 秒、关闭自动上弹。
5. 背包整理和弩自动上弹接入 ModSettings。
6. 最终将 ModSettings、弩自动上弹分别新建 GitHub 仓库并上传；背包整理不能上传。

用户自己负责进游戏验证，不要主动启动或控制游戏。

## 关键路径

- 工作区：`C:\Users\1\Documents\ChatGPT\赛菲莉娅mod开发`
- 游戏：`E:\steam\steamapps\common\Sephiria`
- ModSettings 源码：`mod-settings\Plugin.cs`
- ModSettings 项目：`mod-settings\SephiriaModSettings.csproj`
- 弩 Mod：`crossbow-auto-reload\`
- 背包整理适配版：`backpack-organizer-performance\`（禁止上传）
- 当前部署 DLL：`E:\steam\steamapps\common\Sephiria\BepInEx\plugins\SephiriaModSettings\SephiriaModSettings.dll`
- 日志：`E:\steam\steamapps\common\Sephiria\BepInEx\LogOutput.log`

## 当前版本与部署状态

- 磁盘上 ModSettings：`1.0.6`，源码也是 `1.0.6`。
- 弩自动上弹：`1.1.0`，已部署，三档功能和热重载已实现。
- 背包整理：`2.5.3`，版本号未改，已加 ModSettings 元数据并部署。
- GitHub 上传尚未进行；UI 稳定前不要发布。
- `1.0.5` 曾破坏内容布局，已经撤销。
- 游戏插件目录里有多份 `.bak-*` 备份，包括 1.0.4、1.0.5 前后的版本。

## 当前 UI 的实际状态（1.0.6）

已经正常的部分：

- “模组”Tab 可以点击，也能切回其他 Tab。
- 重复的原版“背包色盲模式”已移除。
- 标题已是纯文本，不再带左右箭头或配置行底板。
- 模组设置行基本恢复为正常间距，热重载能工作。

仍未解决的问题：

- 选择“模组”后，高亮仍出现在“游戏性”位置。
- 顶部 Tab 条整体有横向偏移/溢出，第一项左侧被挤出。
- 用户最新反馈原话：“这不还是歪的吗”。不要再把未经实测/诊断的改动说成已修复。

## 已确认的原版 UI 资源数据

已安装 UnityPy 到：`.tools\unitypy`。

分析脚本：

- `.tools\inspect_unity_options.py`
- `.tools\dump_options_hierarchy.py`

UnityPy 文件由提升权限安装，普通沙箱读取可能显示拒绝访问；运行分析脚本时可能需要 `require_escalated`。

游戏设置界面存在于 `level1` 和 `level2`。当前资源结构（level2）包括：

- `OptionPanel`：GameObject path ID `4543`
- `TabArea`：宽 `433`
- `TabLabel`：GameObject path ID `4675`，横向拉伸，`sizeDelta.x = -51`，实际宽约 `382`
- 原版 5 个按钮均为宽 `74`、高 `20`
- 按钮中心 X：`0, 77, 154, 231, 308`
- 原生间距：`3`
- 五栏总宽：`5*74 + 4*3 = 382`
- 按钮子 `Image` 在资源中为 stretch（anchor 0..1，sizeDelta 0）
- `Base` 宽 `489`，因此理论上六栏原生总宽 `6*74 + 5*3 = 459` 可以放入 Base，但不能简单修改当前 TabLabel 的 `sizeDelta`，因为 1.0.6 实测导致整个 Tab 条向左偏移。

level2 按钮 GameObject path IDs：

- GamePlay `3463`
- Sound `4633`
- Display `3755`
- Keyboard `3131`
- Gamepad `5469`

## 已失败的方案（不要重复）

### 1. 把 6 个按钮等宽压缩到原 382 宽

曾把按钮宽度改成约 `61.17`。结果九宫格/切片高亮边框无法缩到该宽度，出现高亮跨到相邻 Tab、视觉错位。不能继续缩小原生 Tab 宽度。

### 2. 给 HorizontalLayoutGroup 设置 `preferredWidth=0`、`flexibleWidth=1`

父容器带按首选宽度收缩的布局，结果 6 个按钮全部挤到中间。

### 3. 强行重挂/拉伸所有按钮 Image

没有解决真正的序列化引用问题，还影响原版按钮高亮。不要再批量修改全部原版 Image。

### 4. 给克隆内容强加 VerticalLayoutGroup + ContentSizeFitter

原版设置行是固定 `RectTransform` 坐标，不适合直接套纵向布局，结果所有设置行压在一起（1.0.5）。已在 1.0.6 改为按原版模板尺寸绝对排列。

### 5. 仅凭截图猜 `UI_TabButton.butttonImage` 的层级

这是前几次失败的主要原因。资源层级只能说明 GameObject 关系，无法确认运行时 `UI_Tab.tabButtons` 数组顺序及 `butttonImage` 私有字段实际指向。下一步必须做一次运行时诊断。

## 下一步建议（最重要）

先制作一个“只增加日志、不改布局”的诊断版，用户只需启动游戏、打开设置一次，然后直接读取本机 `LogOutput.log`，不需要用户手动传日志。

在 `EnsureTab` 克隆前和克隆后分别记录：

- `tab.tabButtons` 每个索引。
- `button.name`、GameObject 名、InstanceID。
- 按钮 `RectTransform.anchoredPosition / rect.size / parent`。
- `UI_TabButton.butttonImage`（通过现有 `ButtonImageField`）的 GameObject 名、InstanceID。
- `butttonImage.gameObject == button.gameObject`。
- `butttonImage.transform.IsChildOf(button.transform)`。
- backing Image 的父路径、RectTransform 坐标。
- 按钮下所有 TMP 文本内容。
- 点击模组后 `tab.CurrentSelectedTab` 和每个按钮的 `IsActivated`。

重点验证两个怀疑：

1. `tab.tabButtons` 数组顺序可能不等于屏幕从左到右顺序，因此不能使用 `tab.tabButtons[Length-1]` 作为模板；应按按钮 RectTransform 的实际 X 选择最右侧模板。
2. 运行时克隆的 `UI_TabButton.butttonImage` 可能仍指向原版“游戏性”的 Image，导致选择模组时游戏性位置变亮。需要根据日志精确重绑到克隆对象自己的根 Image，而不是猜测 `GetComponentInChildren<Image>()`。

可以考虑用完整 GameObject 克隆并重新取组件：

```csharp
GameObject cloneObject = UnityEngine.Object.Instantiate(sourceButton.gameObject, sourceButton.transform.parent);
UI_TabButton newButton = cloneObject.GetComponent<UI_TabButton>();
```

但只有运行时日志确认 `butttonImage` 指向后再实施。

顶部布局方面，建议不要依赖 HorizontalLayoutGroup 自动居中。取得运行时原按钮 Rect 后，可按 Base/TabArea 的实际宽度计算一个整体居中的六栏区域，并明确设置每个按钮的原生宽 `74` 与中心位置；同时禁用该 TabLabel 上会覆写位置的布局组件。计算必须基于运行时本地 UI 单位，不是屏幕像素，以适配不同分辨率和 CanvasScaler。

## ModSettings 性能与接入协议

当前架构没有每帧读取配置：

- 只在首次打开设置页时扫描 `Chainloader.PluginInfos`。
- UI 改值直接写 `ConfigEntryBase.BoxedValue`。
- 通过 BepInEx `SettingChanged` 响应外部变化。
- 弩 Mod 每帧只读取缓存的静态 float，不读取配置文件。

元数据字符串：

- `ModSettings:Show`
- `ModSettings:ModTitle=...`
- `ModSettings:Name=...`
- `ModSettings:Order=...`
- `ModSettings:Reload=Immediate|NextUse|Restart`
- `ModSettings:HostOnly`
- `ModSettings:Step=...`
- `ModSettings:Option:EnumValue=Label`

完整接入说明在 `mod-settings\README.md`。

## GitHub 信息

- GitHub 用户：`MKfeel`
- 计划新建：`SephiriaModSettings`、`SephiriaCrossbowAutoReload`
- 本机无 `gh` 命令，但 Codex 环境存在 GitHub connector；之前尚未完成仓库创建。
- 背包整理项目禁止上传。

## 交接原则

- 先读当前 `mod-settings\Plugin.cs`、本 handoff 和最新 `LogOutput.log`。
- 不要启动游戏；用户自己验证。
- 不要发布 GitHub，直到用户明确确认 UI 正常。
- 下一步先取得运行时引用数据，再修复；不要继续截图驱动的盲调。
