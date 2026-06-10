using UnityEngine;
using TMPro;
using UnityEngine.UI;
using GameDevTV.RTS.Units;

namespace GameDevTV.RTS.UI.Containers
{
    public class FoundryStarvationUI : MonoBehaviour
    {
        [SerializeField] private GameObject warningPanel;
        [SerializeField] private TextMeshProUGUI warningText;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private float flashSpeed = 4f;

        private void Start()
        {
            if (warningPanel != null)
            {
                warningPanel.SetActive(false);
            }
        }

        private void Update()
        {
            // Find any active FoundryCrawler
            var crawler = Object.FindAnyObjectByType<FoundryCrawler>(FindObjectsInactive.Exclude);
            if (crawler != null && crawler.IsStarving)
            {
                if (warningPanel != null && !warningPanel.activeSelf)
                {
                    warningPanel.SetActive(true);
                }

                float remainingTime = Mathf.Max(0f, crawler.MaxStarvationDuration - crawler.StarvationTimer);
                if (warningText != null)
                {
                    warningText.SetText($"<color=#FF2222><size=130%>⚠️ CRITICAL WARNING: CRAWLER STARVING! ⚠️</size></color>\nHoppers empty! Feed Crawler or Game Over in <color=#FFAA00>{remainingTime:F1}s</color>!");
                }

                if (backgroundImage != null)
                {
                    float alpha = 0.25f + Mathf.PingPong(Time.time * flashSpeed, 0.45f);
                    backgroundImage.color = new Color(0.7f, 0.05f, 0.05f, alpha);
                }
            }
            else
            {
                if (warningPanel != null && warningPanel.activeSelf)
                {
                    warningPanel.SetActive(false);
                }
            }
        }
    }
}