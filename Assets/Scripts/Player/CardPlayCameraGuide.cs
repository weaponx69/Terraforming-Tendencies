using System.Collections.Generic;
using GameDevTV.RTS.Commands;
using GameDevTV.RTS.Environment;
using GameDevTV.RTS.MainGame;
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

            bool mainGame = MainGameController.IsActive;
            if (!mainGame)
            {
                Vector3 cam = PlayerInput.GetCameraFocusPosition();
                if (ColonyTileGrid.HorizontalDistSq(cam, world) <= SkipFlyHorizSq)
                    return false;
            }

            if (mainGame)
                PlayerInput.FlyHandCardToPlaySite(world, durationSeconds);
            else
                PlayerInput.FlyCameraToWorldPosition(world, durationSeconds);
            return true;
        }

        public static bool TryResolvePlayWorld(BuildingSO building, out Vector3 world)
        {
            world = default;
            if (building == null) return false;

            if (MainGameController.IsActive)
                return TryResolveMainGamePlayWorld(building, out world);

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
        public static bool TryGetMainGamePlacementTile(BuildingSO building, out int tileIndex)
        {
            tileIndex = -1;
            var ctrl = MainGameController.Instance;
            var colony = ctrl?.Colony;
            if (colony == null || building == null) return false;

            if (BuildingSiteRegistry.IsMineBuilding(building))
                return TryGetMainGameMinePlacementTile(building, out tileIndex);

            if (!MainGameCardMap.TryGetMainGameId(building, out var id)
                || id == MainGameBuildingId.None
                || id == MainGameBuildingId.CommandPost)
                return false;

            int secondIce = colony.MainGameStep >= 4 ? colony.SecondIceTargetTile() : -1;
            int anchor = PlacementAnchor(colony, id, secondIce);
            if (anchor < 0) anchor = 0;

            if (id == MainGameBuildingId.Aquifer && colony.MainGameStep >= 4 && secondIce >= 0)
            {
                int bestIce = -1;
                int bestD = int.MaxValue;
                for (int i = 0; i < colony.Board.Tiles.Count; i++)
                {
                    var pad = colony.Board.At(i);
                    if (pad.Building != MainGameBuildingId.None) continue;
                    if (pad.Feature == MainGameFeature.Ice || pad.Feature == MainGameFeature.Glacier) continue;
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
                if (pad.Building != MainGameBuildingId.None) continue;
                if (id == MainGameBuildingId.Aquifer
                    && (pad.Feature == MainGameFeature.Ice || pad.Feature == MainGameFeature.Glacier))
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

        static bool TryResolveMainGamePlayWorld(BuildingSO building, out Vector3 world)
        {
            world = default;
            var ctrl = MainGameController.Instance;
            var colony = ctrl?.Colony;
            if (colony == null) return false;

            if (BuildingSiteRegistry.IsMineBuilding(building)
                && DiscoverySystem.TryGetAutoMinePlacement(building, out world, out _))
                return true;

            if (!MainGameCardMap.TryGetMainGameId(building, out var id) || id == MainGameBuildingId.None)
                return false;

            if (TryGetMainGamePlacementTile(building, out int placementTile)
                && ctrl.TryGetCell(placementTile, out Vector2Int placeCell))
            {
                world = ColonyTileGrid.CellToWorldOnHighestGround(placeCell);
                return true;
            }

            if (id == MainGameBuildingId.Aquifer && TryResolveAquiferIceFallback(colony, ctrl, out world))
                return true;

            // Fall back: ghost pad nearest the placement anchor (not the camera).
            var suggestPads = Object.FindObjectsByType<MainGameRevealGhostPad>(FindObjectsInactive.Exclude);
            int secondIce = colony.MainGameStep >= 4 ? colony.SecondIceTargetTile() : -1;
            int anchorTile = PlacementAnchor(colony, id, secondIce);
            Vector3 anchorWorld = anchorTile >= 0 && ctrl.TryGetCell(anchorTile, out Vector2Int anchorCell)
                ? ColonyTileGrid.CellToWorldOnHighestGround(anchorCell)
                : PlayerInput.GetCameraFocusPosition();
            float bestSq = float.MaxValue;
            MainGameRevealGhostPad bestPad = null;
            for (int i = 0; i < suggestPads.Length; i++)
            {
                var pad = suggestPads[i];
                if (pad == null || pad.BoardTileIndex < 0) continue;
                if (!colony.Preview(pad.BoardTileIndex, id, out _)) continue;
                if (id == MainGameBuildingId.Aquifer && !colony.CanPlaceAquiferPad(pad.BoardTileIndex)) continue;
                if (!ctrl.TryGetCell(pad.BoardTileIndex, out Vector2Int _))
                    continue;
                float sq = ColonyTileGrid.HorizontalDistSq(anchorWorld, pad.transform.position);
                if (sq >= bestSq) continue;
                bestSq = sq;
                bestPad = pad;
            }

            if (bestPad != null)
            {
                world = bestPad.transform.position;
                return true;
            }

            return false;
        }

        static bool TryResolveAquiferIceFallback(MainGameColony colony, MainGameController ctrl, out Vector3 world)
        {
            world = default;
            int secondIce = colony.MainGameStep >= 4 ? colony.SecondIceTargetTile() : -1;
            int iceAnchor = -1;
            if (colony.MainGameStep >= 4 && !colony.SecondIceDrill() && secondIce >= 0)
                iceAnchor = secondIce;
            else if (colony.Board.FirstIceTiles.Count > 0)
                iceAnchor = FirstSetElement(colony.Board.FirstIceTiles);
            else
            {
                for (int i = 0; i < colony.Board.Tiles.Count; i++)
                {
                    var t = colony.Board.At(i);
                    if (!t.Revealed || t.DepositLeft <= 0) continue;
                    if (t.Feature != MainGameFeature.Ice && t.Feature != MainGameFeature.Glacier) continue;
                    iceAnchor = i;
                    break;
                }
            }

            if (iceAnchor < 0)
                iceAnchor = colony.NearestRevealedIceTile(FindBuildingTile(colony, MainGameBuildingId.CommandPost));
            if (iceAnchor < 0) return false;
            if (!colony.TryFindAquiferPadNearIce(iceAnchor, out int padTile)) return false;
            if (!ctrl.TryGetCell(padTile, out Vector2Int padCell)) return false;
            world = ColonyTileGrid.CellToWorldOnHighestGround(padCell);
            return true;
        }

        static bool TryGetMainGameMinePlacementTile(BuildingSO building, out int tileIndex)
        {
            tileIndex = -1;
            if (!DiscoverySystem.TryGetAutoMinePlacement(building, out Vector3 world, out _))
                return false;
            var ctrl = MainGameController.Instance;
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

        static int PlacementAnchor(MainGameColony colony, MainGameBuildingId id, int secondIce) => id switch
        {
            MainGameBuildingId.WindTurbine => FindBuildingTile(colony, MainGameBuildingId.Oxygen),
            MainGameBuildingId.Oxygen => FindBuildingTile(colony, MainGameBuildingId.CommandPost),
            MainGameBuildingId.Habitat => FindBuildingTile(colony, MainGameBuildingId.Oxygen),
            MainGameBuildingId.Aquifer when colony.MainGameStep >= 4 && secondIce >= 0 => secondIce,
            MainGameBuildingId.Aquifer when colony.Board.FirstIceTiles.Count > 0
                => FirstSetElement(colony.Board.FirstIceTiles),
            MainGameBuildingId.Solar => FindBuildingTile(colony, MainGameBuildingId.CommandPost),
            _ => FindBuildingTile(colony, MainGameBuildingId.CommandPost)
        };

        static int FindBuildingTile(MainGameColony colony, MainGameBuildingId id)
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