using GameDevTV.RTS.Units;
using UnityEngine;
using UnityEngine.UI;

namespace GameDevTV.RTS.UI.Components
{
    /// <summary>
    /// Always-visible world-space health bar floating above a building.
    /// Lives in the scene root (not under the building) so building visual rebuilds
    /// cannot destroy the bar transform mid-Start. Draws through world meshes.
    /// </summary>
    [RequireComponent(typeof(BaseBuilding))]
    public class BuildingHealthBar : MonoBehaviour
    {
        private const float BarWidth = 2.8f;
        private const float BarHeight = 0.28f;
        private const float TopLift = 0.55f;

        private static Sprite s_WhiteSprite;
        private static Material s_OverlayUiMaterial;

        private BaseBuilding building;
        private Image fillImage;
        private GameObject barGo;
        private Canvas canvas;
        private int lastHealth = int.MinValue;
        private int lastMax = int.MinValue;

        private void Awake()
        {
            building = GetComponent<BaseBuilding>();
        }

        private void Start()
        {
            if (building == null || IsGhostOrInvalid())
            {
                enabled = false;
                return;
            }

            if (!TryBuildBar()) return;
            building.OnHealthUpdated += HandleHealthUpdated;
            ApplyFill();
        }

        private void OnDestroy()
        {
            if (building != null)
                building.OnHealthUpdated -= HandleHealthUpdated;
            DestroyBarGo();
        }

        private void LateUpdate()
        {
            if (building == null || IsGhostOrInvalid())
            {
                if (barGo != null) barGo.SetActive(false);
                return;
            }

            if (barGo == null && !TryBuildBar()) return;
            if (barGo == null) return;

            if (canvas != null && canvas.worldCamera == null && Camera.main != null)
                canvas.worldCamera = Camera.main;

            PositionAboveBuilding();
            // FaceCamera billboards the bar to the player camera (screen-aligned).

            if (building.CurrentHealth != lastHealth || building.MaxHealth != lastMax)
                ApplyFill();
        }

        private void HandleHealthUpdated(AbstractCommandable _, int __, int ___) => ApplyFill();

        private bool IsGhostOrInvalid()
        {
            if (building == null) return true;
            if (!building.enabled) return true;
            string n = building.gameObject.name;
            return n.StartsWith("Ghost_", System.StringComparison.Ordinal)
                || n.StartsWith("GhostPreview_", System.StringComparison.Ordinal);
        }

        private bool TryBuildBar()
        {
            if (barGo != null && fillImage != null) return true;
            DestroyBarGo();

            barGo = new GameObject("Building Health Bar");
            // Scene root — avoid parenting under buildings that rebuild/destroy children.
            barGo.transform.SetParent(null, false);

            canvas = barGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 5000;
            if (Camera.main != null)
                canvas.worldCamera = Camera.main;

            var rt = barGo.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(BarWidth * 100f, BarHeight * 100f);
            barGo.transform.localScale = Vector3.one * 0.01f;
            barGo.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 10f;

            Sprite white = GetWhiteSprite();
            if (white == null)
            {
                DestroyBarGo();
                return false;
            }

            Material overlayMat = GetOverlayUiMaterial();

            var bgGo = new GameObject("Background", typeof(RectTransform));
            bgGo.transform.SetParent(barGo.transform, false);
            var bgRt = bgGo.GetComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;
            var bgImg = bgGo.AddComponent<Image>();
            bgImg.sprite = white;
            bgImg.color = new Color(0.05f, 0.06f, 0.08f, 0.92f);
            bgImg.raycastTarget = false;
            if (overlayMat != null) bgImg.material = overlayMat;

            var fillGo = new GameObject("Fill", typeof(RectTransform));
            fillGo.transform.SetParent(barGo.transform, false);
            var fillRt = fillGo.GetComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = new Vector2(3f, 3f);
            fillRt.offsetMax = new Vector2(-3f, -3f);
            fillImage = fillGo.AddComponent<Image>();
            fillImage.sprite = white;
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            fillImage.color = new Color(0.35f, 0.9f, 0.4f, 1f);
            fillImage.raycastTarget = false;
            if (overlayMat != null) fillImage.material = overlayMat;

            if (barGo.GetComponent<FaceCamera>() == null)
                barGo.AddComponent<FaceCamera>();

            PositionAboveBuilding();
            return fillImage != null && barGo != null;
        }

        private void DestroyBarGo()
        {
            fillImage = null;
            canvas = null;
            if (barGo != null)
            {
                Destroy(barGo);
                barGo = null;
            }
        }

        private static Sprite GetWhiteSprite()
        {
            if (s_WhiteSprite != null) return s_WhiteSprite;

            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply(false, false);
            s_WhiteSprite = Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 100f);
            s_WhiteSprite.name = "BuildingHealthBarWhite";
            return s_WhiteSprite;
        }

        private static Material GetOverlayUiMaterial()
        {
            if (s_OverlayUiMaterial != null) return s_OverlayUiMaterial;

            Shader shader = Shader.Find("UI/Default");
            if (shader == null) return null;

            s_OverlayUiMaterial = new Material(shader)
            {
                name = "BuildingHealthBarOverlay",
                hideFlags = HideFlags.HideAndDontSave
            };
            // Always pass depth so the bar sits visually on top of building meshes.
            s_OverlayUiMaterial.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
            s_OverlayUiMaterial.renderQueue = 5000;
            return s_OverlayUiMaterial;
        }

        private void PositionAboveBuilding()
        {
            if (barGo == null || building == null) return;
            Bounds bounds = GetVisualBounds();
            // Anchor XZ to the building pivot so skewed mesh AABBs don't shove the bar sideways.
            float topY = Mathf.Max(bounds.max.y, building.transform.position.y + 1.5f);
            barGo.transform.position = new Vector3(
                building.transform.position.x,
                topY + TopLift,
                building.transform.position.z);
        }

        private Bounds GetVisualBounds()
        {
            var renderers = building.GetComponentsInChildren<Renderer>();
            bool any = false;
            Bounds b = new Bounds(building.transform.position, Vector3.one);
            foreach (var r in renderers)
            {
                if (r == null || !r.enabled) continue;
                string n = r.gameObject.name.ToLowerInvariant();
                if (n.Contains("vision") || n.Contains("indicator") || n.Contains("health")) continue;
                if (!any)
                {
                    b = r.bounds;
                    any = true;
                }
                else b.Encapsulate(r.bounds);
            }
            if (!any && building.TryGetComponent(out Collider col))
                b = col.bounds;
            return b;
        }

        private void ApplyFill()
        {
            if (building == null || fillImage == null || barGo == null) return;

            if (!barGo.activeSelf)
                barGo.SetActive(true);

            lastHealth = building.CurrentHealth;
            lastMax = Mathf.Max(1, building.MaxHealth);
            float t = Mathf.Clamp01(lastHealth / (float)lastMax);
            fillImage.fillAmount = t;

            if (t > 0.6f)
                fillImage.color = Color.Lerp(new Color(0.95f, 0.85f, 0.2f), new Color(0.3f, 0.9f, 0.35f), (t - 0.6f) / 0.4f);
            else if (t > 0.3f)
                fillImage.color = Color.Lerp(new Color(0.95f, 0.35f, 0.2f), new Color(0.95f, 0.85f, 0.2f), (t - 0.3f) / 0.3f);
            else
                fillImage.color = Color.Lerp(new Color(0.75f, 0.1f, 0.1f), new Color(0.95f, 0.35f, 0.2f), t / 0.3f);
        }
    }
}
