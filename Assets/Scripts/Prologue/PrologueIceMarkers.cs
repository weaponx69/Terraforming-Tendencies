using System.Collections.Generic;
using GameDevTV.RTS.Environment;
using GameDevTV.RTS.Player;
using TMPro;
using UnityEngine;

namespace GameDevTV.RTS.Prologue
{
    /// <summary>
    /// Floating "?" markers over unrevealed ice so the player can see where to send the survey drone.
    /// </summary>
    public class PrologueIceMarkers : MonoBehaviour
    {
        public static PrologueIceMarkers Instance { get; private set; }

        readonly Dictionary<int, GameObject> markers = new();
        readonly Dictionary<int, float> markerGroundY = new();
        const float MarkerHeight = 8.5f;
        const float BobAmp = 0.7f;
        const float BobSpeed = 2.2f;
        /// <summary>Readable from camp without dominating the view.</summary>
        public const float MarkerScale = 3.25f;
        /// <summary>Deposit "?" height — tall enough to read over dunes (ice markers bob higher).</summary>
        public const float DepositFloatHeight = 5.5f;
        public const float MarkerFontSize = 18f;
        const float PingSeconds = 20f;
        float pingUntil;
        int pingTile = -1;
        Transform pingDeposit;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Ensure()
        {
            if (Object.FindAnyObjectByType<PrologueIceMarkers>() != null) return;
            var go = new GameObject("PrologueIceMarkers");
            DontDestroyOnLoad(go);
            go.AddComponent<PrologueIceMarkers>();
        }

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            ClearAll();
        }

        float nextBillboard;

        void LateUpdate()
        {
            if (!PrologueController.IsActive)
            {
                if (markers.Count > 0) ClearAll();
                return;
            }

            if (Time.unscaledTime < nextBillboard) return;
            nextBillboard = Time.unscaledTime + 0.05f;

            // Keep markers facing the camera and gently bobbing (20 Hz — not every frame).
            Camera cam = Camera.main;
            float t = Time.unscaledTime;
            bool pinging = Time.unscaledTime < pingUntil;
            if (pinging && pingDeposit != null)
            {
                float pulse = MarkerScale * (1.45f + Mathf.Sin(t * 8f) * 0.4f);
                pingDeposit.localScale = Vector3.one * pulse;
            }

            foreach (var kv in markers)
            {
                if (kv.Value == null) continue;
                Transform tr = kv.Value.transform;
                Vector3 p = tr.position;
                if (!markerGroundY.TryGetValue(kv.Key, out float groundY))
                {
                    groundY = ColonyTileGrid.SampleGroundY(p);
                    markerGroundY[kv.Key] = groundY;
                }
                float height = MarkerHeight + Mathf.Sin(t * BobSpeed + kv.Key) * BobAmp;
                float scale = MarkerScale;
                if (pinging && kv.Key == pingTile)
                {
                    height += 4f + Mathf.Sin(t * 8f) * 1.2f;
                    scale = MarkerScale * 1.55f;
                }
                p.y = groundY + height;
                tr.position = p;
                tr.localScale = Vector3.one * scale;
                if (cam != null)
                {
                    Vector3 toCam = cam.transform.position - tr.position;
                    toCam.y = 0f;
                    if (toCam.sqrMagnitude > 0.01f)
                        tr.rotation = Quaternion.LookRotation(-toCam.normalized, Vector3.up);
                }
            }
        }

        /// <summary>
        /// Orbital Scan: pulse the next objective — ice, minerals/gas ?, or geothermal feature.
        /// </summary>
        public string PingNearestTarget()
        {
            var colony = PrologueController.Instance?.Colony;
            if (colony == null) return "No signal.";

            if (colony.PrologueStep <= 4 && colony.HasUnrevealedIce())
                return PingNearestIce();

            if (colony.PrologueStep >= 5 && !colony.HasBuilding(PrologueBuildingId.WindTurbine))
            {
                if (TryFindNearestDeposit(out SectorNode geo, out int gDist, out string gDir, wantGeothermal: true))
                    return PingDepositNode(geo, "Geothermal vent", gDist, gDir);
            }

            if (TryFindNearestDeposit(out SectorNode node, out int dist, out string dir, wantGeothermal: false))
            {
                string label = DepositLabel(node);
                bool typed = colony.Research != null
                               && colony.Research.IsUnlocked(PrologueResearchId.ExtraScanType);
                if (typed)
                    return PingDepositNode(node, label, dist, dir);
                return PingDepositNode(node, "Resource", dist, dir);
            }

            if (colony.HasUnrevealedIce())
                return PingNearestIce();

            return "Satellites found no unsurveyed targets near camp.";
        }

        /// <summary>
        /// Company Orbital Scan: highlight the nearest unrevealed ice and say how far it is.
        /// </summary>
        public string PingNearestIce()
        {
            var ctrl = PrologueController.Instance;
            if (ctrl?.Colony == null) return "No ice signal.";

            int home = -1;
            for (int i = 0; i < ctrl.Colony.Board.Tiles.Count; i++)
            {
                if (ctrl.Colony.Board.At(i).Building == PrologueBuildingId.CommandPost)
                {
                    home = i;
                    break;
                }
            }
            if (home < 0) home = 0;

            int best = -1;
            int bestDist = int.MaxValue;
            for (int i = 0; i < ctrl.Colony.Board.Tiles.Count; i++)
            {
                var tile = ctrl.Colony.Board.At(i);
                if (tile.DepositLeft <= 0) continue;
                if (tile.Feature != PrologueFeature.Ice && tile.Feature != PrologueFeature.Glacier) continue;
                if (tile.Revealed) continue;
                int d = ctrl.Colony.Dist(home, i);
                if (d >= bestDist) continue;
                bestDist = d;
                best = i;
            }

            if (best < 0) return "Satellites found no unsurveyed ice.";

            Refresh();
            pingDeposit = null;
            pingTile = best;
            pingUntil = Time.unscaledTime + PingSeconds;
            if (!ctrl.TryGetCell(best, out Vector2Int cell) || !ctrl.TryGetCell(home, out Vector2Int homeCell))
                return $"Ice signal about {bestDist} tiles from camp. Look for the pulsing yellow ?.";

            Vector3 ice = ColonyTileGrid.CellToWorldOnGround(cell);
            Vector3 camp = ColonyTileGrid.CellToWorldOnGround(homeCell);
            Vector3 delta = ice - camp;
            delta.y = 0f;
            string dir = Compass(delta);
            return $"Ice signal {bestDist} tiles {dir} of camp. The yellow ? is pulsing — paint Survey toward it.";
        }

        string PingDepositNode(SectorNode node, string label, int dist, string dir)
        {
            if (node?.questionMarkGO == null)
                return $"{label} about {dist} tiles {dir} — look for a yellow ?.";
            pingTile = -1;
            pingDeposit = node.questionMarkGO.transform;
            pingUntil = Time.unscaledTime + PingSeconds;
            return $"{label} signal {dist} tiles {dir} of camp. The yellow ? is pulsing — paint Survey toward it.";
        }

        bool TryFindNearestDeposit(out SectorNode best, out int bestDist, out string dir, bool wantGeothermal)
        {
            best = null;
            bestDist = int.MaxValue;
            dir = "nearby";
            var ctrl = PrologueController.Instance;
            var colony = ctrl?.Colony;
            if (ctrl == null || colony == null || SectorManager.Instance?.Sectors == null
                || SectorManager.Instance.Sectors.Count == 0)
                return false;

            int home = 0;
            for (int i = 0; i < colony.Board.Tiles.Count; i++)
            {
                if (colony.Board.At(i).Building == PrologueBuildingId.CommandPost)
                {
                    home = i;
                    break;
                }
            }

            foreach (var node in SectorManager.Instance.Sectors[0].Nodes)
            {
                if (node == null || node.isExplored) continue;
                if (node.type == SectorNode.NodeType.Nexus) continue;
                if (wantGeothermal)
                {
                    string lbl = node.labelOverride ?? string.Empty;
                    if (node.type != SectorNode.NodeType.Feature
                        || lbl.IndexOf("Volcan", System.StringComparison.OrdinalIgnoreCase) < 0)
                        continue;
                }

                if (!ctrl.TryGetTileIndex(node.position, out int tile)) continue;
                int d = colony.Dist(home, tile);
                if (d > 8 || d >= bestDist) continue;
                bestDist = d;
                best = node;
            }

            if (best == null || !ctrl.TryGetCell(home, out Vector2Int homeCell)
                || !ctrl.TryGetTileIndex(best.position, out int bestTile)
                || !ctrl.TryGetCell(bestTile, out Vector2Int destCell))
                return best != null;

            Vector3 camp = ColonyTileGrid.CellToWorldOnGround(homeCell);
            Vector3 dest = ColonyTileGrid.CellToWorldOnGround(destCell);
            Vector3 delta = dest - camp;
            delta.y = 0f;
            dir = Compass(delta);
            return true;
        }

        static string DepositLabel(SectorNode node)
        {
            if (node == null) return "Resource";
            if (!string.IsNullOrEmpty(node.labelOverride))
                return node.labelOverride;
            return node.type switch
            {
                SectorNode.NodeType.Minerals => "Minerals",
                SectorNode.NodeType.Gas => "Gas",
                SectorNode.NodeType.Iron => "Iron",
                SectorNode.NodeType.Regolith => "Regolith",
                SectorNode.NodeType.Feature => "Geology",
                _ => "Resource"
            };
        }

        static string Compass(Vector3 flat)
        {
            if (flat.sqrMagnitude < 0.01f) return "beside";
            float ang = Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg;
            if (ang < 0f) ang += 360f;
            if (ang < 22.5f || ang >= 337.5f) return "north";
            if (ang < 67.5f) return "northeast";
            if (ang < 112.5f) return "east";
            if (ang < 157.5f) return "southeast";
            if (ang < 202.5f) return "south";
            if (ang < 247.5f) return "southwest";
            if (ang < 292.5f) return "west";
            return "northwest";
        }

        /// <summary>Boot / planet sync only — spawn ? for unrevealed ice once.</summary>
        public void RebuildAll()
        {
            var ctrl = PrologueController.Instance;
            if (ctrl?.Colony == null)
            {
                ClearAll();
                return;
            }

            var keep = new HashSet<int>();
            var colony = ctrl.Colony;
            for (int i = 0; i < colony.Board.Tiles.Count; i++)
            {
                var tile = colony.Board.At(i);
                if (tile.Feature != PrologueFeature.Ice && tile.Feature != PrologueFeature.Glacier)
                    continue;
                if (tile.DepositLeft <= 0) continue;
                if (tile.Revealed) continue;

                keep.Add(i);
                EnsureMarker(i);
            }

            var stale = new List<int>();
            foreach (var kv in markers)
            {
                if (!keep.Contains(kv.Key))
                    stale.Add(kv.Key);
            }
            foreach (int id in stale)
                RemoveMarker(id);

            PrologueRevealGhosts.BootstrapFromColonyIfNeeded();
        }

        /// <summary>Full pass — prefer <see cref="OnTileRevealed"/> after survey.</summary>
        public void Refresh() => RebuildAll();

        public void OnTileRevealed(int tileIndex)
        {
            var colony = PrologueController.Instance?.Colony;
            if (colony == null) return;
            if (tileIndex < 0 || tileIndex >= colony.Board.Tiles.Count) return;

            var tile = colony.Board.At(tileIndex);
            if (tile.Feature == PrologueFeature.Ice || tile.Feature == PrologueFeature.Glacier)
            {
                if (tile.Revealed || tile.DepositLeft <= 0)
                    RemoveMarker(tileIndex);
            }

            PrologueRevealGhosts.OnTileSurveyRevealed(tileIndex);
        }

        public void EnsureMarker(int tileIndex)
        {
            var ctrl = PrologueController.Instance;
            if (ctrl == null) return;
            if (markers.TryGetValue(tileIndex, out var existing) && existing != null) return;
            markers[tileIndex] = CreateMarker(tileIndex, ctrl);
        }

        public void RemoveMarker(int tileIndex)
        {
            if (markers.TryGetValue(tileIndex, out var go) && go != null)
                Destroy(go);
            markers.Remove(tileIndex);
            markerGroundY.Remove(tileIndex);
        }

        GameObject CreateMarker(int tileIndex, PrologueController ctrl)
        {
            if (!ctrl.TryGetCell(tileIndex, out Vector2Int cell))
                return null;

            Vector3 world = ColonyTileGrid.CellToWorldOnGround(cell);
            markerGroundY[tileIndex] = world.y;
            world.y += MarkerHeight;

            var root = new GameObject($"IceMarker_?_{tileIndex}");
            root.transform.SetParent(transform, false);
            root.transform.position = world;
            root.transform.localScale = Vector3.one * MarkerScale;

            // Bright pedestal so it reads against Mars red even from far away.
            var pedestal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pedestal.name = "Pedestal";
            pedestal.transform.SetParent(root.transform, false);
            pedestal.transform.localPosition = new Vector3(0f, -0.35f, 0f);
            pedestal.transform.localScale = new Vector3(0.55f, 0.08f, 0.55f);
            Object.Destroy(pedestal.GetComponent<Collider>());
            pedestal.GetComponent<MeshRenderer>().sharedMaterial = MakeMat(new Color(1f, 0.35f, 0.85f, 1f));

            var textGo = new GameObject("Question");
            textGo.transform.SetParent(root.transform, false);
            textGo.transform.localPosition = Vector3.zero;
            var tmp = textGo.AddComponent<TextMeshPro>();
            tmp.text = "?";
            tmp.fontSize = MarkerFontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(1f, 0.92f, 0.2f, 1f);
            tmp.fontStyle = FontStyles.Bold;
            tmp.enableAutoSizing = false;
            tmp.raycastTarget = false;
            tmp.outlineWidth = 0.25f;
            tmp.outlineColor = new Color(0.05f, 0.02f, 0.08f, 1f);

            // Big click sphere so the player can hit the ? easily.
            var clickGo = new GameObject("ClickTarget");
            clickGo.transform.SetParent(root.transform, false);
            clickGo.transform.localPosition = Vector3.zero;
            clickGo.transform.localScale = Vector3.one; // world hit size via radius below
            var sphere = clickGo.AddComponent<SphereCollider>();
            sphere.radius = 0.7f; // local; root scale makes a large world click target
            sphere.isTrigger = false;
            var click = clickGo.AddComponent<PrologueIceMarkerClick>();
            click.Setup(tileIndex);
            // Also on root for parent walk.
            var rootClick = root.AddComponent<PrologueIceMarkerClick>();
            rootClick.Setup(tileIndex);

            return root;
        }

        static Material MakeMat(Color c)
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

        void ClearAll()
        {
            foreach (var kv in markers)
            {
                if (kv.Value != null) Destroy(kv.Value);
            }
            markers.Clear();
            markerGroundY.Clear();
        }
    }
}
