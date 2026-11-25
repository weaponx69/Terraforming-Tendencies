using System.Globalization;
using System.Text;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.Prologue;
using UnityEngine;

namespace GameDevTV.RTS.Diagnostics
{
    /// <summary>
    /// Play Mode matrix with burdened-board warm-up, then real Prologue survey stress cases.
    /// </summary>
    public static class ClickOrderDiagnosticAutomation
    {
        public const string ReportPath = "Assets/Playtest/click-order-diagnostic-report.json";

        public static ClickOrderDiagnostic EnsureDiagnostic()
        {
            var existing = UnityEngine.Object.FindAnyObjectByType<ClickOrderDiagnostic>();
            if (existing != null) return existing;

            var go = new GameObject("ClickOrderDiagnostic_Auto");
            UnityEngine.Object.DontDestroyOnLoad(go);
            return go.AddComponent<ClickOrderDiagnostic>();
        }

        /// <summary>CLI step 1 — burden board only (must yield to real frames before FPS read).</summary>
        public static string RunWarmupOnly()
        {
            if (!Application.isPlaying)
                return "{\"error\":\"not_playing\"}";

            var warmup = ClickOrderDiagnosticWarmup.RunBurdenedGameplaySimulation();
            var sb = new StringBuilder(512);
            sb.Append('{');
            sb.Append("\"step\":\"warmup\"");
            sb.Append(",\"prologueActive\":").Append(PrologueController.IsActive ? "true" : "false");
            ClickOrderDiagnosticWarmup.AppendWarmupJson(sb, warmup);
            sb.Append('}');
            return sb.ToString();
        }

        /// <summary>CLI step 2 — P1–P3 on current burdened state.</summary>
        public static string RunStressMatrixOnly()
        {
            if (!Application.isPlaying)
                return "{\"error\":\"not_playing\"}";

            var diag = EnsureDiagnostic();
            Vector3 markWorld = ResolveMarkWorld(out int markTile);

            var sb = new StringBuilder(4096);
            sb.Append('{');
            sb.Append("\"matrixKind\":\"prologue_survey_production_burdened\"");
            sb.Append(",\"timestamp\":\"").Append(System.DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)).Append('\"');
            sb.Append(",\"step\":\"matrix\"");
            sb.Append(",\"prologueActive\":").Append(PrologueController.IsActive ? "true" : "false");
            sb.Append(",\"markTile\":").Append(markTile);
            sb.Append(",\"goal\":").Append(Vec3(markWorld));
            sb.Append(",\"cases\":[");

            var rProd = diag.RunPrologueSurveyStress(PrologueSurveyStressCase.MarkClickProduction);
            AppendProductionCase(sb, "P1_mark_click_async", rProd, first: true);

            sb.Append(',');
            var rSync = diag.RunPrologueSurveyStress(PrologueSurveyStressCase.MarkClickSynchronousPath);
            AppendProductionCase(sb, "P2_mark_click_sync_bfs", rSync, first: false);

            sb.Append(',');
            var rConfirm = diag.RunPrologueSurveyStress(PrologueSurveyStressCase.ConfirmSurveyTeam);
            AppendProductionCase(sb, "P3_confirm_survey_team", rConfirm, first: false);

            sb.Append(']');

            float maxSpike = Mathf.Max(rProd.MaxDispatchSpikeMs, Mathf.Max(rSync.MaxDispatchSpikeMs, rConfirm.MaxDispatchSpikeMs));
            float avgFrame = (rProd.AvgFrameTimeMs + rSync.AvgFrameTimeMs + rConfirm.AvgFrameTimeMs) / 3f;

            sb.Append(",\"avgFrameTimeMs\":").Append(avgFrame.ToString("F3", CultureInfo.InvariantCulture));
            sb.Append(",\"maxDispatchSpikeMs\":").Append(maxSpike.ToString("F3", CultureInfo.InvariantCulture));
            sb.Append('}');

            string json = sb.ToString();
            try
            {
                System.IO.Directory.CreateDirectory("Assets/Playtest");
                System.IO.File.WriteAllText(ReportPath, json);
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogWarning("[ClickOrderDiagnosticAutomation] Write failed: " + ex.Message);
            }

            UnityEngine.Debug.Log("[ClickOrderDiagnosticAutomation] " + json);
            return ReportPath + "|" + json;
        }

        /// <summary>Single eval entry (may hit Pipeline 5s cap — prefer split shell flow).</summary>
        public static string RunMatrixAndWriteReport()
        {
            if (!Application.isPlaying)
                return "{\"error\":\"not_playing\"}";

            string warmupJson = RunWarmupOnly();
            string matrixPart = RunStressMatrixOnly();
            if (matrixPart.Contains('|'))
                matrixPart = matrixPart.Split('|')[1];

            string merged = MergeWarmupAndMatrix(warmupJson, matrixPart);
            try
            {
                System.IO.File.WriteAllText(ReportPath, merged);
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogWarning("[ClickOrderDiagnosticAutomation] Write failed: " + ex.Message);
            }

            return ReportPath + "|" + merged;
        }

        static string MergeWarmupAndMatrix(string warmupJson, string matrixJson)
        {
            if (string.IsNullOrEmpty(warmupJson) || warmupJson.Contains("\"error\""))
                return matrixJson;
            if (matrixJson.StartsWith("{") && warmupJson.StartsWith("{"))
            {
                string w = warmupJson.TrimEnd('}');
                string m = matrixJson.TrimStart('{');
                return w + "," + m;
            }

            return matrixJson;
        }

        static void AppendProductionCase(StringBuilder sb, string id, PrologueSurveyStressResult r, bool first)
        {
            if (!first) { }

            sb.Append('{');
            sb.Append("\"id\":\"").Append(id).Append('\"');
            sb.Append(",\"success\":").Append(r.Success ? "true" : "false");
            if (!string.IsNullOrEmpty(r.Error))
                sb.Append(",\"error\":\"").Append(EscapeJson(r.Error)).Append('\"');
            sb.Append(",\"targetTile\":").Append(r.TargetTile);
            sb.Append(",\"paintCountAfter\":").Append(r.PaintCountAfter);
            sb.Append(",\"scanMode\":").Append(r.ScanMode ? "true" : "false");
            sb.Append(",\"clickFrameDispatchMs\":")
                .Append(r.ClickFrameDispatchMs.ToString("F4", CultureInfo.InvariantCulture));
            sb.Append(",\"pathDrainMs\":").Append(r.PathDrainMs.ToString("F4", CultureInfo.InvariantCulture));
            sb.Append(",\"visionFollowUpMs\":").Append(r.VisionFollowUpMs.ToString("F4", CultureInfo.InvariantCulture));
            sb.Append(",\"totalWallMs\":").Append(r.TotalWallMs.ToString("F4", CultureInfo.InvariantCulture));
            sb.Append(",\"avgFrameTimeMs\":").Append(r.AvgFrameTimeMs.ToString("F4", CultureInfo.InvariantCulture));
            sb.Append(",\"maxDispatchSpikeMs\":").Append(r.MaxDispatchSpikeMs.ToString("F4", CultureInfo.InvariantCulture));
            sb.Append(",\"raycastHit\":true");
            sb.Append(",\"raycastMs\":0");
            sb.Append(",\"asyncDrainedJobs\":0");
            sb.Append(",\"asyncDrainMs\":").Append(r.PathDrainMs.ToString("F4", CultureInfo.InvariantCulture));
            sb.Append(",\"pendingAfter\":").Append(PrologueVisionThrottle.Instance != null && PrologueVisionThrottle.Instance.HasPending ? 1 : 0);
            sb.Append('}');
        }

        static Vector3 ResolveMarkWorld(out int markTile)
        {
            markTile = -1;
            var ctrl = PrologueController.Instance;
            if (ctrl == null) return ResolveGoalFallback();

            int home = -1;
            var colony = ctrl.Colony;
            if (colony != null)
            {
                for (int i = 0; i < colony.Board.Tiles.Count; i++)
                {
                    if (colony.Board.At(i).Building == PrologueBuildingId.CommandPost)
                    {
                        home = i;
                        break;
                    }
                }
            }

            int bestD = int.MaxValue;
            var marks = PrologueInteractableIndex.DepositMarks;
            for (int m = 0; m < marks.Count; m++)
            {
                var mark = marks[m];
                if (mark == null) continue;
                Vector3 ground = mark.GroundPoint;
                ground.y = ColonyTileGrid.SampleGroundY(ground);
                if (!ctrl.TryGetTileIndex(ground, out int tile))
                {
                    var cell = ColonyTileGrid.WorldToCell(ground);
                    if (!ctrl.TryGetTileIndex(ColonyTileGrid.CellToWorldOnGround(cell), out tile))
                        continue;
                }

                int d = home >= 0 && colony != null ? colony.Dist(home, tile) : 0;
                if (d >= bestD) continue;
                bestD = d;
                markTile = tile;
            }

            if (markTile >= 0 && ctrl.TryGetCell(markTile, out var bestCell))
                return ColonyTileGrid.CellToWorldOnGround(bestCell);

            var ice = PrologueInteractableIndex.IceClicks;
            for (int i = 0; i < ice.Count; i++)
            {
                if (ice[i] == null || ice[i].TileIndex < 0) continue;
                markTile = ice[i].TileIndex;
                if (ctrl.TryGetCell(markTile, out var iceCell))
                    return ColonyTileGrid.CellToWorldOnGround(iceCell);
            }

            return ResolveGoalFallback();
        }

        static Vector3 ResolveGoalFallback()
        {
            if (PrologueController.Instance != null)
            {
                Vector2Int homeCell = PrologueController.Instance.ResolveHomeCell();
                Vector3 home = ColonyTileGrid.CellToWorldOnGround(homeCell);
                return home + new Vector3(36f, 0f, 28f);
            }

            return new Vector3(40f, 0f, 30f);
        }

        static string Vec3(Vector3 v) =>
            "{" + string.Format(CultureInfo.InvariantCulture, "\"x\":{0:F2},\"y\":{1:F2},\"z\":{2:F2}", v.x, v.y, v.z) + "}";

        static string EscapeJson(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}