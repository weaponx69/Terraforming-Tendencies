using System.Collections.Generic;
using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.Prologue
{
    /// <summary>Prologue placement checks — one scene scan per invalidation, not per ghost rebuild.</summary>
    public static class PrologueBuildingRegistry
    {
        static readonly List<BaseBuilding> Cached = new(24);
        static bool valid;

        public static void Invalidate() => valid = false;

        public static IReadOnlyList<BaseBuilding> PlacedBuildings
        {
            get
            {
                if (!valid) Rebuild();
                return Cached;
            }
        }

        static void Rebuild()
        {
            Cached.Clear();
            var all = BaseBuilding.ActiveBuildings;
            if (all != null)
            {
                for (int i = 0; i < all.Count; i++)
                {
                    var building = all[i];
                    if (building == null) continue;
                    if (building.name.IndexOf("RevealGhost", System.StringComparison.OrdinalIgnoreCase) >= 0)
                        continue;
                    Cached.Add(building);
                }
            }
            valid = true;
        }
    }
}