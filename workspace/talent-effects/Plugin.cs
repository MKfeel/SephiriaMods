using System.Reflection;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using BepInEx;
using HarmonyLib;
using Mirror;
using UnityEngine;

namespace SephiriaTalentEffects
{
    [BepInPlugin("com.codex.sephiria.talent-effects", "Sephiria Talent Effects", "0.2.1")]
    public sealed class Plugin : BaseUnityPlugin
    {
        private void Awake()
        {
            new Harmony("com.codex.sephiria.talent-effects").PatchAll(typeof(Plugin).Assembly);
            WisdomRewardDefinition.Apply();
            Logger.LogInfo("Survival 20: +1% all damage per full 10 maximum HP (on change). Wisdom 10: miniboss 1 dice; native boss reward 2 dice; legacy unique-pair effect removed.");
        }

        internal static bool IsSurvival20(Component component)
        {
            var metadata = component.GetComponent<PassiveObjectMetadata>();
            return metadata != null && metadata.effectString?.key == "PassiveEffect_Engravings"
                && (component.gameObject.name == "6_LV20_Engravings"
                    || component.gameObject.name == "6_LV20_Engravings(Clone)");
        }

        internal static bool IsWisdom10(Component component)
        {
            var metadata = component.GetComponent<PassiveObjectMetadata>();
            return metadata != null && metadata.effectString?.key == "PassiveEffect_UniquePair"
                && (component.gameObject.name == "8_LV10_UniquePair"
                    || component.gameObject.name == "8_LV10_UniquePair(Clone)");
        }
    }

    [HarmonyPatch(typeof(PassiveDatabase), nameof(PassiveDatabase.Initialize))]
    internal static class WisdomRewardDefinition
    {
        private static void Postfix() => Apply();

        internal static void Apply()
        {
            if (PassiveDatabase.data == null || !PassiveDatabase.data.TryGetValue(8UL, out var wisdom)
                || wisdom.lv10PerkPrefab == null) return;
            var status = wisdom.lv10PerkPrefab.GetComponent<PassiveObject_StatusInstance>();
            if (status == null || !Plugin.IsWisdom10(status)) return;
            // Replace the entire perk effect; native status lifecycle still owns removal.
            status.stats = new[] { "BOSS_REWARD_DICE/2" };
        }
    }

    // Keep the original network prefab and its NetworkBehaviours intact.
    // Only replace its server-side effects; clients use the native synced custom stat.
    [HarmonyPatch]
    internal static class EnablePatch
    {
        private static System.Collections.Generic.IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(PassiveObject_InventorySlot), "OnEffectEnabled");
            yield return AccessTools.Method(typeof(PassiveObject_StatusInstance), "OnEffectEnabled");
        }

        private static bool Prefix(PassiveObject __instance, PlayerAvatar player)
        {
            if (!Plugin.IsSurvival20(__instance)) return true;
            if (__instance is PassiveObject_StatusInstance && NetworkServer.active)
            {
                var effect = __instance.GetComponent<SurvivalDamage>();
                if (effect == null) effect = __instance.gameObject.AddComponent<SurvivalDamage>();
                effect.Begin(player);
            }
            return false;
        }
    }

    [HarmonyPatch]
    internal static class DisablePatch
    {
        private static System.Collections.Generic.IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(PassiveObject_InventorySlot), "OnEffectDisabled");
            yield return AccessTools.Method(typeof(PassiveObject_StatusInstance), "OnEffectDisabled");
        }

        private static bool Prefix(PassiveObject __instance)
        {
            if (!Plugin.IsSurvival20(__instance)) return true;
            // Both native components share one object. Stop is deliberately idempotent.
            __instance.GetComponent<SurvivalDamage>()?.Stop();
            return false;
        }
    }

    [HarmonyPatch]
    internal static class DescriptionPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(PassiveObjectMetadata), nameof(PassiveObjectMetadata.GetEffectStringInner));
            yield return AccessTools.Method(typeof(PassiveObjectMetadata_UniquePair), nameof(PassiveObjectMetadata.GetEffectStringInner));
        }

        private static bool Prefix(PassiveObjectMetadata __instance, ref string __result)
        {
            if (Plugin.IsWisdom10(__instance))
            {
                __result = "击败迷你Boss时获得1个骰子，击败Boss时获得2个骰子。";
                return false;
            }
            if (!Plugin.IsSurvival20(__instance)) return true;
            __result = "每 10 点最大生命值提供 1% 伤害放大（不足 10 点的部分不计）。";
            return false;
        }

    }

    public sealed class SurvivalDamage : MonoBehaviour
    {
        private static readonly Dictionary<PlayerAvatar, SurvivalDamage> Active = new Dictionary<PlayerAvatar, SurvivalDamage>();
        private PlayerAvatar target;
        private int applied;

        public void Begin(PlayerAvatar player)
        {
            Stop();
            if (Active.TryGetValue(player, out var previous)) previous.Stop();
            target = player;
            Active[player] = this;
            Refresh();
        }

        internal static void OnMaxHpChanged(UnitAvatar avatar)
        {
            if (NetworkServer.active && avatar is PlayerAvatar player && Active.TryGetValue(player, out var effect))
                effect.Refresh();
        }

        private void Refresh()
        {
            if (!NetworkServer.active || target == null) return;
            // Same integer percent stat and rounding as native PassiveObject_HPAndDamage.
            int next = Mathf.Max(0, Mathf.FloorToInt(target.MaxHp / 10f));
            if (next == applied) return;
            int delta = next - applied;
            applied = next;
            target.AddCustomStat(ECustomStat.AllDamageBonus, delta);
        }

        public void Stop()
        {
            var previous = target;
            int remove = applied;
            if (!ReferenceEquals(previous, null) && Active.TryGetValue(previous, out var current) && ReferenceEquals(current, this))
                Active.Remove(previous);
            target = null;
            applied = 0;
            if (NetworkServer.active && previous != null && remove != 0)
                previous.AddCustomStat(ECustomStat.AllDamageBonus, -remove);
        }

        private void OnDestroy() => Stop();
    }

    [HarmonyPatch]
    internal static class MaxHpChangedPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (string name in new[] { "NetworkmaxHp", "NetworkfinalMaxHp", "NetworkisHPCursed", "NetworkcursedMaxHp" })
                yield return AccessTools.PropertySetter(typeof(UnitAvatar), name);
        }

        private static void Postfix(UnitAvatar __instance) => SurvivalDamage.OnMaxHpChanged(__instance);
    }

    [HarmonyPatch(typeof(UnitAvatar), nameof(UnitAvatar.Die))]
    internal static class MinibossRewardPatch
    {
        // Weak keys avoid retaining defeated monsters across rooms. One reward per monster instance.
        private static readonly ConditionalWeakTable<UnitAvatar, object> Rewarded = new ConditionalWeakTable<UnitAvatar, object>();

        private static void Prefix(UnitAvatar __instance, out bool __state)
        {
            __state = NetworkServer.active && !__instance.IsDead && __instance.monsterType == EMonsterType.Miniboss;
        }

        private static void Postfix(UnitAvatar __instance, bool __state)
        {
            if (!NetworkServer.active || !__state || !__instance.IsDead
                || Rewarded.TryGetValue(__instance, out _)) return;
            Rewarded.Add(__instance, new object());
            // Same per-player entitlement as BossSpawner. No last-hit requirement.
            // Normal bosses are deliberately left to the original reward spawners.
            foreach (var connection in NetworkServer.connections.Values)
            {
                var identity = connection?.identity;
                if (identity == null) continue;
                var player = identity.GetComponent<PlayerAvatar>();
                if (player != null && player.GetCustomStatUnsafe("BOSSREWARDDICE") > 0)
                    player.RequestCreateDice(1, 1f);
            }
        }
    }
}
