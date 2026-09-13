using System;
using System.Collections.Generic;
using System.Reflection;

namespace UnityEngine
{
    public class Component
    {
        public GameObject gameObject = new GameObject();
        public T GetComponent<T>() where T : class => gameObject.GetComponent<T>();
    }
    public class MonoBehaviour : Component { }
    public class GameObject
    {
        public string name;
        private readonly List<Component> components = new List<Component>();
        public T AddComponent<T>() where T : Component, new()
        { var c = new T { gameObject = this }; components.Add(c); return c; }
        public T GetComponent<T>() where T : class
        { foreach (var c in components) if (c is T t) return t; return null; }
    }
    public static class Mathf
    {
        public static int Max(int a, int b) => Math.Max(a, b);
        public static int FloorToInt(float f) => (int)Math.Floor(f);
    }
}
namespace BepInEx
{
    public class BepInPlugin : Attribute { public BepInPlugin(string a, string b, string c) { } }
    public class BaseUnityPlugin : UnityEngine.MonoBehaviour { public Log Logger = new Log(); }
    public class Log { public void LogInfo(string s) { } }
}
namespace HarmonyLib
{
    public class HarmonyPatch : Attribute { public HarmonyPatch() { } public HarmonyPatch(Type t, string n) { } }
    public class Harmony { public Harmony(string s) { } public void PatchAll(Assembly a) { } }
    public static class AccessTools
    {
        public static MethodInfo Method(Type t, string name) => t.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        public static MethodInfo PropertySetter(Type t, string name) => t.GetProperty(name).SetMethod;
    }
}
namespace Mirror
{
    public class NetworkIdentity : UnityEngine.Component { }
    public class NetworkConnectionToClient { public NetworkIdentity identity; }
    public static class NetworkServer
    {
        public static bool active = true;
        public static Dictionary<int, NetworkConnectionToClient> connections = new Dictionary<int, NetworkConnectionToClient>();
    }
}
public enum ECustomStat { AllDamageBonus }
public enum EMonsterType { Normal, Miniboss, Boss, Dummy }
public class UnitAvatar : UnityEngine.Component
{
    public float NetworkmaxHp { get; set; } = 100;
    public float NetworkfinalMaxHp { get; set; }
    public sbyte NetworkisHPCursed { get; set; }
    public int NetworkcursedMaxHp { get; set; }
    public float MaxHp => NetworkisHPCursed > 0 ? NetworkcursedMaxHp : NetworkmaxHp * (1 + NetworkfinalMaxHp / 100f);
    public bool IsDead;
    public EMonsterType monsterType;
    public void Die() { IsDead = true; }
}
public class PlayerAvatar : UnitAvatar
{
    public int damageBonus, bossDiceStat, diceRequests, statWrites;
    public void AddCustomStat(ECustomStat stat, int value) { damageBonus += value; statWrites++; }
    public int GetCustomStatUnsafe(string name) => name == "BOSSREWARDDICE" ? bossDiceStat : 0;
    public void RequestCreateDice(int amount, float force) { diceRequests += amount; }
}
public class LocalizedString { public string key; }
public class PassiveObject : UnityEngine.Component { }
public class PassiveObject_InventorySlot : PassiveObject
{
    protected void OnEffectEnabled(PlayerAvatar player, bool runtime) { }
    protected void OnEffectDisabled() { }
}
public class PassiveObject_StatusInstance : PassiveObject
{
    public string[] stats = Array.Empty<string>();
    protected void OnEffectEnabled(PlayerAvatar player, bool runtime) { }
    protected void OnEffectDisabled() { }
}
public class PassiveObjectMetadata : UnityEngine.Component
{
    public LocalizedString effectString = new LocalizedString();
    public virtual string GetEffectStringInner() => "native";
}
public class PassiveObjectMetadata_UniquePair : PassiveObjectMetadata
{ public override string GetEffectStringInner() => "native unique pair"; }
public class PassiveEntity { public UnityEngine.GameObject lv10PerkPrefab; }
public static class PassiveDatabase
{
    public static Dictionary<ulong, PassiveEntity> data;
    public static void Initialize() { }
}
