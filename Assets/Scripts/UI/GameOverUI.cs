using System.Collections;
using GameDevTV.RTS.Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameDevTV.RTS.UI
{
    /// <summary>
    /// Listens for GameOverManager.OnGameOver and reveals a full-screen overlay.
    /// Keep this component on an always-active object (e.g. Game Over Canvas root).
    /// Hiding the overlay must not disable this behaviour or event subscriptions are lost.
    /// </summary>
    public class GameOverUI : MonoBehaviour
    {
        [Header("Overlay Panel")]
        [SerializeField] private GameObject overlayPanel;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private float fadeDuration = 1.2f;

        public bool IsVisible =>
            canvasGroup != null && canvasGroup.alpha > 0.01f && canvasGroup.blocksRaycasts
            || (overlayPanel != null && overlayPanel.activeInHierarchy);

        [Header("Text")]
        [SerializeField] private TextMeshProUGUI headlineText;
        [SerializeField] private TextMeshProUGUI reasonText;

        [Header("Buttons")]
        [SerializeField] private Button restartButton;
        [SerializeField] private Button quitButton;

        public static void EnsureAllSubscribed()
        {
            var uis = Resources.FindObjectsOfTypeAll<GameOverUI>();
            foreach (var ui in uis)
            {
                if (ui == null || ui.gameObject.scene.name == null) continue;
                ui.ActivateHierarchy();
                ui.ResolveReferences();
                ui.EnsureSubscribed();
            }
        }

        private void Awake()
        {
            ResolveReferences();
            EnsureSubscribed();
        }

        private void OnEnable()
        {
            EnsureSubscribed();
        }

        private void OnDisable()
        {
            GameOverManager.OnGameOver -= HandleGameOver;
            GameOverManager.OnVictory -= HandleVictory;
        }

        private void Start()
        {
            SetPanelHidden();

            if (restartButton != null)
                restartButton.onClick.AddListener(RestartScene);

            if (quitButton != null)
                quitButton.onClick.AddListener(QuitGame);
        }

        public void ActivateHierarchy()
        {
            Transform current = transform;
            while (current != null)
            {
                if (!current.gameObject.activeSelf)
                    current.gameObject.SetActive(true);
                current = current.parent;
            }

            gameObject.SetActive(true);
        }

        public void EnsureSubscribed()
        {
            GameOverManager.OnGameOver -= HandleGameOver;
            GameOverManager.OnGameOver += HandleGameOver;
            GameOverManager.OnVictory -= HandleVictory;
            GameOverManager.OnVictory += HandleVictory;
        }

        private void ResolveReferences()
        {
            if (overlayPanel == null)
            {
                var t = transform.Find("Game Over Panel");
                if (t == null) t = transform.Find("Panel");
                if (t == null) t = transform.Find("Overlay Panel");
                if (t != null) overlayPanel = t.gameObject;
            }

            if (canvasGroup == null)
            {
                if (overlayPanel != null)
                    canvasGroup = overlayPanel.GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                    canvasGroup = GetComponent<CanvasGroup>();
            }

            if (headlineText == null || reasonText == null || restartButton == null)
            {
                var texts = GetComponentsInChildren<TextMeshProUGUI>(true);
                foreach (var text in texts)
                {
                    if (headlineText == null && text.name.Contains("Headline", System.StringComparison.OrdinalIgnoreCase))
                        headlineText = text;
                    if (reasonText == null && text.name.Contains("Reason", System.StringComparison.OrdinalIgnoreCase))
                        reasonText = text;
                }
            }

            if (restartButton == null || quitButton == null)
            {
                var buttons = GetComponentsInChildren<Button>(true);
                foreach (var button in buttons)
                {
                    if (restartButton == null && button.name.Contains("Restart", System.StringComparison.OrdinalIgnoreCase))
                        restartButton = button;
                    if (quitButton == null && button.name.Contains("Quit", System.StringComparison.OrdinalIgnoreCase))
                        quitButton = button;
                }
            }
        }

        private void HandleVictory()
        {
            if (headlineText != null)
                headlineText.SetText("MISSION SUCCESSFUL");

            if (reasonText != null)
                reasonText.SetText("The planet is now human habitable.\nSectors occupied and terraforming complete.");

            ShowGameOverUI();
        }

        private void HandleGameOver(GameOverManager.GameOverReason reason)
        {
            Debug.Log($"[GameOverUI] HandleGameOver called with reason: {reason}");
            if (headlineText != null)
                headlineText.SetText("MISSION FAILED");

            if (reasonText != null)
            {
                switch (reason)
                {
                    case GameOverManager.GameOverReason.LifeSupport:
                        reasonText.SetText("Life support has collapsed.\nThe colony can no longer sustain itself.");
                        break;
                    case GameOverManager.GameOverReason.MachineryFailure:
                        reasonText.SetText("Critical machinery has failed.\nExpansion and terraforming are no longer possible.");
                        break;
                    case GameOverManager.GameOverReason.HousingShortage:
                        reasonText.SetText("Housing capacity exceeded!\nNew colonists arrived with nowhere to stay. The colony has rebelled.");
                        break;
                    case GameOverManager.GameOverReason.Resources:
                    default:
                        reasonText.SetText("The planet's resources are gone.\nTerraforming has ceased.");
                        break;
                }
            }

            ShowGameOverUI();
        }

        private void ShowGameOverUI()
        {
            Debug.Log("[GameOverUI] Showing Game Over UI. Panel: " + (overlayPanel != null ? overlayPanel.name : "NULL"));
            ActivateHierarchy();

            if (overlayPanel != null && overlayPanel != gameObject)
                overlayPanel.SetActive(true);

            if (canvasGroup != null)
            {
                canvasGroup.blocksRaycasts = true;
                canvasGroup.interactable = true;
            }

            Time.timeScale = 0f;

            if (canvasGroup != null)
                StartCoroutine(FadeIn());
            else
                Debug.LogWarning("[GameOverUI] CanvasGroup is null, no fade will occur.");
        }

        private void SetPanelHidden()
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.blocksRaycasts = false;
                canvasGroup.interactable = false;
                return;
            }

            if (overlayPanel != null && overlayPanel != gameObject)
                overlayPanel.SetActive(false);
        }

        private IEnumerator FadeIn()
        {
            canvasGroup.alpha = 0f;
            float elapsed = 0f;

            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                canvasGroup.alpha = Mathf.Clamp01(elapsed / fadeDuration);
                yield return null;
            }

            canvasGroup.alpha = 1f;
        }

        private void RestartScene()
        {
            Time.timeScale = 1f;
            UnityEngine.SceneManagement.SceneManager.LoadScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex
            );
        }

        private void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
