using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace SephiriaCrossbowAutoReload
{
    public enum AutoReloadMode
    {
        Original,
        Delayed,
        Disabled
    }

    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.sephiria.crossbow-auto-reload";
        public const string PluginName = "Sephiria Crossbow Auto Reload";
        public const string PluginVersion = "1.1.0";

        internal static ManualLogSource Log;
        internal static ConfigEntry<AutoReloadMode> Mode;

        // Update 每帧只读取这个普通字段；ConfigEntry 仅在设置变化时读取一次。
        internal static float AutoReloadDelaySeconds = 1.5f;

        private Harmony harmony;

        private void Awake()
        {
            Log = Logger;
            Mode = Config.Bind(
                "Gameplay",
                "AutoReloadMode",
                AutoReloadMode.Original,
                "控制弩在脱离战斗后的自动上弹行为。手动换弹和弹匣打空后的换弹不受影响。联机时由房主设置统一控制。");

            UpdateCachedDelay();
            Mode.SettingChanged += HandleModeChanged;

            harmony = new Harmony(PluginGuid);
            harmony.PatchAll(typeof(Plugin).Assembly);
            Log.LogInfo($"{PluginName} v{PluginVersion} 已加载（{Mode.Value}，阈值 {AutoReloadDelaySeconds} 秒）。");
        }

        private static void HandleModeChanged(object sender, EventArgs args)
        {
            UpdateCachedDelay();
            Log.LogInfo($"弩自动上弹模式已切换为 {Mode.Value}。");
        }

        private static void UpdateCachedDelay()
        {
            switch (Mode.Value)
            {
                case AutoReloadMode.Delayed:
                    AutoReloadDelaySeconds = 8f;
                    break;
                case AutoReloadMode.Disabled:
                    AutoReloadDelaySeconds = float.PositiveInfinity;
                    break;
                default:
                    AutoReloadDelaySeconds = 1.5f;
                    break;
            }
        }

        private void OnDestroy()
        {
            if (Mode != null) Mode.SettingChanged -= HandleModeChanged;
            harmony?.UnpatchSelf();
        }
    }

    [HarmonyPatch(typeof(UI_OptionsPanel), "OnOpened")]
    internal static class OptionsPanelPatch
    {
        private static void Postfix(UI_OptionsPanel __instance)
        {
            try
            {
                SettingsUiInjector.EnsureInjected(__instance);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"添加弩自动上弹设置失败：{ex}");
            }
        }
    }

    internal static class SettingsUiInjector
    {
        private const string RowName = "CrossbowAutoReloadOptionRow";
        private const string LabelKey = "Mod_CrossbowAutoReload_Label";
        private const string OriginalKey = "Mod_CrossbowAutoReload_Original";
        private const string DelayedKey = "Mod_CrossbowAutoReload_Delayed";
        private const string DisabledKey = "Mod_CrossbowAutoReload_Disabled";

        private static readonly FieldInfo ValueTextField =
            AccessTools.Field(typeof(UI_OptionBox_PartyMemberDamage), "valueText");
        private static readonly FieldInfo BoxField =
            AccessTools.Field(typeof(UI_OptionBox_PartyMemberDamage), "box");

        internal static void EnsureInjected(UI_OptionsPanel panel)
        {
            UI_TabContent gameplay = panel.GetComponentsInChildren<UI_TabContent>(true)
                .FirstOrDefault(content =>
                    content.GetComponentInChildren<UI_OptionBox_PartyMemberDamage>(true) != null);
            if (gameplay == null)
            {
                Plugin.Log.LogWarning("未找到游戏性设置页面，无法添加弩自动上弹选项。");
                return;
            }

            if (gameplay.transform.Find(RowName) != null ||
                gameplay.GetComponentsInChildren<Transform>(true).Any(item => item.name == RowName))
            {
                return;
            }

            UI_OptionBox_PartyMemberDamage template =
                gameplay.GetComponentInChildren<UI_OptionBox_PartyMemberDamage>(true);
            if (template == null || template.transform.parent == null)
            {
                Plugin.Log.LogWarning("未找到可复用的游戏性设置行。");
                return;
            }

            RegisterLocalization();

            Transform parent = template.transform.parent;
            GameObject clone = UnityEngine.Object.Instantiate(template.gameObject, parent);
            clone.name = RowName;
            clone.SetActive(false);

            UI_OptionBox_PartyMemberDamage original =
                clone.GetComponent<UI_OptionBox_PartyMemberDamage>();
            UI_LocalizationStringText valueText =
                (UI_LocalizationStringText)ValueTextField.GetValue(original);
            UI_HorizontalSelectionBox box =
                (UI_HorizontalSelectionBox)BoxField.GetValue(original);
            if (valueText == null || box == null)
            {
                UnityEngine.Object.DestroyImmediate(clone);
                Plugin.Log.LogWarning("复用的游戏性设置行结构不完整。");
                return;
            }

            UI_LocalizationStringText label = clone
                .GetComponentsInChildren<UI_LocalizationStringText>(true)
                .FirstOrDefault(text => text != valueText);
            if (label != null) label.UpdateKey(LabelKey);

            UI_HorizontalSelectionBox previousBox = parent
                .GetComponentsInChildren<UI_HorizontalSelectionBox>(true)
                .Where(item => item != box)
                .LastOrDefault();
            if (previousBox != null)
            {
                previousBox.forceNavDown = box;
                box.forceNavUp = previousBox;
            }

            UnityEngine.Object.DestroyImmediate(original);
            box.numberOfElements = 3;
            clone.AddComponent<CrossbowAutoReloadOptionRow>()
                .Initialize(box, valueText);
            clone.transform.SetAsLastSibling();
            clone.SetActive(true);

            if (parent is RectTransform parentRect)
                LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect);

            Plugin.Log.LogInfo("已在游戏性设置底部添加弩自动上弹选项。");
        }

        private static void RegisterLocalization()
        {
            LocalizationManager manager = LocalizationManager.Instance;
            if (manager == null) return;

            IEnumerable<string> languages = manager.Languages ?? new List<string>();
            if (!languages.Any() && !string.IsNullOrEmpty(manager.CurrentLanguage))
                languages = new[] { manager.CurrentLanguage };

            foreach (string language in languages)
            {
                bool chinese = language.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
                manager.AddModText(language, LabelKey, chinese ? "弩自动上弹" : "Crossbow auto reload");
                manager.AddModText(language, OriginalKey, chinese ? "原版（1.5秒）" : "Original (1.5s)");
                manager.AddModText(language, DelayedKey, chinese ? "延长（8秒）" : "Delayed (8s)");
                manager.AddModText(language, DisabledKey, chinese ? "关闭自动上弹" : "Disable auto reload");
            }
        }

        internal static string GetValueKey(int index)
        {
            switch (index)
            {
                case 1: return DelayedKey;
                case 2: return DisabledKey;
                default: return OriginalKey;
            }
        }
    }

    internal sealed class CrossbowAutoReloadOptionRow : MonoBehaviour
    {
        private UI_HorizontalSelectionBox box;
        private UI_LocalizationStringText valueText;
        private bool initialized;

        internal void Initialize(
            UI_HorizontalSelectionBox selectionBox,
            UI_LocalizationStringText selectionValueText)
        {
            box = selectionBox;
            valueText = selectionValueText;
            initialized = true;
            Refresh();
        }

        private void OnEnable()
        {
            if (!initialized) return;
            box.OnValueChanged += HandleValueChanged;
            Refresh();
        }

        private void OnDisable()
        {
            if (box != null) box.OnValueChanged -= HandleValueChanged;
        }

        private void HandleValueChanged(int index)
        {
            int bounded = Math.Max(0, Math.Min(2, index));
            Plugin.Mode.Value = (AutoReloadMode)bounded;
            Refresh();
        }

        private void Refresh()
        {
            int index = Math.Max(0, Math.Min(2, (int)Plugin.Mode.Value));
            box.ChangeValueWithoutNotify(index);
            valueText.UpdateKey(SettingsUiInjector.GetValueKey(index));
        }
    }

    /// <summary>
    /// 只把原版 Update 中“autoReloatTimer >= 1.5f”的阈值替换成缓存字段。
    /// 原版/8秒/关闭分别对应 1.5、8、正无穷，其余换弹代码保持原样。
    /// </summary>
    [HarmonyPatch(typeof(WeaponSimple_Crossbow), "Update")]
    internal static class CrossbowUpdatePatch
    {
        private static readonly FieldInfo AutoReloadTimerField =
            AccessTools.Field(typeof(WeaponSimple_Crossbow), "autoReloatTimer");
        private static readonly FieldInfo DelayField =
            AccessTools.Field(typeof(Plugin), nameof(Plugin.AutoReloadDelaySeconds));

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            bool replaced = false;

            for (int i = 1; i < codes.Count; i++)
            {
                if (!codes[i - 1].LoadsField(AutoReloadTimerField) ||
                    codes[i].opcode != OpCodes.Ldc_R4 ||
                    !(codes[i].operand is float value) ||
                    Math.Abs(value - 1.5f) > 0.0001f)
                {
                    continue;
                }

                codes[i].opcode = OpCodes.Ldsfld;
                codes[i].operand = DelayField;
                replaced = true;
                break;
            }

            if (!replaced)
            {
                Plugin.Log.LogError("未找到弩脱战自动上弹的 1.5 秒阈值；为避免误改，Mod 未修改换弹逻辑。");
            }

            return codes;
        }
    }
}
