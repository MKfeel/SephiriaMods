using System;
using System.Reflection;
using Mirror;
using UnityEngine;
using SephiriaTalentEffects;

static class Regression
{
    static int checks;
    static object Call(Type type, string method, params object[] args) =>
        type.GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args);
    static void Check(bool value, string name)
    { if (!value) throw new Exception(name); checks++; }
    static PlayerAvatar Connect(int id, int stat)
    {
        var go = new GameObject();
        var player = go.AddComponent<PlayerAvatar>(); player.bossDiceStat = stat;
        NetworkServer.connections[id] = new NetworkConnectionToClient { identity = go.AddComponent<NetworkIdentity>() };
        return player;
    }
    static void Die(UnitAvatar victim, bool succeeds = true)
    {
        object[] args = { victim, false };
        Call(typeof(MinibossRewardPatch), "Prefix", args);
        if (succeeds) victim.IsDead = true;
        Call(typeof(MinibossRewardPatch), "Postfix", victim, args[1]);
    }
    static void Main()
    {
        var player = new PlayerAvatar { NetworkmaxHp = 250, damageBonus = 7 };
        var effect = new SurvivalDamage(); effect.Begin(player);
        Check(player.damageBonus == 32, "activation adds 25 without replacing existing 7");
        effect.Begin(player);
        Check(player.damageBonus == 32, "repeated enable cannot stack");
        player.NetworkmaxHp = 259; SurvivalDamage.OnMaxHpChanged(player);
        Check(player.damageBonus == 32, "fractional interval rounds down");
        player.NetworkmaxHp = 260; SurvivalDamage.OnMaxHpChanged(player);
        Check(player.damageBonus == 33, "max hp change updates immediately");
        player.NetworkfinalMaxHp = 50; SurvivalDamage.OnMaxHpChanged(player);
        Check(player.damageBonus == 46, "percentage hp applies");
        player.NetworkcursedMaxHp = 49; player.NetworkisHPCursed = 1; SurvivalDamage.OnMaxHpChanged(player);
        Check(player.damageBonus == 11, "curse uses effective hp cap");
        player.NetworkcursedMaxHp = 9; SurvivalDamage.OnMaxHpChanged(player);
        Check(player.damageBonus == 7, "below 10 grants zero");
        player.NetworkisHPCursed = 0; SurvivalDamage.OnMaxHpChanged(player);
        Check(player.damageBonus == 46, "curse removal restores contribution");
        int writes = player.statWrites; SurvivalDamage.OnMaxHpChanged(player);
        Check(writes == player.statWrites, "unchanged value causes no stat mutation");
        effect.Stop(); effect.Stop();
        Check(player.damageBonus == 7, "double cleanup preserves other bonuses");
        player.NetworkmaxHp = 500; SurvivalDamage.OnMaxHpChanged(player);
        Check(player.damageBonus == 7, "disabled talent ignores later hp changes");
        Check(typeof(SurvivalDamage).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic) == null, "no periodic update");

        var first = Connect(1, 1); var second = Connect(2, 1); var noTalent = Connect(3, 0);
        NetworkServer.connections[4] = new NetworkConnectionToClient();
        var mini = new UnitAvatar { monsterType = EMonsterType.Miniboss };
        Die(mini);
        Check(first.diceRequests == 1 && second.diceRequests == 1 && noTalent.diceRequests == 0, "native per-player entitlement; no last-hit requirement");
        Die(mini);
        Call(typeof(MinibossRewardPatch), "Postfix", mini, true);
        Check(first.diceRequests == 1, "duplicate death callbacks cannot duplicate rewards");
        Die(new UnitAvatar { monsterType = EMonsterType.Boss });
        Die(new UnitAvatar { monsterType = EMonsterType.Normal });
        Die(new UnitAvatar { monsterType = EMonsterType.Dummy });
        Check(first.diceRequests == 1, "normal boss remains native; normal enemies and dummy excluded");
        Die(new UnitAvatar { monsterType = EMonsterType.Miniboss }, false);
        Check(first.diceRequests == 1, "failed death grants nothing");
        NetworkServer.active = false;
        Die(new UnitAvatar { monsterType = EMonsterType.Miniboss });
        Check(first.diceRequests == 1, "client cannot grant dice");
        NetworkServer.active = true;
        first.bossDiceStat = 0;
        Die(new UnitAvatar { monsterType = EMonsterType.Miniboss });
        Check(first.diceRequests == 1 && second.diceRequests == 2, "reset removes entitlement immediately");

        var wisdom = new GameObject { name = "8_LV10_UniquePair(Clone)" };
        var metadata = wisdom.AddComponent<PassiveObjectMetadata_UniquePair>(); metadata.effectString.key = "PassiveEffect_UniquePair";
        var status = wisdom.AddComponent<PassiveObject_StatusInstance>();
        status.stats = new[] { "UNIQUE_PAIR/1", "BOSS_REWARD_DICE/1" };
        PassiveDatabase.data = new System.Collections.Generic.Dictionary<ulong, PassiveEntity> { [8] = new PassiveEntity { lv10PerkPrefab = wisdom } };
        WisdomRewardDefinition.Apply(); WisdomRewardDefinition.Apply();
        Check(status.stats.Length == 1 && status.stats[0] == "BOSS_REWARD_DICE/2", "legacy unique pair removed; only two boss dice remain");
        second.bossDiceStat = 2;
        Die(new UnitAvatar { monsterType = EMonsterType.Miniboss });
        Check(second.diceRequests == 3, "miniboss reward remains 1 when native boss stat is 2");
        Check((bool)Call(typeof(EnablePatch), "Prefix", status, first), "wisdom native enable preserved");
        Check((bool)Call(typeof(DisablePatch), "Prefix", status), "wisdom native disable preserved");
        object[] description = { metadata, "original unique pair and boss dice" };
        Check(!(bool)Call(typeof(DescriptionPatch), "Prefix", description), "wisdom description skips native branch");
        string once = (string)description[1];
        Check(!(bool)Call(typeof(DescriptionPatch), "Prefix", description), "wisdom description skips native branch");
        Check(once == "击败迷你Boss时获得1个骰子，击败Boss时获得2个骰子。" && once == (string)description[1], "description fully replaced without legacy or duplicate wording");
        Console.WriteLine($"PASS {checks} isolated behavior checks against actual Plugin.cs. Not a Unity/Harmony runtime test.");
    }
}
