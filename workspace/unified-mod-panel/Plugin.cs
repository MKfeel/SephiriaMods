using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SephiriaUnifiedModPanel
{
    [BepInPlugin(Guid, "Sephiria Unified Mod Panel", "0.2.0")]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = "com.sephiria.unifiedmodpanel";
        private static Plugin instance;
        private Harmony harmony;
        private ConfigEntry<Key> hotkey;
        private ConfigEntry<bool> replacePanels;
        private readonly HashSet<MethodBase> patched = new HashSet<MethodBase>();
        private GameObject root;
        private RectTransform body;
        private TMP_Text status;
        private TMP_FontAsset font;
        private Sprite panelSprite;
        private Material panelMaterial, textMaterial;
        private Color panelColor;
        private bool ready, opened, cursorVisible;
        private CursorLockMode cursorLock;
        private readonly Color ink = new Color(0.94f, 0.91f, 0.79f);
        private int closedFrame = -1;

        private void Awake()
        {
            instance = this;
            hotkey = Config.Bind("面板", "打开快捷键", Key.F1, "打开或关闭统一模组配置面板。");
            replacePanels = Config.Bind("面板", "接管旧面板", true, "原生资源准备好后屏蔽旧 F1/F9 面板；关闭本项可恢复。");
            harmony = new Harmony(Guid);
            harmony.Patch(AccessTools.PropertyGetter(typeof(PlayerInputController), "BlockAvatarInput"),
                postfix: new HarmonyMethod(typeof(Plugin), nameof(BlockAvatar)));
            foreach (var method in typeof(PlayerInputController).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                if (method.ReturnType == typeof(void) && method.GetParameters().Length == 1 &&
                    method.GetParameters()[0].ParameterType == typeof(InputAction.CallbackContext))
                    harmony.Patch(method, prefix: new HarmonyMethod(typeof(Plugin), nameof(AllowGameInput)));
            UnityEngine.SceneManagement.SceneManager.activeSceneChanged += SceneChanged;
            StartCoroutine(Prepare());
            Logger.LogInfo("统一模组面板 0.2.0 已加载，仅管理加载开关。默认 F1 打开。");
        }

        private IEnumerator Prepare()
        {
            while (!ready)
            {
                try { CaptureTheme(); }
                catch (Exception e) { ready = false; if (root != null) Destroy(root); Logger.LogWarning("读取原生 UI 资源失败：" + e.Message); }
                if (!ready) yield return new WaitForSecondsRealtime(2);
            }
            PatchOldPanels();
        }

        private void CaptureTheme()
        {
            var options = Resources.FindObjectsOfTypeAll<UI_OptionsPanel>().FirstOrDefault(p => p.gameObject.scene.IsValid());
            if (options == null) return;
            var text = options.GetComponentsInChildren<TextMeshProUGUI>(true).FirstOrDefault(t => t.font != null);
            font = text?.font;
            textMaterial = text?.fontSharedMaterial;
            var panel = options.GetComponentsInChildren<Image>(true)
                .Where(i => i.sprite != null && i.type == Image.Type.Sliced)
                .OrderByDescending(i => i.rectTransform.rect.width * i.rectTransform.rect.height).FirstOrDefault();
            panelSprite = panel?.sprite;
            panelMaterial = panel?.material;
            panelColor = panel != null ? panel.color : Color.white;
            ready = font != null && panelSprite != null;
            if (ready && root == null) BuildWindow();
        }

        private void PatchOldPanels()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (string name in new[] { "SephiriaModManager.Plugin", "ConfigurationManager.ConfigurationManager" })
                {
                    Type type = assembly.GetType(name, false);
                    if (type == null) continue;
                    foreach (string method in name.StartsWith("Sephiria") ? new[] { "HandleUpdate", "OnGUI" } : new[] { "Update", "LateUpdate", "OnGUI" })
                    {
                        var target = AccessTools.DeclaredMethod(type, method);
                        if (target != null && patched.Add(target))
                            harmony.Patch(target, prefix: new HarmonyMethod(typeof(Plugin), nameof(AllowOldPanel)));
                    }
                }
            }
        }

        private static bool AllowOldPanel() => instance == null || !instance.ready || instance.root == null || !instance.replacePanels.Value;
        private static void BlockAvatar(ref bool __result) { if (instance != null && instance.opened) __result = true; }
        private static bool AllowGameInput(InputAction.CallbackContext __0) => instance == null ||
            (!instance.opened && instance.closedFrame != Time.frameCount) || __0.canceled;
        private void SceneChanged(UnityEngine.SceneManagement.Scene previous, UnityEngine.SceneManagement.Scene next) => Close();

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (hotkey.Value != Key.None && keyboard[hotkey.Value].wasPressedThisFrame) { if (opened) Close(); else Open(); }
            else if (opened && keyboard.escapeKey.wasPressedThisFrame) Close();
        }

        private void LateUpdate()
        {
            if (opened) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        }

        private void Open()
        {
            if (!ready) { Logger.LogWarning("原生设置 UI 尚未就绪，请进入主菜单后再按快捷键。"); return; }
            if (EventSystem.current == null) { Logger.LogWarning("游戏 EventSystem 尚未就绪。"); return; }
            try
            {
                if (root == null) BuildWindow();
                PatchOldPanels();
                Refresh();
                cursorVisible = Cursor.visible;
                cursorLock = Cursor.lockState;
                root.SetActive(true);
                opened = true;
            }
            catch (Exception e) { Logger.LogError(e); ready = false; if (root != null) Destroy(root); StartCoroutine(Prepare()); }
        }

        private void Close()
        {
            if (!opened) return;
            opened = false;
            closedFrame = Time.frameCount;
            if (root != null) root.SetActive(false);
            Cursor.lockState = cursorLock;
            Cursor.visible = cursorVisible;
        }

        private void OnDestroy()
        {
            Close();
            UnityEngine.SceneManagement.SceneManager.activeSceneChanged -= SceneChanged;
            harmony?.UnpatchSelf();
            if (root != null) Destroy(root);
            if (instance == this) instance = null;
        }

        private RectTransform Rect(string name, Transform parent, float x, float y, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var r = (RectTransform)go.transform;
            r.SetParent(parent, false);
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0, 1);
            r.anchoredPosition = new Vector2(x, -y);
            r.sizeDelta = new Vector2(w, h);
            return r;
        }

        private Image Background(RectTransform r, Sprite sprite, Color color)
        {
            var img = r.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            img.color = color;
            return img;
        }

        private TMP_Text Label(Transform parent, string text, float x, float y, float w, float h, float size = 17)
        {
            var label = Rect("Label", parent, x, y, w, h).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            if (textMaterial != null) label.fontSharedMaterial = textMaterial;
            label.fontSize = size;
            label.color = ink;
            label.richText = false;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.text = text;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.raycastTarget = false;
            return label;
        }

        private Button Button(Transform parent, string text, float x, float y, float w, Action action)
        {
            var r = Rect(text, parent, x, y, w, 34);
            // Do not stretch a tab highlight or arrow sprite into a button background.
            Background(r, null, new Color(0.08f, 0.09f, 0.14f));
            var edge = Rect("Border", r, 2, 2, w - 4, 30);
            Background(edge, null, new Color(0.40f, 0.47f, 0.62f));
            var fill = Rect("Fill", r, 4, 4, w - 8, 26);
            var image = Background(fill, null, Color.white);
            var b = r.gameObject.AddComponent<Button>();
            b.targetGraphic = image;
            var colors = b.colors;
            colors.normalColor = new Color(0.23f, 0.26f, 0.36f);
            colors.highlightedColor = new Color(0.38f, 0.43f, 0.55f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = new Color(0.16f, 0.19f, 0.27f);
            colors.disabledColor = new Color(0.15f, 0.16f, 0.20f);
            b.colors = colors;
            Label(r, text, 8, 0, w - 16, 34, 16).alignment = TextAlignmentOptions.Center;
            b.onClick.AddListener(() => Guard(action));
            return b;
        }

        private void BuildWindow()
        {
            root = new GameObject("SephiriaUnifiedModPanel", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            DontDestroyOnLoad(root);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30000;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1000, 720);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var shade = Rect("Backdrop", root.transform, 0, 0, 0, 0);
            shade.anchorMax = Vector2.one;
            Background(shade, null, new Color(0, 0, 0, 0.65f));
            var window = Rect("Window", root.transform, 0, 0, 940, 660);
            window.anchorMin = window.anchorMax = window.pivot = new Vector2(0.5f, 0.5f);
            var background = Background(window, panelSprite, panelColor);
            background.material = panelMaterial;
            Label(window, "模组管理", 28, 15, 450, 40, 25);
            Button(window, "关闭", 820, 20, 90, Close);
            Label(window, "仅管理加载开关 · 更改后完全退出并重新启动游戏生效", 28, 64, 740, 40, 18);
            Button(window, "刷新", 790, 66, 120, Refresh);
            var viewport = Rect("ScrollViewport", window, 28, 132, 884, 466);
            Background(viewport, null, new Color(0, 0, 0, 0.12f));
            viewport.gameObject.AddComponent<RectMask2D>();
            body = Rect("Content", viewport, 0, 0, 868, 466);
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = body;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 35;
            status = Label(window, "", 28, 605, 880, 42, 14);
            root.SetActive(false);
        }

        private void Guard(Action action)
        {
            try { action(); }
            catch (Exception e) { Logger.LogError(e); SetStatus("操作失败：" + e.GetBaseException().Message); }
        }
        private void SetStatus(string text) { if (status != null) status.text = text; }
        private void Clear()
        {
            foreach (Transform child in body) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            body.anchoredPosition = Vector2.zero;
        }
        private void Refresh()
        {
            Clear();
            float y = 4;
            Label(body, "BepInEx 插件", 12, y, 840, 36, 22);
            y += 42;
            var plugins = PluginFiles.Scan(Paths.PluginPath, message => Logger.LogWarning(message));
            foreach (var plugin in plugins.OrderBy(p => p.Name))
            {
                var selected = plugin;
                bool self = plugin.Guids.Contains(Guid);
                bool loaded = plugin.Guids.Any(g => Chainloader.PluginInfos.ContainsKey(g));
                Label(body, plugin.Name, 12, y, 640, 30, 18);
                string state = self ? "面板自身 · 保持加载" :
                    "下次启动：" + (plugin.Enabled ? "加载" : "不加载") +
                    "    当前进程：" + (loaded ? "已加载" : "未加载") +
                    (loaded != plugin.Enabled ? "    · 待重启" : "");
                Label(body, state, 12, y + 30, 660, 26, 14);
                Button(body, plugin.Enabled ? "禁用加载" : "恢复加载", 702, y + 9, 150, () => {
                    PluginFiles.SetEnabled(selected, !selected.Enabled, Paths.PluginPath);
                    Refresh();
                    SetStatus("已设置：" + selected.Name + "。完全退出并重新启动游戏后生效；小退不会卸载 BepInEx 补丁。");
                }).interactable = !self;
                y += 72;
            }
            Label(body, "AddOns 模组", 12, y + 8, 840, 36, 22);
            y += 52;
            foreach (string directory in new[] { ActiveRoot, DisabledRoot })
            {
                if (!Directory.Exists(directory)) continue;
                AddonFiles.CheckRoot(directory);
                foreach (string folder in Directory.GetDirectories(directory).OrderBy(f => f))
                {
                    if ((File.GetAttributes(folder) & FileAttributes.ReparsePoint) != 0) continue;
                    bool active = directory == ActiveRoot;
                    string name = Path.GetFileName(folder);
                    string metadata = Path.Combine(folder, "metadata.json");
                    if (!File.Exists(metadata)) continue;
                    string title = name;
                    try { title = (string)JObject.Parse(File.ReadAllText(metadata))["modName"] ?? name; }
                    catch (Exception e) { Logger.LogWarning(name + " metadata: " + e.Message); }
                    string selected = folder;
                    bool loaded = AddOnLoader.LoadedMods.Any(m => string.Equals(Path.GetFileName(m.FolderPath), name, StringComparison.OrdinalIgnoreCase));
                    Label(body, title, 12, y, 640, 30, 18);
                    Label(body, "下次启动：" + (active ? "加载" : "不加载") + "    当前进程：" + (loaded ? "已加载" : "未加载") +
                        (active != loaded ? "    · 待重启" : ""), 12, y + 30, 660, 26, 14);
                    Button(body, active ? "禁用加载" : "恢复加载", 702, y + 9, 150, () => {
                        AddonFiles.Move(selected, active ? ActiveRoot : DisabledRoot, active ? DisabledRoot : ActiveRoot);
                        Refresh();
                        SetStatus("已设置：" + title + "。请完全退出并重新启动游戏；当前版本的 SPMod 不支持完整热卸载。");
                    });
                    y += 72;
                }
            }
            body.sizeDelta = new Vector2(868, Math.Max(y + 16, 466));
            SetStatus("加载开关已保存到磁盘。SPMod 与通用 BepInEx 插件须重启；不在小退时执行不完整卸载。");
        }

        private string ActiveRoot => Path.Combine(Paths.GameRootPath, "AddOns");
        private string DisabledRoot => Path.Combine(Paths.GameRootPath, "AddOns_Disabled");
    }
}
