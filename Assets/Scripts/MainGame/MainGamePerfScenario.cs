using System.Collections.Generic;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.MainGame
{
    /// <summary>
    /// Perf / soak test: mid-run colony with surveyed paths and finished buildings (vertical slice, not a fork).
    /// Enable via <see cref="RequestFilledRun"/> before bootstrap, or <see cref="MainGameController.BeginFilledPerfRun"/>.
    /// </summary>
    public static class MainGamePerfScenario
    {
        public const string EnvVar = "TT_PERF_FILLED_RUN";

        public static bool RequestFilledRun;

        public static bool ShouldApply =>
            RequestFilledRun
            || System.Environment.GetEnvironmentVariable(EnvVar) == "1";

        public static void Apply(MainGameController ctrl)
        {
            RequestFilledRun = false;
            if (ctrl?.Colony == null || ctrl.Colony.Board.Tiles.Count < 8) return;

            MainGamePathGroundOverlay.SetEnabled(true);
            MainGameSearchedTiles.UseBakedPathOverlay = true;
            MainGameRevealGhosts.SilhouetteFootprintsOnly = true;
            MainGameRevealGhosts.FrustumCullRevealGhosts = true;

            int home = HomeTileIndex(ctrl.Colony);
            if (home < 0) return;

            int ice = NearestIceTile(ctrl.Colony, home);
            if (ice < 0) ice = NearestRevealedCandidate(ctrl.Colony, home);

            var path = ice >= 0 ? FindPath(home, ice, ctrl.Colony) : new List<int> { home };
            if (path.Count == 0) path.Add(home);

            var overlay = MainGamePathGroundOverlay.Instance;
            overlay?.FitBoard(ctrl);
            overlay?.ClearPixels();

            for (int i = 0; i < path.Count; i++)
            {
                var t = ctrl.Colony.Board.At(path[i]);
                t.Revealed = true;
                t.Lit = true;
            }

            PaintPathOverlay(ctrl, path);
            MainGameResourceFog.RevealAlongSurveyPath(ctrl, path);

            foreach (int ti in path)
            {
                MainGameIceMarkers.Instance?.OnTileRevealed(ti);
                MainGameRevealGhosts.OnTileSurveyRevealed(ti);
            }

            var used = new HashSet<int> { home };
            int aquiferTile = TakeEmptyNear(ctrl, home, used, ice, MainGameBuildingId.Aquifer);
            int oxygenTile = TakeEmptyNear(ctrl, home, used, home, MainGameBuildingId.Oxygen);
            int hab0 = TakeEmptyNear(ctrl, home, used, oxygenTile, MainGameBuildingId.Habitat);
            int hab1 = TakeEmptyNear(ctrl, home, used, hab0, MainGameBuildingId.Habitat);

            ctrl.Colony.DebugPlaceComplete(aquiferTile, MainGameBuildingId.Aquifer);
            ctrl.Colony.DebugPlaceComplete(oxygenTile, MainGameBuildingId.Oxygen);
            ctrl.Colony.DebugPlaceComplete(hab0, MainGameBuildingId.Habitat);
            ctrl.Colony.DebugPlaceComplete(hab1, MainGameBuildingId.Habitat);

            SpawnAllFinished(ctrl, MainGameBuildingId.Aquifer);
            SpawnAllFinished(ctrl, MainGameBuildingId.Oxygen);
            SpawnAllFinished(ctrl, MainGameBuildingId.Habitat);

            foreach (var id in new[]
                     {
                         MainGameBuildingId.Aquifer, MainGameBuildingId.Oxygen,
                         MainGameBuildingId.Habitat, MainGameBuildingId.WindTurbine
                     })
                ctrl.Colony.Unlocked.Add(id);

            ctrl.Colony.AddStock(MainGameResourceId.Minerals, 200);
            ctrl.Colony.DebugForceStorm(false);
            MainGameBuildingRegistry.Invalidate();
            MainGameRevealGhosts.ResetSession();
            MainGameRevealGhosts.BootstrapFromColonyIfNeeded();
            overlay?.Apply();
            MainGameSearchedTiles.Instance?.RefreshAll();
            ctrl.RefreshHandUnlocks();
            GameDevTV.RTS.UI.MainGameHUD.Instance?.Refresh(ctrl.Colony);

            Debug.Log(
                $"[MainGamePerfScenario] Filled run: path={path.Count} tiles, baked paths, silhouette ghosts, " +
                $"{CountBuildings(ctrl.Colony)} board buildings.");
        }

        public static List<int> FindPath(int home, int goal, MainGameColony colony)
        {
            var path = new List<int>();
            if (colony == null || home < 0 || goal < 0) return path;
            if (home == goal) { path.Add(home); return path; }

            var prev = new Dictionary<int, int>();
            var q = new Queue<int>();
            var seen = new HashSet<int> { home };
            q.Enqueue(home);
            bool found = false;
            int n = colony.Board.Tiles.Count;

            while (q.Count > 0)
            {
                int cur = q.Dequeue();
                if (cur == goal) { found = true; break; }
                for (int i = 0; i < n; i++)
                {
                    if (i == cur) continue;
                    if (colony.Dist(cur, i) != 1) continue;
                    if (!seen.Add(i)) continue;
                    prev[i] = cur;
                    q.Enqueue(i);
                }
            }

            if (!found) return path;
            int step = goal;
            while (true)
            {
                path.Add(step);
                if (step == home) break;
                step = prev[step];
            }
            path.Reverse();
            return path;
        }

        static int HomeTileIndex(MainGameColony colony)
        {
            for (int i = 0; i < colony.Board.Tiles.Count; i++)
                if (colony.Board.At(i).Building == MainGameBuildingId.CommandPost)
                    return i;
            return 0;
        }

        static int NearestIceTile(MainGameColony colony, int from)
        {
            int best = -1;
            int bestD = int.MaxValue;
            for (int i = 0; i < colony.Board.Tiles.Count; i++)
            {
                var t = colony.Board.At(i);
                if (t.DepositLeft <= 0) continue;
                if (t.Feature != MainGameFeature.Ice && t.Feature != MainGameFeature.Glacier) continue;
                int d = colony.Dist(from, i);
                if (d < bestD) { bestD = d; best = i; }
            }
            return best;
        }

        static int NearestRevealedCandidate(MainGameColony colony, int from)
        {
            for (int i = 0; i < colony.Board.Tiles.Count; i++)
            {
                if (colony.Board.At(i).Building != MainGameBuildingId.None) continue;
                if (colony.Dist(from, i) <= 8) return i;
            }
            return from;
        }

        static int TakeEmptyNear(MainGameController ctrl, int home, HashSet<int> used, int preferred,
            MainGameBuildingId id)
        {
            var colony = ctrl.Colony;
            if (preferred >= 0 && preferred < colony.Board.Tiles.Count && !used.Contains(preferred)
                && colony.Board.At(preferred).Building == MainGameBuildingId.None
                && TileOkFor(colony, preferred, id))
            {
                used.Add(preferred);
                return preferred;
            }

            int best = -1;
            int bestD = int.MaxValue;
            for (int i = 0; i < colony.Board.Tiles.Count; i++)
            {
                if (used.Contains(i) || colony.Board.At(i).Building != MainGameBuildingId.None) continue;
                if (!TileOkFor(colony, i, id)) continue;
                int d = colony.Dist(home, i);
                if (d < bestD) { bestD = d; best = i; }
            }
            if (best >= 0) used.Add(best);
            return best >= 0 ? best : home;
        }

        static bool TileOkFor(MainGameColony colony, int tile, MainGameBuildingId id)
        {
            if (id != MainGameBuildingId.Aquifer) return true;
            for (int j = 0; j < colony.Board.Tiles.Count; j++)
            {
                var t = colony.Board.At(j);
                if (t.DepositLeft <= 0) continue;
                if (t.Feature != MainGameFeature.Ice && t.Feature != MainGameFeature.Glacier) continue;
                if (colony.Dist(tile, j) <= MainGameData.HarvestRange) return true;
            }
            return false;
        }

        static void PaintPathOverlay(MainGameController ctrl, List<int> path)
        {
            var overlay = MainGamePathGroundOverlay.Instance;
            if (overlay == null) return;
            for (int i = 0; i < path.Count; i++)
            {
                int tile = path[i];
                if (!ctrl.TryGetCell(tile, out Vector2Int cell)) continue;
                overlay.PaintPad(ColonyTileGrid.CellToWorldOnGround(cell));
                if (i + 1 >= path.Count) continue;
                if (!ctrl.TryGetCell(path[i + 1], out Vector2Int next)) continue;
                overlay.PaintSegment(
                    ColonyTileGrid.CellToWorldOnGround(cell),
                    ColonyTileGrid.CellToWorldOnGround(next));
            }
        }

        static void SpawnAllFinished(MainGameController ctrl, MainGameBuildingId id)
        {
            var def = BuildingDef(id);
            if (def?.Prefab == null) return;

            for (int i = 0; i < ctrl.Colony.Board.Tiles.Count; i++)
            {
                if (ctrl.Colony.Board.At(i).Building != id) continue;
                if (!ctrl.TryGetCell(i, out Vector2Int cell)) continue;

                Vector3 world = ColonyTileGrid.CellToWorldOnGround(cell);
                bool taken = false;
                foreach (var existing in MainGameBuildingRegistry.PlacedBuildings)
                {
                    if (existing == null) continue;
                    if (ColonyTileGrid.WorldToCell(existing.transform.position) != cell) continue;
                    taken = true;
                    break;
                }
                if (taken) continue;

                var go = Object.Instantiate(def.Prefab, world, Quaternion.identity);
                go.name = $"PerfScenario_{def.Name}_{i}";
                if (!go.TryGetComponent(out BaseBuilding building))
                {
                    Object.Destroy(go);
                    continue;
                }

                building.CompleteInstantCardPlace(Owner.Player1, def);
                ColonyGroundMesh.Apply(go);
                if (id == MainGameBuildingId.Aquifer)
                    WaterIceAquiferMeshBuilder.Apply(go);
                MainGameRevealGhosts.OnPadBuilt(world);
            }
        }

        static BuildingSO BuildingDef(MainGameBuildingId id)
        {
            return id switch
            {
                MainGameBuildingId.Aquifer => LoadSo("Water Ice Aquifer", "Cards/WaterIceAquiferCard"),
                MainGameBuildingId.Oxygen => LoadSo("Atmosphere Processor", "Cards/OxygenProcessorCard"),
                MainGameBuildingId.Habitat => LoadSo("Habitat", "Cards/HabitatCard"),
                MainGameBuildingId.WindTurbine => LoadSo("Windmill", "Cards/GeothermalGeneratorCard"),
                _ => null
            };
        }

        static BuildingSO LoadSo(string buildingName, string cardPath)
        {
            var b = BlueprintDraftManager.GetBuildingSOByName(buildingName);
            if (b != null) return b;
            var card = Resources.Load<UnlockBuildingCardSO>(cardPath);
            return card?.buildingToUnlock;
        }

        static int CountBuildings(MainGameColony colony)
        {
            int n = 0;
            foreach (var t in colony.Board.Tiles)
                if (t.Building != MainGameBuildingId.None) n++;
            return n;
        }
    }
}