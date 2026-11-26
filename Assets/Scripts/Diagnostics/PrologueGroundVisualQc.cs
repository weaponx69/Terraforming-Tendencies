using System.Text;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.Prologue;
using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.Diagnostics
{
    /// <summary>Play Mode QC: stress colony, sample FPS, set min/max zoom for ground sharpness checks.</summary>
    public static class PrologueGroundVisualQc
    {
        public const int HeavyBuildingTarget = 200;
        public const int MinAcceptableFps = ProloguePerformanceBudget.MinimumAcceptableFps;

        public static string PrepareHeavyLoad()
        {
            var ctrl = PrologueController.Instance;
            if (ctrl == null || !PrologueController.IsActive || ctrl.Colony == null)
                return "error:not_in_prologue";

            if (!Application.isPlaying)
                return "error:not_playing";

            ProloguePerformanceBudget.ApplyIfNeeded();
            RevealAllTiles(ctrl.Colony);
            ClickOrderDiagnosticWarmup.UnlockWarmupBuildings(ctrl.Colony);
            PrologueGroundVisualTuning.SharpenPlanetTextures(16);

            var path = new System.Collections.Generic.List<int>();
            int home = HomeTile(ctrl.Colony);
            if (home >= 0) path.Add(home);
            int boardSpawned = ClickOrderDiagnosticWarmup.SpawnBuildingClusterPublic(
                ctrl, path, HeavyBuildingTarget, out _);

            int meshSpawned = StressFillWorldMeshes(ctrl, HeavyBuildingTarget);

            for (int i = 0; i < 6; i++)
                PrologueGroundVisualTuning.Tick();
            PrologueGroundVisualTuning.ForceHighQualityForSampling();

            return $"prepared buildings={BaseBuilding.ActiveBuildings.Count} board={boardSpawned} mesh={meshSpawned} " +
                   PrologueGroundVisualTuning.DiagnosticsLine();
        }

        static void RevealAllTiles(PrologueColony colony)
        {
            if (colony == null) return;
            for (int i = 0; i < colony.Board.Tiles.Count; i++)
            {
                var t = colony.Board.At(i);
                t.Revealed = true;
                t.Lit = true;
            }
            PrologueSearchedTiles.Instance?.RefreshAll();
        }

        static int HomeTile(PrologueColony colony)
        {
            for (int i = 0; i < colony.Board.Tiles.Count; i++)
                if (colony.Board.At(i).Building == PrologueBuildingId.CommandPost)
                    return i;
            return colony.Board.Tiles.Count > 0 ? 0 : -1;
        }

        /// <summary>World mesh load independent of hex occupancy (perf QC only).</summary>
        static int StressFillWorldMeshes(PrologueController ctrl, int target)
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
                        ProloguePerformanceBudget.ApplyBuildingVisuals(go);
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
            PrologueGroundVisualTuning.ForceHighQualityForSampling();
            for (int i = 0; i < 4; i++)
                PrologueGroundVisualTuning.Tick();

            int fps = PrologueFpsMonitor.LastSecondFps;
            if (fps <= 0)
                fps = Mathf.RoundToInt(1f / Mathf.Max(Time.unscaledDeltaTime, 0.0001f));

            var sb = new StringBuilder();
            sb.Append("{\"label\":\"").Append(label).Append('\"');
            sb.Append(",\"zoom\":").Append(zoom.ToString("0.##"));
            sb.Append(",\"fps\":").Append(fps);
            sb.Append(",\"buildings\":").Append(BaseBuilding.ActiveBuildings.Count);
            sb.Append(",\"tuningLevel\":").Append(PrologueGroundVisualTuning.CurrentLevel);
            sb.Append(",\"diag\":\"").Append(Escape(PrologueGroundVisualTuning.DiagnosticsLine())).Append('"');
            sb.Append('}');
            return sb.ToString();
        }

        static string Escape(string s) => s?.Replace("\\", "\\\\").Replace("\"", "\\\"") ?? "";
    }
}