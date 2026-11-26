using System.Collections.Generic;
using GameDevTV.RTS.Commands;
using GameDevTV.RTS.Environment;
using GameDevTV.RTS.Prologue;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.Utilities;
using UnityEngine;

namespace GameDevTV.RTS.Player
{
    /// <summary>
    /// When a hand card arms placement, fly the camera to a valid play site (if the view is far away).
    /// </summary>
    public static class CardPlayCameraGuide
    {
        const float DefaultFlySeconds = 1.15f;
        static readonly float SkipFlyHorizSq = ColonyTileGrid.TileSize * ColonyTileGrid.TileSize * 4.84f;

        static readonly List<Vector2Int> neighborScratch = new(8);

        public static bool TryFlyToHandCardPlaySite(BaseCommand command, float durationSeconds = DefaultFlySeconds)
        {
            if (command is not BuildBuildingCommand bbc || bbc.HandIndex < 0 || bbc.Building == null)
                return false;
            if (!TryResolvePlayWorld(bbc.Building, out Vector3 world))
                return false;

            Vector3 cam = PlayerInput.GetCameraFocusPosition();
            if (ColonyTileGrid.HorizontalDistSq(cam, world) <= SkipFlyHorizSq)
                return false;

            PlayerInput.FlyCameraToWorldPosition(world, durationSeconds);
            return true;
        }

        public static bool TryResolvePlayWorld(BuildingSO building, out Vector3 world)
        {
            world = default;
            if (building == null) return false;

            if (PrologueController.IsActive)
                return TryResolveProloguePlayWorld(building, out world);

            if (BuildingSiteRegistry.IsCommandPostBuilding(building))
            {
                Vector3 hint = PlayerInput.GetCameraFocusPosition();
                if (SectorColonization.TryGetFocusedCommandPostPlacement(hint, out world, out _, out _))
                    return true;
            }

            if (BuildingSiteRegistry.IsMineBuilding(building)
                && DiscoverySystem.TryGetAutoMinePlacement(building, out world, out _))
                return true;

            if (DiscoverySystem.TryGetRequiredSectorFeature(building, out _)
                && DiscoverySystem.TryGetAutoFeaturePlacement(building, out world, out _))
                return true;

            return TryFindNearestPlayableHex(building, out world);
        }

        /// <summary>CLI / bots: board tile index for the active prologue building card.</summary>
        public static bool TryGetProloguePlacementTile(BuildingSO building, out int tileIndex)
        {
            tileIndex = -1;
            var ctrl = PrologueController.Instance;
            var colony = ctrl?.Colony;
            if (colony == null || building == null) return false;

            if (BuildingSiteRegistry.IsMineBuilding(building))
                return TryGetPrologueMinePlacementTile(building, out tileIndex);

            if (!PrologueCardMap.TryGetPrologueId(building, out var id)
                || id == PrologueBuildingId.None
                || id == PrologueBuildingId.CommandPost)
                return false;

            int secondIce = colony.PrologueStep >= 4 ? colony.SecondIceTargetTile() : -1;
            int anchor = PlacementAnchor(colony, id, secondIce);
            if (anchor < 0) anchor = 0;

            if (id == PrologueBuildingId.Aquifer && colony.PrologueStep >= 4 && secondIce >= 0)
            {
                int bestIce = -1;
                int bestD = int.MaxValue;
                for (int i = 0; i < colony.Board.Tiles.Count; i++)
                {
                    var pad = colony.Board.At(i);
                    if (pad.Building != PrologueBuildingId.None) continue;
                    if (pad.Feature == PrologueFeature.Ice || pad.Feature == PrologueFeature.Glacier) continue;
                    if (!colony.Preview(i, id, out _)) continue;
                    if (!colony.ReachesNewIce(i)) continue;
                    int d = colony.Dist(secondIce, i);
                    if (d >= bestD) continue;
                    bestD = d;
                    bestIce = i;
                }

                if (bestIce >= 0)
                {
                    tileIndex = bestIce;
                    return true;
                }

                return false;
            }

            int best = -1;
            int bestDist = int.MaxValue;
            for (int i = 0; i < colony.Board.Tiles.Count; i++)
            {
                var pad = colony.Board.At(i);
                if (pad.Building != PrologueBuildingId.None) continue;
                if (id == PrologueBuildingId.Aquifer
                    && (pad.Feature == PrologueFeature.Ice || pad.Feature == PrologueFeature.Glacier))
                    continue;
                if (!colony.Preview(i, id, out _)) continue;
                int d = anchor >= 0 ? colony.Dist(anchor, i) : 0;
                if (d >= bestDist) continue;
                bestDist = d;
                best = i;
            }

            if (best < 0) return false;
            tileIndex = best;
            return true;
        }

        static bool TryResolveProloguePlayWorld(BuildingSO building, out Vector3 world)
        {
            world = default;
            var ctrl = PrologueController.Instance;
            var colony = ctrl?.Colony;
            if (colony == null) return false;

            if (BuildingSiteRegistry.IsMineBuilding(building)
                && DiscoverySystem.TryGetAutoMinePlacement(building, out world, out _))
                return true;

            if (!PrologueCardMap.TryGetPrologueId(building, out var id) || id == PrologueBuildingId.None)
                return false;

            Vector3 cam = PlayerInput.GetCameraFocusPosition();
            var suggestPads = Object.FindObjectsByType<PrologueRevealGhostPad>(FindObjectsInactive.Exclude);
            float bestSq = float.MaxValue;
            PrologueRevealGhostPad bestPad = null;
            for (int i = 0; i < suggestPads.Length; i++)
            {
                var pad = suggestPads[i];
                if (pad == null || pad.BoardTileIndex < 0) continue;
                if (!colony.Preview(pad.BoardTileIndex, id, out _)) continue;
                if (id == PrologueBuildingId.Aquifer && !colony.CanPlaceAquiferPad(pad.BoardTileIndex)) continue;
                float sq = ColonyTileGrid.HorizontalDistSq(cam, pad.transform.position);
                if (sq >= bestSq) continue;
                bestSq = sq;
                bestPad = pad;
            }

            if (bestPad != null)
            {
                world = bestPad.transform.position;
                return true;
            }

            if (id == PrologueBuildingId.Aquifer && TryResolveAquiferIceFallback(colony, ctrl, out world))
                return true;

            if (!TryGetProloguePlacementTile(building, out int tile) || !ctrl.TryGetCell(tile, out Vector2Int cell))
                return false;

            world = ColonyTileGrid.CellToWorldOnHighestGround(cell);
            return true;
        }

        static bool TryResolveAquiferIceFallback(PrologueColony colony, PrologueController ctrl, out Vector3 world)
        {
            world = default;
            int secondIce = colony.PrologueStep >= 4 ? colony.SecondIceTargetTile() : -1;
            int iceAnchor = -1;
            if (colony.PrologueStep >= 4 && !colony.SecondIceDrill() && secondIce >= 0)
                iceAnchor = secondIce;
            else if (colony.Board.FirstIceTiles.Count > 0)
                iceAnchor = FirstSetElement(colony.Board.FirstIceTiles);
            else
            {
                for (int i = 0; i < colony.Board.Tiles.Count; i++)
                {
                    var t = colony.Board.At(i);
                    if (!t.Revealed || t.DepositLeft <= 0) continue;
                    if (t.Feature != PrologueFeature.Ice && t.Feature != PrologueFeature.Glacier) continue;
                    iceAnchor = i;
                    break;
                }
            }

            if (iceAnchor < 0)
                iceAnchor = colony.NearestRevealedIceTile(FindBuildingTile(colony, PrologueBuildingId.CommandPost));
            if (iceAnchor < 0) return false;
            if (!colony.TryFindAquiferPadNearIce(iceAnchor, out int padTile)) return false;
            if (!ctrl.TryGetCell(padTile, out Vector2Int padCell)) return false;
            world = ColonyTileGrid.CellToWorldOnHighestGround(padCell);
            return true;
        }

        static bool TryGetPrologueMinePlacementTile(BuildingSO building, out int tileIndex)
        {
            tileIndex = -1;
            if (!DiscoverySystem.TryGetAutoMinePlacement(building, out Vector3 world, out _))
                return false;
            var ctrl = PrologueController.Instance;
            if (ctrl == null || !ctrl.TryGetTileIndex(world, out tileIndex))
                tileIndex = -1;
            return tileIndex >= 0;
        }

        static bool TryFindNearestPlayableHex(BuildingSO building, out Vector3 world)
        {
            world = default;
            var cmd = ScriptableObject.CreateInstance<BuildBuildingCommand>();
            cmd.Building = building;
            cmd.HandIndex = 0;

            BaseBuilding cp = SectorColonization.FindFocusedPlayerCommandPost();
            Vector3 origin = cp != null
                ? cp.transform.position
                : PlayerInput.GetCameraFocusPosition();
            Vector3 cam = PlayerInput.GetCameraFocusPosition();

            var occupied = ColonyTileGrid.GetOccupiedCells(Owner.Player1);
            var seeds = new List<Vector2Int>(32) { ColonyTileGrid.WorldToCell(origin) };
            if (occupied != null)
            {
                foreach (var cell in occupied)
                    seeds.Add(cell);
            }

            var candidates = new HashSet<Vector2Int>();
            for (int s = 0; s < seeds.Count; s++)
            {
                ColonyTileGrid.CollectOrthogonalNeighborCells(seeds[s], neighborScratch);
                for (int n = 0; n < neighborScratch.Count; n++)
                    candidates.Add(neighborScratch[n]);
                for (int n = 0; n < neighborScratch.Count; n++)
                {
                    var ring = new List<Vector2Int>(6);
                    ColonyTileGrid.CollectOrthogonalNeighborCells(neighborScratch[n], ring);
                    for (int r = 0; r < ring.Count; r++)
                        candidates.Add(ring[r]);
                }
            }

            float bestSq = float.MaxValue;
            bool found = false;
            foreach (var cell in candidates)
            {
                if (occupied != null && occupied.Contains(cell)) continue;
                Vector3 raw = ColonyTileGrid.CellToWorldOnGround(cell);
                Vector3 snap = ColonyTileGrid.SnapForPlacement(raw, Owner.Player1, out _);
                if (!cmd.AllRestrictionsPass(snap, Owner.Player1, requireWorker: false)) continue;
                float sq = ColonyTileGrid.HorizontalDistSq(cam, snap);
                if (sq >= bestSq) continue;
                bestSq = sq;
                world = snap;
                found = true;
            }

            Object.Destroy(cmd);
            return found;
        }

        static int PlacementAnchor(PrologueColony colony, PrologueBuildingId id, int secondIce) => id switch
        {
            PrologueBuildingId.WindTurbine => FindBuildingTile(colony, PrologueBuildingId.Oxygen),
            PrologueBuildingId.Oxygen => FindBuildingTile(colony, PrologueBuildingId.CommandPost),
            PrologueBuildingId.Habitat => FindBuildingTile(colony, PrologueBuildingId.Oxygen),
            PrologueBuildingId.Aquifer when colony.PrologueStep >= 4 && secondIce >= 0 => secondIce,
            PrologueBuildingId.Aquifer when colony.Board.FirstIceTiles.Count > 0
                => FirstSetElement(colony.Board.FirstIceTiles),
            PrologueBuildingId.Solar => FindBuildingTile(colony, PrologueBuildingId.CommandPost),
            _ => FindBuildingTile(colony, PrologueBuildingId.CommandPost)
        };

        static int FindBuildingTile(PrologueColony colony, PrologueBuildingId id)
        {
            for (int i = 0; i < colony.Board.Tiles.Count; i++)
                if (colony.Board.At(i).Building == id) return i;
            return -1;
        }

        static int FirstSetElement(HashSet<int> set)
        {
            foreach (int i in set) return i;
            return -1;
        }
    }
}