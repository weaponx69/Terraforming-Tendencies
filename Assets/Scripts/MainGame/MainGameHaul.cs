using System;
using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.MainGame
{
    /// <summary>
    /// Haul distance is hexes to the nearest finished building (Command Post at the start,
    /// then any closer building that can stage materials).
    /// </summary>
    public static class MainGameHaul
    {
        public static int HexesFromNearestSupply(int tile)
        {
            var ctrl = MainGameController.Instance;
            var colony = ctrl?.Colony;
            if (colony == null) return 0;

            int best = int.MaxValue;
            foreach (var t in colony.Board.Tiles)
            {
                if (t.Index == tile || t.Building == MainGameBuildingId.None || t.Broken) continue;
                best = Math.Min(best, colony.Dist(tile, t.Index));
            }

            foreach (var building in BaseBuilding.ActiveBuildings)
            {
                if (building == null) continue;
                if (building.Progress.State != BuildingProgress.BuildingState.Completed) continue;
                if (!ctrl.TryGetTileIndex(building.transform.position, out int other) || other == tile) continue;
                best = Math.Min(best, colony.Dist(tile, other));
            }

            return best == int.MaxValue ? 0 : best;
        }
    }
}
