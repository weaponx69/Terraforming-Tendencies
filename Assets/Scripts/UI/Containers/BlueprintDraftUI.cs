using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.Environment;

namespace GameDevTV.RTS.UI.Containers
{
    public class BlueprintDraftUI : MonoBehaviour
    {
        public static BlueprintDraftUI Instance { get; private set; }

        [Header("Draft Setup")]
        [SerializeField] private List<BlueprintCardSO> poolOfCards = new();
        [SerializeField] private GameObject draftPanel;

        private List<BlueprintCardSO> runtimePool = new();
        private List<CardUIElements> cardSlots = new();
        private TextMeshProUGUI roundGoalText;

        private struct CardUIElements
        {
            public GameObject cardObj;
            public TextMeshProUGUI titleText;
            public TextMeshProUGUI goalText;
            public TextMeshProUGUI descText;
            public Image iconImage;
            public Button selectButton;
        }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            // Create default card assets at runtime if none are loaded in Inspector
            InitializeDefaultPool();

            // Self-assemble UI if draftPanel isn't assigned
            if (draftPanel == null)
            {
                AssembleUI();
            }

            if (draftPanel != null)
            {
                draftPanel.SetActive(false);
            }
        }

        private void OnEnable()
        {
            GenerationManager.OnGenerationStarted += OnGenerationStarted;
        }

        private void OnDisable()
        {
            GenerationManager.OnGenerationStarted -= OnGenerationStarted;
        }

        private void OnGenerationStarted(int currentGen, int maxGen)
        {
            // First generation usually starts with standard setup.
            // In Terraformers, drafting occurs at the start of EVERY generation (starting from Gen 1 or 2).
            // Let's trigger it for all rounds to give the player an early strategy boost!
            Debug.Log($"[BlueprintDraftUI] Generation {currentGen} started! Triggering blueprint draft selection.");
            ShowDraftSelection();
        }

        public void ShowDraftSelection()
        {
            if (draftPanel == null) return;

            // Pause the game
            Time.timeScale = 0f;
            draftPanel.SetActive(true);

            if (roundGoalText != null && GenerationManager.Instance != null)
            {
                roundGoalText.text = $"ACTIVE ROUND GOAL: {GenerationManager.Instance.CurrentMilestoneDescription.ToUpper()}";
            }

            // Select 3 random unique cards from the pool
            List<BlueprintCardSO> selectedCards = GetRandomCards(3);

            // Populate slots
            for (int i = 0; i < cardSlots.Count; i++)
            {
                if (i < selectedCards.Count)
                {
                    cardSlots[i].cardObj.SetActive(true);
                    var card = selectedCards[i];
                    
                    cardSlots[i].titleText.text = card.cardName.ToUpper();
                    cardSlots[i].goalText.text = card.GetCardGoal().ToUpper();
                    
                    string desc = card.cardDescription;
                    if (card is UnlockBuildingCardSO unlockCard && unlockCard.buildingToUnlock != null)
                    {
                        var building = unlockCard.buildingToUnlock;
                        var sb = new System.Text.StringBuilder();
                        sb.AppendLine(desc);
                        sb.AppendLine();
                        
                        if (building.Cost != null)
                        {
                            var costs = new List<string>();
                            if (building.Cost.Minerals > 0) costs.Add($"{building.Cost.Minerals} Minerals");
                            if (building.Cost.Gas > 0) costs.Add($"{building.Cost.Gas} Gas");
                            if (costs.Count > 0)
                            {
                                sb.AppendLine($"<color=#FFD700>Cost:</color> {string.Join(", ", costs)}");
                            }
                        }

                        if (building.BuildingConfig != null)
                        {
                            var stats = new List<string>();
                            if (building.BuildingConfig.PowerUpkeep > 0) stats.Add($"-{building.BuildingConfig.PowerUpkeep} Power Upkeep");
                            if (building.BuildingConfig.PowerGeneration > 0) stats.Add($"+{building.BuildingConfig.PowerGeneration} Power Gen");
                            if (building.BuildingConfig.HousingCapacity > 0) stats.Add($"+{building.BuildingConfig.HousingCapacity} Housing");
                            if (building.BuildingConfig.BiomassGeneration > 0) stats.Add($"+{building.BuildingConfig.BiomassGeneration} Biomass Gen");
                            if (stats.Count > 0)
                            {
                                sb.AppendLine($"<color=#ADD8E6>Stats:</color> {string.Join(", ", stats)}");
                            }
                        }

                        if (card is TerraformingCardSO tfCard)
                        {
                            var reqs = new List<string>();
                            if (tfCard.minTemperature > -9999f && tfCard.maxTemperature < 9999f)
                            {
                                reqs.Add($"Temp: {tfCard.minTemperature:F0}°C to {tfCard.maxTemperature:F0}°C");
                            }
                            else if (tfCard.minTemperature > -9999f)
                            {
                                reqs.Add($"Temp: >= {tfCard.minTemperature:F0}°C");
                            }
                            else if (tfCard.maxTemperature < 9999f)
                            {
                                reqs.Add($"Temp: <= {tfCard.maxTemperature:F0}°C");
                            }

                            if (tfCard.minOxygen > -9999f && tfCard.maxOxygen < 9999f)
                            {
                                reqs.Add($"O2: {tfCard.minOxygen:F1}% to {tfCard.maxOxygen:F1}%");
                            }
                            else if (tfCard.minOxygen > -9999f)
                            {
                                reqs.Add($"O2: >= {tfCard.minOxygen:F1}%");
                            }
                            else if (tfCard.maxOxygen < 9999f)
                            {
                                reqs.Add($"O2: <= {tfCard.maxOxygen:F1}%");
                            }

                            if (tfCard.minAtmosphere > -9999f && tfCard.maxAtmosphere < 9999f)
                            {
                                reqs.Add($"Atmos: {tfCard.minAtmosphere:F2} to {tfCard.maxAtmosphere:F2} atm");
                            }
                            else if (tfCard.minAtmosphere > -9999f)
                            {
                                reqs.Add($"Atmos: >= {tfCard.minAtmosphere:F2} atm");
                            }
                            else if (tfCard.maxAtmosphere < 9999f)
                            {
                                reqs.Add($"Atmos: <= {tfCard.maxAtmosphere:F2} atm");
                            }

                            if (tfCard.minWater > -9999f && tfCard.maxWater < 9999f)
                            {
                                reqs.Add($"Water: {tfCard.minWater:F1}% to {tfCard.maxWater:F1}%");
                            }
                            else if (tfCard.minWater > -9999f)
                            {
                                reqs.Add($"Water: >= {tfCard.minWater:F1}%");
                            }
                            else if (tfCard.maxWater < 9999f)
                            {
                                reqs.Add($"Water: <= {tfCard.maxWater:F1}%");
                            }

                            if (tfCard.requiredSectorFeature != SectorManager.SectorFeature.None)
                            {
                                reqs.Add($"Feature: {tfCard.requiredSectorFeature}");
                            }

                            if (reqs.Count > 0)
                            {
                                sb.AppendLine($"<color=#FFA07A>Reqs:</color> {string.Join(", ", reqs)}");
                            }
                        }

                        desc = sb.ToString();
                    }

                    cardSlots[i].descText.text = desc;
                    if (cardSlots[i].iconImage != null && card.icon != null)
                    {
                        cardSlots[i].iconImage.sprite = card.icon;
                        cardSlots[i].iconImage.gameObject.SetActive(true);
                    }
                    else if (cardSlots[i].iconImage != null)
                    {
                        cardSlots[i].iconImage.gameObject.SetActive(false);
                    }

                    // Setup button
                    cardSlots[i].selectButton.onClick.RemoveAllListeners();
                    cardSlots[i].selectButton.onClick.AddListener(() => OnCardSelected(card));
                }
                else
                {
                    cardSlots[i].cardObj.SetActive(false);
                }
            }
        }

        private void OnCardSelected(BlueprintCardSO card)
        {
            Debug.Log($"[BlueprintDraftUI] Player drafted card: {card.cardName}");
            BlueprintDraftManager.CompleteDraft(card);
            
            if (draftPanel != null)
            {
                draftPanel.SetActive(false);
            }
        }

        private List<BlueprintCardSO> GetRandomCards(int count)
        {
            List<BlueprintCardSO> tempPool = new List<BlueprintCardSO>();
            foreach (var card in runtimePool)
            {
                if (card != null && card.IsGateMet())
                {
                    tempPool.Add(card);
                }
            }

            List<BlueprintCardSO> results = new List<BlueprintCardSO>();
            int iterations = Mathf.Min(count, tempPool.Count);
            for (int i = 0; i < iterations; i++)
            {
                int index = Random.Range(0, tempPool.Count);
                results.Add(tempPool[index]);
                tempPool.RemoveAt(index);
            }

            return results;
        }

        private void InitializeDefaultPool()
        {
            runtimePool.Clear();

            // Load all card assets from Resources/Cards/
            var loadedCards = Resources.LoadAll<BlueprintCardSO>("Cards");
            foreach (var card in loadedCards)
            {
                if (card != null && !runtimePool.Contains(card))
                {
                    runtimePool.Add(card);

                    // If it is a UnlockBuildingCardSO, register its building to the BlueprintDraftManager
                    if (card is UnlockBuildingCardSO unlockCard && unlockCard.buildingToUnlock != null)
                    {
                        BlueprintDraftManager.RegisterBuildingSO(unlockCard.buildingToUnlock);
                    }
                }
            }

            // Populate the CardDeckController's masterDeck and rebuild the hand
            if (CardDeckController.Instance != null)
            {
                CardDeckController.Instance.MasterDeck.Clear();
                CardDeckController.Instance.MasterDeck.AddRange(runtimePool);
                Debug.Log($"[BlueprintDraftUI] Populated CardDeckController with {runtimePool.Count} cards. Rebuilding hand...");
                CardDeckController.Instance.RebuildDeck();
            }
        }


        private void AssembleUI()
        {
            // Find the correct Main Canvas (prefer "Runtime UI UGUI" for correct "Scale With Screen Size" behavior)
            Canvas mainCanvas = null;
            Canvas[] allCanvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include);
            foreach (var c in allCanvases)
            {
                if (c.gameObject.name == "Runtime UI UGUI")
                {
                    mainCanvas = c;
                    break;
                }
            }
            if (mainCanvas == null)
            {
                mainCanvas = FindAnyObjectByType<Canvas>();
            }

            if (mainCanvas == null)
            {
                Debug.LogError("[BlueprintDraftUI] Could not find any active Canvas in scene to attach self-assembled UI!");
                return;
            }

            // Create draftPanel root
            draftPanel = new GameObject("Blueprint Draft Overlay", typeof(RectTransform));
            draftPanel.transform.SetParent(mainCanvas.transform, false);

            var panelRt = draftPanel.GetComponent<RectTransform>();
            panelRt.anchorMin = Vector2.zero;
            panelRt.anchorMax = Vector2.one;
            panelRt.offsetMin = Vector2.zero;
            panelRt.offsetMax = Vector2.zero;

            // Make the draft panel a high-priority Canvas to render on top of all other UI/Canvases
            var draftCanvas = draftPanel.AddComponent<Canvas>();
            draftCanvas.overrideSorting = true;
            draftCanvas.sortingOrder = 999;
            draftPanel.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            // Add background overlay image
            var bgImg = draftPanel.AddComponent<UnityEngine.UI.Image>();
            bgImg.color = new Color(0.08f, 0.1f, 0.13f, 1.0f); // Make background fully opaque
            bgImg.raycastTarget = true;

            // Title Text
            GameObject titleGo = new GameObject("Draft Title", typeof(RectTransform), typeof(TextMeshProUGUI));
            titleGo.transform.SetParent(draftPanel.transform, false);
            var titleRt = titleGo.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0.1f, 0.85f);
            titleRt.anchorMax = new Vector2(0.9f, 0.95f);
            titleRt.offsetMin = Vector2.zero;
            titleRt.offsetMax = Vector2.zero;

            var titleTmp = titleGo.GetComponent<TextMeshProUGUI>();
            titleTmp.text = "NEW GENERATION: SELECT BLUEPRINT";
            titleTmp.font = Resources.Load<TMP_FontAsset>("Fonts & Materials/Dogfish SDF");
            if (titleTmp.font == null) titleTmp.font = Resources.FindObjectsOfTypeAll<TMP_FontAsset>()[0];
            titleTmp.fontSize = 36f; // Larger title text
            titleTmp.alignment = TextAlignmentOptions.Center;
            titleTmp.color = new Color(0.2f, 0.8f, 1f, 1f); // Electrifying cyan

            // Subtitle / Round Goal Text
            GameObject subtitleGo = new GameObject("Draft Subtitle", typeof(RectTransform), typeof(TextMeshProUGUI));
            subtitleGo.transform.SetParent(draftPanel.transform, false);
            var subtitleRt = subtitleGo.GetComponent<RectTransform>();
            subtitleRt.anchorMin = new Vector2(0.1f, 0.76f);
            subtitleRt.anchorMax = new Vector2(0.9f, 0.84f);
            subtitleRt.offsetMin = Vector2.zero;
            subtitleRt.offsetMax = Vector2.zero;

            roundGoalText = subtitleGo.GetComponent<TextMeshProUGUI>();
            roundGoalText.font = titleTmp.font;
            roundGoalText.fontSize = 22f; // Subtitle text size
            roundGoalText.alignment = TextAlignmentOptions.Center;
            roundGoalText.color = new Color(1f, 0.85f, 0.2f, 1f); // Vibrant Gold
            roundGoalText.text = "";

            // Horizontal layout for cards (shifted down to fit subtitle)
            GameObject cardContainer = new GameObject("Cards Container", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            cardContainer.transform.SetParent(draftPanel.transform, false);
            var ccRt = cardContainer.GetComponent<RectTransform>();
            ccRt.anchorMin = new Vector2(0.05f, 0.20f);
            ccRt.anchorMax = new Vector2(0.95f, 0.75f);
            ccRt.offsetMin = Vector2.zero;
            ccRt.offsetMax = Vector2.zero;

            var hlg = cardContainer.GetComponent<HorizontalLayoutGroup>();
            hlg.spacing = 40f;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlHeight = true;
            hlg.childControlWidth = true;
            hlg.childForceExpandHeight = true;
            hlg.childForceExpandWidth = false; // Prevent auto-stretching card width dynamically based on screen size or count

            // Create 3 Cards
            cardSlots.Clear();
            for (int i = 0; i < 4; i++)
            {
                GameObject cardObj = new GameObject($"Card Slot ({i})", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(LayoutElement));
                cardObj.transform.SetParent(cardContainer.transform, false);

                var cardImg = cardObj.GetComponent<Image>();
                cardImg.color = new Color(0f, 0f, 0f, 1f); // Completely black, fully opaque background

                // Enforce STRICTLY CONSTANT identical card width via LayoutElement
                var le = cardObj.GetComponent<LayoutElement>();
                le.minWidth = 320f;
                le.preferredWidth = 320f;

                var vlg = cardObj.GetComponent<VerticalLayoutGroup>();
                vlg.padding = new RectOffset(20, 20, 20, 20);
                vlg.spacing = 15f;
                vlg.childAlignment = TextAnchor.UpperCenter;
                vlg.childControlHeight = false;
                vlg.childControlWidth = true;
                vlg.childForceExpandHeight = false;
                vlg.childForceExpandWidth = true;

                // Card Title
                GameObject cTitleGo = new GameObject("Card Title", typeof(RectTransform), typeof(TextMeshProUGUI));
                cTitleGo.transform.SetParent(cardObj.transform, false);
                var cTitleRt = cTitleGo.GetComponent<RectTransform>();
                cTitleRt.sizeDelta = new Vector2(280f, 35f);
                var cTitleTmp = cTitleGo.GetComponent<TextMeshProUGUI>();
                cTitleTmp.text = "BLUEPRINT CARD";
                cTitleTmp.fontSize = 26f; // Clean constant title size (permanently increased)
                cTitleTmp.alignment = TextAlignmentOptions.Center;
                cTitleTmp.color = new Color(1f, 0.85f, 0.2f, 1f); // Vibrant Gold
                cTitleTmp.font = titleTmp.font;
                cTitleTmp.textWrappingMode = TextWrappingModes.Normal;

                // Card Goal
                GameObject cGoalGo = new GameObject("Card Goal", typeof(RectTransform), typeof(TextMeshProUGUI));
                cGoalGo.transform.SetParent(cardObj.transform, false);
                var cGoalRt = cGoalGo.GetComponent<RectTransform>();
                cGoalRt.sizeDelta = new Vector2(280f, 20f);
                var cGoalTmp = cGoalGo.GetComponent<TextMeshProUGUI>();
                cGoalTmp.text = "GOAL";
                cGoalTmp.fontSize = 15f; // Small constant sub-header size (permanently increased)
                cGoalTmp.alignment = TextAlignmentOptions.Center;
                cGoalTmp.color = new Color(0.4f, 0.8f, 1f, 1f); // Highly readable cyan-blue
                cGoalTmp.font = titleTmp.font;
                cGoalTmp.textWrappingMode = TextWrappingModes.Normal;

                // Divider Line
                GameObject cDivGo = new GameObject("Divider", typeof(RectTransform), typeof(Image));
                cDivGo.transform.SetParent(cardObj.transform, false);
                var cDivRt = cDivGo.GetComponent<RectTransform>();
                cDivRt.sizeDelta = new Vector2(280f, 2f);
                cDivGo.GetComponent<Image>().color = new Color(0.3f, 0.4f, 0.5f, 1.0f); // Fully opaque divider

                // Card Description (No ContentSizeFitter to prevent dynamic card size changing)
                GameObject cDescGo = new GameObject("Card Description", typeof(RectTransform), typeof(TextMeshProUGUI));
                cDescGo.transform.SetParent(cardObj.transform, false);
                var cDescRt = cDescGo.GetComponent<RectTransform>();
                cDescRt.sizeDelta = new Vector2(280f, 230f); // STRICTLY CONSTANT fixed height text box with extra height for larger font
                var cDescTmp = cDescGo.GetComponent<TextMeshProUGUI>();
                cDescTmp.text = "This is the detailed description of the card's action or unlocked blueprint.";
                cDescTmp.fontSize = 18f; // STRICTLY CONSTANT description text size (permanently increased)
                cDescTmp.alignment = TextAlignmentOptions.TopLeft;
                cDescTmp.color = Color.white;
                cDescTmp.textWrappingMode = TextWrappingModes.Normal;

                // Spacing block before button
                GameObject spacer = new GameObject("Spacer", typeof(RectTransform));
                spacer.transform.SetParent(cardObj.transform, false);
                var spacerRt = spacer.GetComponent<RectTransform>();
                spacerRt.sizeDelta = new Vector2(100f, 15f); // Constant spacing height

                // Select Button
                GameObject cBtnGo = new GameObject("Select Button", typeof(RectTransform), typeof(Image), typeof(Button));
                cBtnGo.transform.SetParent(cardObj.transform, false);
                var cBtnRt = cBtnGo.GetComponent<RectTransform>();
                cBtnRt.sizeDelta = new Vector2(240f, 40f);

                var btnImg = cBtnGo.GetComponent<Image>();
                btnImg.color = new Color(0.2f, 0.7f, 0.3f, 1f); // Sci-Fi Green

                var cBtn = cBtnGo.GetComponent<Button>();
                
                // Button Text
                GameObject btnTxtGo = new GameObject("Button Label", typeof(RectTransform), typeof(TextMeshProUGUI));
                btnTxtGo.transform.SetParent(cBtnGo.transform, false);
                var btnTxtRt = btnTxtGo.GetComponent<RectTransform>();
                btnTxtRt.anchorMin = Vector2.zero;
                btnTxtRt.anchorMax = Vector2.one;
                btnTxtRt.offsetMin = Vector2.zero;
                btnTxtRt.offsetMax = Vector2.zero;

                var btnTxtTmp = btnTxtGo.GetComponent<TextMeshProUGUI>();
                btnTxtTmp.text = "CHOOSE BLUEPRINT";
                btnTxtTmp.fontSize = 20f; // Larger button text (permanently increased)
                btnTxtTmp.alignment = TextAlignmentOptions.Center;
                btnTxtTmp.color = Color.white;
                btnTxtTmp.font = titleTmp.font;

                cardSlots.Add(new CardUIElements
                {
                    cardObj = cardObj,
                    titleText = cTitleTmp,
                    goalText = cGoalTmp,
                    descText = cDescTmp,
                    iconImage = null,
                    selectButton = cBtn
                });
            }

            Debug.Log("[BlueprintDraftUI] Successfully assembled fully functional and dynamic Card Overlay!");
        }
    }
}