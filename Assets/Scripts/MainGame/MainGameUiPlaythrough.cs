using System.Collections.Generic;
using System.Text;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.UI;
using GameDevTV.RTS.UI.Containers;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace GameDevTV.RTS.MainGame
{
    /// <summary>
    /// Player-style UI playthrough for CLI + xdotool. Poll <see cref="StepJson"/> from Play Mode;
    /// shell clicks <c>xdotoolX</c>/<c>xdotoolY</c> (top-left screen) and grabs window screenshots.
    /// </summary>
    public static class MainGameUiPlaythrough
    {
        public const int MaxSteps = 400;

        static int stepIndex;
        static int paintStreak;
        static string lastPhase = "";
        static int stuckPhaseCount;
        static int stuckStep = -1;
        static int stuckWeek = -1;
        static bool depositClipAuditDone;
        static readonly List<string> log = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void ResetOnLoad() => ResetSession();

        public static void ResetSession()
        {
            stepIndex = 0;
            paintStreak = 0;
            lastPhase = "";
            stuckPhaseCount = 0;
            stuckStep = -1;
            stuckWeek = -1;
            depositClipAuditDone = false;
            log.Clear();
        }

        public static string StepJson()
        {
            if (stepIndex >= MaxSteps)
                return Json("error", "max_steps", 0, 0, 0, "step cap");

            if (!Application.isPlaying || !MainGameController.IsActive)
                return Json("wait", "boot", 0, 0, 1500, "waiting for MainGame");

            var colony = MainGameController.Instance?.Colony;
            if (colony == null)
                return Json("wait", "colony", 0, 0, 500, "no colony");

#if UNITY_EDITOR
            int sampledFps = Mathf.RoundToInt(1f / Mathf.Max(Time.unscaledDeltaTime, 0.0001f));
            MainGamePlaytestPerf.NoteFrame(sampledFps);
            if (MainGamePlaytestPerf.TryMitigateLag(out string perfMsg))
                return Json("wait", "perf_mitigate", 0, 0, 800, perfMsg);
#endif

            if (colony.ContractCancelled)
                return Json("done", "cancelled", 0, 0, 0, colony.ContractMessage);

            if (colony.MainGameStep >= MainGameData.MainGameSteps)
                return Json("done", "complete", 0, 0, 0, $"prologue complete step={colony.MainGameStep}");

            if (stuckPhaseCount >= 18)
                return Json("error", "stuck", 0, 0, 0,
                    $"phase={lastPhase} step={colony.MainGameStep} week={colony.Week}", false, "none");

            // Week / opening briefings block the clock — Continue is always real UI.
            if (BriefingUi.TryGetContinueClick(out Vector2 cont))
            {
                paintStreak = 0;
                return Click("briefing_continue", cont, true, "dismiss briefing");
            }

#if UNITY_EDITOR
            if (!depositClipAuditDone
                && MainGameDepositRevealPlaytest.TryRunClipAudit(out Vector3 auditWorld, out string auditMsg))
            {
                depositClipAuditDone = true;
                var cam = PlayerInput.Instance != null ? PlayerInput.Instance.GameplayCamera : Camera.main;
                Vector2 screen = cam != null
                    ? cam.WorldToScreenPoint(auditWorld)
                    : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
                return Click("deposit_clip_audit", screen, true, auditMsg);
            }
#endif

            var scan = MainGameDroneScan.Instance;
            var mission = MainGameSurveyMission.Instance;

            if (scan != null && scan.ScanMode && !scan.IsFlying)
            {
                if (scan.PaintCount == 0 && scan.TryPlaytestRouteToNearestIceMark())
                    return Json("wait", "routed_mark", 0, 0, 350, "auto path toward ice ?");

                if (!scan.PathReachesIceDeposit())
                {
                    if (scan.TryPlaytestNextPaintTowardIce(out int next))
                    {
                        if (!TryGetScreenForTile(next, out Vector2 paintToward))
                            paintToward = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
                        paintStreak++;
                        return Click($"paint_{paintStreak}", paintToward, paintStreak <= 3,
                            $"paint toward ice ({scan.PaintCount + 1})");
                    }

                    if (scan.PaintCount >= MainGameDroneScan.MinConfirmTiles)
                    {
                        if (TryGetCheckmarkScreen(out Vector2 checkEarly))
                            return Click("survey_confirm", checkEarly, true, "confirm survey (path incomplete)");
#if UNITY_EDITOR
                        if (scan.ConfirmPaint())
                            return Json("wait", "survey_start", 0, 0, 500, "editor confirmed survey");
#endif
                    }

                    return Json("wait", "paint_aim", 0, 0, 400, "no paint target toward ice");
                }

                if (TryGetCheckmarkScreen(out Vector2 check))
                    return Click("survey_confirm", check, true, "green checkmark");
            }

            if (mission != null && mission.IsSearching)
            {
                // End Week finishes the mission immediately (FinishOnEndWeek) — avoids time_1x stuck when paused.
                if (MainGameHUD.Instance != null)
                    return Click("end_week_survey", GetEndWeekScreen(), true, "End Week finishes survey");

                return Json("wait", "survey_days", 0, 0, 800,
                    $"day={mission.DaysElapsed:0.0}/{mission.DaysRequired}");
            }

            if (HasActiveConstruction(colony))
            {
                if (MainGameHUD.Instance != null)
                    return Click("end_week_build", GetEndWeekScreen(), true, "End Week advances build");
                return Json("wait", "constructing", 0, 0, 1200, "sites building");
            }

            string wantCard = CardForStep(colony);

            if (PlayerInput.PlacementGhostActive)
            {
                if (string.IsNullOrEmpty(wantCard))
                {
#if UNITY_EDITOR
                    PlayerInput.Instance?.PlaytestCancelPlacement();
#endif
                    return Json("wait", "cancel_ghost", 0, 0, 250, "cleared stray placement ghost");
                }

                if (TryGetPlacementClick(out Vector2 place))
                    return Click("place_ghost", place, true, "confirm placement");
                return Json("wait", "place_aim", 0, 0, 400, "no placement tile");
            }
            if (!string.IsNullOrEmpty(wantCard)
                && BottomBarActionsUI.TryGetHandCardScreenPoint(wantCard, out Vector2 card))
                return Click($"card_{wantCard}", card, true, $"play {wantCard}");

            if (TryMineralHarvestEndWeek(colony))
                return Click("end_week_minerals", GetEndWeekScreen(), true, "End Week — gather minerals for next build");

            if (colony.MainGameStep == 4 && !colony.SecondIceDrill() && !HasActiveConstruction(colony)
                && string.IsNullOrEmpty(CardForStep(colony)) && MainGameHUD.Instance != null)
                return Click("end_week_second_drill", GetEndWeekScreen(), true,
                    "End Week — wait for second aquifer / ice");

            if (colony.MainGameStep == 5 && colony.StormLandWeek >= 0
                && HasCompleteBuilding(colony, MainGameBuildingId.WindTurbine)
                && colony.Week < colony.StormLandWeek + MainGameData.MainGameStormWeeks)
            {
                if (MainGameHUD.Instance != null)
                    return Click("storm_end_week", GetEndWeekScreen(), true,
                        "End Week — storm prep / survival");
            }

            return Json("wait", "thinking", 0, 0, 600,
                $"step={colony.MainGameStep} week={colony.Week}");
        }

        public static string StatusLog() => string.Join("\n", log);

        static string CardForStep(MainGameColony colony)
        {
            if (colony.MainGameStep == 0)
            {
                if (colony.HasSurveyedIce()) return "";
                if (MainGameSurveyMission.Instance != null && MainGameSurveyMission.Instance.IsSearching)
                    return "";
                if (MainGameDroneScan.Instance != null && MainGameDroneScan.Instance.ScanMode)
                    return "";
                return "Survey";
            }
            if (colony.MainGameStep == 1 && !HasCompleteBuilding(colony, MainGameBuildingId.Aquifer))
            {
                if (!colony.HasSurveyedIce()) return "";
                if (colony.GetStock(MainGameResourceId.Minerals) < MainGameData.MineralCost(MainGameBuildingId.Aquifer))
                    return "";
                return "Aquifer";
            }
            if (colony.MainGameStep == 2 && !HasCompleteBuilding(colony, MainGameBuildingId.Oxygen))
                return CanAfford(colony, MainGameBuildingId.Oxygen) ? "Atmosphere" : "";
            if (colony.MainGameStep == 3 && colony.TotalBeds() < 20)
                return CanAfford(colony, MainGameBuildingId.Habitat) ? "Habitat" : "";
            if (colony.MainGameStep == 4)
            {
                if (colony.SecondIceDrill()) return "";
                if (!colony.AnyRevealedNonFirstIce()) return "Survey";
                return CanAfford(colony, MainGameBuildingId.Aquifer) ? "Aquifer" : "";
            }
            if (colony.MainGameStep == 5 && !HasCompleteBuilding(colony, MainGameBuildingId.WindTurbine))
                return CanAfford(colony, MainGameBuildingId.WindTurbine) ? "Windmill" : "";
            return "";
        }

        static bool CanAfford(MainGameColony colony, MainGameBuildingId id)
        {
            int cost = MainGameData.MineralCost(id);
            return cost <= 0 || colony.GetStock(MainGameResourceId.Minerals) >= cost;
        }

        static bool TryMineralHarvestEndWeek(MainGameColony colony)
        {
            if (colony == null || colony.ContractCancelled || MainGameHUD.Instance == null) return false;
            if (HasActiveConstruction(colony) || PlayerInput.PlacementGhostActive) return false;
            MainGameBuildingId? need = colony.MainGameStep switch
            {
                1 when !HasCompleteBuilding(colony, MainGameBuildingId.Aquifer) => MainGameBuildingId.Aquifer,
                2 when !HasCompleteBuilding(colony, MainGameBuildingId.Oxygen) => MainGameBuildingId.Oxygen,
                3 when colony.TotalBeds() < 20 => MainGameBuildingId.Habitat,
                4 when !colony.SecondIceDrill() => MainGameBuildingId.Aquifer,
                5 when !HasCompleteBuilding(colony, MainGameBuildingId.WindTurbine) => MainGameBuildingId.WindTurbine,
                _ => null
            };
            if (need == null) return false;
            if (need == MainGameBuildingId.Aquifer && colony.MainGameStep == 4
                && (CountAquiferPads(colony) >= 2 || HasAquiferUnderConstruction(colony)))
                return false;
            return !CanAfford(colony, need.Value);
        }

        static int CountAquiferPads(MainGameColony colony)
        {
            int n = 0;
            foreach (var t in colony.Board.Tiles)
                if (t.Building == MainGameBuildingId.Aquifer && !t.Broken) n++;
            return n;
        }

        static bool HasAquiferUnderConstruction(MainGameColony colony)
        {
            foreach (var s in colony.Board.Sites)
                if (s.Id == MainGameBuildingId.Aquifer && s.WeeksDone < s.WeeksTotal)
                    return true;
            return false;
        }

        static bool HasCompleteBuilding(MainGameColony colony, MainGameBuildingId id)
        {
            foreach (var t in colony.Board.Tiles)
            {
                if (t.Building != id || t.Broken) continue;
                foreach (var s in colony.Board.Sites)
                {
                    if (s.Tile == t.Index && s.WeeksDone < s.WeeksTotal) return false;
                }
                return true;
            }
            return false;
        }

        static bool HasActiveConstruction(MainGameColony colony)
        {
            foreach (var s in colony.Board.Sites)
                if (s.WeeksDone < s.WeeksTotal)
                    return true;
            return false;
        }

        static string Click(string phase, Vector2 unityBottomLeft, bool screenshot, string message)
        {
            var colony = MainGameController.Instance?.Colony;
            if (colony != null && phase == lastPhase && colony.MainGameStep == stuckStep && colony.Week == stuckWeek)
                stuckPhaseCount++;
            else
            {
                stuckPhaseCount = 0;
                if (colony != null)
                {
                    stuckStep = colony.MainGameStep;
                    stuckWeek = colony.Week;
                }
            }

            stepIndex++;
            lastPhase = phase;
            ToXdotool(unityBottomLeft, out int x, out int y);
            string clickMethod = "xdotool";
#if UNITY_EDITOR
            if (TryExecuteEditorClick(phase, unityBottomLeft))
                clickMethod = "editor";
#endif
            if (screenshot)
                log.Add($"[{stepIndex}] {clickMethod} {phase}: {message}");
            else
                log.Add($"[{stepIndex}] click {phase} ({clickMethod}): {message}");
            return Json("click", phase, x, y, 350, message, screenshot, clickMethod);
        }

        static string Json(string status, string phase, int xdotoolX, int xdotoolY, int waitMs, string message,
            bool screenshot = false, string clickMethod = "xdotool")
        {
            var colony = MainGameController.Instance?.Colony;
            int cStep = colony?.MainGameStep ?? -1;
            int week = colony?.Week ?? -1;
            var scan = MainGameDroneScan.Instance;
            int paint = scan?.PaintCount ?? -1;
            bool searching = MainGameSurveyMission.Instance != null && MainGameSurveyMission.Instance.IsSearching;
#if UNITY_EDITOR
            int fps = Application.isPlaying
                ? Mathf.RoundToInt(1f / Mathf.Max(Time.unscaledDeltaTime, 0.0001f))
                : 0;
            long heapMb = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / (1024 * 1024);
            return $"{{\"status\":\"{status}\",\"phase\":\"{phase}\",\"xdotoolX\":{xdotoolX},\"xdotoolY\":{xdotoolY},\"waitMs\":{waitMs},\"screenshot\":{(screenshot ? "true" : "false")},\"clickMethod\":\"{clickMethod}\",\"colonyStep\":{cStep},\"week\":{week},\"paint\":{paint},\"searching\":{(searching ? "true" : "false")},\"fps\":{fps},\"heapMb\":{heapMb},\"message\":\"{Escape(message)}\"}}";
#else
            return $"{{\"status\":\"{status}\",\"phase\":\"{phase}\",\"xdotoolX\":{xdotoolX},\"xdotoolY\":{xdotoolY},\"waitMs\":{waitMs},\"screenshot\":{(screenshot ? "true" : "false")},\"clickMethod\":\"{clickMethod}\",\"colonyStep\":{cStep},\"week\":{week},\"paint\":{paint},\"searching\":{(searching ? "true" : "false")},\"message\":\"{Escape(message)}\"}}";
#endif
        }

#if UNITY_EDITOR
        static bool TryExecuteEditorClick(string phase, Vector2 unityBottomLeft)
        {
            var scan = MainGameDroneScan.Instance;
            if (phase.StartsWith("paint_") && scan != null)
            {
                if (scan.TryPlaytestNextPaintTowardIce(out int paintTile))
                {
                    scan.SendDroneToTile(paintTile);
                    return true;
                }
                return false;
            }

            if (phase == "survey_confirm" && scan != null)
                return scan.ConfirmPaint();

            if (phase == "briefing_continue")
            {
                MainGameHUD.Instance?.PlaytestDismissBriefing();
                return true;
            }

            if (phase == "time_1x")
            {
                MainGameTimeLapse.Instance?.SetSpeed(MainGameTimeLapse.Speed.Normal);
                return true;
            }

            if (phase is "end_week_survey" or "end_week_build" or "end_week_minerals"
                or "end_week_second_drill" or "storm_end_week")
            {
                MainGameController.Instance?.EndWeek();
                return true;
            }

            if (phase.StartsWith("card_"))
            {
                string key = phase.Length > 5 ? phase.Substring(5) : "";
                if (BottomBarActionsUI.EditorPlaytestSelectHandCard(key))
                    return true;
            }

            if (phase == "place_ghost")
            {
                var input = PlayerInput.Instance;
                var colony = MainGameController.Instance?.Colony;
                if (input != null
                    && PlayerInput.TryGetPlaytestPlacementTile(out int tile)
                    && input.EditorPlaytestPlaceAtTile(tile)
                    && (!PlayerInput.PlacementGhostActive
                        || (colony != null && HasActiveConstruction(colony))))
                    return true;
                if (input != null
                    && input.EditorPlaytestPlaceAtScreen(unityBottomLeft)
                    && (!PlayerInput.PlacementGhostActive
                        || (colony != null && HasActiveConstruction(colony))))
                    return true;
            }

            return UiPlaythroughUtil.TryPointerClick(unityBottomLeft);
        }
#endif

        static string Escape(string s) => s?.Replace("\\", "\\\\").Replace("\"", "'") ?? "";

        static void ToXdotool(Vector2 unityBottomLeft, out int x, out int y)
        {
            x = Mathf.RoundToInt(unityBottomLeft.x);
            y = Mathf.RoundToInt(Screen.height - unityBottomLeft.y);
#if UNITY_EDITOR
            if (TryPlayViewScreenOrigin(out int ox, out int oy))
            {
                x += ox;
                y += oy;
            }
#endif
        }

#if UNITY_EDITOR
        /// <summary>Top-left screen pixel of the active Play Mode view (for xdotool).</summary>
        public static bool TryPlayViewScreenOrigin(out int x, out int y)
        {
            x = y = 0;
            var editorAsm = typeof(UnityEditor.Editor).Assembly;
            var playView = editorAsm.GetType("UnityEditor.PlayModeView")
                ?? editorAsm.GetType("UnityEditor.GameView");
            if (playView == null) return false;

            var method = playView.GetMethod(
                "GetMainPlayModeViewRect",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public
                    | System.Reflection.BindingFlags.NonPublic);
            if (method == null) return false;

            object rectObj = method.Invoke(null, null);
            if (rectObj is not Rect rect) return false;
            x = Mathf.RoundToInt(rect.x);
            y = Mathf.RoundToInt(rect.y);
            return true;
        }
#endif

        static Vector2 GetEndWeekScreen()
        {
            return MainGameHUD.Instance != null && MainGameHUD.Instance.TryGetEndWeekScreenPoint(out Vector2 p)
                ? p
                : new Vector2(Screen.width * 0.85f, Screen.height * 0.92f);
        }

        static bool TryGetCheckmarkScreen(out Vector2 screen)
        {
            screen = default;
            var ctrl = MainGameController.Instance;
            if (ctrl?.Colony == null) return false;
            Camera cam = ViewCamera();
            if (cam == null) return false;

            int home = 0;
            for (int i = 0; i < ctrl.Colony.Board.Tiles.Count; i++)
            {
                if (ctrl.Colony.Board.At(i).Building == MainGameBuildingId.CommandPost)
                {
                    home = i;
                    break;
                }
            }

            var hits = Physics.RaycastAll(
                cam.ScreenPointToRay(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f)),
                5000f, ~0, QueryTriggerInteraction.Collide);
            foreach (var h in hits)
            {
                if (h.transform.GetComponentInParent<MainGameSurveyCheckClick>() != null)
                {
                    screen = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
                    return true;
                }
            }

            foreach (var mark in Object.FindObjectsByType<MainGameSurveyCheckClick>())
            {
                if (mark == null) continue;
                Vector3 w = mark.transform.position + Vector3.up * 0.5f;
                Vector3 s = cam.WorldToScreenPoint(w);
                if (s.z < 0f) continue;
                screen = new Vector2(s.x, s.y);
                return true;
            }

            return false;
        }

        static bool TryGetPaintTargetScreen(out Vector2 screen)
        {
            screen = default;
            var ctrl = MainGameController.Instance;
            var colony = ctrl?.Colony;
            if (ctrl == null || colony == null) return false;

            int home = 0;
            for (int i = 0; i < colony.Board.Tiles.Count; i++)
            {
                if (colony.Board.At(i).Building == MainGameBuildingId.CommandPost)
                {
                    home = i;
                    break;
                }
            }

            int anchor = MainGameDroneScan.Instance != null && MainGameDroneScan.Instance.PaintCount > 0
                ? MainGameDroneScan.Instance.LastPaintedTileIndex
                : home;
            if (anchor < 0) anchor = home;

            int ice = FindUnrevealedIce(colony, home);
            int bestTile = -1;
            float bestScore = float.MaxValue;
            if (!ctrl.TryGetCell(anchor, out Vector2Int anchorCell)) return false;

            var neighbors = new List<Vector2Int>(6);
            ColonyTileGrid.CollectOrthogonalNeighborCells(anchorCell, neighbors);
            Camera cam = ViewCamera();
            Vector2 mouseGuess = new Vector2(Screen.width * 0.52f, Screen.height * 0.55f);

            foreach (var cell in neighbors)
            {
                if (!ctrl.TryGetTileIndex(ColonyTileGrid.CellToWorldOnGround(cell), out int idx)) continue;
                if (MainGameDroneScan.Instance == null || !MainGameDroneScan.Instance.CanAcceptPaint(idx)) continue;

                Vector3 world = ColonyTileGrid.CellToWorldOnGround(cell);
                Vector3 sp = cam.WorldToScreenPoint(world + Vector3.up * 0.75f);
                if (sp.z < 0f) continue;

                int distIce = ice >= 0 ? colony.Dist(idx, ice) : 0;
                float px = Vector2.Distance(new Vector2(sp.x, sp.y), mouseGuess);
                float score = px - distIce * 12f;
                if (score >= bestScore) continue;
                bestScore = score;
                bestTile = idx;
                screen = new Vector2(sp.x, sp.y);
            }

            return bestTile >= 0;
        }

        static bool TryGetScreenForTile(int tile, out Vector2 screen)
        {
            screen = default;
            var ctrl = MainGameController.Instance;
            if (ctrl == null || !ctrl.TryGetCell(tile, out Vector2Int cell)) return false;
            Camera cam = ViewCamera();
            if (cam == null) return false;
            Vector3 world = ColonyTileGrid.CellToWorldOnGround(cell);
            Vector3 sp = cam.WorldToScreenPoint(world + Vector3.up * 0.75f);
            if (sp.z < 0f) return false;
            screen = new Vector2(sp.x, sp.y);
            return true;
        }

        static int FindUnrevealedIce(MainGameColony colony, int home)
        {
            int best = -1;
            int bestD = int.MaxValue;
            for (int i = 0; i < colony.Board.Tiles.Count; i++)
            {
                var t = colony.Board.At(i);
                if (t.DepositLeft <= 0) continue;
                if (t.Feature != MainGameFeature.Ice && t.Feature != MainGameFeature.Glacier) continue;
                if (t.Revealed) continue;
                int d = colony.Dist(home, i);
                if (d >= bestD) continue;
                bestD = d;
                best = i;
            }
            return best;
        }

        static bool TryGetPlacementClick(out Vector2 screen)
        {
            screen = default;
#if UNITY_EDITOR
            if (PlayerInput.TryGetPlaytestPlacementTile(out int tile))
                return TryGetScreenForTile(tile, out screen);
#endif
            return false;
        }

        static int FindBuildingTile(MainGameColony colony, MainGameBuildingId id)
        {
            for (int i = 0; i < colony.Board.Tiles.Count; i++)
                if (colony.Board.At(i).Building == id) return i;
            return -1;
        }

        static int FindAnyRevealedIce(MainGameColony colony)
        {
            for (int i = 0; i < colony.Board.Tiles.Count; i++)
            {
                var t = colony.Board.At(i);
                if (!t.Revealed || t.DepositLeft <= 0) continue;
                if (t.Feature == MainGameFeature.Ice || t.Feature == MainGameFeature.Glacier)
                    return i;
            }
            return -1;
        }

        static Camera ViewCamera()
        {
            var cam = PlayerInput.Instance != null ? PlayerInput.Instance.GameplayCamera : null;
            if (cam != null && cam.enabled) return cam;
            return Camera.main;
        }

        static class BriefingUi
        {
            public static bool TryGetContinueClick(out Vector2 screen)
            {
                screen = default;
                var hud = MainGameHUD.Instance;
                if (hud == null || !hud.TryGetBriefingContinueScreenPoint(out screen)) return false;
                return true;
            }
        }
    }
}