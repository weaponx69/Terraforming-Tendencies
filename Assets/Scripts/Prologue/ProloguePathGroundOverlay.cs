using GameDevTV.RTS.Player;
using UnityEngine;

namespace GameDevTV.RTS.Prologue
{
    /// <summary>
    /// Survey trails as a single ground-aligned texture instead of many cube road meshes.
    /// </summary>
    public sealed class ProloguePathGroundOverlay : MonoBehaviour
    {
        public static ProloguePathGroundOverlay Instance { get; private set; }
        public static bool Enabled;

        static readonly Color32 Dirt = new(102, 61, 31, 235);

        Texture2D tex;
        Color32[] pixels;
        int tw, th;
        float minX, minZ, worldW, worldH;
        MeshRenderer quadRenderer;
        float nextApplyUnscaled;
        bool applyDirty;

        static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        static readonly int MainTexId = Shader.PropertyToID("_MainTex");

        public const float MinApplyIntervalSec = 0.2f;

        public bool NeedsGpuUpload => applyDirty;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void EnsureHost()
        {
            if (Enabled && Instance == null)
                new GameObject("ProloguePathGroundOverlay").AddComponent<ProloguePathGroundOverlay>();
        }

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (tex != null) Destroy(tex);
        }

        public static void SetEnabled(bool on)
        {
            Enabled = on;
            if (!on)
            {
                if (Instance != null) Destroy(Instance.gameObject);
                return;
            }
            if (Instance == null)
                new GameObject("ProloguePathGroundOverlay").AddComponent<ProloguePathGroundOverlay>();
        }

        public bool IsReady => tex != null && pixels != null;

        public void FitBoardIfNeeded(PrologueController ctrl)
        {
            if (IsReady) return;
            FitBoard(ctrl);
        }

        public void FitBoard(PrologueController ctrl)
        {
            if (ctrl?.Colony == null) return;
            minX = float.PositiveInfinity;
            minZ = float.PositiveInfinity;
            float maxX = float.NegativeInfinity;
            float maxZ = float.NegativeInfinity;
            int n = ctrl.Colony.Board.Tiles.Count;
            for (int i = 0; i < n; i++)
            {
                if (!ctrl.TryGetCell(i, out Vector2Int cell)) continue;
                Vector3 w = ColonyTileGrid.CellToWorldOnGround(cell);
                minX = Mathf.Min(minX, w.x);
                minZ = Mathf.Min(minZ, w.z);
                maxX = Mathf.Max(maxX, w.x);
                maxZ = Mathf.Max(maxZ, w.z);
            }
            float pad = ColonyTileGrid.TileSize * 1.25f;
            minX -= pad;
            minZ -= pad;
            maxX += pad;
            maxZ += pad;
            worldW = Mathf.Max(1f, maxX - minX);
            worldH = Mathf.Max(1f, maxZ - minZ);
            int cells = Mathf.Max(8, Mathf.CeilToInt(Mathf.Sqrt(n)));
            tw = Mathf.Clamp(cells * 8, 128, 512);
            th = Mathf.Clamp(cells * 8, 128, 512);
            tex = new Texture2D(tw, th, TextureFormat.RGBA32, false, true)
            {
                name = "ProloguePathOverlay",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            pixels = new Color32[tw * th];
            ClearPixels();
            applyDirty = true;
            EnsureQuad();
            BindTextureToMaterial();
        }

        public void ClearPixels()
        {
            if (pixels == null) return;
            var clear = new Color32(0, 0, 0, 0);
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = clear;
            applyDirty = true;
        }

        public void PaintSegment(Vector3 from, Vector3 to)
        {
            if (pixels == null || tex == null) return;
            int x0 = WorldToTexX(from.x);
            int y0 = WorldToTexY(from.z);
            int x1 = WorldToTexX(to.x);
            int y1 = WorldToTexY(to.z);
            int radius = Mathf.Max(2, Mathf.RoundToInt(ColonyTileGrid.TileSize / worldW * tw * 0.35f));
            DrawLine(x0, y0, x1, y1, radius);
            applyDirty = true;
        }

        public void PaintPad(Vector3 world)
        {
            if (pixels == null) return;
            int cx = WorldToTexX(world.x);
            int cy = WorldToTexY(world.z);
            int r = Mathf.Max(3, Mathf.RoundToInt(ColonyTileGrid.TileSize / worldW * tw * 0.42f));
            FillDisc(cx, cy, r);
            applyDirty = true;
        }

        public void Apply()
        {
            if (tex == null || pixels == null) return;
            applyDirty = true;
            TryApplyNow(force: false);
        }

        /// <summary>Paint confirm, order complete, or drag stroke — upload now (bypass throttle).</summary>
        public void ApplyImmediate()
        {
            if (tex == null || pixels == null) return;
            applyDirty = true;
            nextApplyUnscaled = 0f;
            TryApplyNow(force: true);
        }

        /// <summary>Deferred vision slice — may coalesce to <see cref="MinApplyIntervalSec"/>.</summary>
        public void TryApplyNow(bool force = false)
        {
            if (tex == null || pixels == null) return;
            if (!applyDirty && !force) return;
            if (!force && Time.unscaledTime < nextApplyUnscaled) return;

            tex.SetPixels32(pixels);
            tex.Apply(updateMipmaps: false, makeNoLongerReadable: false);
            BindTextureToMaterial();
            applyDirty = false;
            nextApplyUnscaled = Time.unscaledTime + MinApplyIntervalSec;
        }

        void BindTextureToMaterial()
        {
            if (tex == null || quadRenderer == null || quadRenderer.sharedMaterial == null) return;
            quadRenderer.sharedMaterial.SetTexture(BaseMapId, tex);
            if (quadRenderer.sharedMaterial.HasProperty(MainTexId))
                quadRenderer.sharedMaterial.SetTexture(MainTexId, tex);
        }

        int WorldToTexX(float x) => Mathf.Clamp(Mathf.RoundToInt((x - minX) / worldW * (tw - 1)), 0, tw - 1);
        int WorldToTexY(float z) => Mathf.Clamp(Mathf.RoundToInt((z - minZ) / worldH * (th - 1)), 0, th - 1);

        void DrawLine(int x0, int y0, int x1, int y1, int radius)
        {
            int dx = Mathf.Abs(x1 - x0);
            int dy = Mathf.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1;
            int sy = y0 < y1 ? 1 : -1;
            int err = dx - dy;
            while (true)
            {
                FillDisc(x0, y0, radius);
                if (x0 == x1 && y0 == y1) break;
                int e2 = 2 * err;
                if (e2 > -dy) { err -= dy; x0 += sx; }
                if (e2 < dx) { err += dx; y0 += sy; }
            }
        }

        void FillDisc(int cx, int cy, int radius)
        {
            int r2 = radius * radius;
            for (int y = -radius; y <= radius; y++)
            {
                int ty = cy + y;
                if (ty < 0 || ty >= th) continue;
                for (int x = -radius; x <= radius; x++)
                {
                    if (x * x + y * y > r2) continue;
                    int tx = cx + x;
                    if (tx < 0 || tx >= tw) continue;
                    int i = ty * tw + tx;
                    pixels[i] = Dirt;
                }
            }
        }

        void EnsureQuad()
        {
            float y = ColonyTileGrid.SampleGroundY(new Vector3(minX + worldW * 0.5f, 0f, minZ + worldH * 0.5f)) + 0.04f;
            if (quadRenderer == null)
            {
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.name = "PathOverlayQuad";
                quad.transform.SetParent(transform, false);
                Object.Destroy(quad.GetComponent<Collider>());
                quadRenderer = quad.GetComponent<MeshRenderer>();
                var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Transparent");
                var mat = new Material(shader);
                if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.renderQueue = 1999;
                quadRenderer.sharedMaterial = mat;
                quadRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                quadRenderer.receiveShadows = false;
            }

            var t = quadRenderer.transform;
            t.position = new Vector3(minX + worldW * 0.5f, y, minZ + worldH * 0.5f);
            t.rotation = Quaternion.Euler(90f, 0f, 0f);
            t.localScale = new Vector3(worldW, worldH, 1f);
        }
    }
}