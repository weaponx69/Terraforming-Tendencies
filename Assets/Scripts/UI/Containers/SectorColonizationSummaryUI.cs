using System.Collections;
using GameDevTV.RTS.Environment;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.Utilities;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GameDevTV.RTS.UI.Containers
{
    /// <summary>
    /// Shown after the generation summary when advancing to the next round.
    /// Confirms that the closest sector will receive an auto-built Command Post.
    /// Uses its own overlay Canvas so other UI cannot steal clicks while paused.
    /// </summary>
    public class SectorColonizationSummaryUI : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI bodyText;
        [SerializeField] private Button primaryButton;
        [SerializeField] private TextMeshProUGUI primaryButtonLabel;

        private Canvas overlayCanvas;
        private bool _hasAdvancedGeneration;
        private bool _showRequested;

        public bool IsVisible => panel != null && panel.activeInHierarchy;

        public static SectorColonizationSummaryUI EnsureInstance()
        {
            var existing = Object.FindAnyObjectByType<SectorColonizationSummaryUI>(FindObjectsInactive.Include);
            if (existing != null)
            {
                existing.ActivateHierarchy();
                return existing;
            }

            var controller = new GameObject("Sector Colonization Summary UI Controller");
            var ui = controller.AddComponent<SectorColonizationSummaryUI>();
            ui.ActivateHierarchy();
            return ui;
        }

        private void Awake()
        {
            EnsurePanelBuilt();
            BindPrimaryButton();
        }

        private void Update()
        {
            // timeScale is 0 during this overlay — still allow Enter/Space/click via unscaled input.
            if (!IsVisible) return;

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space))
            {
                OnPrimaryClicked();
                return;
            }

            if (Input.GetMouseButtonDown(0) && primaryButton != null && IsPointerOverPrimaryButton())
            {
                OnPrimaryClicked();
            }
        }

        public void ShowAfterGenerationSummary()
        {
            _showRequested = true;
            _hasAdvancedGeneration = false;
            ActivateHierarchy();
            EnsurePanelBuilt();
            BindPrimaryButton();
            EnsureEventSystem();
            Time.timeScale = 0f;
            ShowPreview();
        }

        public void ShowAfterGenerationSummaryDeferred(MonoBehaviour host)
        {
            if (host != null && host.isActiveAndEnabled)
                host.StartCoroutine(ShowAfterGenerationSummaryNextFrame());
            else
                ShowAfterGenerationSummary();
        }

        private IEnumerator ShowAfterGenerationSummaryNextFrame()
        {
            yield return null;
            ShowAfterGenerationSummary();
        }

        private void ShowPreview()
        {
            var gm = GenerationManager.Instance;
            if (gm == null)
            {
                Hide();
                return;
            }

            var preview = gm.PreviewColonizationBeforeAdvance();
            if (titleText != null)
            {
                titleText.text = preview.WillColonize
                    ? "New Command Post Deployment"
                    : preview.EnteringExpansion
                        ? "Entering Expansion Phase"
                        : "Continue to Next Generation";
            }

            if (bodyText != null)
            {
                if (preview.WillColonize)
                {
                    string sectorName = FormatSectorName(preview.TargetSectorIndex);
                    bodyText.text =
                        $"Advancing to Generation {gm.CurrentGeneration + 1} will automatically build a " +
                        $"Command Post in the closest sector that still needs one.\n\n" +
                        $"Target: {sectorName}\n" +
                        "Fog will lift over its build pads so you can place solar panels and climate buildings immediately.";
                }
                else if (preview.EnteringExpansion)
                {
                    bodyText.text =
                        "You have finished the final terraforming generation.\n\n" +
                        "The expansion phase begins next. Any remaining map sectors can still be claimed with Command Posts.";
                }
                else
                {
                    bodyText.text =
                        "No additional sector deployment is required before the next generation.\n\n" +
                        "Your existing Command Posts will continue operating.";
                }
            }

            SetPrimaryButtonLabel(preview.WillColonize ? "Deploy Command Post" : "Continue");
            BringToFront();
            if (panel != null) panel.SetActive(true);

            Debug.Log($"[SectorColonizationSummaryUI] Preview shown. WillColonize={preview.WillColonize} " +
                      $"TargetSector={preview.TargetSectorIndex} Visible={IsVisible}");
        }

        private void ShowVerification()
        {
            var gm = GenerationManager.Instance;
            if (gm == null)
            {
                Hide();
                return;
            }

            var result = gm.LastColonizationResult;
            if (titleText != null)
            {
                titleText.text = result.Succeeded
                    ? "Command Post Deployed"
                    : result.Attempted
                        ? "Deployment Issue"
                        : "Generation Advanced";
            }

            if (bodyText != null)
            {
                if (result.Attempted)
                {
                    string sectorName = FormatSectorName(result.SectorIndex);
                    bodyText.text = result.Succeeded
                        ? $"Verified: a completed Command Post is now active in {sectorName}.\n\n" +
                          $"{result.Message}\n\n" +
                          $"Generation {gm.CurrentGeneration} has begun. Build pads in the new sector are ready."
                        : $"We tried to deploy a Command Post to {sectorName}, but verification failed.\n\n" +
                          $"{result.Message}\n\n" +
                          "You may need to place a Command Post manually from your hand.";
                }
                else if (gm.IsExpansionPhase)
                {
                    bodyText.text =
                        "Expansion phase active.\n\n" +
                        "Use exploration cards or Command Post placement to claim any remaining sectors.";
                }
                else
                {
                    bodyText.text =
                        $"Generation {gm.CurrentGeneration} has begun.\n\n" +
                        "Continue terraforming from your current Command Posts.";
                }
            }

            SetPrimaryButtonLabel(gm.IsExpansionPhase
                ? "Begin Expansion"
                : $"Continue to Generation {gm.CurrentGeneration}");
            BringToFront();
        }

        private void OnPrimaryClicked()
        {
            if (!_hasAdvancedGeneration)
            {
                var gm = GenerationManager.Instance;
                if (gm == null)
                {
                    Hide();
                    return;
                }

                gm.StartNextGeneration();
                _hasAdvancedGeneration = true;
                Time.timeScale = 0f;
                ShowVerification();
                return;
            }

            FocusCameraOnColonizedCommandPost();
            RunColonizedSectorHandoff();

            if (GenerationManager.Instance != null && !GenerationManager.Instance.IsBetweenRounds)
                Time.timeScale = 1f;
            Hide();
        }

        private static void RunColonizedSectorHandoff()
        {
            BuildingSiteRegistry.RefreshAllMarkers();

            var deck = CardDeckController.Instance;
            if (deck != null)
            {
                deck.PrepareHandForColonizedSector();
            }

            // Prefer selecting the new CP so train/drone cards route correctly immediately.
            var gm = GenerationManager.Instance;
            if (gm != null
                && gm.LastColonizationResult.Succeeded
                && SectorManager.Instance != null
                && gm.LastColonizationResult.SectorIndex >= 0
                && gm.LastColonizationResult.SectorIndex < SectorManager.Instance.Sectors.Count)
            {
                var sector = SectorManager.Instance.Sectors[gm.LastColonizationResult.SectorIndex];
                if (sector?.OccupyingBuilding != null)
                {
                    sector.OccupyingBuilding.Select();
                }
            }
        }

        private static void FocusCameraOnColonizedCommandPost()
        {
            var gm = GenerationManager.Instance;
            if (gm == null) return;

            int sectorIndex = gm.LastColonizationResult.SectorIndex;
            if (!gm.LastColonizationResult.Succeeded && sectorIndex < 0)
            {
                // Fallback: newest active sector if colonization metadata is missing.
                if (SectorManager.Instance?.ActiveSector != null)
                {
                    SectorColonization.TryGetCommandPostFocusPosition(
                        SectorManager.Instance.ActiveSector, out Vector3 fallback);
                    PlayerInput.FocusCameraOnWorldPosition(fallback);
                }
                return;
            }

            if (SectorColonization.TryGetCommandPostFocusPosition(sectorIndex, out Vector3 focus))
            {
                PlayerInput.FocusCameraOnWorldPosition(focus);
            }
        }

        public void InvokePrimaryAction()
        {
            OnPrimaryClicked();
        }

        private void Hide()
        {
            if (panel != null) panel.SetActive(false);
            if (overlayCanvas != null) overlayCanvas.gameObject.SetActive(false);
        }

        private void SetPrimaryButtonLabel(string label)
        {
            if (primaryButtonLabel != null)
                primaryButtonLabel.text = label;
        }

        private static string FormatSectorName(int sectorIndex)
        {
            if (sectorIndex < 0) return "the nearest sector";
            return $"Sector {sectorIndex + 1}";
        }

        private void ActivateHierarchy()
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

        private void BringToFront()
        {
            if (overlayCanvas != null)
            {
                overlayCanvas.gameObject.SetActive(true);
                overlayCanvas.sortingOrder = 5000;
            }

            if (panel != null)
                panel.transform.SetAsLastSibling();

            if (primaryButton != null)
                primaryButton.transform.SetAsLastSibling();
        }

        private void BindPrimaryButton()
        {
            if (primaryButton == null) return;
            primaryButton.onClick.RemoveListener(OnPrimaryClicked);
            primaryButton.onClick.AddListener(OnPrimaryClicked);
            primaryButton.interactable = true;
        }

        private bool IsPointerOverPrimaryButton()
        {
            if (primaryButton == null) return false;
            var rt = primaryButton.transform as RectTransform;
            if (rt == null) return false;

            Canvas canvas = overlayCanvas != null
                ? overlayCanvas
                : primaryButton.GetComponentInParent<Canvas>();
            Camera eventCam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
            return RectTransformUtility.RectangleContainsScreenPoint(rt, Input.mousePosition, eventCam);
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        private void EnsurePanelBuilt()
        {
            if (panel != null && titleText != null && bodyText != null && primaryButton != null)
            {
                BringToFront();
                return;
            }

            if (overlayCanvas == null)
            {
                var canvasGo = new GameObject("Sector Colonization Overlay Canvas");
                canvasGo.transform.SetParent(transform, false);
                overlayCanvas = canvasGo.AddComponent<Canvas>();
                overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                overlayCanvas.sortingOrder = 5000;
                canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                canvasGo.AddComponent<GraphicRaycaster>();
            }

            if (panel == null)
            {
                panel = new GameObject("Sector Colonization Summary Panel");
                panel.transform.SetParent(overlayCanvas.transform, false);

                var panelRt = panel.AddComponent<RectTransform>();
                panelRt.anchorMin = Vector2.zero;
                panelRt.anchorMax = Vector2.one;
                panelRt.offsetMin = Vector2.zero;
                panelRt.offsetMax = Vector2.zero;

                var dimmer = panel.AddComponent<Image>();
                dimmer.color = new Color(0f, 0f, 0f, 0.72f);
                dimmer.raycastTarget = true;
            }

            if (panel != null && !_showRequested)
                panel.SetActive(false);

            if (titleText == null || bodyText == null || primaryButton == null)
            {
                var card = panel.transform.Find("Card");
                GameObject cardGo;
                if (card == null)
                {
                    cardGo = new GameObject("Card");
                    cardGo.transform.SetParent(panel.transform, false);
                    var cardRt = cardGo.AddComponent<RectTransform>();
                    cardRt.anchorMin = new Vector2(0.5f, 0.5f);
                    cardRt.anchorMax = new Vector2(0.5f, 0.5f);
                    cardRt.pivot = new Vector2(0.5f, 0.5f);
                    cardRt.sizeDelta = new Vector2(760f, 460f);

                    var cardImage = cardGo.AddComponent<Image>();
                    cardImage.color = new Color(0.08f, 0.12f, 0.18f, 0.96f);
                    cardImage.raycastTarget = true;
                }
                else
                {
                    cardGo = card.gameObject;
                }

                if (titleText == null)
                    titleText = CreateText(cardGo.transform, "Title", 34, FontStyles.Bold,
                        new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                        new Vector2(0f, -28f), new Vector2(700f, 48f), TextAlignmentOptions.Center);

                if (bodyText == null)
                    bodyText = CreateText(cardGo.transform, "Body", 24, FontStyles.Normal,
                        new Vector2(0.5f, 0.55f), new Vector2(0.5f, 0.55f), new Vector2(0.5f, 0.5f),
                        new Vector2(0f, 10f), new Vector2(680f, 200f), TextAlignmentOptions.TopLeft);

                if (primaryButton == null)
                {
                    var buttonGo = new GameObject("Primary Button");
                    buttonGo.transform.SetParent(cardGo.transform, false);
                    var buttonRt = buttonGo.AddComponent<RectTransform>();
                    buttonRt.anchorMin = new Vector2(0.5f, 0f);
                    buttonRt.anchorMax = new Vector2(0.5f, 0f);
                    buttonRt.pivot = new Vector2(0.5f, 0f);
                    buttonRt.anchoredPosition = new Vector2(0f, 28f);
                    buttonRt.sizeDelta = new Vector2(360f, 64f);

                    var buttonImage = buttonGo.AddComponent<Image>();
                    buttonImage.color = new Color(0.16f, 0.55f, 0.35f, 1f);
                    buttonImage.raycastTarget = true;
                    primaryButton = buttonGo.AddComponent<Button>();
                    primaryButton.targetGraphic = buttonImage;

                    primaryButtonLabel = CreateText(buttonGo.transform, "Label", 24, FontStyles.Bold,
                        Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                        Vector2.zero, Vector2.zero, TextAlignmentOptions.Center);
                }
            }

            BringToFront();
        }

        private static TextMeshProUGUI CreateText(
            Transform parent,
            string name,
            float fontSize,
            FontStyles fontStyle,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 sizeDelta,
            TextAlignmentOptions alignment)
        {
            var textGo = new GameObject(name);
            textGo.transform.SetParent(parent, false);
            var rt = textGo.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPosition;
            rt.sizeDelta = sizeDelta;
            if (anchorMin == Vector2.zero && anchorMax == Vector2.one)
            {
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }

            var text = textGo.AddComponent<TextMeshProUGUI>();
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.alignment = alignment;
            text.color = Color.white;
            text.textWrappingMode = TextWrappingModes.Normal;
            // Critical: TMP defaults to raycastTarget=true and will steal clicks from the button.
            text.raycastTarget = false;
            return text;
        }
    }
}
