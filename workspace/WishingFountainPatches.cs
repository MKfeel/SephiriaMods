using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace SephiriaBondArtifactWishes
{
    [HarmonyPatch(typeof(UI_DimensionPocketPanel), nameof(UI_DimensionPocketPanel.Open))]
    internal static class AddBondArtifactsToWishPoolPatch
    {
        [HarmonyPrefix]
        private static void Prefix(
            PlayerAvatar playerAvatar,
            int cachedDimensionPocketStorage,
            List<ItemMetadata> cachedDimensionPocketItem)
        {
            if (playerAvatar == null || playerAvatar.spawner == null || cachedDimensionPocketItem == null)
            {
                return;
            }

            var knownItemIds = new HashSet<int>();
            foreach (ItemMetadata item in cachedDimensionPocketItem)
            {
                knownItemIds.Add(item.entityID);
            }

            int addedCount = 0;
            foreach (int itemId in playerAvatar.spawner.unlockedCharms)
            {
                if (knownItemIds.Contains(itemId))
                {
                    continue;
                }

                ItemEntity item = ItemDatabase.FindItemById(itemId);
                if (item == null || !item.isDual || item.type != EItemType.Charm)
                {
                    continue;
                }

                if (WishCost.GetItemCost(item) > cachedDimensionPocketStorage)
                {
                    continue;
                }

                cachedDimensionPocketItem.Add(new ItemMetadata(-1, itemId, 1));
                knownItemIds.Add(itemId);
                addedCount++;
            }

            Plugin.ModLogger.LogDebug($"Added {addedCount} unlocked bond artifact(s) to the Wishing Fountain pool.");
        }
    }

    [HarmonyPatch]
    internal static class UseBondArtifactWishCostPatch
    {
        private static readonly FieldInfo RarityField = AccessTools.Field(typeof(ItemEntity), nameof(ItemEntity.rarity));
        private static readonly FieldInfo CannotBeRewardField = AccessTools.Field(typeof(ItemEntity), nameof(ItemEntity.cannotBeReward));
        private static readonly MethodInfo VanillaGetCapacityMethod = AccessTools.Method(
            typeof(UI_DimensionPocketPanel),
            nameof(UI_DimensionPocketPanel.GetCapacity),
            new[] { typeof(EItemRarity) });
        private static readonly MethodInfo SetCostMethod = AccessTools.Method(
            typeof(UI_DimensionVaultItemIcon),
            nameof(UI_DimensionVaultItemIcon.SetCost),
            new[] { typeof(EItemRarity) });
        private static readonly MethodInfo GetItemCostMethod = AccessTools.Method(typeof(WishCost), nameof(WishCost.GetItemCost));
        private static readonly MethodInfo IsRewardBlockedMethod = AccessTools.Method(typeof(WishCost), nameof(WishCost.IsRewardBlocked));
        private static readonly MethodInfo GetRarityForVisibilityMethod = AccessTools.Method(typeof(WishCost), nameof(WishCost.GetRarityForVisibility));
        private static readonly MethodInfo SetIconCostMethod = AccessTools.Method(typeof(WishCost), nameof(WishCost.SetIconCost));

        [HarmonyTargetMethods]
        private static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (MethodBase method in new MethodBase[]
            {
                AccessTools.Method(typeof(UI_DimensionPocketPanel), "OnOpened"),
                AccessTools.Method(typeof(UI_DimensionPocketPanel), "HandleItemClick"),
                AccessTools.Method(typeof(UI_DimensionPocketPanel), "UpdateCapacityText"),
                AccessTools.Method(typeof(UI_DimensionPocketPanel), "UpdateItemSelectable"),
                AccessTools.Method(typeof(UI_PresetPanel), "UpdateDimensionPocketInfo"),
                AccessTools.Method(typeof(PlayerSpawner), nameof(PlayerSpawner.AddDimensionPocketItemsOnServer))
            })
            {
                if (method != null)
                {
                    yield return method;
                }
            }
        }

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(
            IEnumerable<CodeInstruction> source,
            MethodBase __originalMethod)
        {
            var instructions = new List<CodeInstruction>(source);
            int costReplacements = 0;
            int iconReplacements = 0;
            int filterReplacements = 0;

            for (int index = 0; index < instructions.Count; index++)
            {
                CodeInstruction current = instructions[index];

                if (current.opcode == OpCodes.Ldfld && Equals(current.operand, CannotBeRewardField))
                {
                    current.opcode = OpCodes.Call;
                    current.operand = IsRewardBlockedMethod;
                    filterReplacements++;
                    continue;
                }

                if (current.opcode != OpCodes.Ldfld || !Equals(current.operand, RarityField) || index + 1 >= instructions.Count)
                {
                    continue;
                }

                CodeInstruction next = instructions[index + 1];
                if (next.Calls(VanillaGetCapacityMethod))
                {
                    current.opcode = OpCodes.Call;
                    current.operand = GetItemCostMethod;
                    next.opcode = OpCodes.Nop;
                    next.operand = null;
                    costReplacements++;
                    continue;
                }

                if (next.Calls(SetCostMethod))
                {
                    current.opcode = OpCodes.Nop;
                    current.operand = null;
                    next.opcode = OpCodes.Call;
                    next.operand = SetIconCostMethod;
                    iconReplacements++;
                    continue;
                }

                if (__originalMethod.DeclaringType == typeof(UI_DimensionPocketPanel)
                    && __originalMethod.Name == "OnOpened"
                    && LoadsInt32(next, (int)EItemRarity.Eternal))
                {
                    current.opcode = OpCodes.Call;
                    current.operand = GetRarityForVisibilityMethod;
                    filterReplacements++;
                }
            }

            Plugin.ModLogger.LogDebug(
                $"Patched {__originalMethod.DeclaringType?.Name}.{__originalMethod.Name}: "
                + $"cost={costReplacements}, icon={iconReplacements}, filter={filterReplacements}.");

            if (costReplacements == 0)
            {
                Plugin.ModLogger.LogWarning(
                    $"No wishing-cost call was found in {__originalMethod.DeclaringType?.Name}.{__originalMethod.Name}. "
                    + "The game may have updated and this part of the mod may not work.");
            }

            return instructions;
        }

        private static bool LoadsInt32(CodeInstruction instruction, int value)
        {
            switch (value)
            {
                case 0: return instruction.opcode == OpCodes.Ldc_I4_0;
                case 1: return instruction.opcode == OpCodes.Ldc_I4_1;
                case 2: return instruction.opcode == OpCodes.Ldc_I4_2;
                case 3: return instruction.opcode == OpCodes.Ldc_I4_3;
                case 4: return instruction.opcode == OpCodes.Ldc_I4_4;
                case 5: return instruction.opcode == OpCodes.Ldc_I4_5;
                case 6: return instruction.opcode == OpCodes.Ldc_I4_6;
                case 7: return instruction.opcode == OpCodes.Ldc_I4_7;
                case 8: return instruction.opcode == OpCodes.Ldc_I4_8;
                default:
                    return instruction.opcode == OpCodes.Ldc_I4 && Equals(instruction.operand, value);
            }
        }
    }
}
