$ErrorActionPreference = 'Stop'
# Compile the real point service against small host/config doubles. No game is started.
$sample = Join-Path $PSScriptRoot ('.talent-test-' + [guid]::NewGuid().ToString('N') + '.cs')
try {
    @'
using System;
using System.Collections.Generic;
using System.Reflection;
namespace BepInEx.Configuration {
    public class ConfigEntry<T> { public T Value; }
    public class ConfigDescription { public ConfigDescription(string s, object r) {} }
    public class AcceptableValueRange<T> { public AcceptableValueRange(T a, T b) {} }
    public class ConfigFile {
        private Dictionary<string,object> entries = new Dictionary<string,object>();
        public ConfigEntry<T> Bind<T>(string s,string k,T value,object description) {
            string key=s+"/"+k;
            if (!entries.ContainsKey(key)) entries[key]=new ConfigEntry<T>{Value=value};
            return (ConfigEntry<T>)entries[key];
        }
        public void Save() {}
    }
}
namespace HarmonyLib {
    public class HarmonyPatch : Attribute { public HarmonyPatch(Type t,string n) {} }
    public static class AccessTools {
        public static MethodInfo Method(Type t,string n) { return t.GetMethod(n,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic); }
    }
}
namespace Mirror {
    public static class NetworkServer { public static bool active; public static object localConnection; }
}
namespace UnityEngine {
    public enum FindObjectsSortMode { None }
    public static class Object {
        public static PlayerAvatar[] Players;
        public static T[] FindObjectsByType<T>(FindObjectsSortMode mode) { return (T[])(object)Players; }
    }
}
public class PlayerAvatar {
    public bool isLocalPlayer;
    public object connectionToClient;
    public int maxPassivePoint=50;
    public int NetworkmaxPassivePoint { get { return maxPassivePoint; } set { maxPassivePoint=value; } }
    public Dictionary<ulong,int> passiveStats=new Dictionary<ulong,int>();
    public int Resets;
    private void UserCode_CmdResetPassiveStat() { Resets++; passiveStats.Clear(); }
    public void SavePassiveStat(bool ignored) {}
}
namespace SephiriaItemLabTweaks {
    internal static class GameAccess {
        public static bool Allowed=true;
        internal static bool TryGetLocalContext(out object a,out object b,out string error) { a=b=null; error="denied"; return Allowed; }
    }
    internal static class Plugin { internal static Logger ModLogger=new Logger(); }
    internal class Logger { public void LogInfo(string s) {} }
    public static class TalentTests {
        private static int checks;
        private static void Check(bool value,string name) { if(!value) throw new Exception(name); checks++; }
        public static string Run() {
            var cfg=new BepInEx.Configuration.ConfigFile();
            TalentService.Initialize(cfg);
            Mirror.NetworkServer.active=true;
            var player=new PlayerAvatar { isLocalPlayer=true };
            UnityEngine.Object.Players=new[]{player};
            TalentService.Extra.Value=90;
            TalentService.Apply(player);
            TalentService.Apply(player);
            Check(player.maxPassivePoint==140,"repeated apply adds 90 once");
            TalentService.Save(90,false);
            TalentService.Save(90,false);
            Check(player.maxPassivePoint==140 && player.Resets==0,"repeated save preserves allocation");
            player.passiveStats[1]=130;
            TalentService.Save(10,false);
            Check(player.maxPassivePoint==60 && player.Resets==1 && player.passiveStats.Count==0,"lowering below allocation resets");
            TalentService.Save(30,false);
            Check(player.maxPassivePoint==80 && player.Resets==1,"increasing total preserves base");
            TalentService.Save(0,true);
            Check(player.maxPassivePoint==50 && TalentService.Extra.Value==0 && player.Resets==2,"explicit reset");
            TalentService.Save(-1,false); TalentService.Save(10000,false);
            Check(player.maxPassivePoint==50 && TalentService.Extra.Value==0,"invalid values do not mutate");
            GameAccess.Allowed=false;
            TalentService.Save(90,false);
            Check(TalentService.Extra.Value==0,"session restriction enforced inside service");
            GameAccess.Allowed=true;
            TalentService.Extra.Value=90;
            player.NetworkmaxPassivePoint=TalentService.CombineSPModLimit(65,player);
            player.NetworkmaxPassivePoint=TalentService.CombineSPModLimit(65,player);
            Check(player.maxPassivePoint==155,"SPMod fresh base is not cumulative");
            TalentService.Save(100,false);
            Check(player.maxPassivePoint==165,"save after SPMod keeps SPMod bonus");
            player.NetworkmaxPassivePoint=TalentService.CombineSPModLimit(70,player);
            Check(player.maxPassivePoint==170,"SPMod base change preserves extras");
            var remote=new PlayerAvatar();
            TalentService.Apply(remote);
            Check(remote.maxPassivePoint==50 && TalentService.CombineSPModLimit(65,remote)==65,"remote player unchanged");
            Mirror.NetworkServer.active=false;
            TalentService.Apply(player);
            Check(player.maxPassivePoint==170 && TalentService.CombineSPModLimit(65,player)==65,"client cannot apply");
            Mirror.NetworkServer.active=true;
            var next=new PlayerAvatar { isLocalPlayer=true };
            TalentService.BeforeLoad(next); TalentService.BeforeLoad(next);
            Check(next.maxPassivePoint==150,"new character gets extras once before saved allocation");
            cfg.Bind("Internal","ResetOnNextLoad",false,"").Value=true;
            next.passiveStats[1]=20;
            Check(!TalentService.BeforeLoad(next) && next.Resets==1 && next.passiveStats.Count==0,"migrated pending reset skips saved allocation");
            Check(TalentService.BeforeLoad(next) && next.Resets==1,"pending reset consumed once");
            return "PASS: "+checks+" point-service checks (isolated doubles, not game runtime).";
        }
    }
}
'@ | Set-Content -LiteralPath $sample -Encoding utf8
    Add-Type -Path @($sample, (Join-Path $PSScriptRoot 'TalentService.cs'))
    [SephiriaItemLabTweaks.TalentTests]::Run()
} finally {
    if (Test-Path -LiteralPath $sample) { Remove-Item -LiteralPath $sample -Force }
}
