using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SephiriaBondArtifactWishes
{
    internal static class WishCost
    {
        private const int MaxCostPipsPerRow = 6;
        private static readonly Dictionary<int, Image> ExtraCostRows = new Dictionary<int, Image>();

        internal static int GetItemCost(ItemEntity item)
        {
            if (item == null)
            {
                return 0;
            }

            return item.isDual
                ? Plugin.BondArtifactCost.Value
                : UI_DimensionPocketPanel.GetCapacity(item.rarity);
        }

        internal static bool IsRewardBlocked(ItemEntity item)
        {
            return item != null && item.cannotBeReward && !item.isDual;
        }

        internal static EItemRarity GetRarityForVisibility(ItemEntity item)
        {
            if (item != null && item.isDual && item.rarity == EItemRarity.Eternal)
            {
                return EItemRarity.Common;
            }

            return item == null ? EItemRarity.Eternal : item.rarity;
        }

        internal static void SetIconCost(UI_DimensionVaultItemIcon icon, ItemEntity item)
        {
            if (icon == null || item == null)
            {
                return;
            }

            RemoveExtraCostRow(icon);
            icon.SetCost(item.rarity);
            if (!item.isDual || icon.costImage == null)
            {
                return;
            }

            int totalCost = GetItemCost(item);
            int lowerRowCost = Mathf.Min(totalCost, MaxCostPipsPerRow);
            int upperRowCost = Mathf.Max(0, totalCost - lowerRowCost);

            RectTransform lowerRow = icon.costImage.rectTransform;
            SetRowWidth(lowerRow, lowerRowCost);

            if (upperRowCost <= 0)
            {
                return;
            }

            Image upperImage = Object.Instantiate(icon.costImage, icon.costImage.transform.parent);
            upperImage.name = "BondArtifactCostUpperRow";
            upperImage.raycastTarget = false;
            upperImage.transform.SetSiblingIndex(icon.costImage.transform.GetSiblingIndex() + 1);

            RectTransform upperRow = upperImage.rectTransform;
            SetRowWidth(upperRow, upperRowCost);
            AlignUpperRowToLowerLeft(lowerRow, upperRow);

            ExtraCostRows[icon.GetInstanceID()] = upperImage;
        }

        private static void RemoveExtraCostRow(UI_DimensionVaultItemIcon icon)
        {
            int iconId = icon.GetInstanceID();
            if (ExtraCostRows.TryGetValue(iconId, out Image existingRow) && existingRow != null)
            {
                Object.Destroy(existingRow.gameObject);
            }

            ExtraCostRows.Remove(iconId);
        }

        private static void SetRowWidth(RectTransform row, int pipCount)
        {
            Vector2 size = row.sizeDelta;
            size.x = pipCount * 5 + 1;
            row.sizeDelta = size;
        }

        private static void AlignUpperRowToLowerLeft(RectTransform lowerRow, RectTransform upperRow)
        {
            float lowerLeft = lowerRow.anchoredPosition.x - lowerRow.pivot.x * lowerRow.sizeDelta.x;
            Vector2 upperPosition = upperRow.anchoredPosition;
            upperPosition.x = lowerLeft + upperRow.pivot.x * upperRow.sizeDelta.x;
            upperPosition.y = lowerRow.anchoredPosition.y + Mathf.Max(1f, lowerRow.sizeDelta.y - 1f);
            upperRow.anchoredPosition = upperPosition;
        }
    }
}
