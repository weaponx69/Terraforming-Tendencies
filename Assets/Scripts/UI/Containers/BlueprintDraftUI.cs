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

        private struct CardUIElements
        {
            public GameObject cardObj;
            public TextMeshProUGUI titleText;
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
            runtimePool.AddRange(poolOfCards);

            // 1. Solar Panel Blueprint
            var cardSolar = ScriptableObject.CreateInstance<UnlockBuildingCardSO>();
            cardSolar.cardName = "Solar Array Project";
            cardSolar.cardDescription = "Unlocks the ability to construct Solar Panels to generate massive clean grid Power.";
            cardSolar.buildingToUnlock = Resources.Load<BuildingSO>("Buildings/SolarPanel/SolarPanel");
#if UNITY_EDITOR
            if (cardSolar.buildingToUnlock == null) cardSolar.buildingToUnlock = UnityEditor.AssetDatabase.LoadAssetAtPath<BuildingSO>("Assets/Resources/Buildings/SolarPanel/SolarPanel.asset");
#endif
            runtimePool.Add(cardSolar);

            // 2. Oxygen Processor Blueprint
            var cardOxygen = ScriptableObject.CreateInstance<UnlockBuildingCardSO>();
            cardOxygen.cardName = "Atmosphere Processor";
            cardOxygen.cardDescription = "Unlocks the Oxygen Processor to extract carbon dioxide and enrich colony atmosphere.";
            cardOxygen.buildingToUnlock = Resources.Load<BuildingSO>("Buildings/Oxygen Processor/Oxygen Processor");
#if UNITY_EDITOR
            if (cardOxygen.buildingToUnlock == null) cardOxygen.buildingToUnlock = UnityEditor.AssetDatabase.LoadAssetAtPath<BuildingSO>("Assets/Resources/Buildings/Oxygen Processor/Oxygen Processor.asset");
#endif
            runtimePool.Add(cardOxygen);

            // 3. Colonist Habitat Blueprint
            var cardHabitat = ScriptableObject.CreateInstance<UnlockBuildingCardSO>();
            cardHabitat.cardName = "Modular Habitat Dome";
            cardHabitat.cardDescription = "Unlocks the Colonist Habitat building, increasing your maximum colony housing capacity.";
            cardHabitat.buildingToUnlock = Resources.Load<BuildingSO>("Buildings/Habitat/Habitat");
#if UNITY_EDITOR
            if (cardHabitat.buildingToUnlock == null) cardHabitat.buildingToUnlock = UnityEditor.AssetDatabase.LoadAssetAtPath<BuildingSO>("Assets/Resources/Buildings/Habitat/Habitat.asset");
#endif
            runtimePool.Add(cardHabitat);

            if (cardSolar.buildingToUnlock != null) BlueprintDraftManager.RegisterBuildingSO(cardSolar.buildingToUnlock);
            if (cardOxygen.buildingToUnlock != null) BlueprintDraftManager.RegisterBuildingSO(cardOxygen.buildingToUnlock);
            if (cardHabitat.buildingToUnlock != null) BlueprintDraftManager.RegisterBuildingSO(cardHabitat.buildingToUnlock);

            BuildingSO templateBuilding = cardSolar.buildingToUnlock;
            Sprite defaultIcon = templateBuilding != null ? templateBuilding.Icon : null;

            // 4. Heavy Materials Drop
            var cardMats = ScriptableObject.CreateInstance<ResourceShipmentCardSO>();
            cardMats.cardName = "Heavy Alloys Shipment";
            cardMats.cardDescription = "Receive an immediate cargo supply shipment of +400 Materials for base construction.";
            cardMats.materialsAmount = 400;
            runtimePool.Add(cardMats);

            // 5. Bio-Matter Drop
            var cardBio = ScriptableObject.CreateInstance<ResourceShipmentCardSO>();
            cardBio.cardName = "Bio-Dome Culture Serum";
            cardBio.cardDescription = "Deploy advanced fertilizer cultures to instantly receive +150 Biomass.";
            cardBio.biomassAmount = 150;
            runtimePool.Add(cardBio);

            // 6. Drone Assembly
            var cardDrone = ScriptableObject.CreateInstance<SpawnUnitCardSO>();
            cardDrone.cardName = "Mining Drone";
            cardDrone.cardDescription = "Fabricate and deploy an additional fully functioning Mining Drone immediately at your command center.";
#if UNITY_EDITOR
            cardDrone.unitPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Units/Mining Drone/Mining Drone.prefab");
#endif
            runtimePool.Add(cardDrone);

            // 7. Repair Drone
            var cardRepair = ScriptableObject.CreateInstance<SpawnUnitCardSO>();
            cardRepair.cardName = "Automated Repair Crawler";
            cardRepair.cardDescription = "Deploy a specialized Repair Drone to automatically rebuild pipelines and repair bases.";
#if UNITY_EDITOR
            cardRepair.unitPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Units/Repair Drone/Repair Drone.prefab");
#endif
            runtimePool.Add(cardRepair);

            // 8. Gather Speed Buff
            var cardSpeed = ScriptableObject.CreateInstance<PassiveBuffCardSO>();
            cardSpeed.cardName = "High-Power Induction Drills";
            cardSpeed.cardDescription = "Upgrade mining tools. All mining droids gather minerals and deposits +30% faster permanently.";
            cardSpeed.buffType = PassiveBuffCardSO.BuffType.GatherSpeed;
            cardSpeed.multiplier = 1.3f;
            runtimePool.Add(cardSpeed);

            // 9. Power Gen Buff
            var cardPower = ScriptableObject.CreateInstance<PassiveBuffCardSO>();
            cardPower.cardName = "Photovoltaic Tuning Upgrades";
            cardPower.cardDescription = "Install resonance tuners onto solar collectors. All Solar Panels generate +20% grid Power permanently.";
            cardPower.buffType = PassiveBuffCardSO.BuffType.PowerGeneration;
            cardPower.multiplier = 1.20f;
            runtimePool.Add(cardPower);

            // Utility & Mining Deck
            AddThemedBuildingCard("Basalt Strip-Mine", "Unlocks the Basalt Strip-Mine building, providing solid planetary foundations.", "Basalt Strip-Mine", 120, 2f, 0f, 0, 0, float.MinValue, float.MaxValue, float.MinValue, float.MaxValue, float.MinValue, float.MaxValue, SectorManager.SectorFeature.None, templateBuilding, defaultIcon);
            AddThemedBuildingCard("Deep-Core Mining Laser", "Unlocks active fire mining laser. REQUIRES Temperature >= -40C.", "Deep-Core Mining Laser", 200, 5f, 0f, 0, 0, -40f, float.MaxValue, float.MinValue, float.MaxValue, float.MinValue, float.MaxValue, SectorManager.SectorFeature.None, templateBuilding, defaultIcon);
            AddThemedBuildingCard("Water Ice Aquifer", "Extracts subterranean ice reservoirs. REQUIRES Temperature >= -20C.", "Water Ice Aquifer", 150, 3f, 0f, 0, 5, -20f, float.MaxValue, float.MinValue, float.MaxValue, float.MinValue, float.MaxValue, SectorManager.SectorFeature.None, templateBuilding, defaultIcon);
            AddThemedBuildingCard("Geothermal Generator", "Converts thermal vents into clean energy. REQUIRES Temperature >= -10C.", "Geothermal Generator", 250, 0f, 15f, 0, 0, -10f, float.MaxValue, float.MinValue, float.MaxValue, float.MinValue, float.MaxValue, SectorManager.SectorFeature.None, templateBuilding, defaultIcon);
            AddThemedBuildingCard("Lava Tube Outpost", "Establishes a shelter inside a protective lava tube feature.", "Lava Tube Outpost", 180, 2f, 0f, 12, 0, float.MinValue, float.MaxValue, float.MinValue, float.MaxValue, float.MinValue, float.MaxValue, SectorManager.SectorFeature.LavaTube, templateBuilding, defaultIcon);

            // Urban & Residential Deck
            AddThemedBuildingCard("Inflatable Bio-Dome", "Creates modular colonist housing. REQUIRES Atmosphere >= 0.05 atm.", "Inflatable Bio-Dome", 100, 1f, 0f, 10, 0, float.MinValue, float.MaxValue, float.MinValue, float.MaxValue, 0.05f, float.MaxValue, SectorManager.SectorFeature.None, templateBuilding, defaultIcon);
            AddThemedBuildingCard("Urban Green Commons", "Fosters colonist happiness and health. REQUIRES Atmosphere >= 0.15 atm.", "Urban Green Commons", 150, 2f, 0f, 15, 0, float.MinValue, float.MaxValue, float.MinValue, float.MaxValue, 0.15f, float.MaxValue, SectorManager.SectorFeature.None, templateBuilding, defaultIcon);
            AddThemedBuildingCard("Solar Greenhouse", "Integrates vegetation modules into habitats. REQUIRES Atmosphere >= 0.20 atm.", "Solar Greenhouse", 140, 2f, 0f, 0, 2, float.MinValue, float.MaxValue, float.MinValue, float.MaxValue, 0.20f, float.MaxValue, SectorManager.SectorFeature.None, templateBuilding, defaultIcon);
            AddThemedBuildingCard("Subterranean Apartment Block", "Deep housing inside a lava tube, shielded from cosmic radiation.", "Subterranean Apartment Block", 300, 4f, 0f, 30, 0, float.MinValue, float.MaxValue, float.MinValue, float.MaxValue, float.MinValue, float.MaxValue, SectorManager.SectorFeature.LavaTube, templateBuilding, defaultIcon);
            AddThemedBuildingCard("Sector Command Center", "Coordinates regional supply lines from a fault line feature.", "Sector Command Center", 400, 5f, 0f, 20, 0, float.MinValue, float.MaxValue, float.MinValue, float.MaxValue, float.MinValue, float.MaxValue, SectorManager.SectorFeature.FaultLine, templateBuilding, defaultIcon);

            // Science & Terraforming Deck
            AddThemedBuildingCard("GHG Factory", "Vaporizes chemicals to heat the planet. Generates heavy greenhouse gases.", "GHG Factory", 150, 4f, 0f, 0, 0, float.MinValue, float.MaxValue, float.MinValue, float.MaxValue, float.MinValue, float.MaxValue, SectorManager.SectorFeature.None, templateBuilding, defaultIcon);
            AddThemedBuildingCard("Atmospheric Condenser", "Extracts gases from thin air. REQUIRES Atmosphere >= 0.05 atm.", "Atmospheric Condenser", 180, 3f, 0f, 0, 0, float.MinValue, float.MaxValue, float.MinValue, float.MaxValue, 0.05f, float.MaxValue, SectorManager.SectorFeature.None, templateBuilding, defaultIcon);
            AddThemedBuildingCard("Carbon Dioxide Import Laser", "Attracts cometary ice to enrich atmosphere. REQUIRES Atmosphere >= 0.10 atm.", "Carbon Dioxide Import Laser", 250, 6f, 0f, 0, 0, float.MinValue, float.MaxValue, float.MinValue, float.MaxValue, 0.10f, float.MaxValue, SectorManager.SectorFeature.None, templateBuilding, defaultIcon);
            AddThemedBuildingCard("Subglacial Water Extractor", "Drills deep into subglacial water deposits to pump biomass media.", "Subglacial Water Extractor", 220, 4f, 0f, 0, 4, float.MinValue, float.MaxValue, float.MinValue, float.MaxValue, float.MinValue, float.MaxValue, SectorManager.SectorFeature.WaterDeposit, templateBuilding, defaultIcon);
            AddThemedBuildingCard("Magnetic Shield Generator", "Protects regional grids from solar wind from an elevated fault line.", "Magnetic Shield Generator", 350, 0f, 25f, 0, 0, float.MinValue, float.MaxValue, float.MinValue, float.MaxValue, float.MinValue, float.MaxValue, SectorManager.SectorFeature.FaultLine, templateBuilding, defaultIcon);

            // Ecological & Biosphere Deck
            AddThemedBuildingCard("Methanogenic Microbe Spreader", "Spreads methane-producing microbes. REQUIRES Temp >= -30C and Atmos >= 0.05 atm.", "Methanogenic Microbe Spreader", 130, 2f, 0f, 0, 0, -30f, float.MaxValue, float.MinValue, float.MaxValue, 0.05f, float.MaxValue, SectorManager.SectorFeature.None, templateBuilding, defaultIcon);
            AddThemedBuildingCard("Lichen Nursery", "Cultivates rock-decomposing lichens. REQUIRES Temp >= -25C and Atmos >= 0.10 atm.", "Lichen Nursery", 140, 2f, 0f, 0, 3, -25f, float.MaxValue, float.MinValue, float.MaxValue, 0.10f, float.MaxValue, SectorManager.SectorFeature.None, templateBuilding, defaultIcon);
            AddThemedBuildingCard("Genetically Modified Algae Spreader", "Sows oxygen-producing algae pools. REQUIRES Temp >= -15C, Atmos >= 0.15 atm, Oxy >= 1.0%.", "Genetically Modified Algae Spreader", 210, 3f, 0f, 0, 0, -15f, float.MaxValue, 1.0f, float.MaxValue, 0.15f, float.MaxValue, SectorManager.SectorFeature.None, templateBuilding, defaultIcon);
            AddThemedBuildingCard("Greenery Dome", "Advanced glass canopy housing local flora. REQUIRES Temp >= -10C, Atmos >= 0.20 atm, Oxy >= 2.0%.", "Greenery Dome", 280, 4f, 0f, 0, 6, -10f, float.MaxValue, 2.0f, float.MaxValue, 0.20f, float.MaxValue, SectorManager.SectorFeature.None, templateBuilding, defaultIcon);
            AddThemedBuildingCard("Biosphere Center", "Coordinates global ecological cycles from a protected water deposit.", "Biosphere Center", 500, 6f, 0f, 0, 10, float.MinValue, float.MaxValue, float.MinValue, float.MaxValue, float.MinValue, float.MaxValue, SectorManager.SectorFeature.WaterDeposit, templateBuilding, defaultIcon);
        }

        private void AddThemedBuildingCard(
            string cardName,
            string cardDesc,
            string buildingName,
            int materialsCost,
            float powerUpkeep,
            float powerGen,
            int housingCap,
            int biomassGen,
            float minTemp,
            float maxTemp,
            float minOxy,
            float maxOxy,
            float minAtmos,
            float maxAtmos,
            SectorManager.SectorFeature requiredFeature,
            BuildingSO templateBuilding,
            Sprite defaultIcon
        )
        {
            var bldSO = ScriptableObject.CreateInstance<BuildingSO>();
            bldSO.Name = buildingName;
            bldSO.BuildTime = 10f;
            bldSO.Icon = defaultIcon;

            var cost = ScriptableObject.CreateInstance<SupplyCostSO>();
            cost.Minerals = materialsCost;
            if (templateBuilding != null && templateBuilding.Cost != null)
            {
                cost.MineralsSO = templateBuilding.Cost.MineralsSO;
                cost.GasSO = templateBuilding.Cost.GasSO;
            }
            bldSO.Cost = cost;

            if (templateBuilding != null)
            {
                bldSO.Prefab = templateBuilding.Prefab;
                bldSO.PlacementMaterial = templateBuilding.PlacementMaterial;
                bldSO.SightConfig = templateBuilding.SightConfig;
            }

            var config = ScriptableObject.CreateInstance<BuildingConfigSO>();
            config.PowerUpkeep = powerUpkeep;
            config.PowerGeneration = powerGen;
            config.HousingCapacity = housingCap;
            config.BiomassGeneration = biomassGen;
            bldSO.BuildingConfig = config;

            var card = ScriptableObject.CreateInstance<TerraformingCardSO>();
            card.cardName = cardName;
            card.cardDescription = cardDesc;
            card.icon = defaultIcon;
            card.buildingToUnlock = bldSO;

            card.minTemperature = minTemp;
            card.maxTemperature = maxTemp;
            card.minOxygen = minOxy;
            card.maxOxygen = maxOxy;
            card.minAtmosphere = minAtmos;
            card.maxAtmosphere = maxAtmos;
            card.requiredSectorFeature = requiredFeature;

            runtimePool.Add(card);

            BlueprintDraftManager.RegisterBuildingSO(bldSO);
        }

        private void AssembleUI()
        {
            // Find Main Canvas
            Canvas mainCanvas = FindAnyObjectByType<Canvas>();
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

            // Add background overlay image
            var bgImg = draftPanel.AddComponent<Image>();
            bgImg.color = new Color(0.08f, 0.1f, 0.13f, 0.95f); // Deep dark space/slate color

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
            titleTmp.fontSize = 32f;
            titleTmp.alignment = TextAlignmentOptions.Center;
            titleTmp.color = new Color(0.2f, 0.8f, 1f, 1f); // Electrifying cyan

            // Horizontal layout for cards
            GameObject cardContainer = new GameObject("Cards Container", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            cardContainer.transform.SetParent(draftPanel.transform, false);
            var ccRt = cardContainer.GetComponent<RectTransform>();
            ccRt.anchorMin = new Vector2(0.05f, 0.28f);
            ccRt.anchorMax = new Vector2(0.95f, 0.83f);
            ccRt.offsetMin = Vector2.zero;
            ccRt.offsetMax = Vector2.zero;

            var hlg = cardContainer.GetComponent<HorizontalLayoutGroup>();
            hlg.spacing = 40f;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlHeight = true;
            hlg.childControlWidth = true;
            hlg.childForceExpandHeight = true;
            hlg.childForceExpandWidth = true;

            // Create 3 Cards
            cardSlots.Clear();
            for (int i = 0; i < 3; i++)
            {
                GameObject cardObj = new GameObject($"Card Slot ({i})", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
                cardObj.transform.SetParent(cardContainer.transform, false);

                var cardImg = cardObj.GetComponent<Image>();
                cardImg.color = new Color(0.12f, 0.15f, 0.18f, 1f); // Slate grey

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
                var cTitleTmp = cTitleGo.GetComponent<TextMeshProUGUI>();
                cTitleTmp.text = "BLUEPRINT CARD";
                cTitleTmp.fontSize = 20f;
                cTitleTmp.alignment = TextAlignmentOptions.Center;
                cTitleTmp.color = new Color(1f, 0.85f, 0.2f, 1f); // Vibrant Gold
                cTitleTmp.font = titleTmp.font;

                // Divider Line
                GameObject cDivGo = new GameObject("Divider", typeof(RectTransform), typeof(Image));
                cDivGo.transform.SetParent(cardObj.transform, false);
                var cDivRt = cDivGo.GetComponent<RectTransform>();
                cDivRt.sizeDelta = new Vector2(250f, 2f);
                cDivGo.GetComponent<Image>().color = new Color(0.3f, 0.4f, 0.5f, 0.5f);

                // Card Description
                GameObject cDescGo = new GameObject("Card Description", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(ContentSizeFitter));
                cDescGo.transform.SetParent(cardObj.transform, false);
                var cDescRt = cDescGo.GetComponent<RectTransform>();
                cDescRt.sizeDelta = new Vector2(260f, 180f);
                var cDescTmp = cDescGo.GetComponent<TextMeshProUGUI>();
                cDescTmp.text = "This is the detailed description of the card's action or unlocked blueprint.";
                cDescTmp.fontSize = 13f;
                cDescTmp.alignment = TextAlignmentOptions.TopLeft;
                cDescTmp.color = Color.white;
                cDescTmp.textWrappingMode = TextWrappingModes.Normal;

                var fitter = cDescGo.GetComponent<ContentSizeFitter>();
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

                // Spacing block before button
                GameObject spacer = new GameObject("Spacer", typeof(RectTransform));
                spacer.transform.SetParent(cardObj.transform, false);
                var spacerRt = spacer.GetComponent<RectTransform>();
                spacerRt.sizeDelta = new Vector2(100f, 40f);

                // Select Button
                GameObject cBtnGo = new GameObject("Select Button", typeof(RectTransform), typeof(Image), typeof(Button));
                cBtnGo.transform.SetParent(cardObj.transform, false);
                var cBtnRt = cBtnGo.GetComponent<RectTransform>();
                cBtnRt.sizeDelta = new Vector2(220f, 40f);

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
                btnTxtTmp.fontSize = 14f;
                btnTxtTmp.alignment = TextAlignmentOptions.Center;
                btnTxtTmp.color = Color.white;
                btnTxtTmp.font = titleTmp.font;

                cardSlots.Add(new CardUIElements
                {
                    cardObj = cardObj,
                    titleText = cTitleTmp,
                    descText = cDescTmp,
                    iconImage = null,
                    selectButton = cBtn
                });
            }

            Debug.Log("[BlueprintDraftUI] Successfully assembled fully functional and dynamic Card Overlay!");
        }
    }
}