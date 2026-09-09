using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.Audio;

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
        private Slider musicSlider;
        private Slider sfxSlider;

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

            EnsureVolumeControls();
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

            // Build sliders when opening — catches late UI / domain reload cases.
            if (isPaused) EnsureVolumeControls();
            
            if (menuPanel != null)
                menuPanel.SetActive(isPaused);

            if (!isPaused && slotPanel != null)
                slotPanel.SetActive(false);

            Time.timeScale = isPaused ? 0f : 1f;

            if (isPaused)
                SyncVolumeSlidersFromAudio();
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

        private void EnsureVolumeControls()
        {
            if (menuPanel == null) return;

            // Scene wires menuPanel to "Content"; the visible box is child "Menu Panel".
            Transform host = menuPanel.transform.Find("Menu Panel");
            if (host == null) host = menuPanel.transform;

            Transform existing = host.Find("Volume Controls");
            if (existing == null && menuPanel.transform != host)
                existing = menuPanel.transform.Find("Volume Controls");

            // Empty shell from a failed earlier build blocks rebuild — destroy and recreate.
            if (existing != null)
            {
                bool complete = existing.Find("Music Row/Slider") != null
                    && existing.Find("SFX Row/Slider") != null;
                if (!complete)
                {
                    // Immediate: same-frame recreate must not Find() the doomed empty shell.
                    DestroyImmediate(existing.gameObject);
                    existing = null;
                    musicSlider = null;
                    sfxSlider = null;
                }
            }

            if (existing != null)
            {
                // Reparent if an older build put them on Content.
                if (existing.parent != host)
                    existing.SetParent(host, false);
                existing.SetAsLastSibling();
                PositionVolumeRoot(existing as RectTransform);
                musicSlider ??= existing.Find("Music Row/Slider")?.GetComponent<Slider>();
                sfxSlider ??= existing.Find("SFX Row/Slider")?.GetComponent<Slider>();
                LayoutMenuButtonsAroundVolume(host);
                return;
            }

            TMP_FontAsset font = null;
            var existingTmp = host.GetComponentInChildren<TextMeshProUGUI>(true);
            if (existingTmp != null) font = existingTmp.font;

            var audio = AudioManager.Instance;
            float musicVol = audio != null ? audio.MusicVolume : 0.5f;
            float sfxVol = audio != null ? audio.SfxVolume : 0.85f;

            GameObject root = new GameObject("Volume Controls", typeof(RectTransform), typeof(Image));
            root.transform.SetParent(host, false);
            root.transform.SetAsLastSibling();
            var rootImg = root.GetComponent<Image>();
            rootImg.color = new Color(0.05f, 0.07f, 0.1f, 0.92f);
            rootImg.raycastTarget = true;

            RectTransform rootRt = root.GetComponent<RectTransform>();
            PositionVolumeRoot(rootRt);

            var vlg = root.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 8f;
            vlg.padding = new RectOffset(10, 10, 8, 8);
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.childControlHeight = true;
            vlg.childControlWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childForceExpandWidth = true;

            musicSlider = CreateVolumeRow(root.transform, "Music", font, musicVol, v =>
            {
                AudioManager.Instance?.SetMusicVolume(v);
            });
            sfxSlider = CreateVolumeRow(root.transform, "SFX", font, sfxVol, v =>
            {
                AudioManager.Instance?.SetSfxVolume(v);
            });

            LayoutMenuButtonsAroundVolume(host);
        }

        /// <summary>
        /// Scene buttons all sit at center (0,0) and stack on top of each other / the volume plate.
        /// Nudge them into a vertical stack under the volume block.
        /// </summary>
        private static void LayoutMenuButtonsAroundVolume(Transform host)
        {
            if (host == null) return;
            string[] names = { "Resume Button", "Save Button", "Load Button", "Quit Button" };
            float[] ys = { 40f, -20f, -80f, -140f };
            for (int i = 0; i < names.Length; i++)
            {
                var t = host.Find(names[i]) as RectTransform;
                if (t == null) continue;
                t.anchorMin = t.anchorMax = new Vector2(0.5f, 0.5f);
                t.pivot = new Vector2(0.5f, 0.5f);
                t.anchoredPosition = new Vector2(0f, ys[i]);
            }

            // Keep title as a header strip, not a full-panel stretch that fights siblings.
            var title = host.Find("Title") as RectTransform;
            if (title != null)
            {
                title.anchorMin = new Vector2(0f, 1f);
                title.anchorMax = new Vector2(1f, 1f);
                title.pivot = new Vector2(0.5f, 1f);
                title.sizeDelta = new Vector2(0f, 48f);
                title.anchoredPosition = new Vector2(0f, 0f);
            }
        }

        private static void PositionVolumeRoot(RectTransform rootRt)
        {
            if (rootRt == null) return;
            // Below the title strip at the top of the 300x400 Menu Panel.
            rootRt.anchorMin = new Vector2(0.5f, 1f);
            rootRt.anchorMax = new Vector2(0.5f, 1f);
            rootRt.pivot = new Vector2(0.5f, 1f);
            rootRt.sizeDelta = new Vector2(260f, 96f);
            rootRt.anchoredPosition = new Vector2(0f, -52f);
            rootRt.localScale = Vector3.one;
        }

        private static Slider CreateVolumeRow(
            Transform parent, string label, TMP_FontAsset font, float initial, System.Action<float> onChanged)
        {
            GameObject row = new GameObject(label + " Row", typeof(RectTransform));
            row.transform.SetParent(parent, false);
            var rowLe = row.AddComponent<LayoutElement>();
            rowLe.minHeight = 36f;
            rowLe.preferredHeight = 40f;

            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 10f;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;
            hlg.padding = new RectOffset(0, 0, 0, 0);

            GameObject labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(row.transform, false);
            var labelLe = labelGo.AddComponent<LayoutElement>();
            labelLe.minWidth = 64f;
            labelLe.preferredWidth = 72f;
            var labelTmp = labelGo.AddComponent<TextMeshProUGUI>();
            if (font != null) labelTmp.font = font;
            labelTmp.text = label;
            labelTmp.fontSize = 18f;
            labelTmp.alignment = TextAlignmentOptions.MidlineLeft;
            labelTmp.color = Color.white;
            labelTmp.raycastTarget = false;

            GameObject sliderGo = new GameObject("Slider", typeof(RectTransform));
            sliderGo.transform.SetParent(row.transform, false);
            var sliderLe = sliderGo.AddComponent<LayoutElement>();
            sliderLe.flexibleWidth = 1f;
            sliderLe.minWidth = 140f;

            // Background track
            var bg = new GameObject("Background", typeof(RectTransform));
            bg.transform.SetParent(sliderGo.transform, false);
            var bgRt = bg.GetComponent<RectTransform>();
            bgRt.anchorMin = new Vector2(0f, 0.35f);
            bgRt.anchorMax = new Vector2(1f, 0.65f);
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;
            var bgImg = bg.AddComponent<Image>();
            bgImg.color = new Color(0.12f, 0.14f, 0.18f, 0.95f);

            // Fill area + fill
            var fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(sliderGo.transform, false);
            var fillAreaRt = fillArea.GetComponent<RectTransform>();
            fillAreaRt.anchorMin = new Vector2(0f, 0.35f);
            fillAreaRt.anchorMax = new Vector2(1f, 0.65f);
            fillAreaRt.offsetMin = new Vector2(6f, 0f);
            fillAreaRt.offsetMax = new Vector2(-6f, 0f);

            var fill = new GameObject("Fill", typeof(RectTransform));
            fill.transform.SetParent(fillArea.transform, false);
            var fillRt = fill.GetComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;
            var fillImg = fill.AddComponent<Image>();
            fillImg.color = new Color(0.25f, 0.65f, 0.95f, 1f);

            // Handle
            var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleArea.transform.SetParent(sliderGo.transform, false);
            var handleAreaRt = handleArea.GetComponent<RectTransform>();
            handleAreaRt.anchorMin = new Vector2(0f, 0f);
            handleAreaRt.anchorMax = new Vector2(1f, 1f);
            handleAreaRt.offsetMin = new Vector2(8f, 0f);
            handleAreaRt.offsetMax = new Vector2(-8f, 0f);

            var handle = new GameObject("Handle", typeof(RectTransform));
            handle.transform.SetParent(handleArea.transform, false);
            var handleRt = handle.GetComponent<RectTransform>();
            handleRt.sizeDelta = new Vector2(18f, 18f);
            var handleImg = handle.AddComponent<Image>();
            handleImg.color = new Color(0.85f, 0.92f, 1f, 1f);

            var slider = sliderGo.AddComponent<Slider>();
            slider.fillRect = fillRt;
            slider.handleRect = handleRt;
            slider.targetGraphic = handleImg;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.value = Mathf.Clamp01(initial);
            slider.onValueChanged.AddListener(v => onChanged?.Invoke(v));

            return slider;
        }

        private void SyncVolumeSlidersFromAudio()
        {
            var audio = AudioManager.Instance;
            if (audio == null) return;
            if (musicSlider != null) musicSlider.SetValueWithoutNotify(audio.MusicVolume);
            if (sfxSlider != null) sfxSlider.SetValueWithoutNotify(audio.SfxVolume);
        }
    }
}
