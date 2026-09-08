using System.Collections.Generic;
using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.Player
{
    /// <summary>
    /// Combolands-style square tile grid for card placement.
    /// Buildings occupy one cell; orthogonal neighbors = joined tiles.
    /// </summary>
    public static class ColonyTileGrid
    {
        public const float TileSize = 12f;

        /// <summary>Stay on the current snap cell until the cursor clearly enters another.</summary>
        public const float StickRadius = TileSize * 0.62f;

        /// <summary>Extra distance the new cell must beat before we leave the sticky cell.</summary>
        public const float StickHysteresis = TileSize * 0.28f;

        private static readonly Vector2Int[] Ortho =
        {
            new Vector2Int(1, 0),
            new Vector2Int(-1, 0),
            new Vector2Int(0, 1),
            new Vector2Int(0, -1),
        };

        public static Vector2Int WorldToCell(Vector3 world)
        {
            return new Vector2Int(
                Mathf.RoundToInt(world.x / TileSize),
                Mathf.RoundToInt(world.z / TileSize));
        }

        public static Vector3 CellToWorld(Vector2Int cell, float y)
        {
            return new Vector3(cell.x * TileSize, y, cell.y * TileSize);
        }

        public static Vector3 SnapToTile(Vector3 world)
        {
            Vector2Int cell = WorldToCell(world);
            return CellToWorld(cell, world.y);
        }

        public static float HorizontalDistSq(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return dx * dx + dz * dz;
        }

        /// <summary>
        /// Snap to the cell under the cursor. Occupied cells resolve to the nearest empty
        /// edge. Optional sticky cell prevents flickering between neighbors.
        /// </summary>
        public static Vector3 SnapForPlacement(
            Vector3 rawWorld,
            Owner owner,
            ref Vector2Int? stickyCell,
            out int joinCount)
        {
            joinCount = 0;
            HashSet<Vector2Int> occupied = GetOccupiedCells(owner);
            Vector2Int desired = ResolveDesiredCell(rawWorld, occupied);

            Vector2Int chosen = desired;
            if (stickyCell.HasValue)
            {
                Vector2Int sticky = stickyCell.Value;
                bool stickyValid = !occupied.Contains(sticky);
                if (stickyValid)
                {
                    Vector3 stickyWorld = CellToWorld(sticky, rawWorld.y);
                    float distStickySq = HorizontalDistSq(rawWorld, stickyWorld);
                    float stickRadiusSq = StickRadius * StickRadius;

                    if (distStickySq <= stickRadiusSq)
                    {
                        chosen = sticky;
                    }
                    else if (sticky == desired)
                    {
                        chosen = sticky;
                    }
                    else
                    {
                        Vector3 desiredWorld = CellToWorld(desired, rawWorld.y);
                        float distDesiredSq = HorizontalDistSq(rawWorld, desiredWorld);
                        float hyst = StickHysteresis;
                        // Only leave sticky when the cursor is clearly closer to desired.
                        if (distDesiredSq + hyst * hyst < distStickySq)
                            chosen = desired;
                        else
                            chosen = sticky;
                    }
                }
            }

            if (occupied.Contains(chosen))
                chosen = ResolveDesiredCell(rawWorld, occupied);

            stickyCell = chosen;
            joinCount = CountOrthogonalNeighbors(chosen, occupied);
            return CellToWorld(chosen, rawWorld.y);
        }

        /// <summary>Stateless snap for final place / CanHandle (no hysteresis).</summary>
        public static Vector3 SnapForPlacement(Vector3 rawWorld, Owner owner, out int joinCount)
        {
            Vector2Int? sticky = null;
            return SnapForPlacement(rawWorld, owner, ref sticky, out joinCount);
        }

        private static Vector2Int ResolveDesiredCell(Vector3 rawWorld, HashSet<Vector2Int> occupied)
        {
            Vector2Int rawCell = WorldToCell(rawWorld);
            if (!occupied.Contains(rawCell))
                return rawCell;

            // Cursor is over an existing tile — pick the empty ortho edge closest to the cursor.
            Vector2Int best = rawCell;
            float bestDistSq = float.MaxValue;
            bool found = false;
            for (int i = 0; i < Ortho.Length; i++)
            {
                Vector2Int candidate = rawCell + Ortho[i];
                if (occupied.Contains(candidate)) continue;

                Vector3 candWorld = CellToWorld(candidate, rawWorld.y);
                float distSq = HorizontalDistSq(rawWorld, candWorld);
                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    best = candidate;
                    found = true;
                }
            }

            if (found) return best;

            // Completely boxed in — search a small ring for any empty cell.
            for (int r = 1; r <= 3; r++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    for (int dz = -r; dz <= r; dz++)
                    {
                        if (Mathf.Abs(dx) != r && Mathf.Abs(dz) != r) continue;
                        Vector2Int candidate = new Vector2Int(rawCell.x + dx, rawCell.y + dz);
                        if (occupied.Contains(candidate)) continue;
                        Vector3 candWorld = CellToWorld(candidate, rawWorld.y);
                        float distSq = HorizontalDistSq(rawWorld, candWorld);
                        if (distSq < bestDistSq)
                        {
                            bestDistSq = distSq;
                            best = candidate;
                            found = true;
                        }
                    }
                }
                if (found) return best;
            }

            return rawCell;
        }

        public static HashSet<Vector2Int> GetOccupiedCells(Owner owner)
        {
            var set = new HashSet<Vector2Int>();
            foreach (var b in BaseBuilding.ActiveBuildings)
            {
                if (b == null || b.Owner != owner) continue;
                if (!b.enabled) continue; // placement ghosts are disabled
                if (b.name.StartsWith("Ghost_", System.StringComparison.Ordinal)
                    || b.name.StartsWith("GhostPreview_", System.StringComparison.Ordinal))
                    continue;
                var state = b.Progress.State;
                if (state != BuildingProgress.BuildingState.Completed
                    && state != BuildingProgress.BuildingState.Building
                    && state != BuildingProgress.BuildingState.Paused)
                    continue;
                set.Add(WorldToCell(b.transform.position));
            }
            return set;
        }

        public static int CountOrthogonalNeighbors(Vector2Int cell, Owner owner)
        {
            return CountOrthogonalNeighbors(cell, GetOccupiedCells(owner));
        }

        public static int CountOrthogonalNeighbors(Vector2Int cell, HashSet<Vector2Int> occupied)
        {
            int n = 0;
            for (int i = 0; i < Ortho.Length; i++)
            {
                if (occupied.Contains(cell + Ortho[i])) n++;
            }
            return n;
        }

        public static void CollectOrthogonalNeighborBuildings(
            Vector2Int cell, Owner owner, List<BaseBuilding> results)
        {
            results.Clear();
            var occupiedBuildings = new Dictionary<Vector2Int, BaseBuilding>();
            foreach (var b in BaseBuilding.ActiveBuildings)
            {
                if (b == null || b.Owner != owner) continue;
                if (!b.enabled) continue;
                if (b.name.StartsWith("Ghost_", System.StringComparison.Ordinal)
                    || b.name.StartsWith("GhostPreview_", System.StringComparison.Ordinal))
                    continue;
                var state = b.Progress.State;
                if (state != BuildingProgress.BuildingState.Completed
                    && state != BuildingProgress.BuildingState.Building)
                    continue;
                occupiedBuildings[WorldToCell(b.transform.position)] = b;
            }

            for (int i = 0; i < Ortho.Length; i++)
            {
                if (occupiedBuildings.TryGetValue(cell + Ortho[i], out var neighbor))
                    results.Add(neighbor);
            }
        }

        /// <summary>True when two world positions share an edge on the tile grid.</summary>
        public static bool AreOrthogonalNeighbors(Vector3 a, Vector3 b)
        {
            Vector2Int ca = WorldToCell(a);
            Vector2Int cb = WorldToCell(b);
            int dx = Mathf.Abs(ca.x - cb.x);
            int dz = Mathf.Abs(ca.y - cb.y);
            return (dx + dz) == 1;
        }
    }
}
