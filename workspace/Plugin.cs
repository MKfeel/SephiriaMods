using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace SephiriaBondArtifactWishes
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInProcess("Sephiria.exe")]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.codex.sephiria.bondartifactwishes";
        public const string PluginName = "Sephiria Bond Artifact Wishes";
        public const string PluginVersion = "0.2.0";

        internal static ConfigEntry<int> BondArtifactCost { get; private set; }
        internal static ManualLogSource ModLogger { get; private set; }

        private Harmony _harmony;

        private void Awake()
        {
            ModLogger = Logger;
            BondArtifactCost = Config.Bind(
                "WishingFountain",
                "BondArtifactCost",
                9,
                new ConfigDescription(
                    "Cost of each unlocked bond artifact (ItemEntity.isDual) in the Wishing Fountain.",
                    new AcceptableValueRange<int>(0, 999)));

            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll(typeof(Plugin).Assembly);

            Logger.LogInfo($"{PluginName} {PluginVersion} loaded. Bond artifact cost: {BondArtifactCost.Value}.");
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
        }
    }
}
