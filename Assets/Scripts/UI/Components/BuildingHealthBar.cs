using GameDevTV.RTS.Units;
using UnityEngine;
using UnityEngine.UI;

namespace GameDevTV.RTS.UI.Components
{
    /// <summary>
    /// Always-visible world-space health bar under a building (repair state at a glance).
    /// </summary>
    [RequireComponent(typeof(BaseBuilding))]
    public class BuildingHealthBar : MonoBehaviour
    {
        private const float BarWidth = 2.4f;
        private const float BarHeight = 0.22f;
        private const float GroundClearance = 0.35f;

        private BaseBuilding building;
        private Image fillImage;
        private Transform barRoot;
        private int lastHealth = int.MinValue;
        private int lastMax = int.MinValue;

        private void Awake()
        {
            building = GetComponent<BaseBuilding>();
        }

        private void Start()
        {
            if (building == null) return;
            if (IsGhostOrInvalid())
            {
                enabled = false;
                return;
            }

            EnsureBar();
            building.OnHealthUpdated += HandleHealthUpdated;
            Refresh();
        }

        private void OnDestroy()
        {
            if (building != null)
                building.OnHealthUpdated -= HandleHealthUpdated;
        }

        private void LateUpdate()
        {
            if (building == null || barRoot == null) return;
            if (IsGhostOrInvalid())
            {
                if (barRoot.gameObject.activeSelf) barRoot.gameObject.SetActive(false);
                return;
            }

            PositionUnderBuilding();
            if (Camera.main != null)
                barRoot.rotation = Quaternion.LookRotation(barRoot.position - Camera.main.transform.position);

            if (building.CurrentHealth != lastHealth || building.MaxHealth != lastMax)
                Refresh();
        }

        private void HandleHealthUpdated(AbstractCommandable _, int __, int ___) => Refresh();

        private bool IsGhostOrInvalid()
        {
            if (building == null) return true;
            if (!building.enabled) return true;
            string n = building.gameObject.name;
            return n.StartsWith("Ghost_", System.StringComparison.Ordinal)
                || n.StartsWith("GhostPreview_", System.StringComparison.Ordinal);
        }

        private void EnsureBar()
        {
            if (barRoot != null) return;

            var root = new GameObject("Building Health Bar");
            root.transform.SetParent(transform, false);
            barRoot = root.transform;

            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 40;
            var rt = root.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(BarWidth * 100f, BarHeight * 100f);
            root.transform.localScale = Vector3.one * 0.01f;

            root.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 10f;

            // Background
            var bgGo = new GameObject("Background", typeof(RectTransform));
            bgGo.transform.SetParent(root.transform, false);
            var bgRt = bgGo.GetComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;
            var bgImg = bgGo.AddComponent<Image>();
            bgImg.color = new Color(0.08f, 0.09f, 0.11f, 0.85f);
            bgImg.raycastTarget = false;

            // Fill
            var fillGo = new GameObject("Fill", typeof(RectTransform));
            fillGo.transform.SetParent(root.transform, false);
            var fillRt = fillGo.GetComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = new Vector2(2f, 2f);
            fillRt.offsetMax = new Vector2(-2f, -2f);
            fillImage = fillGo.AddComponent<Image>();
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            fillImage.color = new Color(0.35f, 0.9f, 0.4f, 1f);
            fillImage.raycastTarget = false;

            PositionUnderBuilding();
        }

        private void PositionUnderBuilding()
        {
            if (barRoot == null || building == null) return;
            Bounds bounds = GetVisualBounds();
            Vector3 pos = bounds.center;
            pos.y = bounds.min.y - GroundClearance;
            barRoot.position = pos;
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

        private void Refresh()
        {
            if (building == null || fillImage == null) return;
            if (barRoot != null && !barRoot.gameObject.activeSelf)
                barRoot.gameObject.SetActive(true);

            lastHealth = building.CurrentHealth;
            lastMax = Mathf.Max(1, building.MaxHealth);
            float t = Mathf.Clamp01(lastHealth / (float)lastMax);
            fillImage.fillAmount = t;

            // Green → yellow → red
            if (t > 0.6f)
                fillImage.color = Color.Lerp(new Color(0.95f, 0.85f, 0.2f), new Color(0.3f, 0.9f, 0.35f), (t - 0.6f) / 0.4f);
            else if (t > 0.3f)
                fillImage.color = Color.Lerp(new Color(0.95f, 0.35f, 0.2f), new Color(0.95f, 0.85f, 0.2f), (t - 0.3f) / 0.3f);
            else
                fillImage.color = Color.Lerp(new Color(0.75f, 0.1f, 0.1f), new Color(0.95f, 0.35f, 0.2f), t / 0.3f);
        }
    }
}
