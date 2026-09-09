using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SephiriaModSettings
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.sephiria.modsettings";
        public const string PluginName = "Sephiria Mod Settings";
        public const string PluginVersion = "1.0.11";

        internal static ManualLogSource Log;
        private Harmony harmony;

        private void Awake()
        {
            Log = Logger;
            harmony = new Harmony(PluginGuid);
            harmony.PatchAll(typeof(Plugin).Assembly);
            Log.LogInfo($"{PluginName} v{PluginVersion} 已加载。配置只在打开模组页面时扫描，不进行每帧轮询。");
        }

        private void OnDestroy()
        {
            harmony?.UnpatchSelf();
        }
    }

    [HarmonyPatch(typeof(UI_OptionsPanel), "OnOpened")]
    internal static class OptionsPanelPatch
    {
        private static void Prefix(UI_OptionsPanel __instance)
        {
            try
            {
                ModSettingsUi.EnsureTab(__instance);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"创建模组设置页面失败：{ex}");
            }
        }
    }

    [HarmonyPatch]
    internal static class OptionsTabDiagnosticPatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(
                typeof(UI_Tab),
                "SelectTab",
                new[] { typeof(int), typeof(GameObject).MakeByRefType() });
        }

        private static void Postfix(UI_Tab __instance)
        {
            ModSettingsUi.DumpVisualDiagnostics(__instance);
        }
    }

    internal static class MetadataProtocol
    {
        internal const string Prefix = "ModSettings:";
        internal const string Show = Prefix + "Show";

        internal static SettingMetadata Parse(ConfigEntryBase entry)
        {
            var result = new SettingMetadata
            {
                DisplayName = entry.Definition.Key,
                Order = 1000,
                ReloadMode = "Immediate"
            };

            object[] tags = entry.Description?.Tags ?? Array.Empty<object>();
            foreach (string tag in tags.OfType<string>())
            {
                if (string.Equals(tag, Show, StringComparison.OrdinalIgnoreCase))
                {
                    result.Visible = true;
                    continue;
                }

                if (!tag.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string payload = tag.Substring(Prefix.Length);
                int equals = payload.IndexOf('=');
                string key = equals >= 0 ? payload.Substring(0, equals) : payload;
                string value = equals >= 0 ? payload.Substring(equals + 1) : string.Empty;

                if (key.Equals("Name", StringComparison.OrdinalIgnoreCase)) result.DisplayName = value;
                else if (key.Equals("ModTitle", StringComparison.OrdinalIgnoreCase)) result.ModTitle = value;
                else if (key.Equals("Reload", StringComparison.OrdinalIgnoreCase)) result.ReloadMode = value;
                else if (key.Equals("HostOnly", StringComparison.OrdinalIgnoreCase)) result.HostOnly = true;
                else if (key.Equals("Order", StringComparison.OrdinalIgnoreCase) &&
                         int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int order))
                    result.Order = order;
                else if (key.Equals("Step", StringComparison.OrdinalIgnoreCase) &&
                         double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double step))
                    result.Step = step;
                else if (key.StartsWith("Option:", StringComparison.OrdinalIgnoreCase))
                    result.OptionLabels[key.Substring("Option:".Length)] = value;
            }

            return result;
        }
    }

    internal sealed class SettingMetadata
    {
        internal bool Visible;
        internal string DisplayName;
        internal string ModTitle;
        internal string ReloadMode;
        internal bool HostOnly;
        internal int Order;
        internal double? Step;
        internal readonly Dictionary<string, string> OptionLabels =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    internal sealed class ModGroup
    {
        internal string Guid;
        internal string Title;
        internal readonly List<SettingDescriptor> Settings = new List<SettingDescriptor>();
    }

    internal sealed class SettingDescriptor
    {
        internal ConfigEntryBase Entry;
        internal SettingMetadata Metadata;
        internal readonly List<object> Values = new List<object>();
        internal readonly List<string> Labels = new List<string>();

        internal int CurrentIndex
        {
            get
            {
                object current = Entry.BoxedValue;
                for (int i = 0; i < Values.Count; i++)
                {
                    if (Equals(Values[i], current)) return i;
                }
                return 0;
            }
        }
    }

    internal static class ConfigScanner
    {
        internal static List<ModGroup> Scan()
        {
            var groups = new List<ModGroup>();
            foreach (BepInEx.PluginInfo info in Chainloader.PluginInfos.Values
                         .OrderBy(value => value.Metadata.Name, StringComparer.OrdinalIgnoreCase))
            {
                if (info.Instance == null || info.Metadata.GUID == Plugin.PluginGuid)
                {
                    continue;
                }

                var descriptors = new List<SettingDescriptor>();
                string title = null;
                foreach (ConfigDefinition definition in info.Instance.Config.Keys)
                {
                    ConfigEntryBase entry = info.Instance.Config[definition];
                    SettingMetadata metadata = MetadataProtocol.Parse(entry);
                    if (!metadata.Visible)
                    {
                        continue;
                    }

                    SettingDescriptor descriptor = BuildDescriptor(entry, metadata);
                    if (descriptor == null || descriptor.Values.Count < 2)
                    {
                        Plugin.Log.LogWarning($"跳过不支持的模组设置：{info.Metadata.Name}/{entry.Definition.Key} ({entry.SettingType.Name})");
                        continue;
                    }

                    descriptors.Add(descriptor);
                    if (!string.IsNullOrWhiteSpace(metadata.ModTitle)) title = metadata.ModTitle;
                }

                if (descriptors.Count == 0)
                {
                    continue;
                }

                var group = new ModGroup
                {
                    Guid = info.Metadata.GUID,
                    Title = string.IsNullOrWhiteSpace(title) ? info.Metadata.Name : title
                };
                group.Settings.AddRange(descriptors
                    .OrderBy(item => item.Metadata.Order)
                    .ThenBy(item => item.Entry.Definition.Section, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(item => item.Entry.Definition.Key, StringComparer.OrdinalIgnoreCase));
                groups.Add(group);
            }
            return groups;
        }

        private static SettingDescriptor BuildDescriptor(ConfigEntryBase entry, SettingMetadata metadata)
        {
            var descriptor = new SettingDescriptor { Entry = entry, Metadata = metadata };
            Type type = entry.SettingType;

            if (type == typeof(bool))
            {
                Add(descriptor, false, LocalText("关闭", "Off"));
                Add(descriptor, true, LocalText("开启", "On"));
                return descriptor;
            }

            if (type.IsEnum)
            {
                foreach (object value in Enum.GetValues(type))
                {
                    Add(descriptor, value, GetOptionLabel(metadata, value));
                }
                return descriptor;
            }

            AcceptableValueBase acceptable = entry.Description?.AcceptableValues;
            if (acceptable != null && TryBuildList(descriptor, acceptable, metadata))
            {
                return descriptor;
            }

            if ((type == typeof(int) || type == typeof(float) || type == typeof(double)) &&
                acceptable != null && TryBuildRange(descriptor, acceptable, metadata, type))
            {
                return descriptor;
            }

            return null;
        }

        private static bool TryBuildList(
            SettingDescriptor descriptor, AcceptableValueBase acceptable, SettingMetadata metadata)
        {
            PropertyInfo property = acceptable.GetType().GetProperty("AcceptableValues");
            if (property?.GetValue(acceptable, null) is IEnumerable values)
            {
                foreach (object value in values)
                {
                    Add(descriptor, value, GetOptionLabel(metadata, value));
                }
                return descriptor.Values.Count > 0;
            }
            return false;
        }

        private static bool TryBuildRange(
            SettingDescriptor descriptor, AcceptableValueBase acceptable,
            SettingMetadata metadata, Type type)
        {
            PropertyInfo minProperty = acceptable.GetType().GetProperty("MinValue");
            PropertyInfo maxProperty = acceptable.GetType().GetProperty("MaxValue");
            if (minProperty == null || maxProperty == null)
            {
                return false;
            }

            double min = Convert.ToDouble(minProperty.GetValue(acceptable, null), CultureInfo.InvariantCulture);
            double max = Convert.ToDouble(maxProperty.GetValue(acceptable, null), CultureInfo.InvariantCulture);
            double step = metadata.Step ?? (type == typeof(int) ? 1d : Math.Max((max - min) / 20d, 0.01d));
            if (step <= 0d) return false;

            // 防止错误元数据一次创建成千上万个 UI 选项。
            double count = Math.Floor((max - min) / step) + 1d;
            if (count > 201d)
            {
                step = (max - min) / 100d;
                if (type == typeof(int)) step = Math.Max(1d, Math.Ceiling(step));
            }

            AddNumericValue(descriptor, min, type);
            double firstAligned = Math.Ceiling(min / step) * step;
            for (double value = firstAligned; value <= max + step * 0.25d && descriptor.Values.Count < 201; value += step)
            {
                AddNumericValue(descriptor, Math.Min(value, max), type);
            }
            AddNumericValue(descriptor, max, type);

            object current = descriptor.Entry.BoxedValue;
            if (!descriptor.Values.Any(item => Equals(item, current)))
            {
                descriptor.Values.Add(current);
                descriptor.Labels.Add(FormatNumber(current));
                SortNumeric(descriptor);
            }
            return descriptor.Values.Count > 0;
        }

        private static void AddNumericValue(SettingDescriptor descriptor, double value, Type type)
        {
            object converted;
            if (type == typeof(int)) converted = Convert.ToInt32(Math.Round(value));
            else if (type == typeof(float)) converted = Convert.ToSingle(value);
            else converted = value;
            if (descriptor.Values.Any(item => Equals(item, converted))) return;
            Add(descriptor, converted, FormatNumber(converted));
        }

        private static void SortNumeric(SettingDescriptor descriptor)
        {
            var zipped = descriptor.Values.Zip(descriptor.Labels, (value, label) => new { value, label })
                .OrderBy(item => Convert.ToDouble(item.value, CultureInfo.InvariantCulture))
                .ToList();
            descriptor.Values.Clear();
            descriptor.Labels.Clear();
            foreach (var item in zipped)
            {
                descriptor.Values.Add(item.value);
                descriptor.Labels.Add(item.label);
            }
        }

        private static void Add(SettingDescriptor descriptor, object value, string label)
        {
            descriptor.Values.Add(value);
            descriptor.Labels.Add(label);
        }

        private static string GetOptionLabel(SettingMetadata metadata, object value)
        {
            string key = value?.ToString() ?? string.Empty;
            return metadata.OptionLabels.TryGetValue(key, out string label) ? label : key;
        }

        private static string FormatNumber(object value)
        {
            if (value is float single) return single.ToString("0.##", CultureInfo.InvariantCulture);
            if (value is double number) return number.ToString("0.##", CultureInfo.InvariantCulture);
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        private static string LocalText(string chinese, string english)
        {
            string language = LocalizationManager.Instance?.CurrentLanguage ?? string.Empty;
            return language.StartsWith("zh", StringComparison.OrdinalIgnoreCase) ? chinese : english;
        }
    }

    internal static class ModSettingsUi
    {
        private const string TabName = "ModSettings_Tab_Mod";
        private const string ContentName = "ModSettings_Content_Mod";

        private static readonly FieldInfo PanelTabField = AccessTools.Field(typeof(UI_OptionsPanel), "tab");
        private static readonly FieldInfo CommonValueTextField = AccessTools.Field(typeof(UI_OptionBox_Common_Integer), "valueText");
        private static readonly FieldInfo CommonBoxField = AccessTools.Field(typeof(UI_OptionBox_Common_Integer), "box");

        internal static void DumpVisualDiagnostics(UI_Tab tab)
        {
            if (tab?.tabButtons == null || tab.tabContents == null ||
                !tab.tabContents.Any(content => content != null && content.name == ContentName))
            {
                return;
            }

            var report = new StringBuilder();
            GameObject selectedObject = EventSystem.current?.currentSelectedGameObject;
            report.AppendLine($"[Tab视觉诊断] selectedTab={tab.CurrentSelectedTab} " +
                $"eventSelected={GetPath(selectedObject?.transform)}");

            for (int i = 0; i < tab.tabButtons.Length; i++)
            {
                UI_TabButton tabButton = tab.tabButtons[i];
                if (tabButton == null) continue;
                Button button = tabButton.GetComponent<Button>();
                Graphic target = button?.targetGraphic;
                report.AppendLine($"  button[{i}] {tabButton.name} activated={tabButton.IsActivated} " +
                    $"rect={DescribeRect(tabButton.transform as RectTransform)} " +
                    $"buttonEnabled={button?.enabled} interactable={button?.interactable} " +
                    $"transition={button?.transition} target={GetPath(target?.transform)}");

                if (button != null)
                {
                    SpriteState sprites = button.spriteState;
                    report.AppendLine($"    states highlighted={DescribeSprite(sprites.highlightedSprite)} " +
                        $"selected={DescribeSprite(sprites.selectedSprite)} pressed={DescribeSprite(sprites.pressedSprite)} " +
                        $"disabled={DescribeSprite(sprites.disabledSprite)}");
                }

                foreach (Image image in tabButton.GetComponentsInChildren<Image>(true))
                {
                    report.AppendLine($"    image path={GetPath(image.transform)} id={image.GetInstanceID()} " +
                        $"enabled={image.enabled} active={image.gameObject.activeInHierarchy} " +
                        $"sprite={DescribeSprite(image.sprite)} type={image.type} " +
                        $"rect={DescribeRect(image.rectTransform)}");
                }
            }

            for (int i = 0; i < tab.tabContents.Length; i++)
            {
                UI_TabContent content = tab.tabContents[i];
                if (content == null) continue;
                Image image = content.GetComponent<Image>();
                report.AppendLine($"  content[{i}] {content.name} opened={content.IsOpened} " +
                    $"active={content.gameObject.activeInHierarchy} rect={DescribeRect(content.transform as RectTransform)} " +
                    $"rootImage={(image == null ? "<none>" : $"enabled={image.enabled} sprite={DescribeSprite(image.sprite)}")}");
            }

            Plugin.Log.LogInfo(report.ToString().TrimEnd());
        }

        private static string DescribeSprite(Sprite sprite)
        {
            return sprite == null ? "<null>" : $"{sprite.name}#{sprite.GetInstanceID()}";
        }

        private static string DescribeRect(RectTransform rect)
        {
            if (rect == null) return "<none>";
            return $"pos=({rect.anchoredPosition.x:0.##},{rect.anchoredPosition.y:0.##}) " +
                $"size=({rect.rect.width:0.##},{rect.rect.height:0.##}) " +
                $"anchors=({rect.anchorMin.x:0.##},{rect.anchorMin.y:0.##})-({rect.anchorMax.x:0.##},{rect.anchorMax.y:0.##}) " +
                $"pivot=({rect.pivot.x:0.##},{rect.pivot.y:0.##})";
        }

        private static string GetPath(Transform transform)
        {
            if (transform == null) return "<null>";
            var names = new Stack<string>();
            for (Transform current = transform; current != null; current = current.parent)
                names.Push(current.name);
            return string.Join("/", names.ToArray());
        }

        internal static void EnsureTab(UI_OptionsPanel panel)
        {
            UI_Tab tab = (UI_Tab)PanelTabField.GetValue(panel);
            if (tab == null || tab.tabButtons == null || tab.tabContents == null)
            {
                Plugin.Log.LogWarning("原版设置页 Tab 结构不可用。");
                return;
            }

            UI_TabContent existing = tab.tabContents.FirstOrDefault(content => content != null && content.name == ContentName);
            if (existing != null)
            {
                return;
            }

            UI_TabContent gameplay = panel.GetComponentsInChildren<UI_TabContent>(true)
                .FirstOrDefault(content => content.GetComponentInChildren<UI_OptionBox_PartyMemberDamage>(true) != null);
            if (gameplay == null)
            {
                Plugin.Log.LogWarning("未找到可复用的游戏性设置页面。");
                return;
            }

            int templateIndex = Array.IndexOf(tab.tabContents, gameplay);
            if (templateIndex < 0 || templateIndex >= tab.tabButtons.Length)
            {
                Plugin.Log.LogWarning("游戏性 Tab 不在原版 Tab 数组中。");
                return;
            }

            // Preserve the native right-edge sprites. Select the source by its
            // actual visual position rather than assuming array order.
            UI_TabButton sourceButton = tab.tabButtons
                .Where(button => button != null && button.transform is RectTransform)
                .OrderBy(button => ((RectTransform)button.transform).anchoredPosition.x)
                .LastOrDefault();
            if (sourceButton == null)
            {
                Plugin.Log.LogWarning("未找到可复用的原版 Tab 按钮。");
                return;
            }

            GameObject cloneObject = UnityEngine.Object.Instantiate(
                sourceButton.gameObject, sourceButton.transform.parent);
            UI_TabButton newButton = cloneObject.GetComponent<UI_TabButton>();
            if (newButton == null)
            {
                UnityEngine.Object.DestroyImmediate(cloneObject);
                Plugin.Log.LogWarning("克隆的 Tab 按钮缺少 UI_TabButton。");
                return;
            }
            newButton.name = TabName;
            newButton.gameObject.name = TabName;
            SetLocalizedText(newButton.gameObject, "模组", "Mods");

            UI_TabContent newContent = UnityEngine.Object.Instantiate(gameplay, gameplay.transform.parent);
            newContent.name = ContentName;
            newContent.gameObject.name = ContentName;
            newContent.gameObject.SetActive(false);
            // Instantiate appends the clone as the top-most sibling, which can
            // put its raycast surface above the tab strip. Keep it next to the
            // original content in the prefab's established draw order.
            newContent.transform.SetSiblingIndex(gameplay.transform.GetSiblingIndex() + 1);

            tab.tabButtons = tab.tabButtons.Concat(new[] { newButton }).ToArray();
            tab.tabContents = tab.tabContents.Concat(new[] { newContent }).ToArray();

            int newTabIndex = tab.tabButtons.Length - 1;
            Button clickButton = newButton.GetComponent<Button>();
            if (clickButton != null)
            {
                // The clone carries the source button's serialized SelectTab(4)
                // callback. Replace the whole event so it selects the new slot.
                clickButton.onClick = new Button.ButtonClickedEvent();
                clickButton.onClick.AddListener(() => tab.SelectTab(newTabIndex));
            }
            else
            {
                Plugin.Log.LogWarning("模组 Tab 缺少原版 Button 点击组件。");
            }

            NormalizeTabButtonLayout(tab.tabButtons, newButton.transform.parent);

            RebuildContent(newContent);
            Plugin.Log.LogInfo("已在原版设置中添加“模组”Tab。");
        }

        private static void NormalizeTabButtonLayout(UI_TabButton[] buttons, Transform parent)
        {
            if (buttons == null || buttons.Length == 0 || parent == null) return;

            RectTransform[] rects = buttons
                .Where(button => button != null && button.transform.parent == parent)
                .Select(button => button.transform as RectTransform)
                .Where(rect => rect != null)
                .ToArray();
            if (rects.Length != buttons.Length || rects.Length < 2) return;

            // Extracted prefab values (both level1 and level2): every button is
            // 74 units wide and adjacent centers are 77 units apart. Do not
            // shrink the sliced active-tab sprite; its borders cannot fit in the
            // 61-unit width used by the previous attempt. Expand the strip by
            // exactly one native button step instead. The panel is 489 units
            // wide, so the resulting 459-unit strip fits without overflow.
            float buttonWidth = rects.Take(rects.Length - 1).Average(rect => rect.rect.width);
            RectTransform[] originalRects = rects.Take(rects.Length - 1)
                .OrderBy(rect => rect.anchoredPosition.x)
                .ToArray();
            float step = originalRects.Length > 1
                ? originalRects.Take(originalRects.Length - 1)
                    .Zip(originalRects.Skip(1), (left, right) => right.anchoredPosition.x - left.anchoredPosition.x)
                    .Where(value => value > 0.1f)
                    .DefaultIfEmpty(buttonWidth + 3f)
                    .Average()
                : buttonWidth + 3f;
            float spacing = Math.Max(0f, step - buttonWidth);
            float requiredWidth = buttonWidth * rects.Length + spacing * (rects.Length - 1);

            if (parent is RectTransform parentRect)
            {
                // Keep the prefab's stretch anchors. ContentSizeFitter and the
                // native layout group use this coordinate system and will keep
                // the expanded strip centered over TabArea.
                Vector2 size = parentRect.sizeDelta;
                size.x += requiredWidth - parentRect.rect.width;
                parentRect.sizeDelta = size;
            }

            HorizontalLayoutGroup horizontal = parent.GetComponent<HorizontalLayoutGroup>();
            if (horizontal != null && parent is RectTransform layoutRect)
            {
                horizontal.enabled = true;
                LayoutRebuilder.ForceRebuildLayoutImmediate(layoutRect);
            }

            Plugin.Log.LogInfo($"顶部 Tab 保持原生布局：{buttons.Length} × {buttonWidth:0.##}，间距 {spacing:0.##}，宽度 {requiredWidth:0.##}。");
        }

        private static void RebuildContent(UI_TabContent content)
        {
            UI_OptionBox_Common_Integer commonTemplate =
                content.GetComponentInChildren<UI_OptionBox_Common_Integer>(true);
            UI_OptionBox_PartyMemberDamage partyTemplate =
                content.GetComponentInChildren<UI_OptionBox_PartyMemberDamage>(true);

            Component templateComponent = (Component)commonTemplate ?? partyTemplate;
            if (templateComponent == null || templateComponent.transform.parent == null)
            {
                Plugin.Log.LogWarning("模组页面没有可复用的原版设置行。");
                return;
            }

            ScrollRect scroll = content.GetComponentInChildren<ScrollRect>(true);
            Transform scrollContent = scroll != null ? scroll.content : null;
            Transform rowsParent = scrollContent ?? templateComponent.transform.parent;
            GameObject rowTemplate = templateComponent.gameObject;
            RectTransform templateRect = rowTemplate.transform as RectTransform;
            float rowWidth = templateRect != null && templateRect.rect.width > 1f ? templateRect.rect.width : 397f;
            float rowHeight = templateRect != null && templateRect.rect.height > 1f ? templateRect.rect.height : 20f;
            rowTemplate.transform.SetParent(content.transform, false);
            rowTemplate.SetActive(false);

            for (int i = rowsParent.childCount - 1; i >= 0; i--)
            {
                UnityEngine.Object.DestroyImmediate(rowsParent.GetChild(i).gameObject);
            }

            foreach (LayoutGroup layout in rowsParent.GetComponents<LayoutGroup>())
                UnityEngine.Object.DestroyImmediate(layout);
            foreach (ContentSizeFitter fitter in rowsParent.GetComponents<ContentSizeFitter>())
                UnityEngine.Object.DestroyImmediate(fitter);

            List<ModGroup> groups = ConfigScanner.Scan();
            UI_HorizontalSelectionBox previousBox = null;
            UI_HorizontalSelectionBox firstBox = null;
            float top = 4f;

            foreach (ModGroup group in groups)
            {
                CreateTitleRow(rowTemplate, rowsParent, group.Title, rowWidth, rowHeight, top);
                top += rowHeight + 4f;
                foreach (SettingDescriptor descriptor in group.Settings)
                {
                    UI_HorizontalSelectionBox box = CreateSettingRow(
                        rowTemplate, rowsParent, descriptor, rowWidth, rowHeight, top);
                    if (box == null) continue;
                    top += rowHeight + 2f;
                    if (firstBox == null) firstBox = box;
                    if (previousBox != null)
                    {
                        previousBox.forceNavDown = box;
                        box.forceNavUp = previousBox;
                    }
                    previousBox = box;
                }
                top += 8f;
            }

            UnityEngine.Object.DestroyImmediate(rowTemplate);
            content.selectionOnOpened = firstBox?.gameObject;
            if (scroll != null) scroll.verticalNormalizedPosition = 1f;

            if (rowsParent is RectTransform rect)
            {
                float viewportHeight = scroll != null && scroll.viewport != null ? scroll.viewport.rect.height : 0f;
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = new Vector2(0f, Math.Max(top, viewportHeight));
            }

            Plugin.Log.LogInfo($"模组设置页面已刷新：{groups.Count} 个 Mod，{groups.Sum(group => group.Settings.Count)} 个设置项。");
        }

        private static void CreateTitleRow(
            GameObject template, Transform parent, string title,
            float width, float height, float top)
        {
            var row = new GameObject("ModTitle_" + Sanitize(title), typeof(RectTransform));
            row.name = "ModTitle_" + Sanitize(title);
            row.transform.SetParent(parent, false);
            row.SetActive(false);
            RectTransform rowRect = (RectTransform)row.transform;
            PositionAbsoluteRow(rowRect, width, height, top);
            TextMeshProUGUI sourceText = template.GetComponentsInChildren<TextMeshProUGUI>(true).FirstOrDefault();

            if (sourceText != null)
            {
                TextMeshProUGUI titleText = UnityEngine.Object.Instantiate(sourceText, row.transform);
                titleText.gameObject.name = "ModSettings_TitleText";
                UI_LocalizationStringText localization = titleText.GetComponent<UI_LocalizationStringText>();
                if (localization != null) UnityEngine.Object.DestroyImmediate(localization);
                RectTransform titleRect = titleText.rectTransform;
                titleRect.anchorMin = Vector2.zero;
                titleRect.anchorMax = Vector2.one;
                titleRect.offsetMin = new Vector2(18f, 0f);
                titleRect.offsetMax = new Vector2(-18f, 0f);
                titleText.enabled = true;
                titleText.text = title;
                titleText.alignment = TextAlignmentOptions.MidlineLeft;
                titleText.fontStyle = FontStyles.Bold;
                titleText.fontSize = sourceText.fontSize + 1f;
                titleText.color = new Color(0.55f, 1f, 0.68f, 1f);
                titleText.raycastTarget = false;
            }

            row.SetActive(true);
        }

        private static UI_HorizontalSelectionBox CreateSettingRow(
            GameObject template, Transform parent, SettingDescriptor descriptor,
            float width, float height, float top)
        {
            GameObject row = UnityEngine.Object.Instantiate(template, parent);
            row.name = "ModSetting_" + Sanitize(descriptor.Entry.Definition.Key);
            row.SetActive(false);
            if (row.transform is RectTransform rowRect)
                PositionAbsoluteRow(rowRect, width, height, top);

            UI_LocalizationStringText valueText;
            UI_HorizontalSelectionBox box;
            if (!TryGetCommonParts(row, out valueText, out box))
            {
                UnityEngine.Object.DestroyImmediate(row);
                return null;
            }

            RemoveOptionBehaviour(row);
            UI_LocalizationStringText label = row.GetComponentsInChildren<UI_LocalizationStringText>(true)
                .FirstOrDefault(text => text != valueText);
            if (label != null)
            {
                label.enabled = false;
                string suffix = descriptor.Metadata.HostOnly ? "（房主）" : string.Empty;
                label.text.text = descriptor.Metadata.DisplayName + suffix;
            }
            valueText.enabled = false;

            box.numberOfElements = descriptor.Values.Count;
            ModSettingRow controller = row.AddComponent<ModSettingRow>();
            controller.Initialize(descriptor, box, valueText.text);
            row.SetActive(true);
            return box;
        }

        private static void PositionAbsoluteRow(
            RectTransform rect, float width, float height, float top)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(width * 0.5f + 4f, -(top + height * 0.5f));
            rect.localScale = Vector3.one;
        }

        private static bool TryGetCommonParts(
            GameObject row, out UI_LocalizationStringText valueText, out UI_HorizontalSelectionBox box)
        {
            UI_OptionBox_Common_Integer common = row.GetComponent<UI_OptionBox_Common_Integer>();
            if (common != null)
            {
                valueText = (UI_LocalizationStringText)CommonValueTextField.GetValue(common);
                box = (UI_HorizontalSelectionBox)CommonBoxField.GetValue(common);
                return valueText != null && box != null;
            }

            UI_OptionBox_PartyMemberDamage party = row.GetComponent<UI_OptionBox_PartyMemberDamage>();
            if (party != null)
            {
                valueText = (UI_LocalizationStringText)AccessTools.Field(party.GetType(), "valueText").GetValue(party);
                box = (UI_HorizontalSelectionBox)AccessTools.Field(party.GetType(), "box").GetValue(party);
                return valueText != null && box != null;
            }

            valueText = null;
            box = null;
            return false;
        }

        private static void RemoveOptionBehaviour(GameObject row)
        {
            UI_OptionBox_Common_Integer common = row.GetComponent<UI_OptionBox_Common_Integer>();
            if (common != null) UnityEngine.Object.DestroyImmediate(common);
            UI_OptionBox_PartyMemberDamage party = row.GetComponent<UI_OptionBox_PartyMemberDamage>();
            if (party != null) UnityEngine.Object.DestroyImmediate(party);
        }

        private static void SetLocalizedText(GameObject root, string chinese, string english)
        {
            string language = LocalizationManager.Instance?.CurrentLanguage ?? string.Empty;
            string value = language.StartsWith("zh", StringComparison.OrdinalIgnoreCase) ? chinese : english;
            UI_LocalizationStringText text = root.GetComponentInChildren<UI_LocalizationStringText>(true);
            if (text != null)
            {
                text.enabled = false;
                text.text.text = value;
            }
            else
            {
                TextMeshProUGUI tmp = root.GetComponentInChildren<TextMeshProUGUI>(true);
                if (tmp != null) tmp.text = value;
            }
        }

        private static string Sanitize(string value)
        {
            return new string((value ?? "Setting").Select(character =>
                char.IsLetterOrDigit(character) ? character : '_').ToArray());
        }
    }

    internal sealed class ModSettingRow : MonoBehaviour
    {
        private SettingDescriptor descriptor;
        private UI_HorizontalSelectionBox box;
        private TextMeshProUGUI valueText;
        private bool initialized;

        internal void Initialize(
            SettingDescriptor setting, UI_HorizontalSelectionBox selectionBox, TextMeshProUGUI text)
        {
            descriptor = setting;
            box = selectionBox;
            valueText = text;
            initialized = true;
            Refresh();
        }

        private void OnEnable()
        {
            if (!initialized || box == null) return;
            box.OnValueChanged += HandleValueChanged;
            descriptor.Entry.ConfigFile.SettingChanged += HandleExternalSettingChanged;
            Refresh();
        }

        private void OnDisable()
        {
            if (box != null) box.OnValueChanged -= HandleValueChanged;
            if (descriptor?.Entry?.ConfigFile != null)
                descriptor.Entry.ConfigFile.SettingChanged -= HandleExternalSettingChanged;
        }

        private void HandleValueChanged(int index)
        {
            if (index < 0 || index >= descriptor.Values.Count) return;
            descriptor.Entry.BoxedValue = descriptor.Values[index];
            Refresh();
            Plugin.Log.LogInfo($"已更新 {descriptor.Entry.Definition.Section}/{descriptor.Entry.Definition.Key} = {descriptor.Entry.BoxedValue}");
        }

        private void HandleExternalSettingChanged(object sender, SettingChangedEventArgs args)
        {
            if (args.ChangedSetting == descriptor.Entry) Refresh();
        }

        private void Refresh()
        {
            if (!initialized || box == null || valueText == null) return;
            int index = Math.Max(0, Math.Min(descriptor.CurrentIndex, descriptor.Values.Count - 1));
            box.ChangeValueWithoutNotify(index);
            valueText.text = descriptor.Labels[index];
        }
    }
}
