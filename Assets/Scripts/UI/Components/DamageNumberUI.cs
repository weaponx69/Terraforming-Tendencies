using UnityEngine;
using TMPro;

namespace GameDevTV.RTS.UI.Components
{
    /// <summary>
    /// Floating damage number that appears over a commandable when it takes damage.
    /// Animates upward and fades out over ~1 second, then self-destructs.
    /// Creates its own Canvas + TextMeshPro at runtime — no prefab needed.
    /// </summary>
    public class DamageNumberUI : MonoBehaviour
    {
        [Header("Animation")]
        [SerializeField] private float floatSpeed = 1.5f;
        [SerializeField] private float fadeDuration = 1.0f;
        [SerializeField] private float randomOffsetRange = 0.5f;

        [Header("Text Style")]
        [SerializeField] private int fontSize = 36;
        [SerializeField] private Color damageColor = Color.red;
        [SerializeField] private Color criticalColor = new Color(1f, 0.5f, 0f); // Orange
        [SerializeField] private int criticalThreshold = 10;

        private TextMeshProUGUI label;
        private CanvasGroup canvasGroup;
        private float elapsed;
        private int damageToShow;

        /// <summary>
        /// Spawn a floating damage number at the given world position.
        /// </summary>
        public static void Spawn(Vector3 worldPosition, int damage)
        {
            GameObject go = new GameObject("DamageNumber");
            go.transform.position = worldPosition;

            var dmg = go.AddComponent<DamageNumberUI>();
            dmg.damageToShow = damage;
        }

        private void Awake()
        {
            // Create a World Space Canvas
            GameObject canvasGO = new GameObject("Canvas");
            canvasGO.transform.SetParent(transform, false);

            Canvas canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.GetComponent<Transform>().localScale = Vector3.one * 0.01f; // Scale down to world-space

            // Add CanvasGroup for fading
            canvasGroup = canvasGO.AddComponent<CanvasGroup>();

            // Add TextMeshPro
            GameObject textGO = new GameObject("Damage Text");
            textGO.transform.SetParent(canvasGO.transform, false);

            label = textGO.AddComponent<TextMeshProUGUI>();
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = fontSize;
            label.fontStyle = FontStyles.Bold;
            label.color = damageColor;

            // Set rect transform size
            RectTransform rect = label.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(200, 100);
            rect.anchoredPosition = Vector2.zero;

            // Ensure it renders on top
            canvas.sortingOrder = 1000;
        }

        private void Start()
        {
            Show(damageToShow);
        }

        public void Show(int damage)
        {
            // Color by severity
            if (damage >= criticalThreshold)
                label.color = criticalColor;
            else
                label.color = damageColor;

            label.text = $"-{damage}";

            // Random horizontal offset for visual variety
            Vector3 offset = new Vector3(
                Random.Range(-randomOffsetRange, randomOffsetRange),
                0,
                Random.Range(-randomOffsetRange, randomOffsetRange)
            );
            transform.position += offset;

            elapsed = 0f;
            canvasGroup.alpha = 1f;
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            float t = elapsed / fadeDuration;

            // Float upward
            transform.position += Vector3.up * floatSpeed * Time.deltaTime;

            // Fade out
            if (canvasGroup != null)
                canvasGroup.alpha = Mathf.Lerp(1f, 0f, t);

            // Self-destruct when animation completes
            if (t >= 1f)
                Destroy(gameObject);
        }
    }
}