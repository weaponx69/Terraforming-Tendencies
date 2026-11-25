using System.Collections.Generic;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.Prologue
{
    /// <summary>
    /// Perf / soak test: mid-run colony with surveyed paths and finished buildings (vertical slice, not a fork).
    /// Enable via <see cref="RequestFilledRun"/> before bootstrap, or <see cref="PrologueController.BeginFilledPerfRun"/>.
    /// </summary>
    public static class ProloguePerfScenario
    {
        public const string EnvVar = "TT_PERF_FILLED_RUN";

        public static bool RequestFilledRun;

        public static bool ShouldApply =>
            RequestFilledRun
            || System.Environment.GetEnvironmentVariable(EnvVar) == "1";

        public static void Apply(PrologueController ctrl)
        {
            RequestFilledRun = false;
            if (ctrl?.Colony == null || ctrl.Colony.Board.Tiles.Count < 8) return;

            ProloguePathGroundOverlay.SetEnabled(true);
            PrologueSearchedTiles.UseBakedPathOverlay = true;
            PrologueRevealGhosts.SilhouetteFootprintsOnly = true;
            PrologueRevealGhosts.FrustumCullRevealGhosts = true;

            int home = HomeTileIndex(ctrl.Colony);
            if (home < 0) return;

            int ice = NearestIceTile(ctrl.Colony, home);
            if (ice < 0) ice = NearestRevealedCandidate(ctrl.Colony, home);

            var path = ice >= 0 ? FindPath(home, ice, ctrl.Colony) : new List<int> { home };
            if (path.Count == 0) path.Add(home);

            var overlay = ProloguePathGroundOverlay.Instance;
            overlay?.FitBoard(ctrl);
            overlay?.ClearPixels();

            for (int i = 0; i < path.Count; i++)
            {
                var t = ctrl.Colony.Board.At(path[i]);
                t.Revealed = true;
                t.Lit = true;
            }

            PaintPathOverlay(ctrl, path);
            PrologueResourceFog.RevealAlongSurveyPath(ctrl, path);

            foreach (int ti in path)
            {
                PrologueIceMarkers.Instance?.OnTileRevealed(ti);
                PrologueRevealGhosts.OnTileSurveyRevealed(ti);
            }

            var used = new HashSet<int> { home };
            int aquiferTile = TakeEmptyNear(ctrl, home, used, ice, PrologueBuildingId.Aquifer);
            int oxygenTile = TakeEmptyNear(ctrl, home, used, home, PrologueBuildingId.Oxygen);
            int hab0 = TakeEmptyNear(ctrl, home, used, oxygenTile, PrologueBuildingId.Habitat);
            int hab1 = TakeEmptyNear(ctrl, home, used, hab0, PrologueBuildingId.Habitat);

            ctrl.Colony.DebugPlaceComplete(aquiferTile, PrologueBuildingId.Aquifer);
            ctrl.Colony.DebugPlaceComplete(oxygenTile, PrologueBuildingId.Oxygen);
            ctrl.Colony.DebugPlaceComplete(hab0, PrologueBuildingId.Habitat);
            ctrl.Colony.DebugPlaceComplete(hab1, PrologueBuildingId.Habitat);

            SpawnAllFinished(ctrl, PrologueBuildingId.Aquifer);
            SpawnAllFinished(ctrl, PrologueBuildingId.Oxygen);
            SpawnAllFinished(ctrl, PrologueBuildingId.Habitat);

            foreach (var id in new[]
                     {
                         PrologueBuildingId.Aquifer, PrologueBuildingId.Oxygen,
                         PrologueBuildingId.Habitat, PrologueBuildingId.WindTurbine
                     })
                ctrl.Colony.Unlocked.Add(id);

            ctrl.Colony.AddStock(PrologueResourceId.Minerals, 200);
            ctrl.Colony.DebugForceStorm(false);
            PrologueBuildingRegistry.Invalidate();
            PrologueRevealGhosts.ResetSession();
            PrologueRevealGhosts.BootstrapFromColonyIfNeeded();
            overlay?.Apply();
            PrologueSearchedTiles.Instance?.RefreshAll();
            ctrl.RefreshHandUnlocks();
            GameDevTV.RTS.UI.PrologueHUD.Instance?.Refresh(ctrl.Colony);

            Debug.Log(
                $"[ProloguePerfScenario] Filled run: path={path.Count} tiles, baked paths, silhouette ghosts, " +
                $"{CountBuildings(ctrl.Colony)} board buildings.");
        }

        public static List<int> FindPath(int home, int goal, PrologueColony colony)
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

        static int HomeTileIndex(PrologueColony colony)
        {
            for (int i = 0; i < colony.Board.Tiles.Count; i++)
                if (colony.Board.At(i).Building == PrologueBuildingId.CommandPost)
                    return i;
            return 0;
        }

        static int NearestIceTile(PrologueColony colony, int from)
        {
            int best = -1;
            int bestD = int.MaxValue;
            for (int i = 0; i < colony.Board.Tiles.Count; i++)
            {
                var t = colony.Board.At(i);
                if (t.DepositLeft <= 0) continue;
                if (t.Feature != PrologueFeature.Ice && t.Feature != PrologueFeature.Glacier) continue;
                int d = colony.Dist(from, i);
                if (d < bestD) { bestD = d; best = i; }
            }
            return best;
        }

        static int NearestRevealedCandidate(PrologueColony colony, int from)
        {
            for (int i = 0; i < colony.Board.Tiles.Count; i++)
            {
                if (colony.Board.At(i).Building != PrologueBuildingId.None) continue;
                if (colony.Dist(from, i) <= 8) return i;
            }
            return from;
        }

        static int TakeEmptyNear(PrologueController ctrl, int home, HashSet<int> used, int preferred,
            PrologueBuildingId id)
        {
            var colony = ctrl.Colony;
            if (preferred >= 0 && preferred < colony.Board.Tiles.Count && !used.Contains(preferred)
                && colony.Board.At(preferred).Building == PrologueBuildingId.None
                && TileOkFor(colony, preferred, id))
            {
                used.Add(preferred);
                return preferred;
            }

            int best = -1;
            int bestD = int.MaxValue;
            for (int i = 0; i < colony.Board.Tiles.Count; i++)
            {
                if (used.Contains(i) || colony.Board.At(i).Building != PrologueBuildingId.None) continue;
                if (!TileOkFor(colony, i, id)) continue;
                int d = colony.Dist(home, i);
                if (d < bestD) { bestD = d; best = i; }
            }
            if (best >= 0) used.Add(best);
            return best >= 0 ? best : home;
        }

        static bool TileOkFor(PrologueColony colony, int tile, PrologueBuildingId id)
        {
            if (id != PrologueBuildingId.Aquifer) return true;
            for (int j = 0; j < colony.Board.Tiles.Count; j++)
            {
                var t = colony.Board.At(j);
                if (t.DepositLeft <= 0) continue;
                if (t.Feature != PrologueFeature.Ice && t.Feature != PrologueFeature.Glacier) continue;
                if (colony.Dist(tile, j) <= PrologueData.HarvestRange) return true;
            }
            return false;
        }

        static void PaintPathOverlay(PrologueController ctrl, List<int> path)
        {
            var overlay = ProloguePathGroundOverlay.Instance;
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

        static void SpawnAllFinished(PrologueController ctrl, PrologueBuildingId id)
        {
            var def = BuildingDef(id);
            if (def?.Prefab == null) return;

            for (int i = 0; i < ctrl.Colony.Board.Tiles.Count; i++)
            {
                if (ctrl.Colony.Board.At(i).Building != id) continue;
                if (!ctrl.TryGetCell(i, out Vector2Int cell)) continue;

                Vector3 world = ColonyTileGrid.CellToWorldOnGround(cell);
                bool taken = false;
                foreach (var existing in PrologueBuildingRegistry.PlacedBuildings)
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
                if (id == PrologueBuildingId.Aquifer)
                    WaterIceAquiferMeshBuilder.Apply(go);
                PrologueRevealGhosts.OnPadBuilt(world);
            }
        }

        static BuildingSO BuildingDef(PrologueBuildingId id)
        {
            return id switch
            {
                PrologueBuildingId.Aquifer => LoadSo("Water Ice Aquifer", "Cards/WaterIceAquiferCard"),
                PrologueBuildingId.Oxygen => LoadSo("Oxygen Processor", "Cards/OxygenProcessorCard"),
                PrologueBuildingId.Habitat => LoadSo("Habitat", "Cards/HabitatCard"),
                PrologueBuildingId.WindTurbine => LoadSo("Geothermal Generator", "Cards/GeothermalGeneratorCard"),
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

        static int CountBuildings(PrologueColony colony)
        {
            int n = 0;
            foreach (var t in colony.Board.Tiles)
                if (t.Building != PrologueBuildingId.None) n++;
            return n;
        }
    }
}