using System.Collections;
using GameDevTV.RTS.Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameDevTV.RTS.UI
{
    /// <summary>
    /// Listens for GameOverManager.OnGameOver and reveals a full-screen overlay.
    ///
    /// Wire in Inspector:
    ///   - overlayPanel     : a Canvas > Panel (CanvasGroup for fade)
    ///   - headlineText     : "MISSION FAILED" TextMeshProUGUI
    ///   - reasonText       : subtitle TextMeshProUGUI
    ///   - restartButton    : Button that reloads the active scene
    ///   - quitButton       : Button that quits the application
    ///
    /// The panel should start INACTIVE in the scene so it's hidden until triggered.
    /// </summary>
    public class GameOverUI : MonoBehaviour
    {
        // ── Inspector ──────────────────────────────────────────────────────────────
        [Header("Overlay Panel")]
        [SerializeField] private GameObject overlayPanel;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private float fadeDuration = 1.2f;

        [Header("Text")]
        [SerializeField] private TextMeshProUGUI headlineText;
        [SerializeField] private TextMeshProUGUI reasonText;

        [Header("Buttons")]
        [SerializeField] private Button restartButton;
        [SerializeField] private Button quitButton;

        // ── Lifecycle ──────────────────────────────────────────────────────────────
        private void OnEnable()
        {
            Debug.Log("[GameOverUI] OnEnable called. Subscribing to GameOverManager events.");
            GameOverManager.OnGameOver += HandleGameOver;
            GameOverManager.OnVictory += HandleVictory;
        }

        private void OnDisable()
        {
            Debug.Log("[GameOverUI] OnDisable called. Stack trace:\n" + System.Environment.StackTrace);
            GameOverManager.OnGameOver -= HandleGameOver;
            GameOverManager.OnVictory -= HandleVictory;
        }

        private void Start()
        {
            if (overlayPanel != null) overlayPanel.SetActive(false);

            if (restartButton != null)
                restartButton.onClick.AddListener(RestartScene);

            if (quitButton != null)
                quitButton.onClick.AddListener(QuitGame);
        }

        // ── Handlers ────────────────────────────────────────────────────────────────
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
            if (overlayPanel != null)
                overlayPanel.SetActive(true);

            // Pause the simulation
            Time.timeScale = 0f;

            // Fade in
            if (canvasGroup != null)
                StartCoroutine(FadeIn());
            else
                Debug.LogWarning("[GameOverUI] CanvasGroup is null, no fade will occur.");
        }

        // ── Helpers ────────────────────────────────────────────────────────────────
        private IEnumerator FadeIn()
        {
            canvasGroup.alpha = 0f;
            float elapsed = 0f;

            // Use unscaled time so the fade still runs after Time.timeScale = 0
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
