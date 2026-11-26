using System.Collections.Generic;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.Units;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace GameDevTV.RTS.Prologue
{
    /// <summary>
    /// Survey Team paint mode: play the Survey card, paint up to the card's tile budget,
    /// click ✓ — then <see cref="PrologueSurveyMission"/> runs colony days + team animation.
    /// </summary>
    public class PrologueDroneScan : MonoBehaviour
    {
        public static PrologueDroneScan Instance { get; private set; }

        /// <summary>Off in slice perf defaults — rubber-band BFS + raycasts each frame cost FPS in paint mode.</summary>
        public static bool EnableRubberBandPreview;

        /// <summary>Diagnostics only: run full hex BFS + RebuildMarks on the click frame (pre-queue behaviour).</summary>
        public static bool DiagnosticForceSynchronousMarkPath;

        /// <summary>Yellow cylinder per painted hex — off by default; use HUD tile count instead.</summary>
        public static bool ShowPaintMarkMeshes;

        public bool ScanMode { get; private set; }
        /// <summary>Survey Team card played — waiting for the player to click a ? before paint mode.</summary>
        public bool AwaitingMarkPick { get; private set; }
        /// <summary>True while the survey team is in the field (days ticking).</summary>
        public bool IsFlying => PrologueSurveyMission.Instance != null && PrologueSurveyMission.Instance.IsSearching;
        public bool HasPaint => route.Count > 0;
        public int PaintCount => route.Count;
        public int LastPaintedTileIndex => route.Count > 0 ? route[route.Count - 1] : -1;

        public bool CanAcceptPaint(int tileIndex)
        {
            if (!ScanMode || IsFlying) return false;
            var colony = PrologueController.Instance?.Colony;
            if (colony == null) return false;
            return string.IsNullOrEmpty(PaintRefusal(colony, tileIndex));
        }

        public const int MinConfirmTiles = 2;

        int maxPaintTiles = PrologueSurveyMission.DefaultTileBudget;
        int missionDays = PrologueSurveyMission.DefaultDays;

        readonly List<int> route = new();
        readonly List<GameObject> marks = new();

        GameObject checkmarkGo;
        int droneTile = -1;
        bool paintStrokeActive;

        LineRenderer paintPreviewLine;
        const int RubberBandSegments = 20;
        const float PreviewRefreshSec = 1f / 15f;
        const float PaintDragIntervalSec = 1f / 20f;
        float nextPreviewAt;
        float nextPaintDragAt;
        Vector2 lastPreviewMouse = new(float.NaN, float.NaN);
        float nextBillboardAt;
        int routingMarkTarget = -1;
        int selectedMarkGoal = -1;
        int paintHitsFrame = -1;
        Vector2Int paintHitCellCache;
        bool paintHitCellValid;
        static readonly RaycastHit[] paintRayHits = new RaycastHit[24];

        public bool IsRoutingMark => routingMarkTarget >= 0;
        static readonly Color PreviewOk = new(1f, 0.88f, 0.22f, 0.92f);
        static readonly Color PreviewBad = new(1f, 0.38f, 0.28f, 0.75f);
        static readonly Color PreviewOverBudget = new(1f, 0.45f, 0.12f, 0.9f);

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            ClearMarks();
            if (paintPreviewLine != null)
                Destroy(paintPreviewLine.gameObject);
        }

        /// <summary>HUD / legacy entry — starts a default Survey Team paint mission.</summary>
        public void SetScanMode(bool on)
        {
            if (!on)
            {
                CancelPaint();
                ScanMode = false;
                AwaitingMarkPick = false;
                selectedMarkGoal = -1;
                SetSurveyPaintVisuals(false);
                return;
            }
            BeginPaintMission(PrologueSurveyMission.DefaultTileBudget, PrologueSurveyMission.DefaultDays);
        }

        /// <summary>Survey Team card — pick a ? before painting. Orbital Scan may pan the camera.</summary>
        public void ArmSurveyFromCard(int tileBudget, int days)
        {
            if (PrologueSurveyMission.Instance != null && PrologueSurveyMission.Instance.IsSearching)
            {
                ShowHint("Wait for the current Survey Team to finish.");
                return;
            }

            maxPaintTiles = Mathf.Max(MinConfirmTiles, tileBudget);
            missionDays = Mathf.Max(1, days);
            AwaitingMarkPick = true;
            selectedMarkGoal = -1;
            ScanMode = false;
            CancelPaint();

            PrologueIceMarkers.Instance?.EnsureMarkersForActiveObjectives();
            ShowHint(
                $"Survey Team ready — click a yellow ? (near the on-screen icon if a hill blocks the ray), then paint from camp " +
                $"(up to {maxPaintTiles} tiles). Play Orbital Scan to find or pan to a target. " +
                $"{missionDays} colony day{(missionDays == 1 ? "" : "s")} after [OK].");
        }

        /// <summary>Enter paint mode with the card's tile budget and day cost (playtests / legacy).</summary>
        public void BeginPaintMission(int tileBudget, int days)
        {
            if (PrologueSurveyMission.Instance != null && PrologueSurveyMission.Instance.IsSearching)
            {
                ShowHint("Wait for the current Survey Team to finish.");
                return;
            }

            maxPaintTiles = Mathf.Max(MinConfirmTiles, tileBudget);
            missionDays = Mathf.Max(1, days);
            AwaitingMarkPick = false;
            selectedMarkGoal = -1;
            CancelPaint();
            ScanMode = true;
            SetSurveyPaintVisuals(true);
            nextPreviewAt = 0f;
            lastPreviewMouse = new Vector2(float.NaN, float.NaN);
            var ctrl = PrologueController.Instance;
            droneTile = ctrl?.Colony != null ? FindHomeLit(ctrl.Colony) : -1;
            if (ctrl != null)
                PrologueSearchedTiles.Instance?.EnsureOverlayReadyPublic(ctrl);
            ShowHint(
                $"Survey Team ready — paint from camp toward a yellow ? (up to {maxPaintTiles} tiles), then click [OK]. " +
                $"{missionDays} colony day{(missionDays == 1 ? "" : "s")} after ✓.");
        }

        void BeginPaintTowardMark(int markTile)
        {
            if (markTile < 0) return;
            int priorGoal = selectedMarkGoal;
            bool switchingTarget = ScanMode && priorGoal >= 0 && priorGoal != markTile;

            AwaitingMarkPick = false;
            selectedMarkGoal = markTile;

            CancelPaint();
            ScanMode = true;
            SetSurveyPaintVisuals(true);
            nextPreviewAt = 0f;
            lastPreviewMouse = new Vector2(float.NaN, float.NaN);

            var ctrl = PrologueController.Instance;
            droneTile = ctrl?.Colony != null ? FindHomeLit(ctrl.Colony) : -1;
            if (ctrl != null)
            {
                PrologueSearchedTiles.Instance?.EnsureOverlayReadyPublic(ctrl);
                ProloguePathGroundOverlay.Instance?.FitBoardIfNeeded(ctrl);
            }

            PrologueIceMarkers.Instance?.PulseTileForSurvey(markTile);

            string change = switchingTarget ? "New target — " : "";
            ShowHint(
                $"{change}Click or drag on lit ground next to camp to paint toward this ? — up to {maxPaintTiles} tiles, then [OK] ({missionDays} day{(missionDays == 1 ? "" : "s")}).");
        }

        static void SetSurveyPaintVisuals(bool active)
        {
            EnableRubberBandPreview = active;
            ShowPaintMarkMeshes = active;
        }

        public void NotifySearchFinished()
        {
            ScanMode = false;
            AwaitingMarkPick = false;
            selectedMarkGoal = -1;
            SetSurveyPaintVisuals(false);
            CancelPaint();
        }

        /// <summary>Ice-marker / legacy: add one tile to the paint trail (does not search yet).</summary>
        public void SendDroneToTile(int goalTile) => TryAddPaint(goalTile);

        /// <summary>Confirm paint → Survey Team starts colony-day search on the path.</summary>
        public bool ConfirmPaint() => ConfirmPaint(MinConfirmTiles);

        bool ConfirmPaint(int minimumTiles)
        {
            if (PrologueSurveyMission.Instance != null && PrologueSurveyMission.Instance.IsSearching)
                return false;
            if (route.Count < minimumTiles)
            {
                ShowHint($"Paint at least {minimumTiles} tiles, then click the green [OK].");
                return false;
            }

            var path = new List<int>(route);
            int days = missionDays;
            PrologueSearchedTiles.Instance?.PaintTrailTiles(path, immediateGpu: true);
            AwaitingMarkPick = false;
            selectedMarkGoal = -1;
            SetSurveyPaintVisuals(false);
            CancelPaint();
            ScanMode = false;

            var mission = PrologueSurveyMission.Instance
                          ?? new GameObject("PrologueSurveyMission").AddComponent<PrologueSurveyMission>();
            return mission.StartSearch(path, days);
        }

        public void CancelPaint()
        {
            route.Clear();
            paintStrokeActive = false;
            ClearMarks();
            HidePaintPreview();
        }

        void LateUpdate()
        {
            if (!PrologueController.IsActive || !ScanMode || IsFlying || !EnableRubberBandPreview)
            {
                HidePaintPreview();
                return;
            }

            bool due = Time.unscaledTime >= nextPreviewAt;
            if (!due && !MouseMovedSinceLastPreview())
                return;
            nextPreviewAt = Time.unscaledTime + PreviewRefreshSec;
            UpdatePaintPreview();
        }

        bool MouseMovedSinceLastPreview()
        {
            if (Mouse.current == null) return false;
            Vector2 mouse = Mouse.current.position.ReadValue();
            if (float.IsNaN(lastPreviewMouse.x))
                return true;
            return Vector2.SqrMagnitude(mouse - lastPreviewMouse) > 36f;
        }

        void Update()
        {
            if (!PrologueController.IsActive) return;
            if (Mouse.current == null) return;
            if (IsFlying) return;

            if (AwaitingMarkPick)
            {
                if (PointerBlocksWorldClick()) return;
                if (Mouse.current.leftButton.wasPressedThisFrame)
                {
                    if (TryHoverQuestionMarkTile(out int pick)
                        || TryClickDeposit(out pick)
                        || TryClickIceMarker(out pick))
                        BeginPaintTowardMark(pick);
                    else if (PrologueIceMarkers.Instance != null
                             && PrologueIceMarkers.Instance.TryGetNearestSurveyBoardTile(out int auto, out _))
                        BeginPaintTowardMark(auto);
                    else
                        ShowHint("Click the yellow ? — or play Orbital Scan to pan to the nearest target.");
                }
                return;
            }

            if (!ScanMode) return;

            if (Time.unscaledTime >= nextBillboardAt)
            {
                nextBillboardAt = Time.unscaledTime + 0.08f;
                BillboardCheckmark();
            }

            if (Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame)
            {
                ConfirmPaint();
                return;
            }

            if (Mouse.current.rightButton.wasPressedThisFrame)
            {
                if (route.Count > 0)
                {
                    route.RemoveAt(route.Count - 1);
                    RebuildMarks();
                    ShowHint(route.Count == 0
                        ? "Trail cleared — click or drag from the camp or a surveyed path to start again."
                        : route.Count >= MinConfirmTiles
                            ? $"Trail {route.Count}/{maxPaintTiles} — keep painting, or click the green [OK] to search."
                            : $"Trail {route.Count}/{maxPaintTiles} — paint one more tile for [OK].");
                }
                else
                {
                    ShowHint("No trail yet — click or drag from lit ground toward a yellow ?.");
                }
                return;
            }

            if (PointerBlocksWorldClick()) return;

            bool pressed = Mouse.current.leftButton.wasPressedThisFrame;
            bool held = Mouse.current.leftButton.isPressed;
            bool released = Mouse.current.leftButton.wasReleasedThisFrame;

            if (pressed)
            {
                if (TryClickCheckmark())
                {
                    paintStrokeActive = false;
                    ConfirmPaint();
                    return;
                }

                if (TryClickIceMarker(out int iceTile))
                {
                    if (iceTile != selectedMarkGoal || !ScanMode)
                        BeginPaintTowardMark(iceTile);
                    else
                    {
                        paintStrokeActive = true;
                        TryPaintUnderCursor(allowIceMarker: false);
                    }
                    return;
                }

                if (TryClickDeposit(out int depositTile))
                {
                    if (depositTile != selectedMarkGoal || !ScanMode)
                        BeginPaintTowardMark(depositTile);
                    else
                    {
                        paintStrokeActive = true;
                        TryPaintUnderCursor(allowIceMarker: false);
                    }
                    return;
                }

                if (TryResolvePaintTile(allowIceMarker: false, out int paintTile)
                    && !route.Contains(paintTile)
                    && string.IsNullOrEmpty(PaintRefusal(PrologueController.Instance.Colony, paintTile)))
                {
                    paintStrokeActive = true;
                    TryAddPaint(paintTile);
                    return;
                }

                paintStrokeActive = true;
                TryPaintUnderCursor(allowIceMarker: false);
            }
            else if (held && paintStrokeActive)
            {
                if (Time.unscaledTime >= nextPaintDragAt)
                {
                    nextPaintDragAt = Time.unscaledTime + PaintDragIntervalSec;
                    TryPaintUnderCursor(allowIceMarker: false);
                }
            }
            else if (released)
            {
                paintStrokeActive = false;
                FlushPaintOverlayGpu();
                if (route.Count >= MinConfirmTiles)
                    ShowHint($"Trail {route.Count}/{maxPaintTiles} — keep painting, or click the green [OK] to search ({missionDays} days).");
                else if (route.Count > 0)
                    ShowHint($"Trail {route.Count}/{maxPaintTiles} — paint at least {MinConfirmTiles} tiles for [OK].");
            }
        }

        void TryPaintUnderCursor(bool allowIceMarker)
        {
            if (TryResolvePaintTile(allowIceMarker, out int tile))
                TryAddPaint(tile);
        }

        bool TryResolvePaintTile(bool allowIceMarker, out int tile)
        {
            tile = -1;
            if (allowIceMarker && TryClickIceMarker(out int markerTile))
            {
                tile = markerTile;
                return true;
            }
            if (TryPickBestPaintTile(out tile))
                return true;
            return false;
        }

        /// <summary>
        /// Combine plane picks, terrain hits, and screen-nearest valid neighbor so uphill paths do not stall at 2 hexes.
        /// </summary>
        bool TryPickBestPaintTile(out int tileIndex)
        {
            tileIndex = -1;
            var ctrl = PrologueController.Instance;
            var colony = ctrl?.Colony;
            Camera cam = ViewCamera();
            if (ctrl == null || colony == null || cam == null || Mouse.current == null)
                return false;

            var candidates = new HashSet<Vector2Int>();
            if (TryPickPaintCell(out Vector2Int onCampPlane))
                candidates.Add(onCampPlane);

            if (route.Count > 0 && ctrl.TryGetCell(route[route.Count - 1], out Vector2Int lastCell))
            {
                float trailY = ColonyTileGrid.SampleGroundY(ColonyTileGrid.CellToWorldOnGround(lastCell));
                if (TryPickPaintCellOnPlane(trailY, out Vector2Int onTrailPlane))
                    candidates.Add(onTrailPlane);

                var neighbors = new List<Vector2Int>(6);
                ColonyTileGrid.CollectOrthogonalNeighborCells(lastCell, neighbors);
                for (int i = 0; i < neighbors.Count; i++)
                    candidates.Add(neighbors[i]);
            }
            else if (droneTile >= 0 && ctrl.TryGetCell(droneTile, out Vector2Int homeCell))
            {
                var neighbors = new List<Vector2Int>(6);
                ColonyTileGrid.CollectOrthogonalNeighborCells(homeCell, neighbors);
                for (int i = 0; i < neighbors.Count; i++)
                    candidates.Add(neighbors[i]);
            }

            Vector2 mouse = Mouse.current.position.ReadValue();
            bool haveNear = false;
            foreach (var cell in candidates)
            {
                if (!ctrl.TryGetTileIndex(ColonyTileGrid.CellToWorldOnGround(cell), out int probe)) continue;
                Vector3 w = ColonyTileGrid.CellToWorldOnGround(cell);
                Vector3 sc = cam.WorldToScreenPoint(w + Vector3.up * 0.5f);
                if (sc.z < 0f) continue;
                if (Vector2.Distance(new Vector2(sc.x, sc.y), mouse) <= 96f)
                {
                    haveNear = true;
                    break;
                }
            }

            if (!haveNear && TryPickPaintCell(out Vector2Int planeCell))
                candidates.Add(planeCell);
            float bestPx = float.MaxValue;
            int best = -1;
            foreach (var cell in candidates)
            {
                if (!ctrl.TryGetTileIndex(ColonyTileGrid.CellToWorldOnGround(cell), out int idx)) continue;
                if (!string.IsNullOrEmpty(PaintRefusal(colony, idx))) continue;

                Vector3 world = ColonyTileGrid.CellToWorldOnGround(cell);
                Vector3 screen = cam.WorldToScreenPoint(world + Vector3.up * 0.5f);
                if (screen.z < 0f) continue;
                float px = Vector2.Distance(new Vector2(screen.x, screen.y), mouse);
                if (px >= bestPx) continue;
                bestPx = px;
                best = idx;
            }

            if (best < 0) return false;
            tileIndex = best;
            return true;
        }

        bool TryAddPaint(int tile)
        {
            var ctrl = PrologueController.Instance;
            if (ctrl?.Colony == null || IsFlying) return false;

            string why = PaintRefusal(ctrl.Colony, tile);
            if (!string.IsNullOrEmpty(why))
            {
                bool quiet = why == "Already painted"
                             || (Mouse.current != null && Mouse.current.leftButton.isPressed
                                 && !Mouse.current.leftButton.wasPressedThisFrame);
                if (!quiet)
                    ShowHint(why);
                return false;
            }

            route.Add(tile);
            RebuildMarks();
            var searched = PrologueSearchedTiles.Instance;
            searched?.EnsureOverlayReadyPublic(ctrl);
            searched?.PaintTrailTiles(new[] { tile }, immediateGpu: true);

            ShowHint(route.Count >= MinConfirmTiles
                ? $"Trail {route.Count}/{maxPaintTiles} — keep painting, or click the green [OK] to search."
                : $"Trail {route.Count}/{maxPaintTiles} — paint one more touching tile for [OK].");
            return true;
        }

        string PaintRefusal(PrologueColony colony, int tile)
        {
            if (tile < 0 || tile >= colony.Board.Tiles.Count)
                return "Off the map.";
            if (route.Contains(tile))
                return "Already painted";

            if (route.Count >= maxPaintTiles)
                return $"Paint budget full ({maxPaintTiles}/{maxPaintTiles}). Click the green [OK] to search.";

            if (route.Count == 0)
            {
                bool nearHome = droneTile >= 0 && (tile == droneTile || colony.Dist(droneTile, tile) == 1);
                if (!TouchesSurveyed(colony, tile) && !nearHome)
                    return "Start on the camp or on a path you already surveyed.";
                return string.Empty;
            }

            if (colony.Dist(route[route.Count - 1], tile) != 1)
                return "Paint the next tile beside the last one.";

            return string.Empty;
        }

        static bool TouchesSurveyed(PrologueColony colony, int tile)
        {
            if (IsSurveyed(colony, tile)) return true;
            var ctrl = PrologueController.Instance;
            if (ctrl == null || !ctrl.TryGetCell(tile, out Vector2Int cell)) return false;

            var neighbors = new List<Vector2Int>(6);
            ColonyTileGrid.CollectOrthogonalNeighborCells(cell, neighbors);
            for (int i = 0; i < neighbors.Count; i++)
            {
                if (!ctrl.TryGetTileIndex(ColonyTileGrid.CellToWorldOnGround(neighbors[i]), out int n)) continue;
                if (IsSurveyed(colony, n)) return true;
            }
            return false;
        }

        static bool IsSurveyed(PrologueColony colony, int index)
        {
            var t = colony.Board.At(index);
            return t.Lit || t.Revealed || t.Building == PrologueBuildingId.CommandPost;
        }

        void RebuildMarks()
        {
            ClearMarks();
            var ctrl = PrologueController.Instance;
            if (ctrl == null) return;

            if (ShowPaintMarkMeshes)
            {
                var mat = MarkMaterial(new Color(1f, 0.75f, 0.25f, 1f));
                for (int i = 0; i < route.Count; i++)
                {
                    if (!ctrl.TryGetCell(route[i], out Vector2Int cell)) continue;
                    Vector3 pos = ColonyTileGrid.CellToWorldOnGround(cell);
                    pos.y += 0.35f;

                    var mark = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    mark.name = $"SurveyPaint_{route[i]}";
                    mark.transform.position = pos;
                    mark.transform.localScale = new Vector3(3.2f, 0.08f, 3.2f);
                    Object.Destroy(mark.GetComponent<Collider>());
                    mark.GetComponent<MeshRenderer>().sharedMaterial = mat;
                    marks.Add(mark);
                }
            }

            if (route.Count >= MinConfirmTiles)
                SpawnCheckmarkOnLast(ctrl);
        }

        void SpawnCheckmarkOnLast(PrologueController ctrl)
        {
            int last = route[route.Count - 1];
            if (!ctrl.TryGetCell(last, out Vector2Int cell)) return;
            Vector3 pos = ColonyTileGrid.CellToWorldOnGround(cell);
            pos.y += 6.2f;

            checkmarkGo = new GameObject("SurveyCheckmark");
            checkmarkGo.transform.position = pos;
            checkmarkGo.AddComponent<PrologueSurveyCheckClick>();

            var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pole.name = "Pole";
            pole.transform.SetParent(checkmarkGo.transform, false);
            pole.transform.localPosition = new Vector3(0f, -1.1f, 0f);
            pole.transform.localScale = new Vector3(0.12f, 1.0f, 0.12f);
            Object.Destroy(pole.GetComponent<Collider>());
            pole.GetComponent<MeshRenderer>().sharedMaterial = MarkMaterial(new Color(0.1f, 0.45f, 0.2f, 1f));

            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = "Disc";
            disc.transform.SetParent(checkmarkGo.transform, false);
            disc.transform.localPosition = Vector3.zero;
            disc.transform.localScale = new Vector3(1.6f, 0.08f, 1.6f);
            Object.Destroy(disc.GetComponent<Collider>());
            disc.GetComponent<MeshRenderer>().sharedMaterial = MarkMaterial(new Color(0.12f, 0.78f, 0.32f, 1f));

            var label = new GameObject("CheckLabel");
            label.transform.SetParent(checkmarkGo.transform, false);
            label.transform.localPosition = new Vector3(0f, 0.55f, 0f);
            var tm = label.AddComponent<TextMesh>();
            tm.text = "OK";
            tm.fontSize = 48;
            tm.characterSize = 0.22f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = Color.white;
            tm.fontStyle = FontStyle.Bold;

            var clickVol = new GameObject("CheckClick");
            clickVol.transform.SetParent(checkmarkGo.transform, false);
            clickVol.transform.localPosition = Vector3.zero;
            var cap = clickVol.AddComponent<CapsuleCollider>();
            cap.direction = 1;
            cap.radius = 2.4f;
            cap.height = 9.5f;
            cap.center = new Vector3(0f, -3.1f, 0f);

            // No point light — extra pixel pass per frame on the dense planet mesh.
        }

        void ClearMarks()
        {
            for (int i = 0; i < marks.Count; i++)
            {
                if (marks[i] != null) Destroy(marks[i]);
            }
            marks.Clear();
            if (checkmarkGo != null)
            {
                Destroy(checkmarkGo);
                checkmarkGo = null;
            }
        }

        void BillboardCheckmark()
        {
            if (checkmarkGo == null) return;
            var ctrl = PrologueController.Instance;
            if (ctrl != null && route.Count > 0)
            {
                int last = route[route.Count - 1];
                if (ctrl.TryGetCell(last, out Vector2Int cell))
                {
                    Vector3 ground = ColonyTileGrid.CellToWorldOnGround(cell);
                    checkmarkGo.transform.position = ground + Vector3.up * 6.2f;
                }
            }

            Camera cam = ViewCamera();
            if (cam == null) return;
            var label = checkmarkGo.transform.Find("CheckLabel");
            if (label != null)
                label.rotation = Quaternion.LookRotation(label.position - cam.transform.position);
        }

        static Material paintTrailMat;
        static Material checkPoleMat;
        static Material checkDiscMat;

        static Material MarkMaterial(Color c)
        {
            if (Mathf.Approximately(c.r, 1f) && Mathf.Approximately(c.g, 0.75f))
            {
                if (paintTrailMat != null) return paintTrailMat;
                paintTrailMat = CreateUnlit(c);
                return paintTrailMat;
            }

            if (Mathf.Approximately(c.g, 0.78f))
            {
                if (checkDiscMat != null) return checkDiscMat;
                checkDiscMat = CreateUnlit(c);
                return checkDiscMat;
            }

            if (checkPoleMat != null) return checkPoleMat;
            checkPoleMat = CreateUnlit(c);
            return checkPoleMat;
        }

        static Material CreateUnlit(Color c)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit")
                         ?? Shader.Find("Unlit/Color")
                         ?? Shader.Find("Standard");
            var m = new Material(shader);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            m.color = c;
            return m;
        }

        static void ShowHint(string msg)
        {
            GameDevTV.RTS.UI.PrologueHUD.Instance?.ShowNote(new List<string> { msg });
        }

        static bool PointerBlocksWorldClick() => GameDevTV.RTS.UI.UiPlaythroughUtil.PointerBlocksWorldInteraction();

        bool TryClickCheckmark()
        {
            if (checkmarkGo == null || route.Count < MinConfirmTiles) return false;
            return IsMouseNearCheckmark();
        }

        bool IsMouseNearCheckmark()
        {
            Camera cam = ViewCamera();
            if (cam == null || checkmarkGo == null || Mouse.current == null) return false;
            Vector3 world = checkmarkGo.transform.position + Vector3.up * 0.45f;
            Vector3 screen = cam.WorldToScreenPoint(world);
            if (screen.z < 0f) return false;
            Vector2 mouse = Mouse.current.position.ReadValue();
            const float pickPx = 56f;
            return Vector2.Distance(new Vector2(screen.x, screen.y), mouse) <= pickPx;
        }

        bool TryClickIceMarker(out int tileIndex) =>
            TryPickIceTileFromRaycast(out tileIndex) || TryPickIceTileNearCursor(out tileIndex);

        bool TryClickDeposit(out int tileIndex) =>
            TryPickDepositFromRaycast(out tileIndex) || TryPickDepositTileNearCursor(out tileIndex);

        bool TryPickIceTileFromRaycast(out int tileIndex)
        {
            tileIndex = -1;
            Camera cam = ViewCamera();
            if (cam == null || Mouse.current == null) return false;
            Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (!Physics.Raycast(ray, out RaycastHit hit, 1200f, ~0, QueryTriggerInteraction.Ignore))
                return false;
            var ice = hit.collider.GetComponentInParent<PrologueIceMarkerClick>();
            if (ice == null || ice.TileIndex < 0) return false;
            tileIndex = ice.TileIndex;
            return true;
        }

        bool TryPickDepositFromRaycast(out int tileIndex)
        {
            tileIndex = -1;
            var ctrl = PrologueController.Instance;
            Camera cam = ViewCamera();
            if (ctrl == null || cam == null || Mouse.current == null) return false;
            Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (!Physics.Raycast(ray, out RaycastHit hit, 1200f, ~0, QueryTriggerInteraction.Ignore))
                return false;
            var mark = hit.collider.GetComponentInParent<PrologueDepositMark>();
            if (mark == null || !TryTileForSurveyMark(ctrl, mark.GroundPoint, out int tile)) return false;
            tileIndex = tile;
            return true;
        }

        bool TryPickDepositTileNearCursor(out int tileIndex)
        {
            tileIndex = -1;
            var ctrl = PrologueController.Instance;
            Camera cam = ViewCamera();
            if (ctrl == null || cam == null || Mouse.current == null) return false;

            const float pickPx = 72f;
            float bestPx = float.MaxValue;
            int best = -1;
            Vector2 mouse = Mouse.current.position.ReadValue();
            var depositMarks = PrologueInteractableIndex.DepositMarks;
            for (int m = 0; m < depositMarks.Count; m++)
            {
                var mark = depositMarks[m];
                if (mark == null) continue;
                if (!TryTileForSurveyMark(ctrl, mark.GroundPoint, out int tile)) continue;
                Vector3 screen = cam.WorldToScreenPoint(mark.transform.position);
                if (screen.z < 0f) continue;
                float px = Vector2.Distance(new Vector2(screen.x, screen.y), mouse);
                if (px > pickPx || px >= bestPx) continue;
                bestPx = px;
                best = tile;
            }

            if (best < 0) return false;
            tileIndex = best;
            return true;
        }

        bool TrySendTeamToMark(int target)
        {
            var ctrl = PrologueController.Instance;
            var colony = ctrl?.Colony;
            if (colony == null) return false;

            int start = route.Count > 0 ? route[route.Count - 1] : droneTile;
            if (start < 0) start = FindHomeLit(colony);
            if (start < 0 || target < 0) return false;
            if (target == start || route.Contains(target))
            {
                ShowHint("That ? is already on the trail.");
                return true;
            }

            if (routingMarkTarget == target)
                return true;

            if (DiagnosticForceSynchronousMarkPath)
            {
                routingMarkTarget = target;
                if (PrologueHexPathQueue.TryFindPathNow(ctrl, colony, start, target, out var syncPath, tryAltGoals: true))
                    ApplyMarkPath(target, syncPath, true);
                else
                    ApplyMarkPath(target, null, false);
                return true;
            }

            routingMarkTarget = target;
            var queue = PrologueHexPathQueue.Instance
                          ?? new GameObject("PrologueHexPathQueue").AddComponent<PrologueHexPathQueue>();
            queue.RequestMarkPath(this, ctrl, colony, start, target);
            ShowHint("Painting route…");
            return true;
        }

        /// <summary>CLI / diagnostic: order survey team toward a deposit or ice ? tile (real paint pipeline).</summary>
        public bool AutomationOrderMarkTile(int targetTile) => TrySendTeamToMark(targetTile);

        /// <summary>Reset paint mode for repeatable matrix runs.</summary>
        public void AutomationPreparePaintMission()
        {
            if (PrologueSurveyMission.Instance != null && PrologueSurveyMission.Instance.IsSearching)
                PrologueSurveyMission.Instance.AbortForPlaytest();
            CancelPaint();
            BeginPaintMission(PrologueSurveyMission.DefaultTileBudget, PrologueSurveyMission.DefaultDays);
        }

        /// <summary>Diagnostics: preload a multi-segment paint trail without running confirm.</summary>
        public void AutomationSeedPaintRoute(IReadOnlyList<int> tilePath)
        {
            CancelPaint();
            BeginPaintMission(PrologueSurveyMission.DefaultTileBudget, PrologueSurveyMission.DefaultDays);
            if (tilePath == null) return;
            var colony = PrologueController.Instance?.Colony;
            if (colony == null) return;

            for (int i = 0; i < tilePath.Count; i++)
            {
                int tile = tilePath[i];
                if (tile < 0 || route.Contains(tile)) continue;
                if (!string.IsNullOrEmpty(PaintRefusal(colony, tile)))
                {
                    if (route.Count == 0)
                        route.Add(tile);
                    continue;
                }

                route.Add(tile);
            }

            if (route.Count > 0)
            {
                RebuildMarks();
                PrologueSearchedTiles.Instance?.PaintTrailTiles(route, immediateGpu: true);
            }
        }

        /// <summary>Called from <see cref="PrologueHexPathQueue"/> when mark routing finishes.</summary>
        public void ApplyMarkPath(int target, List<int> path, bool ok)
        {
            routingMarkTarget = -1;
            var ctrl = PrologueController.Instance;
            var colony = ctrl?.Colony;
            if (colony == null) return;

            if (!ok || path == null || path.Count < 2)
            {
                ShowHint("Can't reach that ? from camp — paint closer, then click the ? again.");
                return;
            }

            int need = path.Count - 1;
            int room = maxPaintTiles - route.Count;
            if (need > room)
            {
                ShowHint($"That ? is {need} tiles away. You can paint {Mathf.Max(0, room)} more — shorten the route or raise survey budget.");
                return;
            }

            var painted = new List<int>();
            for (int i = 1; i < path.Count; i++)
            {
                if (route.Contains(path[i])) continue;
                route.Add(path[i]);
                painted.Add(path[i]);
            }

            if (painted.Count > 0)
            {
                PrologueSearchedTiles.Instance?.EnsureOverlayReadyPublic(ctrl);
                PrologueSearchedTiles.Instance?.PaintTrailTiles(painted, immediateGpu: true);
            }

            paintStrokeActive = false;
            HidePaintPreview();
            RebuildMarks();
            ShowHint(route.Count >= MinConfirmTiles
                ? $"Path painted to the ? ({route.Count}/{maxPaintTiles}) — click the green [OK] to send the team ({missionDays} days)."
                : $"Paint at least {MinConfirmTiles - route.Count} more tile{(MinConfirmTiles - route.Count == 1 ? "" : "s")} along the path, then click the green [OK].");
        }

        static bool TryTileForSurveyMark(PrologueController ctrl, Vector3 ground, out int tileIndex)
        {
            tileIndex = -1;
            if (ctrl == null) return false;
            ground.y = ColonyTileGrid.SampleGroundY(ground);
            if (ctrl.TryGetTileIndex(ground, out tileIndex))
                return true;

            Vector2Int cell = ColonyTileGrid.WorldToCell(ground);
            if (ctrl.TryGetTileIndex(ColonyTileGrid.CellToWorldOnGround(cell), out tileIndex))
                return true;

            var colony = ctrl.Colony;
            if (colony == null) return false;

            int best = -1;
            int bestD = int.MaxValue;
            for (int i = 0; i < colony.Board.Tiles.Count; i++)
            {
                if (!ctrl.TryGetCell(i, out Vector2Int c)) continue;
                int d = ColonyTileGrid.HexDistance(cell, c);
                if (d >= bestD) continue;
                bestD = d;
                best = i;
            }
            if (best < 0 || bestD > 2) return false;
            tileIndex = best;
            return true;
        }

        static bool TryHexPathToGoal(PrologueController ctrl, PrologueColony colony, int start, int goal, out List<int> path) =>
            PrologueHexPathQueue.TryFindPathNow(ctrl, colony, start, goal, out path, tryAltGoals: true);

        static int FindHomeLit(PrologueColony c)
        {
            for (int i = 0; i < c.Board.Tiles.Count; i++)
            {
                var t = c.Board.At(i);
                if (t.Building == PrologueBuildingId.CommandPost) return i;
            }
            for (int i = 0; i < c.Board.Tiles.Count; i++)
                if (c.Board.At(i).Lit) return i;
            return c.Board.Tiles.Count > 0 ? 0 : -1;
        }

        static Camera ViewCamera()
        {
            var cam = GameDevTV.RTS.Player.PlayerInput.Instance != null
                ? GameDevTV.RTS.Player.PlayerInput.Instance.GameplayCamera
                : null;
            if (cam != null && cam.enabled) return cam;
            return Camera.main;
        }

        /// <summary>
        /// Hex under cursor: terrain raycast first, then trail/camp plane fallback for flat areas.
        /// </summary>
        bool TryPickPaintCell(out Vector2Int cell)
        {
            if (paintHitsFrame == Time.frameCount && paintHitCellValid)
            {
                cell = paintHitCellCache;
                return true;
            }

            var ctrl = PrologueController.Instance;
            Camera cam = ViewCamera();

            if (TryRaycastPaintGround(cam, out Vector3 ground)
                && ctrl != null
                && TryTileForSurveyMark(ctrl, ground, out int fromTerrain)
                && ctrl.TryGetCell(fromTerrain, out Vector2Int terrainCell))
            {
                cell = terrainCell;
                paintHitsFrame = Time.frameCount;
                paintHitCellCache = cell;
                paintHitCellValid = true;
                return true;
            }

            if (route.Count > 0 && ctrl != null && ctrl.TryGetCell(route[route.Count - 1], out Vector2Int lastCell))
            {
                float trailY = ColonyTileGrid.SampleGroundY(ColonyTileGrid.CellToWorldOnGround(lastCell));
                if (TryPickPaintCellOnPlane(trailY, out cell))
                {
                    paintHitsFrame = Time.frameCount;
                    paintHitCellCache = cell;
                    paintHitCellValid = true;
                    return true;
                }
            }

            if (!TryPickPaintCellOnPlane(PaintPlaneY(), out cell))
            {
                paintHitCellValid = false;
                return false;
            }

            paintHitsFrame = Time.frameCount;
            paintHitCellCache = cell;
            paintHitCellValid = true;
            return true;
        }

        static bool TryRaycastPaintGround(Camera cam, out Vector3 ground)
        {
            ground = default;
            if (cam == null || Mouse.current == null) return false;

            Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
            int count = Physics.RaycastNonAlloc(
                ray, paintRayHits, 800f, ~0, QueryTriggerInteraction.Ignore);
            float bestDist = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                var hit = paintRayHits[i];
                if (hit.collider == null) continue;
                var t = hit.collider.transform;
                if (t.GetComponentInParent<PrologueSurveyCheckClick>() != null) continue;
                if (t.GetComponentInParent<PrologueIceMarkerClick>() != null) continue;
                if (t.GetComponentInParent<BaseBuilding>() != null) continue;
                if (t.GetComponentInParent<LineRenderer>() != null
                    && t.name.IndexOf("Survey", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;
                if (hit.distance >= bestDist) continue;
                bestDist = hit.distance;
                ground = hit.point;
            }

            return bestDist < float.MaxValue;
        }

        static bool TryPickPaintCellOnPlane(float planeY, out Vector2Int cell)
        {
            cell = default;
            Camera cam = ViewCamera();
            if (cam == null || Mouse.current == null) return false;

            var plane = new Plane(Vector3.up, new Vector3(0f, planeY, 0f));
            Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (!plane.Raycast(ray, out float enter)) return false;

            cell = ColonyTileGrid.WorldToCell(ray.GetPoint(enter));
            return true;
        }

        static void FlushPaintOverlayGpu() =>
            ProloguePathGroundOverlay.Instance?.ApplyImmediate();

        float PaintPlaneY()
        {
            var ctrl = PrologueController.Instance;
            if (ctrl == null) return 0f;
            int anchor = droneTile >= 0 ? droneTile : FindHomeLit(ctrl.Colony);
            if (anchor < 0 || !ctrl.TryGetCell(anchor, out Vector2Int home)) return 0f;
            Vector3 w = ColonyTileGrid.CellToWorld(home, 0f);
            return ColonyTileGrid.SampleGroundY(w);
        }

        void EnsurePaintPreviewLine()
        {
            if (paintPreviewLine != null) return;
            var go = new GameObject("SurveyRubberBandPreview");
            go.transform.SetParent(transform, false);
            paintPreviewLine = go.AddComponent<LineRenderer>();
            paintPreviewLine.useWorldSpace = true;
            paintPreviewLine.shadowCastingMode = ShadowCastingMode.Off;
            paintPreviewLine.receiveShadows = false;
            paintPreviewLine.numCapVertices = 6;
            paintPreviewLine.numCornerVertices = 4;
            paintPreviewLine.textureMode = LineTextureMode.Stretch;
            var shader = Shader.Find("Universal Render Pipeline/Unlit")
                         ?? Shader.Find("Unlit/Color")
                         ?? Shader.Find("Sprites/Default");
            var mat = new Material(shader);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", PreviewOk);
            paintPreviewLine.material = mat;
            paintPreviewLine.positionCount = 0;
        }

        void HidePaintPreview()
        {
            if (paintPreviewLine != null)
                paintPreviewLine.positionCount = 0;
        }

        void UpdatePaintPreview()
        {
            if (Mouse.current != null)
                lastPreviewMouse = Mouse.current.position.ReadValue();

            if (PointerBlocksWorldClick())
            {
                HidePaintPreview();
                return;
            }

            var ctrl = PrologueController.Instance;
            var colony = ctrl?.Colony;
            if (ctrl == null || colony == null)
            {
                HidePaintPreview();
                return;
            }

            int start = route.Count > 0 ? route[route.Count - 1] : droneTile;
            if (start < 0) start = FindHomeLit(colony);
            if (start < 0 || !ctrl.TryGetCell(start, out Vector2Int startCell))
            {
                HidePaintPreview();
                return;
            }

            Vector3 startPos = TileBandPoint(startCell);
            int goalTile = selectedMarkGoal >= 0 ? selectedMarkGoal : -1;
            if (goalTile < 0 && TryHoverQuestionMarkTile(out int markTile))
                goalTile = markTile;
            else if (goalTile < 0 && TryPickBestPaintTile(out int pickTile))
                goalTile = pickTile;
            else if (TryPickPaintCell(out Vector2Int hoverCell)
                     && ctrl.TryGetTileIndex(ColonyTileGrid.CellToWorldOnGround(hoverCell), out int hoverIdx))
                goalTile = hoverIdx;

            Vector3 cursorPos = TryGetMouseBandPoint(out Vector3 raw) ? raw : startPos;

            var stroke = new List<Vector3>(64) { startPos };
            Color color = PreviewBad;
            List<int> path = null;
            bool viable = false;
            if (goalTile >= 0 && goalTile != start)
            {
                var pathQueue = PrologueHexPathQueue.Instance
                                ?? new GameObject("PrologueHexPathQueue").AddComponent<PrologueHexPathQueue>();
                pathQueue.RequestPreviewPath(ctrl, colony, start, goalTile);
                viable = pathQueue.TryGetPreviewPath(start, goalTile, out path) && path != null && path.Count > 1;
            }

            if (viable)
            {
                color = PreviewOk;
                int need = path.Count - 1;
                int room = maxPaintTiles - route.Count;
                if (need > room)
                    color = PreviewOverBudget;

                for (int i = 1; i < path.Count; i++)
                {
                    if (!ctrl.TryGetCell(path[i], out Vector2Int c)) continue;
                    stroke.Add(TileBandPoint(c));
                }

                Vector3 anchor = stroke[stroke.Count - 1];
                Vector3 stretchEnd = cursorPos;
                if (TryHoverQuestionMarkTile(out _) && ctrl.TryGetCell(goalTile, out Vector2Int goalCell))
                    stretchEnd = TileBandPoint(goalCell);

                AppendRubberBand(stroke, anchor, stretchEnd);
            }
            else
                AppendRubberBand(stroke, startPos, cursorPos);

            EnsurePaintPreviewLine();
            float pulse = 0.26f + 0.07f * Mathf.Sin(Time.unscaledTime * 11f);
            paintPreviewLine.widthMultiplier = pulse;
            paintPreviewLine.startColor = color;
            paintPreviewLine.endColor = new Color(color.r, color.g, color.b, color.a * 0.65f);
            if (paintPreviewLine.material != null)
                paintPreviewLine.material.color = color;

            paintPreviewLine.positionCount = stroke.Count;
            for (int i = 0; i < stroke.Count; i++)
                paintPreviewLine.SetPosition(i, stroke[i]);
        }

        static Vector3 TileBandPoint(Vector2Int cell)
        {
            Vector3 p = ColonyTileGrid.CellToWorldOnGround(cell);
            return p + Vector3.up * 0.5f;
        }

        static void AppendRubberBand(List<Vector3> stroke, Vector3 from, Vector3 to)
        {
            if (stroke.Count > 0 && Vector3.SqrMagnitude(stroke[stroke.Count - 1] - from) > 0.0001f)
                stroke.Add(from);

            float dist = Vector3.Distance(from, to);
            float sag = Mathf.Clamp(dist * 0.09f, 0.06f, 2.8f);
            float wobble = Mathf.Sin(Time.unscaledTime * 13f) * Mathf.Clamp(dist * 0.015f, 0.02f, 0.35f);

            for (int i = 1; i <= RubberBandSegments; i++)
            {
                float t = i / (float)RubberBandSegments;
                Vector3 p = Vector3.Lerp(from, to, t);
                p.y -= Mathf.Sin(t * Mathf.PI) * (sag + wobble);
                stroke.Add(p);
            }
        }

        bool TryGetMouseBandPoint(out Vector3 point)
        {
            point = default;
            if (TryRaycastPaintGround(ViewCamera(), out Vector3 ground))
            {
                point = ColonyTileGrid.ApplyGroundY(ground) + Vector3.up * 0.5f;
                return true;
            }
            if (TryPickPaintCell(out Vector2Int cell))
            {
                point = ColonyTileGrid.CellToWorldOnGround(cell) + Vector3.up * 0.5f;
                return true;
            }
            return false;
        }

        bool TryPickIceTileNearCursor(out int tileIndex)
        {
            tileIndex = -1;
            var ctrl = PrologueController.Instance;
            Camera cam = ViewCamera();
            if (ctrl == null || cam == null || Mouse.current == null) return false;

            const float pickPx = 112f;
            float bestPx = float.MaxValue;
            int best = -1;
            Vector2 mouse = Mouse.current.position.ReadValue();
            var iceClicks = PrologueInteractableIndex.IceClicks;
            for (int c = 0; c < iceClicks.Count; c++)
            {
                var click = iceClicks[c];
                if (click == null) continue;
                Vector3 screen = cam.WorldToScreenPoint(click.transform.position);
                if (screen.z < 0f) continue;
                float px = Vector2.Distance(new Vector2(screen.x, screen.y), mouse);
                if (px > pickPx || px >= bestPx) continue;
                bestPx = px;
                best = click.TileIndex;
            }

            if (best < 0) return false;
            tileIndex = best;
            return true;
        }

        bool TryHoverQuestionMarkTile(out int tileIndex)
        {
            tileIndex = -1;
            if (TryPickDepositTileNearCursor(out tileIndex))
                return true;

            var ctrl = PrologueController.Instance;
            Camera cam = ViewCamera();
            if (ctrl == null || cam == null || Mouse.current == null) return false;

            const float pickPx = 112f;
            float bestPx = float.MaxValue;
            int best = -1;
            Vector2 mouse = Mouse.current.position.ReadValue();
            var iceClicks = PrologueInteractableIndex.IceClicks;
            for (int c = 0; c < iceClicks.Count; c++)
            {
                var click = iceClicks[c];
                if (click == null) continue;
                Vector3 screen = cam.WorldToScreenPoint(click.transform.position);
                if (screen.z < 0f) continue;
                float px = Vector2.Distance(new Vector2(screen.x, screen.y), mouse);
                if (px > pickPx || px >= bestPx) continue;
                bestPx = px;
                best = click.TileIndex;
            }

            if (best < 0) return false;
            tileIndex = best;
            return true;
        }

        /// <summary>Ui playthrough: trail touches ice (within one hex of a deposit).</summary>
        public bool PathReachesIceDeposit()
        {
            var colony = PrologueController.Instance?.Colony;
            if (colony == null || route.Count < MinConfirmTiles) return false;
            if (colony.PrologueStep >= 4 && colony.SecondIceTargetTile() >= 0)
                return colony.ReachesNewIce(route[route.Count - 1]);
            int goal = FindNearestIceGoal(colony);
            if (goal < 0) return false;
            return colony.Dist(route[route.Count - 1], goal) <= 1;
        }

        public bool TryPlaytestNextPaintTowardIce(out int tileIndex)
        {
            tileIndex = -1;
            var ctrl = PrologueController.Instance;
            var colony = ctrl?.Colony;
            if (ctrl == null || colony == null || !ScanMode || IsFlying) return false;

            int start = route.Count > 0 ? route[route.Count - 1] : droneTile >= 0 ? droneTile : FindHomeLit(colony);
            if (start < 0) return false;

            int goal = FindNearestIceGoal(colony);
            if (goal < 0) return false;
            if (!TryHexPathToGoal(ctrl, colony, start, goal, out var path) || path == null || path.Count < 2)
                return false;

            for (int i = 1; i < path.Count; i++)
            {
                int step = path[i];
                if (route.Contains(step)) continue;
                if (!string.IsNullOrEmpty(PaintRefusal(colony, step))) continue;
                tileIndex = step;
                return true;
            }

            return TryPlaytestGreedyPaintStep(ctrl, colony, start, goal, out tileIndex);
        }

        /// <summary>When BFS path steps are already painted, pick any valid neighbor that moves closer to ice.</summary>
        bool TryPlaytestGreedyPaintStep(PrologueController ctrl, PrologueColony colony, int start, int goal,
            out int tileIndex)
        {
            tileIndex = -1;
            if (!ctrl.TryGetCell(start, out Vector2Int startCell)) return false;
            var neighbors = new List<Vector2Int>(6);
            ColonyTileGrid.CollectOrthogonalNeighborCells(startCell, neighbors);

            int best = -1;
            int bestD = int.MaxValue;
            int startDist = colony.Dist(start, goal);
            foreach (var cell in neighbors)
            {
                if (!ctrl.TryGetTileIndex(ColonyTileGrid.CellToWorldOnGround(cell), out int idx)) continue;
                if (!string.IsNullOrEmpty(PaintRefusal(colony, idx))) continue;
                int d = colony.Dist(idx, goal);
                if (d > startDist) continue;
                if (d >= bestD) continue;
                bestD = d;
                best = idx;
            }

            if (best < 0) return false;
            tileIndex = best;
            return true;
        }

        /// <summary>Paint a full valid path toward the nearest ice "?" when the budget allows.</summary>
        public bool TryPlaytestRouteToNearestIceMark()
        {
            var ctrl = PrologueController.Instance;
            var colony = ctrl?.Colony;
            if (ctrl == null || colony == null || !ScanMode || IsFlying) return false;

            int home = FindHomeLit(colony);
            if (home < 0) return false;

            int bestTile = -1;
            int bestD = int.MaxValue;
            int newIce = colony.PrologueStep >= 4 ? colony.SecondIceTargetTile() : -1;
            var depositMarks = PrologueInteractableIndex.DepositMarks;
            for (int m = 0; m < depositMarks.Count; m++)
            {
                var mark = depositMarks[m];
                if (mark == null) continue;
                if (!TryTileForSurveyMark(ctrl, mark.GroundPoint, out int tile)) continue;
                if (newIce >= 0 && tile != newIce) continue;
                var t = colony.Board.At(tile);
                if (t.Feature != PrologueFeature.Ice && t.Feature != PrologueFeature.Glacier) continue;
                int d = colony.Dist(home, tile);
                if (d >= bestD) continue;
                bestD = d;
                bestTile = tile;
            }

            if (bestTile < 0) return false;
            return TrySendTeamToMark(bestTile);
        }

        static int FindNearestIceGoal(PrologueColony colony)
        {
            if (colony.PrologueStep >= 4)
            {
                int nu = colony.SecondIceTargetTile();
                if (nu >= 0) return nu;
            }

            int home = FindHomeLit(colony);
            if (home < 0) return -1;
            int best = -1;
            int bestD = int.MaxValue;
            for (int i = 0; i < colony.Board.Tiles.Count; i++)
            {
                var t = colony.Board.At(i);
                if (t.DepositLeft <= 0) continue;
                if (t.Feature != PrologueFeature.Ice && t.Feature != PrologueFeature.Glacier) continue;
                if (colony.PrologueStep >= 4 && colony.Board.FirstIceTiles.Contains(i)) continue;
                int d = colony.Dist(home, i);
                if (d >= bestD) continue;
                bestD = d;
                best = i;
            }
            return best;
        }
    }
}
