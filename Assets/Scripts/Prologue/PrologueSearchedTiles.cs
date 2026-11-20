using System.Collections.Generic;
using GameDevTV.RTS.Environment;
using GameDevTV.RTS.Player;
using UnityEngine;

namespace GameDevTV.RTS.Prologue
{
    /// <summary>
    /// Lighter ground tint on tiles the survey drone has already searched (Unreal lit path).
    /// </summary>
    public class PrologueSearchedTiles : MonoBehaviour
    {
        public static PrologueSearchedTiles Instance { get; private set; }

        readonly Dictionary<int, GameObject> overlays = new();
        static readonly Color SearchedTint = new(0.82f, 0.92f, 1f, 0.55f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Ensure()
        {
            if (Object.FindAnyObjectByType<PrologueSearchedTiles>() != null) return;
            var go = new GameObject("PrologueSearchedTiles");
            DontDestroyOnLoad(go);
            go.AddComponent<PrologueSearchedTiles>();
        }

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Clear();
        }

        public void RefreshAll()
        {
            var ctrl = PrologueController.Instance;
            if (ctrl?.Colony == null)
            {
                Clear();
                return;
            }

            var keep = new HashSet<int>();
            for (int i = 0; i < ctrl.Colony.Board.Tiles.Count; i++)
            {
                var t = ctrl.Colony.Board.At(i);
                if (!t.Revealed && !t.Lit) continue;
                keep.Add(i);
                EnsureOverlay(i, ctrl);
            }

            var stale = new List<int>();
            foreach (var kv in overlays)
            {
                if (!keep.Contains(kv.Key)) stale.Add(kv.Key);
            }
            foreach (int id in stale)
            {
                if (overlays.TryGetValue(id, out var go) && go != null) Destroy(go);
                overlays.Remove(id);
            }
        }

        public void MarkTile(int tileIndex)
        {
            var ctrl = PrologueController.Instance;
            if (ctrl == null) return;
            EnsureOverlay(tileIndex, ctrl);

            if (ctrl.TryGetCell(tileIndex, out Vector2Int cell))
            {
                Vector3 world = ColonyTileGrid.CellToWorld(cell, 0f);
                HexGridManager.Instance?.RevealHex(world);
            }
        }

        void EnsureOverlay(int tileIndex, PrologueController ctrl)
        {
            if (overlays.TryGetValue(tileIndex, out var existing) && existing != null) return;
            if (!ctrl.TryGetCell(tileIndex, out Vector2Int cell)) return;

            Vector3 pos = ColonyTileGrid.CellToWorld(cell, 0f);
            pos.y = 0.12f;

            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = $"SearchedTile_{tileIndex}";
            disc.transform.SetParent(transform, false);
            disc.transform.position = pos;
            // Flat-to-flat ≈ TileSize * √3; cover the hex face lightly.
            float w = ColonyTileGrid.TileSize * 1.65f;
            disc.transform.localScale = new Vector3(w, 0.04f, w);
            Object.Destroy(disc.GetComponent<Collider>());

            var mr = disc.GetComponent<MeshRenderer>();
            mr.sharedMaterial = MakeMat(SearchedTint);
            overlays[tileIndex] = disc;
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
            // Transparent-ish feel via alpha on Unlit when supported.
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
            return m;
        }

        void Clear()
        {
            foreach (var kv in overlays)
            {
                if (kv.Value != null) Destroy(kv.Value);
            }
            overlays.Clear();
        }
    }
}
