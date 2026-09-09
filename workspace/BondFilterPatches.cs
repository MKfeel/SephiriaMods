using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace SephiriaBondArtifactWishes
{
    internal static class BondFilter
    {
        internal const string FilterData = "__BOND_ARTIFACT__";

        private static readonly Dictionary<int, UI_JournalPanel_SearchOptionButton> Buttons =
            new Dictionary<int, UI_JournalPanel_SearchOptionButton>();

        private static readonly FieldInfo RarityButtonsField =
            AccessTools.Field(typeof(UI_DimensionPocketPanel), "rarityOptionButtons");

        internal static void CreateButton(UI_DimensionPocketPanel panel)
        {
            int panelId = panel.GetInstanceID();
            if (Buttons.TryGetValue(panelId, out UI_JournalPanel_SearchOptionButton existing) && existing != null)
            {
                return;
            }

            UI_JournalPanel_SearchOptionButton button = Object.Instantiate(
                panel.searchOptionButtonPrefab,
                panel.searchOptionButtonContainer);

            button.name = "BondArtifactFilterButton";
            button.Initialize(
                GetLocalizedLabel(),
                FilterData,
                panel.SelectRarity,
                new Color32(204, 66, 171, 255),
                null);

            var rarityButtons = RarityButtonsField?.GetValue(panel) as List<UI_JournalPanel_SearchOptionButton>;
            if (rarityButtons != null && rarityButtons.Count > 0)
            {
                int insertIndex = rarityButtons[rarityButtons.Count - 1].transform.GetSiblingIndex() + 1;
                button.transform.SetSiblingIndex(insertIndex);
            }

            Buttons[panelId] = button;
        }

        internal static void UpdateButtonLabel(UI_DimensionPocketPanel panel)
        {
            if (Buttons.TryGetValue(panel.GetInstanceID(), out UI_JournalPanel_SearchOptionButton button)
                && button != null)
            {
                button.UpdateShowText(GetLocalizedLabel());
            }
        }

        private static string GetLocalizedLabel()
        {
            return new LocalizedString("ItemRarity_Dual").ToString();
        }
    }

    [HarmonyPatch(typeof(UI_DimensionPocketPanel), "Awake")]
    internal static class CreateBondFilterButtonPatch
    {
        [HarmonyPostfix]
        private static void Postfix(UI_DimensionPocketPanel __instance)
        {
            BondFilter.CreateButton(__instance);
        }
    }

    [HarmonyPatch(typeof(UI_DimensionPocketPanel), "HandleLanguageChanged")]
    internal static class LocalizeBondFilterButtonPatch
    {
        [HarmonyPostfix]
        private static void Postfix(UI_DimensionPocketPanel __instance)
        {
            BondFilter.UpdateButtonLabel(__instance);
        }
    }

    [HarmonyPatch(typeof(UI_DimensionPocketPanel), "ApplySearchOption")]
    internal static class ApplyBondFilterPatch
    {
        private static readonly FieldInfo IconsField =
            AccessTools.Field(typeof(UI_DimensionPocketPanel), "icons");
        private static readonly FieldInfo SelectedRarityField =
            AccessTools.Field(typeof(UI_DimensionPocketPanel), "selectedRarity");
        private static readonly FieldInfo SelectedTagField =
            AccessTools.Field(typeof(UI_DimensionPocketPanel), "selectedTag");
        private static readonly FieldInfo FavoriteModeField =
            AccessTools.Field(typeof(UI_DimensionPocketPanel), "isFavoriteItemMode");
        private static readonly MethodInfo IsItemFavoriteMethod =
            AccessTools.Method(typeof(UI_DimensionPocketPanel), "IsItemFavorite");
        private static readonly MethodInfo SortingIconsMethod =
            AccessTools.Method(typeof(UI_DimensionPocketPanel), "SortingIcons");

        [HarmonyPrefix]
        private static bool Prefix(UI_DimensionPocketPanel __instance)
        {
            string selectedRarity = SelectedRarityField?.GetValue(__instance) as string;
            if (selectedRarity != BondFilter.FilterData)
            {
                return true;
            }

            var icons = IconsField?.GetValue(__instance) as List<UI_DimensionVaultItemIcon>;
            string selectedTag = SelectedTagField?.GetValue(__instance) as string ?? string.Empty;
            bool favoriteMode = FavoriteModeField != null && (bool)FavoriteModeField.GetValue(__instance);

            if (icons != null)
            {
                foreach (UI_DimensionVaultItemIcon icon in icons)
                {
                    if (icon == null || icon.itemIcon == null)
                    {
                        continue;
                    }

                    ItemEntity item = ItemDatabase.FindItemById(icon.itemIcon.Item.entityID);
                    bool show = item != null && item.isDual;

                    if (show && favoriteMode)
                    {
                        show = IsItemFavoriteMethod != null
                            && (bool)IsItemFavoriteMethod.Invoke(__instance, new object[] { item.id });
                    }

                    if (show && selectedTag.Length > 0)
                    {
                        show = selectedTag == "NONE"
                            ? item.categories == null || item.categories.Count == 0
                            : item.categories != null && item.categories.Contains(selectedTag);
                    }

                    icon.gameObject.SetActive(show);
                }
            }

            SortingIconsMethod?.Invoke(__instance, null);
            return false;
        }
    }
}
