using UnityEngine;
using TMPro;
using UnityEngine.UI;
using GameDevTV.RTS.Player;

namespace GameDevTV.RTS.UI.Containers
{
    /// <summary>
    /// Left-side HUD readout for Colony Act weeks remaining — always visible
    /// while placing so the player can see the turn budget at a glance.
    /// </summary>
    public class WeeksLeftUI : MonoBehaviour
    {
        private TextMeshProUGUI labelText;
        private TextMeshProUGUI valueText;
        private Image background;
        private bool layoutReady;
        private int lastShown = int.MinValue;

        private void OnEnable()
        {
            ColonyActManager.OnActStateChanged += Refresh;
        }

        private void OnDisable()
        {
            ColonyActManager.OnActStateChanged -= Refresh;
        }

        private void Start()
        {
            SetupLayout();
            Refresh();
        }

        private void SetupLayout()
        {
            if (layoutReady) return;
            layoutReady = true;

            RectTransform rect = gameObject.GetComponent<RectTransform>();
            if (rect == null) rect = gameObject.AddComponent<RectTransform>();
            // Left side, below the top resource strip, clear of Integrity meter.
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(168f, 72f);
            rect.anchoredPosition = new Vector2(18f, -150f);

            background = gameObject.GetComponent<Image>();
            if (background == null) background = gameObject.AddComponent<Image>();
            background.color = new Color(0.03f, 0.04f, 0.07f, 0.90f);
            background.raycastTarget = false;

            var outline = gameObject.GetComponent<Outline>();
            if (outline == null) outline = gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.20f, 0.55f, 0.85f, 0.85f);
            outline.effectDistance = new Vector2(2f, -2f);

            TMP_FontAsset projectFont = null;
            var allTmp = Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include);
            foreach (var tmp in allTmp)
            {
                if (tmp != null && tmp.font != null)
                {
                    projectFont = tmp.font;
                    break;
                }
            }

            GameObject labelGo = new GameObject("Weeks Label");
            labelGo.transform.SetParent(transform, false);
            RectTransform labelRt = labelGo.AddComponent<RectTransform>();
            labelRt.anchorMin = new Vector2(0f, 0.52f);
            labelRt.anchorMax = new Vector2(1f, 1f);
            labelRt.offsetMin = new Vector2(12f, 0f);
            labelRt.offsetMax = new Vector2(-12f, -6f);

            labelText = labelGo.AddComponent<TextMeshProUGUI>();
            if (projectFont != null) labelText.font = projectFont;
            labelText.fontSize = 14f;
            labelText.alignment = TextAlignmentOptions.Left;
            labelText.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
            labelText.color = new Color(0.55f, 0.95f, 1f, 1f);
            labelText.text = "Weeks Left";
            labelText.raycastTarget = false;

            GameObject valueGo = new GameObject("Weeks Value");
            valueGo.transform.SetParent(transform, false);
            RectTransform valueRt = valueGo.AddComponent<RectTransform>();
            valueRt.anchorMin = new Vector2(0f, 0f);
            valueRt.anchorMax = new Vector2(1f, 0.58f);
            valueRt.offsetMin = new Vector2(12f, 6f);
            valueRt.offsetMax = new Vector2(-12f, 0f);

            valueText = valueGo.AddComponent<TextMeshProUGUI>();
            if (projectFont != null) valueText.font = projectFont;
            valueText.fontSize = 28f;
            valueText.alignment = TextAlignmentOptions.Left;
            valueText.fontStyle = FontStyles.Bold;
            valueText.color = Color.white;
            valueText.text = "—";
            valueText.raycastTarget = false;
        }

        private void Update()
        {
            // Lightweight poll so the panel updates even if event fires before layout.
            if (ColonyActManager.Instance == null) return;
            int weeks = ColonyActManager.Instance.WeeksRemaining;
            if (weeks != lastShown) Refresh();
        }

        private void Refresh()
        {
            if (!layoutReady) SetupLayout();
            if (valueText == null) return;

            if (ColonyActManager.Instance == null)
            {
                valueText.text = "—";
                valueText.color = new Color(0.78f, 0.82f, 0.86f, 1f);
                lastShown = int.MinValue;
                return;
            }

            int weeks = ColonyActManager.Instance.WeeksRemaining;
            lastShown = weeks;
            valueText.text = weeks.ToString();
            valueText.color = weeks <= 2
                ? new Color(1f, 0.54f, 0.54f, 1f)
                : weeks <= 4
                    ? new Color(1f, 0.88f, 0.54f, 1f)
                    : new Color(0.78f, 0.82f, 0.86f, 1f);
        }
    }
}
