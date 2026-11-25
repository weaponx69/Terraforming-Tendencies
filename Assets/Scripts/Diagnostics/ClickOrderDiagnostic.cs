using System.Collections.Generic;
using System.Diagnostics;
using GameDevTV.RTS.Prologue;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.AI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace GameDevTV.RTS.Diagnostics
{
    /// <summary>Metrics from exercising real Prologue survey code (not NavMesh stubs).</summary>
    public struct PrologueSurveyStressResult
    {
        public bool Success;
        public string Error;
        public int TargetTile;
        public int PaintCountAfter;
        public bool PrologueActive;
        public bool ScanMode;
        public double ClickFrameDispatchMs;
        public double PathDrainMs;
        public double VisionFollowUpMs;
        public double TotalWallMs;
        public float AvgFrameTimeMs;
        public float MaxDispatchSpikeMs;
    }

    public enum PrologueSurveyStressCase
    {
        MarkClickProduction,
        MarkClickSynchronousPath,
        ConfirmSurveyTeam,
    }
    /// <summary>
    /// Isolates FPS hitches on survey / move orders: strict ground raycast vs path assignment.
    /// Attach to an empty GameObject; assign camera, ClickPlane layer mask, and movable units.
    /// </summary>
    public sealed class ClickOrderDiagnostic : MonoBehaviour
    {
        const string LogTag = "[ClickOrderDiagnostic]";

        [Header("Raycast")]
        [SerializeField] Camera raycastCamera;
        [SerializeField] LayerMask clickPlaneLayers;
        [SerializeField] string clickPlaneLayerName = "ClickPlane";
        [SerializeField] float rayDistance = 5000f;

        [Header("Units to receive orders")]
        [SerializeField] Transform[] dispatchUnits;

        [Header("Diagnostic toggles")]
        [Tooltip("ON: log hit point only — no path work, no receiver updates.")]
        [SerializeField] bool runRaycastOnly;

        [Tooltip("ON: enqueue path jobs; at most one job per frame. OFF: full path work on the click frame.")]
        [SerializeField] bool useAsynchronousQueue;

        [Header("Path source")]
        [Tooltip("OFF: two-point stub (isolates NavMesh/path cost). ON: NavMesh.CalculatePath.")]
        [SerializeField] bool useNavMeshPath = true;

        [SerializeField] bool skipClicksOverUi = true;
        [SerializeField] bool logTiming = true;

        readonly Queue<PathJob> jobQueue = new();
        ClickPathReceiver[] receivers;

        struct PathJob
        {
            public int ReceiverIndex;
            public Vector3 Start;
            public Vector3 Goal;
        }

        void Awake()
        {
            if (raycastCamera == null)
                raycastCamera = Camera.main;

            EnsureClickPlaneMask();
            BindReceivers();
        }

        /// <summary>Play Mode automation: re-bind camera and units after spawning the diagnostic.</summary>
        public void SetAutomationRefs(Camera cam, Transform[] units)
        {
            if (cam != null) raycastCamera = cam;
            if (units != null && units.Length > 0) dispatchUnits = units;
            EnsureClickPlaneMask();
            BindReceivers();
        }

        void EnsureClickPlaneMask()
        {
            if (clickPlaneLayers.value != 0) return;
            int layer = LayerMask.NameToLayer(clickPlaneLayerName);
            if (layer < 0)
            {
                UnityEngine.Debug.LogWarning(
                    $"{LogTag} Layer '{clickPlaneLayerName}' missing — assign Click Plane Layers manually.");
                return;
            }

            clickPlaneLayers = 1 << layer;
        }

        void BindReceivers()
        {
            if (dispatchUnits == null || dispatchUnits.Length == 0)
            {
                receivers = System.Array.Empty<ClickPathReceiver>();
                return;
            }

            receivers = new ClickPathReceiver[dispatchUnits.Length];
            for (int i = 0; i < dispatchUnits.Length; i++)
            {
                Transform t = dispatchUnits[i];
                if (t == null) continue;
                var receiver = t.GetComponent<ClickPathReceiver>();
                if (receiver == null)
                    receiver = t.gameObject.AddComponent<ClickPathReceiver>();
                receivers[i] = receiver;
            }
        }

        void Update()
        {
            if (useAsynchronousQueue && !runRaycastOnly)
                DrainQueue(1);

            if (!WasPrimaryClickThisFrame()) return;
            ProcessPlayerClick();
        }

        void ProcessPlayerClick()
        {
            if (skipClicksOverUi && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            var rayWatch = Stopwatch.StartNew();
            if (!TryRaycastClickPlane(out RaycastHit hit))
            {
                if (logTiming)
                    UnityEngine.Debug.Log($"{LogTag} Raycast missed (mask={clickPlaneLayers.value}).");
                return;
            }

            rayWatch.Stop();
            if (logTiming)
                UnityEngine.Debug.Log(
                    $"{LogTag} Hit {hit.point} layer={LayerMask.LayerToName(hit.collider.gameObject.layer)} " +
                    $"raycast={rayWatch.Elapsed.TotalMilliseconds:F3} ms");

            DispatchToGoal(hit.point, rayWatch.Elapsed.TotalMilliseconds);
        }

        /// <summary>Automation / CLI: configure toggles at runtime.</summary>
        public void Configure(bool raycastOnly, bool asyncQueue, bool navMeshPath)
        {
            runRaycastOnly = raycastOnly;
            useAsynchronousQueue = asyncQueue;
            useNavMeshPath = navMeshPath;
            jobQueue.Clear();
        }

        /// <summary>Ground raycast only — returns timing JSON fragment.</summary>
        public bool ProbeRaycastScreen(Vector2 screen, out double raycastMs, out Vector3 hitPoint)
        {
            hitPoint = default;
            raycastMs = 0;
            if (raycastCamera == null) return false;
            var w = Stopwatch.StartNew();
            Ray ray = raycastCamera.ScreenPointToRay(screen);
            bool ok = Physics.Raycast(ray, out RaycastHit hit, rayDistance, clickPlaneLayers, QueryTriggerInteraction.Ignore);
            w.Stop();
            raycastMs = w.Elapsed.TotalMilliseconds;
            if (!ok) return false;
            hitPoint = hit.point;
            return true;
        }

        /// <summary>Skip input; exercise dispatch pipeline to a world goal (after optional raycast-only gate).</summary>
        public double DispatchToGoal(Vector3 goal, double raycastMsLogged = 0)
        {
            if (runRaycastOnly) return raycastMsLogged;

            var dispatchWatch = Stopwatch.StartNew();
            for (int i = 0; i < receivers.Length; i++)
            {
                if (receivers[i] == null || dispatchUnits[i] == null) continue;
                var job = new PathJob
                {
                    ReceiverIndex = i,
                    Start = dispatchUnits[i].position,
                    Goal = goal
                };

                if (useAsynchronousQueue)
                    jobQueue.Enqueue(job);
                else
                    Fulfill(job);
            }

            dispatchWatch.Stop();
            if (logTiming && !useAsynchronousQueue)
                UnityEngine.Debug.Log(
                    $"{LogTag} Sync path dispatch ({receivers.Length} unit(s)) " +
                    $"wall={dispatchWatch.Elapsed.TotalMilliseconds:F3} ms");
            return dispatchWatch.Elapsed.TotalMilliseconds;
        }

        /// <summary>Drain async queue synchronously (automation end-of-burst).</summary>
        public int DrainAllQueued()
        {
            int n = 0;
            while (jobQueue.Count > 0)
            {
                Fulfill(jobQueue.Dequeue());
                n++;
            }

            return n;
        }

        public int PendingJobs => jobQueue.Count;
        public int UnitCount => receivers?.Length ?? 0;

        /// <summary>
        /// Drives <see cref="PrologueDroneScan"/> / survey mission — real hex paint, BFS, reveal, and fog queues.
        /// </summary>
        public PrologueSurveyStressResult RunPrologueSurveyStress(PrologueSurveyStressCase scenario)
        {
            var result = new PrologueSurveyStressResult
            {
                PrologueActive = PrologueController.IsActive,
            };

            if (!PrologueController.IsActive || PrologueController.Instance?.Colony == null)
            {
                result.Error = "prologue_inactive";
                return result;
            }

            var scan = PrologueDroneScan.Instance;
            if (scan == null)
            {
                var go = new GameObject("PrologueDroneScan_Diagnostic");
                scan = go.AddComponent<PrologueDroneScan>();
            }

            int markTile = ResolveSurveyMarkTile();
            if (markTile < 0)
            {
                result.Error = "no_survey_mark";
                return result;
            }

            result.TargetTile = markTile;

            bool prevSync = PrologueDroneScan.DiagnosticForceSynchronousMarkPath;
            try
            {
                switch (scenario)
                {
                    case PrologueSurveyStressCase.MarkClickProduction:
                        PrologueDroneScan.DiagnosticForceSynchronousMarkPath = false;
                        result = ExecuteMarkClickStress(scan, markTile);
                        break;
                    case PrologueSurveyStressCase.MarkClickSynchronousPath:
                        PrologueDroneScan.DiagnosticForceSynchronousMarkPath = true;
                        result = ExecuteMarkClickStress(scan, markTile);
                        break;
                    case PrologueSurveyStressCase.ConfirmSurveyTeam:
                        PrologueDroneScan.DiagnosticForceSynchronousMarkPath = false;
                        ExecuteMarkClickStress(scan, markTile);
                        result = ExecuteConfirmStress(scan);
                        result.TargetTile = markTile;
                        break;
                }
            }
            finally
            {
                PrologueDroneScan.DiagnosticForceSynchronousMarkPath = prevSync;
            }

            result.PrologueActive = PrologueController.IsActive;
            result.ScanMode = scan.ScanMode;
            return result;
        }

        static PrologueSurveyStressResult ExecuteMarkClickStress(PrologueDroneScan scan, int markTile)
        {
            var result = new PrologueSurveyStressResult { TargetTile = markTile };
            if (PrologueSurveyMission.Instance != null && PrologueSurveyMission.Instance.IsSearching)
                PrologueSurveyMission.Instance.AbortForPlaytest();
            scan.CancelPaint();
            scan.BeginPaintMission(PrologueSurveyMission.DefaultTileBudget, PrologueSurveyMission.DefaultDays);

            PrologueFpsMonitor.BeginBurstCapture();
            var total = Stopwatch.StartNew();
            var clickWatch = Stopwatch.StartNew();
            bool clicked = scan.AutomationOrderMarkTile(markTile);
            clickWatch.Stop();
            PrologueFpsMonitor.RecordSyntheticSpike(clickWatch.Elapsed.TotalMilliseconds);

            result.ClickFrameDispatchMs = clickWatch.Elapsed.TotalMilliseconds;
            result.Success = clicked;

            var drainWatch = Stopwatch.StartNew();
            DrainSurveyPathPipeline(scan);
            drainWatch.Stop();
            PrologueFpsMonitor.RecordSyntheticSpike(drainWatch.Elapsed.TotalMilliseconds);
            result.PathDrainMs = drainWatch.Elapsed.TotalMilliseconds;

            result.VisionFollowUpMs = PrologueVisionThrottle.TickOneSliceMs();
            PrologueFpsMonitor.RecordSyntheticSpike(result.VisionFollowUpMs);

            total.Stop();
            PrologueFpsMonitor.EndBurstCapture();

            result.PaintCountAfter = scan.PaintCount;
            result.TotalWallMs = total.Elapsed.TotalMilliseconds;
            result.AvgFrameTimeMs = PrologueFpsMonitor.AvgFrameTimeMs;
            result.MaxDispatchSpikeMs = PrologueFpsMonitor.MaxDispatchSpikeMs;
            result.Success = clicked && scan.PaintCount > 0;
            if (!result.Success && string.IsNullOrEmpty(result.Error))
                result.Error = scan.PaintCount == 0 ? "paint_route_empty" : "click_failed";
            return result;
        }

        static PrologueSurveyStressResult ExecuteConfirmStress(PrologueDroneScan scan)
        {
            var result = new PrologueSurveyStressResult();
            if (scan.PaintCount < PrologueDroneScan.MinConfirmTiles)
            {
                result.Error = "insufficient_paint_for_confirm";
                result.PaintCountAfter = scan.PaintCount;
                return result;
            }

            PrologueFpsMonitor.BeginBurstCapture();
            var total = Stopwatch.StartNew();
            var clickWatch = Stopwatch.StartNew();
            bool confirmed = scan.ConfirmPaint();
            clickWatch.Stop();
            PrologueFpsMonitor.RecordSyntheticSpike(clickWatch.Elapsed.TotalMilliseconds);

            result.ClickFrameDispatchMs = clickWatch.Elapsed.TotalMilliseconds;
            result.Success = confirmed;

            result.VisionFollowUpMs = PrologueVisionThrottle.TickOneSliceMs();
            PrologueFpsMonitor.RecordSyntheticSpike(result.VisionFollowUpMs);

            total.Stop();
            PrologueFpsMonitor.EndBurstCapture();

            result.TotalWallMs = total.Elapsed.TotalMilliseconds;
            result.AvgFrameTimeMs = PrologueFpsMonitor.AvgFrameTimeMs;
            result.MaxDispatchSpikeMs = PrologueFpsMonitor.MaxDispatchSpikeMs;
            result.PaintCountAfter = scan.PaintCount;
            if (!confirmed)
                result.Error = "confirm_rejected";
            return result;
        }

        static void DrainSurveyPathPipeline(PrologueDroneScan scan)
        {
            var pathQueue = PrologueHexPathQueue.Instance
                            ?? new GameObject("PrologueHexPathQueue").AddComponent<PrologueHexPathQueue>();

            var watchdog = Stopwatch.StartNew();
            while ((scan.IsRoutingMark || pathQueue.HasPendingJobs) && watchdog.ElapsedMilliseconds < 15_000)
            {
                var slice = Stopwatch.StartNew();
                pathQueue.Pump(PrologueHexPathQueue.ExpansionsPerFrame * 8);
                slice.Stop();
                PrologueFpsMonitor.RecordSyntheticSpike(slice.Elapsed.TotalMilliseconds);
            }
        }

        static int ResolveSurveyMarkTile()
        {
            var ctrl = PrologueController.Instance;
            if (ctrl == null) return -1;

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

            int best = -1;
            int bestD = int.MaxValue;
            var marks = PrologueInteractableIndex.DepositMarks;
            for (int m = 0; m < marks.Count; m++)
            {
                var mark = marks[m];
                if (mark == null) continue;
                if (!TryTileForSurveyMarkDiagnostic(ctrl, mark.GroundPoint, out int tile)) continue;
                int d = home >= 0 && colony != null ? colony.Dist(home, tile) : 0;
                if (d >= bestD) continue;
                bestD = d;
                best = tile;
            }

            if (best >= 0) return best;

            var ice = PrologueInteractableIndex.IceClicks;
            for (int i = 0; i < ice.Count; i++)
            {
                if (ice[i] == null) continue;
                if (ice[i].TileIndex >= 0) return ice[i].TileIndex;
            }

            return -1;
        }

        static bool TryTileForSurveyMarkDiagnostic(PrologueController ctrl, Vector3 ground, out int tileIndex)
        {
            tileIndex = -1;
            if (ctrl == null) return false;
            ground.y = GameDevTV.RTS.Player.ColonyTileGrid.SampleGroundY(ground);
            if (ctrl.TryGetTileIndex(ground, out tileIndex)) return true;
            var cell = GameDevTV.RTS.Player.ColonyTileGrid.WorldToCell(ground);
            return ctrl.TryGetTileIndex(
                GameDevTV.RTS.Player.ColonyTileGrid.CellToWorldOnGround(cell), out tileIndex);
        }

        void DrainQueue(int maxJobs)
        {
            for (int n = 0; n < maxJobs && jobQueue.Count > 0; n++)
                Fulfill(jobQueue.Dequeue());
        }

        void Fulfill(PathJob job)
        {
            Vector3[] corners = BuildPathCorners(job.Start, job.Goal);
            receivers[job.ReceiverIndex].StoreRoute(corners);
        }

        Vector3[] BuildPathCorners(Vector3 start, Vector3 goal)
        {
            if (!useNavMeshPath)
                return new[] { start, goal };

            var path = new NavMeshPath();
            if (NavMesh.CalculatePath(start, goal, NavMesh.AllAreas, path) && path.corners.Length > 0)
                return path.corners;

            return new[] { start, goal };
        }

        bool TryRaycastClickPlane(out RaycastHit hit)
        {
            hit = default;
            if (raycastCamera == null) return false;

            Ray ray = raycastCamera.ScreenPointToRay(ScreenPoint());
            return Physics.Raycast(
                ray,
                out hit,
                rayDistance,
                clickPlaneLayers,
                QueryTriggerInteraction.Ignore);
        }

        static Vector2 ScreenPoint()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
                return Mouse.current.position.ReadValue();
#endif
            return Input.mousePosition;
        }

        static bool WasPrimaryClickThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
#else
            return Input.GetMouseButtonDown(0);
#endif
        }
    }

    /// <summary>
    /// Stores the last route only — no length aggregation or per-corner logging on store.
    /// </summary>
    public sealed class ClickPathReceiver : MonoBehaviour
    {
        Vector3[] route;

        public int CornerCount => route?.Length ?? 0;

        public Vector3 GetCorner(int index) => route != null && index >= 0 && index < route.Length ? route[index] : default;

        public void StoreRoute(IReadOnlyList<Vector3> corners)
        {
            if (corners == null || corners.Count == 0)
            {
                route = null;
                return;
            }

            route = new Vector3[corners.Count];
            for (int i = 0; i < corners.Count; i++)
                route[i] = corners[i];
        }

        public void StoreRoute(Vector3[] corners)
        {
            if (corners == null || corners.Length == 0)
            {
                route = null;
                return;
            }

            route = new Vector3[corners.Length];
            System.Array.Copy(corners, route, corners.Length);
        }
    }
}