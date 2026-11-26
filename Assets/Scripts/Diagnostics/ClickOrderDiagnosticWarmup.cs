using System.Collections.Generic;
using System.Text;
using GameDevTV.RTS.Environment;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.Prologue;
using GameDevTV.RTS.Units;
using UnityEngine;
using UnityEngine.AI;

namespace GameDevTV.RTS.Diagnostics
{
    /// <summary>
    /// Burdens the prologue board before click-order profiling (trails, reveals, buildings, NavMesh carve).
    /// </summary>
    public static class ClickOrderDiagnosticWarmup
    {
        public const int DefaultSurveyLegs = 4;
        public const int DefaultBuildingCount = 16;
        public const int VisionPumpSlices = 8;

        public struct WarmupReport
        {
            public bool Success;
            public string Error;
            public int HomeTile;
            public int SurveyLegs;
            public int PaintTiles;
            public int BuildingsSpawned;
            public int NavMeshObstaclesEnabled;
            public int FpsAfterWarmup;
            public bool ActiveSurveyMission;
        }

        public static WarmupReport RunBurdenedGameplaySimulation(
            int surveyLegs = DefaultSurveyLegs,
            int buildingCount = DefaultBuildingCount)
        {
            var report = new WarmupReport { Success = false };
            var ctrl = PrologueController.Instance;
            if (ctrl == null || !PrologueController.IsActive || ctrl.Colony == null)
            {
                report.Error = "prologue_inactive";
                return report;
            }

            PrologueSurveyMission.Instance?.AbortForPlaytest();
            PrologueDroneScan.Instance?.CancelPaint();

            UnlockWarmupBuildingsInternal(ctrl.Colony);
            report.HomeTile = HomeTile(ctrl.Colony);
            if (report.HomeTile < 0)
            {
                report.Error = "no_home";
                return report;
            }

            var cumulativePath = new List<int> { report.HomeTile };
            var waypoints = PickSurveyWaypoints(ctrl, report.HomeTile, surveyLegs);
            report.SurveyLegs = waypoints.Count;

            int cursor = report.HomeTile;
            for (int w = 0; w < waypoints.Count; w++)
            {
                int goal = waypoints[w];
                if (!PrologueHexPathQueue.TryFindPathNow(ctrl, ctrl.Colony, cursor, goal, out var leg, tryAltGoals: true)
                    || leg == null || leg.Count < 2)
                    continue;

                for (int i = 1; i < leg.Count; i++)
                {
                    int tile = leg[i];
                    if (!cumulativePath.Contains(tile))
                        cumulativePath.Add(tile);
                }

                var revealed = new List<int>();
                for (int i = 1; i < leg.Count; i++)
                    revealed.Add(leg[i]);

                ctrl.Colony.RevealTiles(revealed);
                LitTiles(ctrl.Colony, revealed);
                PrologueVisionThrottle.QueueSurveyFog(ctrl, revealed);
                PrologueSearchedTiles.Instance?.RefreshForTiles(revealed);
                foreach (int tile in revealed)
                    PrologueIceMarkers.Instance?.OnTileRevealed(tile);

                cursor = goal;
            }

            report.PaintTiles = cumulativePath.Count;

            var scan = PrologueDroneScan.Instance
                       ?? new GameObject("PrologueDroneScan_Warmup").AddComponent<PrologueDroneScan>();
            scan.AutomationSeedPaintRoute(cumulativePath);

            if (cumulativePath.Count >= PrologueDroneScan.MinConfirmTiles)
            {
                var mission = PrologueSurveyMission.Instance
                              ?? new GameObject("PrologueSurveyMission").AddComponent<PrologueSurveyMission>();
                var dayPath = new List<int>(cumulativePath);
                if (mission.StartSearch(dayPath, PrologueSurveyMission.DefaultDays))
                {
                    int mid = Mathf.Clamp(dayPath.Count / 2, 0, dayPath.Count - 1);
                    mission.AutomationSimulateRevealProgress(mid);
                    report.ActiveSurveyMission = mission.IsSearching;
                }
            }

            report.BuildingsSpawned = SpawnBuildingCluster(ctrl, cumulativePath, buildingCount, out report.NavMeshObstaclesEnabled);

            PrologueBuildingRegistry.Invalidate();
            ProloguePathGroundOverlay.Instance?.Apply();
            PrologueSearchedTiles.Instance?.RefreshAll();

            for (int i = 0; i < VisionPumpSlices; i++)
                PrologueVisionThrottle.TickOneSliceMs();

            report.FpsAfterWarmup = PrologueFpsMonitor.LastSecondFps;
            report.Success = true;
            return report;
        }

        public static void AppendWarmupJson(StringBuilder sb, WarmupReport report)
        {
            sb.Append(",\"warmup\":{");
            sb.Append("\"success\":").Append(report.Success ? "true" : "false");
            if (!string.IsNullOrEmpty(report.Error))
                sb.Append(",\"error\":\"").Append(Escape(report.Error)).Append('\"');
            sb.Append(",\"homeTile\":").Append(report.HomeTile);
            sb.Append(",\"surveyLegs\":").Append(report.SurveyLegs);
            sb.Append(",\"paintTiles\":").Append(report.PaintTiles);
            sb.Append(",\"buildingsSpawned\":").Append(report.BuildingsSpawned);
            sb.Append(",\"navMeshObstacles\":").Append(report.NavMeshObstaclesEnabled);
            sb.Append(",\"activeSurveyMission\":").Append(report.ActiveSurveyMission ? "true" : "false");
            sb.Append(",\"fpsAfterWarmup\":").Append(report.FpsAfterWarmup);
            sb.Append('}');
        }

        public static void UnlockWarmupBuildings(PrologueColony colony) =>
            UnlockWarmupBuildingsInternal(colony);

        static void UnlockWarmupBuildingsInternal(PrologueColony colony)
        {
            foreach (var id in new[]
                     {
                         PrologueBuildingId.Habitat, PrologueBuildingId.Oxygen,
                         PrologueBuildingId.WindTurbine, PrologueBuildingId.Aquifer
                     })
                colony.Unlocked.Add(id);
        }

        static int HomeTile(PrologueColony colony)
        {
            for (int i = 0; i < colony.Board.Tiles.Count; i++)
                if (colony.Board.At(i).Building == PrologueBuildingId.CommandPost)
                    return i;
            return colony.Board.Tiles.Count > 0 ? 0 : -1;
        }

        static List<int> PickSurveyWaypoints(PrologueController ctrl, int home, int count)
        {
            var picks = new List<int>(count);
            var colony = ctrl.Colony;
            int[] rings = { 3, 5, 7, 9 };
            for (int r = 0; r < rings.Length && picks.Count < count; r++)
            {
                for (int i = 0; i < colony.Board.Tiles.Count && picks.Count < count; i++)
                {
                    if (colony.Dist(home, i) != rings[r]) continue;
                    if (colony.Board.At(i).Building != PrologueBuildingId.None) continue;
                    if (picks.Contains(i)) continue;
                    picks.Add(i);
                }
            }

            while (picks.Count < count)
            {
                int best = -1;
                int bestD = -1;
                for (int i = 0; i < colony.Board.Tiles.Count; i++)
                {
                    if (i == home || picks.Contains(i)) continue;
                    if (colony.Board.At(i).Building != PrologueBuildingId.None) continue;
                    int d = colony.Dist(home, i);
                    if (d <= bestD) continue;
                    bestD = d;
                    best = i;
                }

                if (best < 0) break;
                picks.Add(best);
            }

            return picks;
        }

        public static int SpawnBuildingClusterPublic(
            PrologueController ctrl,
            List<int> nearPath,
            int targetCount,
            out int navObstacles) =>
            SpawnBuildingCluster(ctrl, nearPath, targetCount, out navObstacles);

        static int SpawnBuildingCluster(
            PrologueController ctrl,
            List<int> nearPath,
            int targetCount,
            out int navObstacles)
        {
            navObstacles = 0;
            var used = new HashSet<int>();
            foreach (var t in ctrl.Colony.Board.Tiles)
                if (t.Building != PrologueBuildingId.None)
                    used.Add(t.Index);

            var ids = new[]
            {
                PrologueBuildingId.Habitat, PrologueBuildingId.Oxygen,
                PrologueBuildingId.WindTurbine, PrologueBuildingId.Habitat,
                PrologueBuildingId.Oxygen, PrologueBuildingId.Habitat
            };

            int spawned = 0;
            for (int b = 0; b < targetCount; b++)
            {
                int tile = PickBuildingPad(ctrl, nearPath, used, ids[b % ids.Length]);
                if (tile < 0) break;

                ctrl.Colony.DebugPlaceComplete(tile, ids[b % ids.Length]);
                if (!SpawnFinishedBuilding(ctrl, tile, ids[b % ids.Length]))
                    continue;

                spawned++;
            }

            foreach (var building in BaseBuilding.ActiveBuildings)
            {
                if (building == null || !building.name.StartsWith("DiagWarmup_")) continue;
                navObstacles += EnableNavMeshCarving(building.gameObject);
            }

            return spawned;
        }

        static int PickBuildingPad(PrologueController ctrl, List<int> nearPath, HashSet<int> used, PrologueBuildingId id)
        {
            var colony = ctrl.Colony;
            int anchor = nearPath.Count > 0 ? nearPath[nearPath.Count / 2] : HomeTile(colony);

            int best = -1;
            int bestScore = int.MaxValue;
            for (int i = 0; i < colony.Board.Tiles.Count; i++)
            {
                if (used.Contains(i)) continue;
                if (colony.Board.At(i).Building != PrologueBuildingId.None) continue;
                if (!colony.Board.At(i).Revealed) continue;
                int dPath = nearPath.Count > 0 ? MinDistToPath(colony, i, nearPath) : colony.Dist(anchor, i);
                int dHome = colony.Dist(HomeTile(colony), i);
                int score = dPath * 2 + dHome;
                if (id == PrologueBuildingId.Aquifer && !NearIce(colony, i)) continue;
                if (score >= bestScore) continue;
                bestScore = score;
                best = i;
            }

            if (best >= 0) used.Add(best);
            return best;
        }

        static int MinDistToPath(PrologueColony colony, int tile, List<int> path)
        {
            int best = int.MaxValue;
            for (int i = 0; i < path.Count; i++)
                best = Mathf.Min(best, colony.Dist(tile, path[i]));
            return best;
        }

        static bool NearIce(PrologueColony colony, int tile)
        {
            for (int i = 0; i < colony.Board.Tiles.Count; i++)
            {
                var t = colony.Board.At(i);
                if (t.DepositLeft <= 0) continue;
                if (t.Feature != PrologueFeature.Ice && t.Feature != PrologueFeature.Glacier) continue;
                if (colony.Dist(tile, i) <= PrologueData.HarvestRange) return true;
            }

            return false;
        }

        static bool SpawnFinishedBuilding(PrologueController ctrl, int tile, PrologueBuildingId id)
        {
            var def = BuildingDef(id);
            if (def?.Prefab == null || !ctrl.TryGetCell(tile, out Vector2Int cell)) return false;

            Vector3 world = ColonyTileGrid.CellToWorldOnGround(cell);
            var go = Object.Instantiate(def.Prefab, world, Quaternion.identity);
            go.name = $"DiagWarmup_{def.Name}_{tile}";
            if (!go.TryGetComponent(out BaseBuilding building))
            {
                Object.Destroy(go);
                return false;
            }

            building.CompleteInstantCardPlace(Owner.Player1, def);
            ColonyGroundMesh.Apply(go);
            if (id == PrologueBuildingId.Aquifer)
                WaterIceAquiferMeshBuilder.Apply(go);
            PrologueRevealGhosts.OnPadBuilt(world);
            return true;
        }

        static BuildingSO BuildingDef(PrologueBuildingId id) => id switch
        {
            PrologueBuildingId.Aquifer => LoadSo("Water Ice Aquifer", "Cards/WaterIceAquiferCard"),
            PrologueBuildingId.Oxygen => LoadSo("Atmosphere Processor", "Cards/OxygenProcessorCard"),
            PrologueBuildingId.Habitat => LoadSo("Habitat", "Cards/HabitatCard"),
            PrologueBuildingId.WindTurbine => LoadSo("Windmill", "Cards/GeothermalGeneratorCard"),
            _ => null
        };

        static BuildingSO LoadSo(string buildingName, string cardPath)
        {
            var b = BlueprintDraftManager.GetBuildingSOByName(buildingName);
            if (b != null) return b;
            var card = Resources.Load<UnlockBuildingCardSO>(cardPath);
            return card?.buildingToUnlock;
        }

        static int EnableNavMeshCarving(GameObject root)
        {
            int n = 0;
            foreach (var obstacle in root.GetComponentsInChildren<NavMeshObstacle>(true))
            {
                if (obstacle == null) continue;
                obstacle.enabled = true;
                obstacle.carving = true;
                obstacle.carveOnlyStationary = true;
                n++;
            }

            return n;
        }

        static void LitTiles(PrologueColony colony, IList<int> indices)
        {
            if (colony == null || indices == null) return;
            for (int i = 0; i < indices.Count; i++)
            {
                int idx = indices[i];
                if (idx < 0 || idx >= colony.Board.Tiles.Count) continue;
                colony.Board.At(idx).Lit = true;
            }
        }

        static string Escape(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}