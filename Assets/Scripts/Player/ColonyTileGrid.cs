using System.Collections.Generic;
using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.Player
{
    /// <summary>
    /// Combolands-style flat-topped hex grid for card placement.
    /// <see cref="TileSize"/> is center-to-vertex radius so one hex ≈ one building (~5 m across).
    /// </summary>
    public static class ColonyTileGrid
    {
        /// <summary>Hex outer radius (center → vertex). Flat-to-flat width = √3 × TileSize ≈ 4.3 m; vertex span = 2 × TileSize = 5 m.</summary>
        public const float TileSize = 2.5f;

        /// <summary>Horizontal span of a hex (vertex to vertex).</summary>
        public static float HexWidth => TileSize * 2f;

        /// <summary>Vertical span of a hex (flat to flat).</summary>
        public static float HexHeight => TileSize * Sqrt3;

        private const float Sqrt3 = 1.73205080757f;

        /// <summary>Stay on the current snap cell until the cursor clearly enters another.</summary>
        public const float StickRadius = TileSize * 0.85f;

        /// <summary>Extra distance the new cell must beat before we leave the sticky cell.</summary>
        public const float StickHysteresis = TileSize * 0.35f;

        /// <summary>Flat-topped axial neighbor offsets (edge-adjacent).</summary>
        private static readonly Vector2Int[] HexNeighbors =
        {
            new Vector2Int(+1, 0),
            new Vector2Int(+1, -1),
            new Vector2Int(0, -1),
            new Vector2Int(-1, 0),
            new Vector2Int(-1, +1),
            new Vector2Int(0, +1),
        };

        public static Vector2Int WorldToCell(Vector3 world)
        {
            float x = world.x;
            float z = world.z;
            float q = (2f / 3f * x) / TileSize;
            float r = ((-1f / 3f) * x + (Sqrt3 / 3f) * z) / TileSize;
            return AxialRound(q, r);
        }

        public static Vector3 CellToWorld(Vector2Int cell, float y)
        {
            float x = TileSize * (1.5f * cell.x);
            float z = TileSize * ((Sqrt3 / 2f) * cell.x + Sqrt3 * cell.y);
            return new Vector3(x, y, z);
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

        public static int HexDistance(Vector2Int a, Vector2Int b)
        {
            int dq = a.x - b.x;
            int dr = a.y - b.y;
            return (Mathf.Abs(dq) + Mathf.Abs(dq + dr) + Mathf.Abs(dr)) / 2;
        }

        /// <summary>
        /// Snap to the hex under the cursor. Occupied cells resolve to the nearest empty
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

            Vector2Int best = rawCell;
            float bestDistSq = float.MaxValue;
            bool found = false;
            for (int i = 0; i < HexNeighbors.Length; i++)
            {
                Vector2Int candidate = rawCell + HexNeighbors[i];
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

            // Boxed in — walk hex rings for any empty cell.
            for (int ring = 1; ring <= 3; ring++)
            {
                foreach (var candidate in CellsInRing(rawCell, ring))
                {
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
            }

            return rawCell;
        }

        private static IEnumerable<Vector2Int> CellsInRing(Vector2Int center, int radius)
        {
            if (radius <= 0)
            {
                yield return center;
                yield break;
            }

            Vector2Int hex = center + HexNeighbors[4] * radius; // start west
            for (int i = 0; i < 6; i++)
            {
                for (int j = 0; j < radius; j++)
                {
                    yield return hex;
                    hex += HexNeighbors[i];
                }
            }
        }

        private static Vector2Int AxialRound(float q, float r)
        {
            float s = -q - r;
            int qi = Mathf.RoundToInt(q);
            int ri = Mathf.RoundToInt(r);
            int si = Mathf.RoundToInt(s);

            float qDiff = Mathf.Abs(qi - q);
            float rDiff = Mathf.Abs(ri - r);
            float sDiff = Mathf.Abs(si - s);

            if (qDiff > rDiff && qDiff > sDiff)
                qi = -ri - si;
            else if (rDiff > sDiff)
                ri = -qi - si;

            return new Vector2Int(qi, ri);
        }

        public static HashSet<Vector2Int> GetOccupiedCells(Owner owner)
        {
            var set = new HashSet<Vector2Int>();
            foreach (var b in BaseBuilding.ActiveBuildings)
            {
                if (b == null || b.Owner != owner) continue;
                if (!b.enabled) continue;
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
            for (int i = 0; i < HexNeighbors.Length; i++)
            {
                if (occupied.Contains(cell + HexNeighbors[i])) n++;
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

            for (int i = 0; i < HexNeighbors.Length; i++)
            {
                if (occupiedBuildings.TryGetValue(cell + HexNeighbors[i], out var neighbor))
                    results.Add(neighbor);
            }
        }

        /// <summary>True when two world positions share an edge on the hex grid.</summary>
        public static bool AreOrthogonalNeighbors(Vector3 a, Vector3 b)
        {
            return HexDistance(WorldToCell(a), WorldToCell(b)) == 1;
        }
    }
}
