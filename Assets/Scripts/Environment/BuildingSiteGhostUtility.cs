using GameDevTV.RTS.Commands;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.Environment
{
    /// <summary>
    /// Resolves building ghosts for reserved site markers.
    /// </summary>
    public static class BuildingSiteGhostUtility
    {
        public static BuildingSO ResolveBuildingForSite(BuildingSiteSlot site, BuildingSO previewBuilding = null)
        {
            if (previewBuilding != null)
            {
                return previewBuilding;
            }

            if (site == null) return null;

            return site.Kind switch
            {
                BuildingSiteKind.CommandPost => BlueprintDraftManager.GetBuildingSOByName("Command Post"),
                BuildingSiteKind.Solar => BlueprintDraftManager.GetBuildingSOByName("Solar Panel"),
                BuildingSiteKind.Mine => ResolveMineBuilding(site),
                // Default paired-pad preview is Oxygen Processor — the bootstrap consumer building.
                BuildingSiteKind.PairedBuilding => BlueprintDraftManager.GetBuildingSOByName("Oxygen Processor"),
                _ => null
            };
        }

        public static GameObject GetGhostPrefab(BuildingSO building)
        {
            if (building == null) return null;

            foreach (var template in Resources.FindObjectsOfTypeAll<BuildBuildingCommand>())
            {
                if (template?.Building != null &&
                    template.Building.Name == building.Name &&
                    template.GhostPrefab != null)
                {
                    return template.GhostPrefab;
                }
            }

            return building.Prefab;
        }

        private static BuildingSO ResolveMineBuilding(BuildingSiteSlot site)
        {
            string buildingName = site.LinkedResourceType switch
            {
                SectorNode.NodeType.Gas => "Gas Well",
                SectorNode.NodeType.Iron => "Iron Ore Extractor",
                SectorNode.NodeType.Regolith => "Basalt Strip-Mine",
                _ => "Deep-Core Mining Laser"
            };

            string cardPath = site.LinkedResourceType switch
            {
                SectorNode.NodeType.Gas => "Cards/GasWellCard",
                SectorNode.NodeType.Iron => "Cards/IronOreExtractorCard",
                SectorNode.NodeType.Regolith => "Cards/BasaltStripMineCard",
                _ => "Cards/DeepCoreMiningLaserCard"
            };

            BuildingSO building = BlueprintDraftManager.GetBuildingSOByName(buildingName);
            if (building == null)
            {
                var card = Resources.Load<UnlockBuildingCardSO>(cardPath);
                if (card?.buildingToUnlock != null)
                {
                    BlueprintDraftManager.RegisterBuildingSO(card.buildingToUnlock);
                    building = card.buildingToUnlock;
                }
            }

            return building
                ?? BlueprintDraftManager.GetBuildingSOByName("Deep-Core Mining Laser")
                ?? BlueprintDraftManager.GetBuildingSOByName("Deep Core Mining Laser");
        }
    }
}
