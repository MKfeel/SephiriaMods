using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SephiriaItemLabTweaks
{
    internal static class TalentPanel
    {
        internal static void Add(object drawer, bool chinese)
        {
            var extra = TalentService.Extra;
            Func<int, bool, string> save = TalentService.Save;

            var row = (RectTransform)ReflectionUtil.Invoke(drawer, "CreateRow", 18f);
            row.gameObject.name = "ItemLabTweaks_TalentRow";
            ReflectionUtil.Invoke(drawer, "AddCompactLabel", row, chinese ? "额外天赋点" : "Extra talents", 54f);
            var input = (TMP_InputField)ReflectionUtil.Invoke(drawer, "CreateInput", row,
                extra.Value.ToString(), new Action<string>(_ => { }));
            input.contentType = TMP_InputField.ContentType.IntegerNumber;
            input.characterLimit = 4;
            input.SetTextWithoutNotify(extra.Value.ToString());
            (input.GetComponent<LayoutElement>() ?? input.gameObject.AddComponent<LayoutElement>()).preferredWidth = 38f;

            TMP_Text feedback = null;
            Action<bool> commit = reset =>
            {
                try
                {
                    // All point operations share Item Lab's local-session restrictions.
                    if (!GameAccess.TryGetLocalContext(out _, out _, out string error))
                    {
                        feedback.text = error;
                        return;
                    }
                    int value = 0;
                    if (!reset && (!int.TryParse(input.text, out value) || value < 0 || value > 9999))
                    {
                        feedback.text = chinese ? "请输入 0～9999 的整数" : "Enter an integer from 0 to 9999.";
                        return;
                    }
                    string message = save(value, reset);
                    input.SetTextWithoutNotify(extra.Value.ToString());
                    feedback.text = TranslateResult(message, chinese);
                    Plugin.ModLogger.LogInfo(message);
                }
                catch (Exception ex)
                {
                    feedback.text = chinese ? "天赋点修改失败，请查看日志" : "Talent update failed. Check the log.";
                    Plugin.ModLogger.LogError(ex);
                }
            };

            ReflectionUtil.Invoke(drawer, "AddButtonTo", row, chinese ? "保存" : "SAVE",
                new Action(() => commit(false)), 18f, 32f, null);
            ReflectionUtil.Invoke(drawer, "AddButtonTo", row, chinese ? "重置" : "RESET",
                new Action(() => commit(true)), 18f, 32f, null);
            feedback = AddNote(drawer, chinese ? "0～9999；重置会清除当前加点" : "0-9999; RESET also clears allocated talents.");
        }

        private static TMP_Text AddNote(object drawer, string text)
        {
            var row = (RectTransform)ReflectionUtil.Invoke(drawer, "CreateRow", 14f);
            row.gameObject.name = "ItemLabTweaks_TalentStatus";
            ReflectionUtil.Invoke(drawer, "AddCompactLabel", row, text, 0f);
            var label = row.GetComponentInChildren<TMP_Text>();
            label.fontSize = 4.5f;
            label.enableAutoSizing = true;
            label.fontSizeMin = 3.5f;
            label.fontSizeMax = 4.5f;
            return label;
        }

        private static string TranslateResult(string message, bool chinese)
        {
            if (chinese) return message;
            if (message.StartsWith("已保存：额外 "))
                return message.Replace("已保存：额外 ", "Saved: ").Replace(" 点", " extra points");
            if (message == "额外点数及现有天赋已重置") return "Extra points and allocated talents reset.";
            return message;
        }
    }
}
