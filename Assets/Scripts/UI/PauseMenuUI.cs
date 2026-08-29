using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using GameDevTV.RTS.Player;

namespace GameDevTV.RTS.UI
{
    public class PauseMenuUI : MonoBehaviour
    {
        public GameObject menuPanel;
        public GameObject slotPanel; // New: Panel to select slots
        public Button resumeButton;
        public Button saveButton;
        public Button loadButton;
        public Button quitButton;

        public bool IsPauseMenuVisible =>
            (menuPanel != null && menuPanel.activeInHierarchy) ||
            (slotPanel != null && slotPanel.activeInHierarchy);

        // Slot buttons
        public Button slot1Button;
        public Button slot2Button;
        public Button slot3Button;
        public Button backButton;

        private bool isPaused = false;
        private bool isSaving = false; // Track if we are saving or loading

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
            if (slotPanel != null) slotPanel.SetActive(false);

            if (resumeButton != null)
            {
                resumeButton.onClick.RemoveListener(TogglePause);
                resumeButton.onClick.AddListener(TogglePause);
            }
            if (saveButton != null)
            {
                saveButton.onClick.RemoveListener(OnSaveClicked);
                saveButton.onClick.AddListener(OnSaveClicked);
            }
            if (loadButton != null)
            {
                loadButton.onClick.RemoveListener(OnLoadClicked);
                loadButton.onClick.AddListener(OnLoadClicked);
            }
            if (quitButton != null)
            {
                quitButton.onClick.RemoveListener(QuitGame);
                quitButton.onClick.AddListener(QuitGame);
            }

            // Slot button setup
            if (slot1Button != null) slot1Button.onClick.AddListener(() => SelectSlot(1));
            if (slot2Button != null) slot2Button.onClick.AddListener(() => SelectSlot(2));
            if (slot3Button != null) slot3Button.onClick.AddListener(() => SelectSlot(3));
            if (backButton != null) backButton.onClick.AddListener(CloseSlotPanel);
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

            if (!isPaused && slotPanel != null)
                slotPanel.SetActive(false);

            Time.timeScale = isPaused ? 0f : 1f;
        }

        private void OnSaveClicked()
        {
            isSaving = true;
            ShowSlotPanel();
        }

        private void OnLoadClicked()
        {
            isSaving = false;
            ShowSlotPanel();
        }

        private void ShowSlotPanel()
        {
            if (menuPanel != null) menuPanel.SetActive(false);
            if (slotPanel != null) slotPanel.SetActive(true);
        }

        private void CloseSlotPanel()
        {
            if (slotPanel != null) slotPanel.SetActive(false);
            if (menuPanel != null) menuPanel.SetActive(true);
        }

        private void SelectSlot(int slot)
        {
            if (isSaving)
            {
                SaveSystem.SaveGame(slot);
            }
            else
            {
                SaveSystem.LoadGame(slot);
            }
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
