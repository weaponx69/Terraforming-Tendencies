using System.Collections.Generic;
using GameDevTV.RTS.Environment;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.Units;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GameDevTV.RTS.UI.Containers
{
    /// <summary>
    /// Schematic planet minimap (Colony Acts). Mounts into the scene Minimap Container
    /// (user-placed). Sectors + geology tint, buildings, camera crosshair; click to jump.
    /// </summary>
    public class MinimapUI : MonoBehaviour
    {
        public static MinimapUI Instance { get; private set; }

        private const int TexSize = 256;

        private RectTransform panel;
        private RawImage mapImage;
        private Texture2D mapTex;
        private Color32[] pixels;
        private float mapW;
        private float mapH;
        private bool dirty = true;

        private static readonly Color32 Ground = new(28, 32, 38, 255);
        private static readonly Color32 GridLine = new(48, 54, 64, 255);
        private static readonly Color32 Occupied = new(70, 110, 90, 255);
        private static readonly Color32 Water = new(55, 140, 180, 255);
        private static readonly Color32 Glacier = new(200, 230, 245, 255);
        private static readonly Color32 Volcano = new(160, 70, 40, 255);
        private static readonly Color32 Fault = new(150, 120, 50, 255);
        private static readonly Color32 Lava = new(180, 80, 30, 255);
        private static readonly Color32 BuildingDot = new(220, 220, 230, 255);
        private static readonly Color32 CommandDot = new(90, 220, 255, 255);
        private static readonly Color32 CameraDot = new(255, 230, 80, 255);
        private static readonly Color32 CameraRing = new(255, 200, 60, 255);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoSpawn()
        {
            if (FindAnyObjectByType<MinimapUI>() != null) return;
            var go = new GameObject("MinimapUI");
            go.AddComponent<MinimapUI>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            EnsureUi();
        }

        private void Start()
        {
            // Scene UI may spawn after RuntimeInitialize — remount once if needed.
            if (panel == null || !panel)
                EnsureUi();
            else if (mapImage != null && mapImage.transform.parent == null)
                EnsureUi();
            else
                TryMountIntoSceneContainer();
        }

        private void OnEnable()
        {
            PlanetGenerator.OnPlanetGenerated += HandlePlanetGenerated;
            ColonyActManager.OnActStateChanged += MarkDirty;
        }

        private void OnDisable()
        {
            PlanetGenerator.OnPlanetGenerated -= HandlePlanetGenerated;
            ColonyActManager.OnActStateChanged -= MarkDirty;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (mapTex != null)
            {
                Destroy(mapTex);
                mapTex = null;
            }
        }

        private void HandlePlanetGenerated()
        {
            RefreshMapBounds();
            MarkDirty();
        }

        private void MarkDirty() => dirty = true;

        private void EnsureUi()
        {
            if (mapTex == null)
            {
                mapTex = new Texture2D(TexSize, TexSize, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    name = "MinimapTex"
                };
                pixels = new Color32[TexSize * TexSize];
            }

            if (!TryMountIntoSceneContainer())
            {
                // Fallback until Bottom Bar exists: left-bottom overlay (not right).
                BuildFallbackPanel();
            }

            RefreshMapBounds();
        }

        /// <summary>
        /// Prefer the scene's Minimap Container (user-placed). Fills Minimap / Mask area.
        /// </summary>
        private bool TryMountIntoSceneContainer()
        {
            Transform container = FindMinimapContainer();
            if (container == null) return false;

            container.gameObject.SetActive(true);

            // Prefer deepest Minimap leaf under Mask; else container itself.
            Transform mount = container.Find("Background/Minimap Mask/Minimap")
                ?? container.Find("Minimap Mask/Minimap")
                ?? FindChildNamed(container, "Minimap")
                ?? container;

            if (mount is not RectTransform mountRt) return false;

            // Clear placeholder Image on Background so our map reads cleanly.
            Transform bg = container.Find("Background");
            if (bg != null)
            {
                var bgImage = bg.GetComponent<Image>();
                if (bgImage != null)
                {
                    bgImage.enabled = true;
                    bgImage.color = new Color(0.06f, 0.08f, 0.1f, 0.85f);
                    bgImage.raycastTarget = false;
                }
            }

            if (mapImage == null)
            {
                // Reuse mount GO if empty; otherwise add child Map.
                var existing = mount.Find("Map");
                GameObject rawGo;
                if (existing != null)
                {
                    rawGo = existing.gameObject;
                    mapImage = rawGo.GetComponent<RawImage>() ?? rawGo.AddComponent<RawImage>();
                }
                else
                {
                    // If mount is the leaf "Minimap" with only CanvasRenderer, put RawImage on it.
                    mapImage = mount.GetComponent<RawImage>();
                    if (mapImage == null && mount.childCount == 0)
                    {
                        mapImage = mount.gameObject.AddComponent<RawImage>();
                        rawGo = mount.gameObject;
                    }
                    else
                    {
                        rawGo = new GameObject("Map", typeof(RectTransform));
                        rawGo.transform.SetParent(mount, false);
                        var rawRt = rawGo.GetComponent<RectTransform>();
                        rawRt.anchorMin = Vector2.zero;
                        rawRt.anchorMax = Vector2.one;
                        rawRt.offsetMin = Vector2.zero;
                        rawRt.offsetMax = Vector2.zero;
                        mapImage = rawGo.AddComponent<RawImage>();
                    }
                }

                mapImage.raycastTarget = true;
                mapImage.color = Color.white;
                mapImage.texture = mapTex;

                var catcher = mapImage.GetComponent<MinimapClickCatcher>()
                    ?? mapImage.gameObject.AddComponent<MinimapClickCatcher>();
                catcher.Owner = this;
            }
            else
            {
                // Remount existing map into the scene container.
                Transform mapT = mapImage.transform;
                if (mapT.parent != mount && mapT != mount)
                {
                    mapT.SetParent(mount, false);
                    if (mapT is RectTransform rt)
                    {
                        rt.anchorMin = Vector2.zero;
                        rt.anchorMax = Vector2.one;
                        rt.offsetMin = Vector2.zero;
                        rt.offsetMax = Vector2.zero;
                    }
                }
                mapImage.texture = mapTex;
            }

            panel = mountRt;

            LiftMinimapAboveCardHand(container as RectTransform);

            // Drop fallback overlay canvas if we successfully mounted into the scene.
            Transform fallback = transform.Find("MinimapCanvas");
            if (fallback != null && mapImage != null && !mapImage.transform.IsChildOf(fallback))
                Destroy(fallback.gameObject);

            // Host component under the container so it lives with the scene UI.
            if (transform.parent != container)
                transform.SetParent(container, false);

            return true;
        }

        /// <summary>
        /// Card hand docks bottom-left (~220px tall). Pin the Minimap Container
        /// above that strip on the left and give it a higher sorting canvas so
        /// cards never cover it.
        /// </summary>
        private static void LiftMinimapAboveCardHand(RectTransform container)
        {
            if (container == null) return;

            // Detach from Bottom Bar layout siblings if needed — keep under same canvas root.
            // Position: bottom-left, raised above card dock.
            const float handClearance = 350f; // card height (~220) + ~1" gap above hand
            const float mapSize = 200f;
            const float leftPad = 16f;

            container.anchorMin = new Vector2(0f, 0f);
            container.anchorMax = new Vector2(0f, 0f);
            container.pivot = new Vector2(0f, 0f);
            container.sizeDelta = new Vector2(mapSize, mapSize);
            container.anchoredPosition = new Vector2(leftPad, handClearance);
            container.localScale = Vector3.one;
            container.SetAsLastSibling();

            // Ensure Background / Mask fill the container.
            foreach (var rt in container.GetComponentsInChildren<RectTransform>(true))
            {
                if (rt == container) continue;
                if (rt.name is "Background" or "Minimap Mask" or "Minimap")
                {
                    rt.anchorMin = Vector2.zero;
                    rt.anchorMax = Vector2.one;
                    rt.offsetMin = Vector2.zero;
                    rt.offsetMax = Vector2.zero;
                    rt.localScale = Vector3.one;
                }
            }

            var canvas = container.GetComponent<Canvas>();
            if (canvas == null) canvas = container.gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 120; // above bottom-bar card hand
            if (container.GetComponent<GraphicRaycaster>() == null)
                container.gameObject.AddComponent<GraphicRaycaster>();
        }

        private void BuildFallbackPanel()
        {
            if (panel != null && transform.Find("MinimapCanvas") != null) return;

            var canvasGo = new GameObject("MinimapCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 45;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasGo.AddComponent<GraphicRaycaster>();

            var panelGo = new GameObject("MinimapPanel", typeof(RectTransform));
            panelGo.transform.SetParent(canvasGo.transform, false);
            panel = panelGo.GetComponent<RectTransform>();
            // Left — matches intended Minimap Container side until scene mount succeeds.
            panel.anchorMin = new Vector2(0f, 0f);
            panel.anchorMax = new Vector2(0f, 0f);
            panel.pivot = new Vector2(0f, 0f);
            panel.anchoredPosition = new Vector2(16f, 350f);
            panel.sizeDelta = new Vector2(200f, 200f);

            var frame = panelGo.AddComponent<Image>();
            frame.color = new Color(0.08f, 0.1f, 0.12f, 0.92f);
            frame.raycastTarget = true;

            var rawGo = new GameObject("Map", typeof(RectTransform));
            rawGo.transform.SetParent(panel, false);
            var rawRt = rawGo.GetComponent<RectTransform>();
            rawRt.anchorMin = Vector2.zero;
            rawRt.anchorMax = Vector2.one;
            rawRt.offsetMin = new Vector2(4f, 4f);
            rawRt.offsetMax = new Vector2(-4f, -4f);
            mapImage = rawGo.AddComponent<RawImage>();
            mapImage.raycastTarget = true;
            mapImage.color = Color.white;
            mapImage.texture = mapTex;
            var catcher = rawGo.AddComponent<MinimapClickCatcher>();
            catcher.Owner = this;
        }

        private static Transform FindMinimapContainer()
        {
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
            {
                if (t != null && t.name == "Minimap Container")
                    return t;
            }
            return null;
        }

        private static Transform FindChildNamed(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var found = FindChildNamed(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }

        private void RefreshMapBounds()
        {
            mapW = 0f;
            mapH = 0f;
            var wrapper = Object.FindAnyObjectByType<MapWrapper>();
            if (wrapper != null && wrapper.MapWidth > 1f && wrapper.MapHeight > 1f)
            {
                mapW = wrapper.MapWidth;
                mapH = wrapper.MapHeight;
                return;
            }

            if (PlanetGenerator.Instance?.Config != null)
            {
                mapW = PlanetGenerator.Instance.Config.MapWidth * PlanetGenerator.Instance.CellSize;
                mapH = PlanetGenerator.Instance.Config.MapHeight * PlanetGenerator.Instance.CellSize;
            }
        }

        private void LateUpdate()
        {
            // Prefer scene Minimap Container once Bottom Bar is ready.
            if (mapImage != null && transform.Find("MinimapCanvas") != null)
                TryMountIntoSceneContainer();

            if (mapTex == null) return;
            if (mapW <= 1f || mapH <= 1f)
                RefreshMapBounds();
            if (mapW <= 1f || mapH <= 1f) return;

            RebuildTexture();
            dirty = false;
        }

        private void RebuildTexture()
        {
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = Ground;

            var sectors = SectorManager.Instance?.Sectors;
            if (sectors != null && sectors.Count > 0)
            {
                foreach (var sector in sectors)
                {
                    if (sector == null) continue;
                    Color32 fill = FeatureColor(sector);
                    if (sector.IsOccupied) fill = Occupied;
                    DrawSectorCell(sector, fill);
                }

                // Sector grid lines
                foreach (var sector in sectors)
                {
                    if (sector == null) continue;
                    DrawSectorBorder(sector, GridLine);
                }
            }

            DrawBuildings();
            DrawCameraMarker();

            mapTex.SetPixels32(pixels);
            mapTex.Apply(false);
        }

        private static Color32 FeatureColor(SectorManager.Sector sector)
        {
            return sector.Feature switch
            {
                SectorManager.SectorFeature.WaterDeposit => Water,
                SectorManager.SectorFeature.Glacier => Glacier,
                SectorManager.SectorFeature.Volcano => Volcano,
                SectorManager.SectorFeature.FaultLine => Fault,
                SectorManager.SectorFeature.LavaTube => Lava,
                _ => Ground
            };
        }

        private void DrawSectorCell(SectorManager.Sector sector, Color32 color)
        {
            GetSectorUvRect(sector, out int x0, out int y0, out int x1, out int y1);
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                    SetPixel(x, y, color);
            }
        }

        private void DrawSectorBorder(SectorManager.Sector sector, Color32 color)
        {
            GetSectorUvRect(sector, out int x0, out int y0, out int x1, out int y1);
            for (int x = x0; x <= x1; x++)
            {
                SetPixel(x, y0, color);
                SetPixel(x, y1, color);
            }
            for (int y = y0; y <= y1; y++)
            {
                SetPixel(x0, y, color);
                SetPixel(x1, y, color);
            }
        }

        private void GetSectorUvRect(SectorManager.Sector sector, out int x0, out int y0, out int x1, out int y1)
        {
            // Approximate cell from sector spacing.
            float secW = mapW;
            float secH = mapH;
            int count = SectorManager.Instance != null ? Mathf.Max(1, SectorManager.Instance.Sectors.Count) : 1;
            // Prefer PlanetGenerator sector grid when available.
            if (PlanetGenerator.Instance?.Config != null)
            {
                int sx = Mathf.Max(1, PlanetGenerator.Instance.Config.SectorsX);
                int sy = Mathf.Max(1, PlanetGenerator.Instance.Config.SectorsY);
                secW = mapW / sx;
                secH = mapH / sy;
            }
            else
            {
                int side = Mathf.Max(1, Mathf.RoundToInt(Mathf.Sqrt(count)));
                secW = mapW / side;
                secH = mapH / side;
            }

            float cx = sector.Center.x;
            float cz = sector.Center.z;
            float minX = cx - secW * 0.5f;
            float maxX = cx + secW * 0.5f;
            float minZ = cz - secH * 0.5f;
            float maxZ = cz + secH * 0.5f;

            WorldToPixel(minX, minZ, out x0, out y0);
            WorldToPixel(maxX, maxZ, out x1, out y1);
            if (x0 > x1) (x0, x1) = (x1, x0);
            if (y0 > y1) (y0, y1) = (y1, y0);
            x0 = Mathf.Clamp(x0, 0, TexSize - 1);
            x1 = Mathf.Clamp(x1, 0, TexSize - 1);
            y0 = Mathf.Clamp(y0, 0, TexSize - 1);
            y1 = Mathf.Clamp(y1, 0, TexSize - 1);
        }

        private void DrawBuildings()
        {
            IReadOnlyList<BaseBuilding> buildings = BaseBuilding.ActiveBuildings;
            if (buildings == null) return;
            foreach (var b in buildings)
            {
                if (b == null || b.Owner != Owner.Player1) continue;
                WorldToPixel(b.transform.position.x, b.transform.position.z, out int px, out int py);
                bool isCp = b.BuildingSO != null
                    && b.BuildingSO.Name.IndexOf("Command", System.StringComparison.OrdinalIgnoreCase) >= 0;
                Color32 c = isCp ? CommandDot : BuildingDot;
                int r = isCp ? 2 : 1;
                for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                    SetPixel(px + dx, py + dy, c);
            }
        }

        private void DrawCameraMarker()
        {
            Vector3 focus = PlayerInput.GetCameraFocusPosition();
            WorldToPixel(focus.x, focus.z, out int cx, out int cy);
            // Crosshair
            for (int i = -6; i <= 6; i++)
            {
                SetPixel(cx + i, cy, CameraRing);
                SetPixel(cx, cy + i, CameraRing);
            }
            SetPixel(cx, cy, CameraDot);
            // Soft view ring
            for (int a = 0; a < 24; a++)
            {
                float rad = a * Mathf.PI * 2f / 24f;
                int ox = cx + Mathf.RoundToInt(Mathf.Cos(rad) * 10f);
                int oy = cy + Mathf.RoundToInt(Mathf.Sin(rad) * 10f);
                SetPixel(ox, oy, CameraRing);
            }
        }

        private void WorldToPixel(float worldX, float worldZ, out int px, out int py)
        {
            float u = Mathf.Clamp01(worldX / mapW);
            float v = Mathf.Clamp01(worldZ / mapH);
            px = Mathf.Clamp(Mathf.FloorToInt(u * (TexSize - 1)), 0, TexSize - 1);
            py = Mathf.Clamp(Mathf.FloorToInt(v * (TexSize - 1)), 0, TexSize - 1);
        }

        private void PixelToWorld(int px, int py, out float worldX, out float worldZ)
        {
            float u = (px + 0.5f) / TexSize;
            float v = (py + 0.5f) / TexSize;
            worldX = u * mapW;
            worldZ = v * mapH;
        }

        private void SetPixel(int x, int y, Color32 color)
        {
            if ((uint)x >= TexSize || (uint)y >= TexSize) return;
            pixels[y * TexSize + x] = color;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!TryScreenToWorld(eventData.position, out Vector3 world)) return;
            PlayerInput.FocusCameraOnWorldPosition(world);
            MarkDirty();
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!TryScreenToWorld(eventData.position, out Vector3 world)) return;
            PlayerInput.FocusCameraOnWorldPosition(world);
        }

        private bool TryScreenToWorld(Vector2 screenPos, out Vector3 world)
        {
            world = Vector3.zero;
            if (mapImage == null || mapW <= 1f) return false;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    mapImage.rectTransform, screenPos, null, out Vector2 local))
                return false;

            Rect r = mapImage.rectTransform.rect;
            float u = Mathf.InverseLerp(r.xMin, r.xMax, local.x);
            float v = Mathf.InverseLerp(r.yMin, r.yMax, local.y);
            int px = Mathf.Clamp(Mathf.FloorToInt(u * TexSize), 0, TexSize - 1);
            int py = Mathf.Clamp(Mathf.FloorToInt(v * TexSize), 0, TexSize - 1);
            PixelToWorld(px, py, out float wx, out float wz);
            float y = PlayerInput.GetCameraFocusPosition().y;
            world = new Vector3(wx, y, wz);
            return true;
        }

        /// <summary>Forwards UI pointer events from the RawImage to <see cref="MinimapUI"/>.</summary>
        private sealed class MinimapClickCatcher : MonoBehaviour, IPointerClickHandler, IDragHandler
        {
            public MinimapUI Owner;

            public void OnPointerClick(PointerEventData eventData) => Owner?.OnPointerClick(eventData);
            public void OnDrag(PointerEventData eventData) => Owner?.OnDrag(eventData);
        }
    }
}
