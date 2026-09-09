using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using HarmonyLib;

namespace SephiriaTalentSPCompat
{
    [BepInPlugin(Id, "Sephiria Talent SPMod Compatibility", "1.0.0")]
    [BepInDependency(TalentId)]
    [BepInProcess("Sephiria.exe")]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Id = "com.codex.sephiria.talent-spmod-compat";
        private const string TalentId = "com.codex.sephiria.talent-customizer";
        private static ConfigEntry<int> extra;
        private static Func<PlayerAvatar, bool> isLocalServer;
        private static Action<PlayerAvatar> apply;
        private static Plugin instance;
        private readonly Harmony harmony = new Harmony(Id);
        private bool finished;

        private void Awake()
        {
            instance = this;
            // Reuse the original plugin's ownership checks and applied-point bookkeeping.
            var talent = Chainloader.PluginInfos[TalentId].Instance;
            var type = talent.GetType();
            extra = (ConfigEntry<int>)AccessTools.Field(type, "Extra").GetValue(null);
            isLocalServer = (Func<PlayerAvatar, bool>)Delegate.CreateDelegate(typeof(Func<PlayerAvatar, bool>), AccessTools.Method(type, "IsLocalServer"));
            apply = (Action<PlayerAvatar>)Delegate.CreateDelegate(typeof(Action<PlayerAvatar>), AccessTools.Method(type, "Apply"));
            harmony.Patch(AccessTools.Method(typeof(AddOnLoader), "LoadAll"),
                postfix: new HarmonyMethod(typeof(Plugin), nameof(AfterAddOnsLoaded)));
            Logger.LogInfo("兼容补丁已加载，等待 SPMod 程序集。");
            Update();
        }

        private static void AfterAddOnsLoaded() => instance.Update();

        private void Update()
        {
            if (finished) return;
            var type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("SPMod.StarSephiriaPatches+PlayerAvatar_Patches", false))
                .FirstOrDefault(t => t != null);
            if (type == null) return;
            finished = true;
            try
            {
                var target = AccessTools.Method(type, "UpdateMaxPassivePoint", new[] { typeof(PlayerAvatar) });
                if (target == null) throw new MissingMethodException(type.FullName, "UpdateMaxPassivePoint");
                harmony.Patch(target, transpiler: new HarmonyMethod(typeof(Plugin), nameof(Transpiler)));
                Logger.LogInfo("SPMod 天赋兼容已启用：上限 = SPMod 原上限 + Talent Customizer 额外点数；在超额检查前应用。");
            }
            catch (Exception ex)
            {
                Logger.LogError("SPMod 天赋兼容安装失败：" + ex);
            }
        }

        internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.ToList();
            var setter = AccessTools.PropertySetter(typeof(PlayerAvatar), "NetworkmaxPassivePoint");
            var matches = code.Where(i => i.Calls(setter)).ToList();
            if (matches.Count != 1)
                throw new InvalidOperationException("预期一个天赋上限写入点，实际 " + matches.Count);
            var original = matches[0];
            var index = code.IndexOf(original);
            // Stack at setter: [player, SPMod limit]. Pass the limit and player to the helper.
            var loadPlayer = new CodeInstruction(OpCodes.Ldarg_0);
            loadPlayer.labels.AddRange(original.labels);
            loadPlayer.blocks.AddRange(original.blocks);
            original.labels.Clear();
            original.blocks.Clear();
            code.InsertRange(index, new[] {
                loadPlayer,
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Plugin), nameof(CombineLimit)))
            });
            return code;
        }

        private static int CombineLimit(int spLimit, PlayerAvatar player)
        {
            if (!isLocalServer(player)) return spLimit;
            // Apply first so Save() keeps subtracting the correct previous extra amount.
            apply(player);
            return checked(spLimit + extra.Value);
        }
    }
}
