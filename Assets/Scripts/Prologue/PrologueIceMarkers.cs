using System.Collections.Generic;
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
        const float MarkerHeight = 6.5f;
        const float BobAmp = 0.55f;
        const float BobSpeed = 2.2f;
        const float MarkerScale = 8f;

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

        void LateUpdate()
        {
            if (!PrologueController.IsActive)
            {
                if (markers.Count > 0) ClearAll();
                return;
            }

            // Keep markers facing the camera and gently bobbing.
            Camera cam = Camera.main;
            float t = Time.unscaledTime;
            foreach (var kv in markers)
            {
                if (kv.Value == null) continue;
                Transform tr = kv.Value.transform;
                Vector3 p = tr.position;
                p.y = MarkerHeight + Mathf.Sin(t * BobSpeed + kv.Key) * BobAmp;
                tr.position = p;
                if (cam != null)
                {
                    Vector3 toCam = cam.transform.position - tr.position;
                    toCam.y = 0f;
                    if (toCam.sqrMagnitude > 0.01f)
                        tr.rotation = Quaternion.LookRotation(-toCam.normalized, Vector3.up);
                }
            }
        }

        /// <summary>Rebuild "?" markers for every unrevealed ice deposit.</summary>
        public void Refresh()
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
                if (tile.Revealed) continue; // found ice no longer needs a ?

                keep.Add(i);
                if (!markers.ContainsKey(i) || markers[i] == null)
                    markers[i] = CreateMarker(i, ctrl);
            }

            // Drop markers for ice that was revealed or removed.
            var stale = new List<int>();
            foreach (var kv in markers)
            {
                if (!keep.Contains(kv.Key))
                    stale.Add(kv.Key);
            }
            foreach (int id in stale)
            {
                if (markers.TryGetValue(id, out var go) && go != null)
                    Destroy(go);
                markers.Remove(id);
            }
        }

        GameObject CreateMarker(int tileIndex, PrologueController ctrl)
        {
            if (!ctrl.TryGetCell(tileIndex, out Vector2Int cell))
                return null;

            Vector3 world = ColonyTileGrid.CellToWorld(cell, 0f);
            world.y = MarkerHeight;

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
            tmp.fontSize = 12f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(1f, 0.92f, 0.2f, 1f);
            tmp.fontStyle = FontStyles.Bold;
            tmp.enableAutoSizing = false;
            tmp.raycastTarget = false;
            tmp.outlineWidth = 0.25f;
            tmp.outlineColor = new Color(0.05f, 0.02f, 0.08f, 1f);

            var lightGo = new GameObject("Glow");
            lightGo.transform.SetParent(root.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 0.2f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.55f, 0.95f);
            light.intensity = 6f;
            light.range = 18f;

            // Big click sphere so the player can hit the ? easily.
            var clickGo = new GameObject("ClickTarget");
            clickGo.transform.SetParent(root.transform, false);
            clickGo.transform.localPosition = Vector3.zero;
            clickGo.transform.localScale = Vector3.one; // world hit size via radius below
            var sphere = clickGo.AddComponent<SphereCollider>();
            sphere.radius = 0.55f; // local; root is scaled ~8 → large world click target
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
        }
    }
}
