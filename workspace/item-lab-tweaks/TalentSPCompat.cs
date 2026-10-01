using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace SephiriaItemLabTweaks
{
    internal static class TalentSPCompat
    {
        private static Harmony harmony;
        private static bool finished;
        private static float nextCheck;

        internal static void Initialize(Harmony owner)
        {
            harmony = owner;
            finished = false;
            nextCheck = 0;
            TryInstall();
        }

        internal static void Stop() { harmony = null; finished = true; }

        internal static void Poll()
        {
            if (finished || Time.unscaledTime < nextCheck) return;
            nextCheck = Time.unscaledTime + 1f;
            TryInstall();
        }

        internal static void TryInstall()
        {
            if (harmony == null || finished) return;
            var type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("SPMod.StarSephiriaPatches+PlayerAvatar_Patches", false))
                .FirstOrDefault(t => t != null);
            if (type == null) return;
            finished = true;
            try
            {
                var target = AccessTools.Method(type, "UpdateMaxPassivePoint", new[] { typeof(PlayerAvatar) });
                if (target == null) throw new MissingMethodException(type.FullName, "UpdateMaxPassivePoint");
                harmony.Patch(target, transpiler: new HarmonyMethod(typeof(TalentSPCompat), nameof(Transpiler)));
                Plugin.ModLogger.LogInfo("SPMod 天赋兼容已启用：上限 = SPMod 原上限 + Item Lab 额外点数。");
            }
            catch (Exception ex) { Plugin.ModLogger.LogError("SPMod 天赋兼容安装失败：" + ex); }
        }

        internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.ToList();
            var setter = AccessTools.PropertySetter(typeof(PlayerAvatar), "NetworkmaxPassivePoint");
            var matches = code.Where(i => i.Calls(setter)).ToList();
            if (matches.Count != 1) throw new InvalidOperationException("预期一个天赋上限写入点，实际 " + matches.Count);
            var original = matches[0];
            var loadPlayer = new CodeInstruction(OpCodes.Ldarg_0);
            loadPlayer.labels.AddRange(original.labels);
            loadPlayer.blocks.AddRange(original.blocks);
            original.labels.Clear();
            original.blocks.Clear();
            code.InsertRange(code.IndexOf(original), new[] {
                loadPlayer,
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(TalentService), nameof(TalentService.CombineSPModLimit)))
            });
            return code;
        }
    }

    [HarmonyPatch(typeof(AddOnLoader), "LoadAll")]
    internal static class TalentAddOnsLoadedPatch
    {
        private static void Postfix() => TalentSPCompat.TryInstall();
    }
}
