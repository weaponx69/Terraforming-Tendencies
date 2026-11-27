using System.Text;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.MainGame;
using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.Diagnostics
{
    /// <summary>Play Mode QC: stress colony, sample FPS, set min/max zoom for ground sharpness checks.</summary>
    public static class MainGameGroundVisualQc
    {
        public const int HeavyBuildingTarget = 200;
        public const int MinAcceptableFps = MainGamePerformanceBudget.MinimumAcceptableFps;

        public static string PrepareHeavyLoad()
        {
            var ctrl = MainGameController.Instance;
            if (ctrl == null || !MainGameController.IsActive || ctrl.Colony == null)
                return "error:not_in_prologue";

            if (!Application.isPlaying)
                return "error:not_playing";

            MainGamePerformanceBudget.ApplyIfNeeded();
            RevealAllTiles(ctrl.Colony);
            ClickOrderDiagnosticWarmup.UnlockWarmupBuildings(ctrl.Colony);
            MainGameGroundVisualTuning.SharpenPlanetTextures(16);

            var path = new System.Collections.Generic.List<int>();
            int home = HomeTile(ctrl.Colony);
            if (home >= 0) path.Add(home);
            int boardSpawned = ClickOrderDiagnosticWarmup.SpawnBuildingClusterPublic(
                ctrl, path, HeavyBuildingTarget, out _);

            int meshSpawned = StressFillWorldMeshes(ctrl, HeavyBuildingTarget);

            for (int i = 0; i < 6; i++)
                MainGameGroundVisualTuning.Tick();
            MainGameGroundVisualTuning.ForceHighQualityForSampling();

            return $"prepared buildings={BaseBuilding.ActiveBuildings.Count} board={boardSpawned} mesh={meshSpawned} " +
                   MainGameGroundVisualTuning.DiagnosticsLine();
        }

        static void RevealAllTiles(MainGameColony colony)
        {
            if (colony == null) return;
            for (int i = 0; i < colony.Board.Tiles.Count; i++)
            {
                var t = colony.Board.At(i);
                t.Revealed = true;
                t.Lit = true;
            }
            MainGameSearchedTiles.Instance?.RefreshAll();
        }

        static int HomeTile(MainGameColony colony)
        {
            for (int i = 0; i < colony.Board.Tiles.Count; i++)
                if (colony.Board.At(i).Building == MainGameBuildingId.CommandPost)
                    return i;
            return colony.Board.Tiles.Count > 0 ? 0 : -1;
        }

        /// <summary>World mesh load independent of hex occupancy (perf QC only).</summary>
        static int StressFillWorldMeshes(MainGameController ctrl, int target)
        {
            var hab = BlueprintDraftManager.GetBuildingSOByName("Habitat");
            if (hab?.Prefab == null) return 0;
            int home = HomeTile(ctrl.Colony);
            if (home < 0 || !ctrl.TryGetCell(home, out var homeCell)) return 0;

            int added = 0;
            for (int ring = 1; BaseBuilding.ActiveBuildings.Count < target && ring < 48; ring++)
            {
                for (int dx = -ring; dx <= ring && BaseBuilding.ActiveBuildings.Count < target; dx++)
                {
                    for (int dz = -ring; dz <= ring && BaseBuilding.ActiveBuildings.Count < target; dz++)
                    {
                        if (Mathf.Abs(dx) != ring && Mathf.Abs(dz) != ring) continue;
                        var cell = new Vector2Int(homeCell.x + dx * 2, homeCell.y + dz * 2);
                        var world = ColonyTileGrid.CellToWorldOnGround(cell);
                        var go = Object.Instantiate(hab.Prefab, world, Quaternion.identity);
                        go.name = $"GroundQcStress_{cell.x}_{cell.y}";
                        if (!go.TryGetComponent(out BaseBuilding building))
                        {
                            Object.Destroy(go);
                            continue;
                        }
                        building.CompleteInstantCardPlace(Owner.Player1, hab);
                        ColonyGroundMesh.Apply(go);
                        MainGamePerformanceBudget.ApplyBuildingVisuals(go);
                        added++;
                    }
                }
            }
            return added;
        }

        public static string SetZoomAndSample(string label)
        {
            var input = PlayerInput.Instance;
            if (input == null) return $"{{\"label\":\"{label}\",\"error\":\"no_input\"}}";

            float zoom = label switch
            {
                "close" => input.PlaytestMinZoom,
                "far" => input.PlaytestMaxZoom,
                "mid" => (input.PlaytestMinZoom + input.PlaytestMaxZoom) * 0.5f,
                _ => input.PlaytestZoomDistance
            };
            input.PlaytestSetZoomDistance(zoom);
            var ctrl = MainGameController.Instance;
            if (label == "close" && ctrl != null && ctrl.TryGetCell(HomeTile(ctrl.Colony), out var homeCell))
                PlayerInput.FocusCameraOnWorldPosition(ColonyTileGrid.CellToWorldOnHighestGround(homeCell));
            MainGameGroundVisualTuning.ForceHighQualityForSampling();
            for (int i = 0; i < 8; i++)
                MainGameGroundVisualTuning.Tick();

            int fps = MainGameFpsMonitor.LastSecondFps;
            if (fps <= 0)
                fps = Mathf.RoundToInt(1f / Mathf.Max(Time.unscaledDeltaTime, 0.0001f));

            var sb = new StringBuilder();
            sb.Append("{\"label\":\"").Append(label).Append('\"');
            sb.Append(",\"zoom\":").Append(zoom.ToString("0.##"));
            sb.Append(",\"fps\":").Append(fps);
            sb.Append(",\"buildings\":").Append(BaseBuilding.ActiveBuildings.Count);
            sb.Append(",\"tuningLevel\":").Append(MainGameGroundVisualTuning.CurrentLevel);
            sb.Append(",\"diag\":\"").Append(Escape(MainGameGroundVisualTuning.DiagnosticsLine())).Append('"');
            sb.Append('}');
            return sb.ToString();
        }

        static string Escape(string s) => s?.Replace("\\", "\\\\").Replace("\"", "\\\"") ?? "";

        public static string OpeningSnapshot()
        {
            var ctrl = MainGameController.Instance;
            var colony = ctrl?.Colony;
            if (ctrl == null || !MainGameController.IsActive || colony == null)
                return "{\"ok\":false,\"error\":\"not_main_game\"}";

            int home = -1;
            int nearestIce = -1;
            int nearestIceDist = -1;
            for (int i = 0; i < colony.Board.Tiles.Count; i++)
            {
                var t = colony.Board.At(i);
                if (t.Building == MainGameBuildingId.CommandPost) home = i;
                if (t.DepositLeft > 0
                    && (t.Feature == MainGameFeature.Ice || t.Feature == MainGameFeature.Glacier)
                    && !t.Revealed)
                {
                    if (home < 0) continue;
                    int d = colony.Dist(home, i);
                    if (nearestIce < 0 || d < nearestIceDist)
                    {
                        nearestIce = i;
                        nearestIceDist = d;
                    }
                }
            }

            var layout = LandingSiteResourceLayout.ForHomeCell(ctrl.ResolveHomeCell());
            string homeCell = ctrl.TryGetCell(home, out Vector2Int hc)
                ? $"{hc.x},{hc.y}"
                : "?";
            bool contract = colony.ContractCancelled;
            float screenDx = 0f;
            float screenDy = 0f;
            float zoom = 0f;
            var input = PlayerInput.Instance;
            var cam = input != null ? input.GameplayCamera : Camera.main;
            if (cam != null)
            {
                Vector3 cpWorld = ctrl.CampCameraWorld();
                if (cpWorld.sqrMagnitude < 0.01f && home >= 0 && ctrl.TryGetCell(home, out Vector2Int homeC))
                    cpWorld = ColonyTileGrid.CellToWorldOnHighestGround(homeC) + Vector3.up * 2.5f;
                Vector3 screen = cam.WorldToScreenPoint(cpWorld);
                screenDx = screen.x - Screen.width * 0.5f;
                screenDy = screen.y - Screen.height * 0.5f;
            }

            if (input != null)
                zoom = MainGameController.IsActive ? input.MainGameSlantZoom : input.PlaytestZoomDistance;
            return "{" +
                   $"\"ok\":true,\"home\":{home},\"homeCell\":\"{homeCell}\",\"nearestIce\":{nearestIce},\"iceDist\":{nearestIceDist}," +
                   $"\"layoutIce\":{layout.PrimaryIceHexDist()}," +
                   $"\"landing\":\"{LandingSiteResourceLayout.ActiveLandingCell.x},{LandingSiteResourceLayout.ActiveLandingCell.y}\"," +
                   $"\"faith\":{colony.Faith},\"week\":{colony.Week},\"cancelled\":{(contract ? "true" : "false")}," +
                   $"\"campScreenDx\":{screenDx:0.#},\"campScreenDy\":{screenDy:0.#},\"zoom\":{zoom:0.##}" +
                   "}";
        }

        public static string RunOrbitalSurveyProbe()
        {
            var colony = MainGameController.Instance?.Colony;
            if (colony == null) return "{\"error\":\"no_colony\"}";
            int faith0 = colony.Faith;
            int week0 = colony.Week;
            string ping = MainGameIceMarkers.Instance?.PingNearestTarget() ?? "no ping";
            MainGameDroneScan.Instance?.AutomationPreparePaintMission();
            if (MainGameIceMarkers.Instance != null
                && MainGameIceMarkers.Instance.TryGetNearestSurveyBoardTile(out int tile, out _))
                MainGameDroneScan.Instance?.AutomationOrderMarkTile(tile);
            return $"{{\"ping\":\"{Escape(ping)}\",\"faithBefore\":{faith0},\"weekBefore\":{week0},\"after\":{OpeningSnapshot()}}}";
        }
    }
}