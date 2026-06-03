using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using GameDevTV.RTS.Player;

namespace GameDevTV.RTS.UI
{
    public class PauseMenuUI : MonoBehaviour
    {
        public GameObject menuPanel;
        public Button resumeButton;
        public Button saveButton;
        public Button loadButton;
        public Button quitButton;

        private bool isPaused = false;

        private void Awake()
        {
            // Auto-hookup the HUD Menu Button if it exists in the scene
            GameObject menuBtnGO = GameObject.Find("Menu Button");
            if (menuBtnGO != null)
            {
                Button mainBtn = menuBtnGO.GetComponent<Button>();
                if (mainBtn != null)
                {
                    mainBtn.onClick.RemoveListener(TogglePause);
                    mainBtn.onClick.AddListener(TogglePause);
                }
            }
        }

        private void Start()
        {
            if (menuPanel != null) menuPanel.SetActive(false);

            if (resumeButton != null)
            {
                resumeButton.onClick.RemoveListener(TogglePause);
                resumeButton.onClick.AddListener(TogglePause);
            }
            if (saveButton != null)
            {
                saveButton.onClick.RemoveListener(SaveGame);
                saveButton.onClick.AddListener(SaveGame);
            }
            if (loadButton != null)
            {
                loadButton.onClick.RemoveListener(LoadGame);
                loadButton.onClick.AddListener(LoadGame);
            }
            if (quitButton != null)
            {
                quitButton.onClick.RemoveListener(QuitGame);
                quitButton.onClick.AddListener(QuitGame);
            }
        }

        private void Update()
        {
            if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                TogglePause();
            }
        }

        public void TogglePause()
        {
            isPaused = !isPaused;
            
            if (menuPanel != null)
                menuPanel.SetActive(isPaused);

            Time.timeScale = isPaused ? 0f : 1f;
            
            // Log for debugging
            // Debug.Log($"[PauseMenuUI] TogglePause: isPaused={isPaused}, panelActive={menuPanel?.activeSelf}");
        }

        private void SaveGame()
        {
            SaveSystem.SaveGame();
            TogglePause();
        }

        private void LoadGame()
        {
            SaveSystem.LoadGame();
            TogglePause();
        }

        private void QuitGame()
        {
            Time.timeScale = 1f;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
