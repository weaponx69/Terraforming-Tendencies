using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.Player
{
    /// <summary>One resolution path for hand/draft card art (building icons + card sprites).</summary>
    public static class BlueprintCardIcon
    {
        public static Sprite Resolve(BlueprintCardSO card)
        {
            if (card == null) return null;

            if (card.icon != null) return card.icon;

            if (card is UnlockBuildingCardSO unlock && unlock.buildingToUnlock != null)
            {
                var fromBuilding = BuildingHandIconResolver.Resolve(unlock.buildingToUnlock);
                if (fromBuilding != null) return fromBuilding;
            }

            if (card is SpawnUnitCardSO spawn && spawn.unitPrefab != null)
            {
                var unit = spawn.unitPrefab.GetComponent<AbstractUnit>();
                if (unit != null && unit.Icon != null) return unit.Icon;
            }

            return null;
        }
    }
}