using System.Collections.Generic;
using GameDevTV.RTS.Environment;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.Utilities;
using UnityEngine;

namespace GameDevTV.RTS.MainGame
{
    /// <summary>Scrap a placed prologue building — Materials refund, board + world stay in sync.</summary>
    public static class MainGameDemolish
    {
        public static int EstimateRefund(BaseBuilding building)
        {
            if (building == null || building.ResolvedBuildingSO == null) return 0;
            int cost = MineralCostFor(building.ResolvedBuildingSO);
            if (cost <= 0) return 0;
            float rate = building.Progress.State == BuildingProgress.BuildingState.Building ? 0.75f : 0.5f;
            return Mathf.Max(0, Mathf.RoundToInt(cost * rate));
        }

        public static bool TryDemolish(BaseBuilding building, bool refund = true)
        {
            if (!MainGameController.IsActive || building == null || building.Owner != Owner.Player1) return false;
            if (building.Progress.State == BuildingProgress.BuildingState.Destroyed) return false;

            var ctrl = MainGameController.Instance;
            if (ctrl?.Colony == null) return false;
            if (!ctrl.TryGetTileIndex(building.transform.position, out int tile)) return false;

            if (!MainGameCardMap.TryGetMainGameId(building.ResolvedBuildingSO, out var id)
                && !TryIdFromColonyTile(ctrl.Colony, tile, out id))
                return false;

            bool underConstruction = building.Progress.State == BuildingProgress.BuildingState.Building
                                     || SiteOnTile(ctrl.Colony, tile) != null;
            if (!ctrl.Colony.TryDemolishTile(tile, id, underConstruction, refund, out int minerals, out string reason))
            {
                if (!string.IsNullOrEmpty(reason))
                    GameDevTV.RTS.UI.MainGameHUD.Instance?.ShowNote(new List<string> { reason });
                return false;
            }

            string label = building.ResolvedBuildingSO != null && !string.IsNullOrEmpty(building.ResolvedBuildingSO.Name)
                ? building.ResolvedBuildingSO.Name
                : building.name;
            var note = new List<string> { $"Demolished {label}." };
            if (refund && minerals > 0)
                note.Add($"+{minerals} materials (have {ctrl.Colony.GetStock(MainGameResourceId.Minerals)}).");
            GameDevTV.RTS.UI.MainGameHUD.Instance?.ShowNote(note);
            GameDevTV.RTS.UI.MainGameHUD.Instance?.Refresh(ctrl.Colony);
            ctrl.RefreshHandUnlocks();
            MainGameBuildingRegistry.Invalidate();

            building.Die();
            return true;
        }

        static bool TryIdFromColonyTile(MainGameColony colony, int tile, out MainGameBuildingId id)
        {
            id = MainGameBuildingId.None;
            if (tile < 0 || tile >= colony.Board.Tiles.Count) return false;
            var site = SiteOnTile(colony, tile);
            if (site != null)
            {
                id = site.Id;
                return id != MainGameBuildingId.None;
            }
            id = colony.Board.At(tile).Building;
            return id != MainGameBuildingId.None;
        }

        static MainGameSite SiteOnTile(MainGameColony colony, int tile)
        {
            foreach (var s in colony.Board.Sites)
                if (s.Tile == tile) return s;
            return null;
        }

        static int MineralCostFor(BuildingSO def)
        {
            if (def == null) return 0;
            if (BuildingSiteRegistry.IsMineBuilding(def))
                return MainGameData.MineMineralCost;
            if (!MainGameCardMap.TryGetMainGameId(def, out var id) || id == MainGameBuildingId.CommandPost)
                return 0;
            return MainGameData.MineralCost(id);
        }
    }
}