using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using BepInEx.Configuration;
using HarmonyLib;
using Mirror;

namespace SephiriaItemLabTweaks
{
    internal static class TalentService
    {
        internal sealed class Applied { public int Value; }
        private static readonly ConditionalWeakTable<PlayerAvatar, Applied> AppliedPoints = new ConditionalWeakTable<PlayerAvatar, Applied>();
        private static readonly MethodInfo ResetMethod = AccessTools.Method(typeof(PlayerAvatar), "UserCode_CmdResetPassiveStat");
        private static ConfigFile config;
        internal static ConfigEntry<int> Extra;
        private static ConfigEntry<bool> pendingReset;

        internal static void Initialize(ConfigFile ownerConfig)
        {
            config = ownerConfig;
            Extra = config.Bind("Talents", "ExtraPoints", 0,
                new ConfigDescription("额外天赋点总量，重复保存不叠加。", new AcceptableValueRange<int>(0, 9999)));
            pendingReset = config.Bind("Internal", "ResetOnNextLoad", false, "迁移的待执行重置；下次加载本地角色时清除旧加点。");
        }

        internal static bool IsLocalServer(PlayerAvatar player) => NetworkServer.active &&
            (player.isLocalPlayer || (NetworkServer.localConnection != null && player.connectionToClient == NetworkServer.localConnection));

        internal static void Apply(PlayerAvatar player)
        {
            if (!IsLocalServer(player)) return;
            var applied = AppliedPoints.GetOrCreateValue(player);
            player.NetworkmaxPassivePoint = checked(player.maxPassivePoint - applied.Value + Extra.Value);
            applied.Value = Extra.Value;
        }

        // SPMod supplies a fresh base on every update, so combine directly and synchronize
        // the bookkeeping before Save subtracts the previous extra amount.
        internal static int CombineSPModLimit(int baseLimit, PlayerAvatar player)
        {
            if (!IsLocalServer(player)) return baseLimit;
            int limit = checked(baseLimit + Extra.Value);
            AppliedPoints.GetOrCreateValue(player).Value = Extra.Value;
            return limit;
        }

        internal static string Save(int value, bool reset)
        {
            if (value < 0 || value > 9999) return "请输入 0～9999 的整数";
            if (!GameAccess.TryGetLocalContext(out _, out _, out string error)) return error;
            if (ResetMethod == null) return "未找到原生天赋重置方法，未保存";
            var players = UnityEngine.Object.FindObjectsByType<PlayerAvatar>(UnityEngine.FindObjectsSortMode.None)
                .Where(IsLocalServer).ToArray();
            if (players.Length == 0) return "未找到本地角色，未保存";
            if (reset) value = 0;
            foreach (var player in players)
            {
                var applied = AppliedPoints.GetOrCreateValue(player);
                int limit = checked(player.maxPassivePoint - applied.Value + value);
                if (reset || player.passiveStats.Sum(pair => pair.Value) > limit)
                {
                    ResetMethod.Invoke(player, null);
                    player.SavePassiveStat(false);
                }
                player.NetworkmaxPassivePoint = limit;
                applied.Value = value;
            }
            Extra.Value = value;
            pendingReset.Value = false;
            config.Save();
            return reset ? "额外点数及现有天赋已重置" : $"已保存：额外 {value} 点";
        }

        internal static bool BeforeLoad(PlayerAvatar player)
        {
            Apply(player);
            if (!IsLocalServer(player) || !pendingReset.Value) return true;
            if (ResetMethod == null) throw new MissingMethodException(typeof(PlayerAvatar).FullName, "UserCode_CmdResetPassiveStat");
            ResetMethod.Invoke(player, null);
            player.SavePassiveStat(false);
            pendingReset.Value = false;
            config.Save();
            Plugin.ModLogger.LogInfo("已执行迁移的待处理天赋重置；跳过旧加点数据。");
            return false;
        }
    }

    [HarmonyPatch(typeof(PlayerAvatar), "OnStartServer")]
    internal static class InitialTalentPointsPatch
    {
        private static void Postfix(PlayerAvatar __instance) => TalentService.Apply(__instance);
    }

    [HarmonyPatch(typeof(PlayerAvatar), "LoadPassiveStatOnServer")]
    internal static class LoadTalentPointsPatch
    {
        private static bool Prefix(PlayerAvatar __instance) => TalentService.BeforeLoad(__instance);
    }
}
