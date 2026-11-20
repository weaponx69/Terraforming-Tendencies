using System.Collections.Generic;
using GameDevTV.RTS.Player;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
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

        public bool ScanMode { get; private set; }
        /// <summary>True while the survey team is in the field (days ticking).</summary>
        public bool IsFlying => PrologueSurveyMission.Instance != null && PrologueSurveyMission.Instance.IsSearching;
        public bool HasPaint => route.Count > 0;
        public int PaintCount => route.Count;

        public const int MinConfirmTiles = 2;

        int maxPaintTiles = PrologueSurveyMission.DefaultTileBudget;
        int missionDays = PrologueSurveyMission.DefaultDays;

        readonly List<int> route = new();
        readonly List<GameObject> marks = new();

        GameObject checkmarkGo;
        int droneTile = -1;
        bool paintStrokeActive;

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            ClearMarks();
        }

        /// <summary>HUD / legacy entry — starts a default Survey Team paint mission.</summary>
        public void SetScanMode(bool on)
        {
            if (!on)
            {
                CancelPaint();
                ScanMode = false;
                return;
            }
            BeginPaintMission(PrologueSurveyMission.DefaultTileBudget, PrologueSurveyMission.DefaultDays);
        }

        /// <summary>Enter paint mode with the card's tile budget and day cost.</summary>
        public void BeginPaintMission(int tileBudget, int days)
        {
            if (PrologueSurveyMission.Instance != null && PrologueSurveyMission.Instance.IsSearching)
            {
                ShowHint("Wait for the current Survey Team to finish.");
                return;
            }

            maxPaintTiles = Mathf.Max(MinConfirmTiles, tileBudget);
            missionDays = Mathf.Max(1, days);
            CancelPaint();
            ScanMode = true;
            var ctrl = PrologueController.Instance;
            droneTile = ctrl?.Colony != null ? FindHomeLit(ctrl.Colony) : -1;
            ShowHint(
                $"Survey Team ready — paint up to {maxPaintTiles} touching tiles toward a yellow ?, then ✓. " +
                $"Search takes {missionDays} colony day{(missionDays == 1 ? "" : "s")}.");
        }

        public void NotifySearchFinished()
        {
            ScanMode = false;
            CancelPaint();
        }

        /// <summary>Ice-marker / legacy: add one tile to the paint trail (does not search yet).</summary>
        public void SendDroneToTile(int goalTile) => TryAddPaint(goalTile);

        /// <summary>Confirm paint → Survey Team starts colony-day search on the path.</summary>
        public bool ConfirmPaint()
        {
            if (PrologueSurveyMission.Instance != null && PrologueSurveyMission.Instance.IsSearching)
                return false;
            if (route.Count < MinConfirmTiles)
            {
                ShowHint($"Paint at least {MinConfirmTiles} tiles, then click the green ✓.");
                return false;
            }

            var path = new List<int>(route);
            int days = missionDays;
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
        }

        void Update()
        {
            if (!PrologueController.IsActive) return;
            if (Mouse.current == null) return;
            if (!ScanMode) return;
            if (IsFlying) return;

            BillboardCheckmark();

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
                        ? "Trail cleared — click or drag from lit ground to start again."
                        : route.Count >= MinConfirmTiles
                            ? $"Trail {route.Count}/{maxPaintTiles} — keep painting, or click the green ✓ to search."
                            : $"Trail {route.Count}/{maxPaintTiles} — paint one more tile for the ✓.");
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

                if (TryResolvePaintTile(allowIceMarker: true, out int paintTile)
                    && !route.Contains(paintTile)
                    && string.IsNullOrEmpty(PaintRefusal(PrologueController.Instance.Colony, paintTile)))
                {
                    paintStrokeActive = true;
                    TryAddPaint(paintTile);
                    return;
                }

                paintStrokeActive = true;
                TryPaintUnderCursor(allowIceMarker: true);
            }
            else if (held && paintStrokeActive)
            {
                TryPaintUnderCursor(allowIceMarker: false);
            }
            else if (released)
            {
                paintStrokeActive = false;
                if (route.Count >= MinConfirmTiles)
                    ShowHint($"Trail {route.Count}/{maxPaintTiles} — keep painting, or click the green ✓ to search ({missionDays} days).");
                else if (route.Count > 0)
                    ShowHint($"Trail {route.Count}/{maxPaintTiles} — paint at least {MinConfirmTiles} tiles for the ✓.");
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
            if (TryRaycastGround(out Vector3 world)
                && PrologueController.Instance != null
                && PrologueController.Instance.TryGetTileIndex(world, out int hit))
            {
                tile = hit;
                return true;
            }
            return false;
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

            ShowHint(route.Count >= MinConfirmTiles
                ? $"Trail {route.Count}/{maxPaintTiles} — keep painting, or click the green ✓ to search."
                : $"Trail {route.Count}/{maxPaintTiles} — paint one more touching tile for the ✓.");
            return true;
        }

        string PaintRefusal(PrologueColony colony, int tile)
        {
            if (tile < 0 || tile >= colony.Board.Tiles.Count)
                return "Off the map.";
            if (route.Contains(tile))
                return "Already painted";

            if (route.Count >= maxPaintTiles)
                return $"Paint budget full ({maxPaintTiles}/{maxPaintTiles}). Click the green ✓ to search.";

            if (route.Count == 0)
            {
                bool nearHome = droneTile >= 0 && (tile == droneTile || colony.Dist(droneTile, tile) == 1);
                if (!TouchesLit(colony, tile) && !nearHome)
                    return "Start the trail on or beside the lit ground (Command Post).";
                return string.Empty;
            }

            if (colony.Dist(route[route.Count - 1], tile) != 1)
                return "Paint the next tile beside the last one.";

            return string.Empty;
        }

        static bool TouchesLit(PrologueColony colony, int tile)
        {
            if (colony.Board.At(tile).Lit) return true;
            for (int i = 0; i < colony.Board.Tiles.Count; i++)
            {
                if (!colony.Board.At(i).Lit) continue;
                if (colony.Dist(tile, i) == 1) return true;
            }
            return false;
        }

        void RebuildMarks()
        {
            ClearMarks();
            var ctrl = PrologueController.Instance;
            if (ctrl == null) return;

            var mat = MarkMaterial(new Color(1f, 0.75f, 0.25f, 1f));
            for (int i = 0; i < route.Count; i++)
            {
                if (!ctrl.TryGetCell(route[i], out Vector2Int cell)) continue;
                Vector3 pos = ColonyTileGrid.CellToWorld(cell, 0f);
                pos.y = 0.35f;

                var mark = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                mark.name = $"SurveyPaint_{route[i]}";
                mark.transform.position = pos;
                mark.transform.localScale = new Vector3(3.2f, 0.08f, 3.2f);
                Object.Destroy(mark.GetComponent<Collider>());
                mark.GetComponent<MeshRenderer>().sharedMaterial = mat;
                marks.Add(mark);
            }

            if (route.Count >= MinConfirmTiles)
                SpawnCheckmarkOnLast(ctrl);
        }

        void SpawnCheckmarkOnLast(PrologueController ctrl)
        {
            int last = route[route.Count - 1];
            if (!ctrl.TryGetCell(last, out Vector2Int cell)) return;
            Vector3 pos = ColonyTileGrid.CellToWorld(cell, 0f);
            pos.y = 6.2f;

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
            tm.text = "✓";
            tm.fontSize = 48;
            tm.characterSize = 0.22f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = Color.white;
            tm.fontStyle = FontStyle.Bold;

            var hit = checkmarkGo.AddComponent<SphereCollider>();
            hit.radius = 1.5f;
            hit.center = new Vector3(0f, 0.2f, 0f);

            var lightGo = new GameObject("CheckLight");
            lightGo.transform.SetParent(checkmarkGo.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 0.3f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.35f, 1f, 0.45f);
            light.intensity = 4f;
            light.range = 12f;
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
            Camera cam = Camera.main;
            if (cam == null) return;
            var label = checkmarkGo.transform.Find("CheckLabel");
            if (label != null)
                label.rotation = Quaternion.LookRotation(label.position - cam.transform.position);
        }

        static Material MarkMaterial(Color c)
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
            GameDevTV.RTS.UI.PrologueHUD.Instance?.ShowWeekReport(new List<string> { msg });
        }

        static bool PointerBlocksWorldClick()
        {
            if (EventSystem.current == null) return false;
            if (!EventSystem.current.IsPointerOverGameObject()) return false;

            var results = new List<RaycastResult>(8);
            var ped = new PointerEventData(EventSystem.current)
            {
                position = Mouse.current.position.ReadValue()
            };
            EventSystem.current.RaycastAll(ped, results);
            foreach (var r in results)
            {
                if (r.gameObject == null) continue;
                if (r.gameObject.GetComponent<Button>() != null) return true;
                if (r.gameObject.GetComponentInParent<Button>() != null) return true;
                if (r.gameObject.name.IndexOf("Card", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }

        bool TryClickCheckmark()
        {
            if (checkmarkGo == null || route.Count < MinConfirmTiles) return false;
            Camera cam = Camera.main;
            if (cam == null) return false;
            Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (!Physics.Raycast(ray, out RaycastHit hit, 5000f)) return false;
            Transform t = hit.transform;
            while (t != null)
            {
                if (t.GetComponent<PrologueSurveyCheckClick>() != null) return true;
                if (t.gameObject == checkmarkGo) return true;
                t = t.parent;
            }
            return false;
        }

        bool TryClickIceMarker(out int tileIndex)
        {
            tileIndex = -1;
            Camera cam = Camera.main;
            if (cam == null) return false;
            Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (!Physics.Raycast(ray, out RaycastHit hit, 5000f)) return false;

            Transform t = hit.transform;
            while (t != null)
            {
                var click = t.GetComponent<PrologueIceMarkerClick>();
                if (click != null)
                {
                    tileIndex = click.TileIndex;
                    return tileIndex >= 0;
                }
                if (t.name.StartsWith("IceMarker_"))
                {
                    int us = t.name.LastIndexOf('_');
                    if (us >= 0 && int.TryParse(t.name.Substring(us + 1), out int idx))
                    {
                        tileIndex = idx;
                        return true;
                    }
                }
                t = t.parent;
            }
            return false;
        }

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

        static bool TryRaycastGround(out Vector3 world)
        {
            world = default;
            Camera cam = Camera.main;
            if (cam == null) return false;
            Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
            var hits = Physics.RaycastAll(ray, 5000f);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                if (IsCheckmarkHit(hit.transform)) continue;
                world = hit.point;
                return true;
            }
            return false;
        }

        static bool IsCheckmarkHit(Transform t)
        {
            while (t != null)
            {
                if (t.GetComponent<PrologueSurveyCheckClick>() != null) return true;
                if (t.name == "SurveyCheckmark") return true;
                t = t.parent;
            }
            return false;
        }
    }
}
