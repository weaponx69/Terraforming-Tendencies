using System.Collections.Generic;
using System.Linq;
using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Events;
using GameDevTV.RTS.Environment;
using GameDevTV.RTS.UI.Containers;
using GameDevTV.RTS.Units;
using UnityEngine;
using TMPro;
using GameDevTV.RTS.Player;
using UnityEngine.UI;

namespace GameDevTV.RTS.UI
{
    [ExecuteAlways]
    public class RuntimeUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI materialsLabelText;
        [SerializeField] private TextMeshProUGUI materialsValueText;

        // Oxygen & Sector UI
        [SerializeField] private TextMeshProUGUI oxygenLabelText;
        [SerializeField] private TextMeshProUGUI oxygenValueText;
        [SerializeField] private TextMeshProUGUI sectorsLabelText;
        [SerializeField] private TextMeshProUGUI sectorsValueText;
        [SerializeField] private TextMeshProUGUI integrityLabelText;
        [SerializeField] private TextMeshProUGUI integrityValueText;
        [SerializeField] private TextMeshProUGUI populationText;

        [SerializeField] private ActionsUI actionsUI;
        [SerializeField] private BuildingSelectedUI buildingSelectedUI;
        [SerializeField] private UnitIconUI unitIconUI;
        [SerializeField] private SingleUnitSelectedUI singleUnitSelectedUI;
        [SerializeField] private UnitTransportUI unitTransportUI;
        [SerializeField] private GlobalCommanderUI globalCommanderUI;
        [SerializeField] private AbilityHandUI abilityHandUI;
        [SerializeField] private BottomBarActionsUI bottomBarActionsUI;

        [SerializeField] private Image iconImage;

        [SerializeField] private AbstractCommandable globalCommander;

        private HashSet<AbstractCommandable> selectedUnits = new(12);
        private Owner displayedOwner = Owner.Player1;

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            displayedOwner = GameOverManager.MonitoredOwner;

            Bus<UnitSelectedEvent>.OnEvent[displayedOwner] += HandleUnitSelected;
            Bus<UnitDeselectedEvent>.OnEvent[displayedOwner] += HandleUnitDeselected;
            Bus<UnitDeathEvent>.OnEvent[displayedOwner] += HandleUnitDeath;
            Bus<SupplyEvent>.OnEvent[displayedOwner] += HandleSupplyChange;
            Bus<UnitLoadEvent>.OnEvent[displayedOwner] += HandleLoadUnit;
            Bus<UnitUnloadEvent>.OnEvent[displayedOwner] += HandleUnloadUnit;
            Bus<BuildingSpawnEvent>.OnEvent[displayedOwner] += HandleBuildingSpawn;
            Bus<UpgradeResearchedEvent>.OnEvent[displayedOwner] += HandleUpgradeResearched;
            Bus<BuildingDeathEvent>.OnEvent[displayedOwner] += HandleBuildingDeath;

            Supplies.OnOxygenChanged += HandleOxygenChanged;
            Supplies.OnMaterialsChanged += HandleMaterialsChanged;
            Supplies.OnIntegrityChanged += HandleIntegrityChanged;
            Supplies.OnBiomassChanged += HandleBiomassChanged;
            Supplies.OnFoodChanged += HandleFoodChanged;
            Supplies.OnPowerChanged += HandlePowerChanged;
            Supplies.OnPopulationChanged += HandlePopulationChanged;
            Supplies.OnPopulationLimitChanged += HandlePopulationLimitChanged;
            Supplies.OnTemperatureChanged += HandleTemperatureChanged;
            Supplies.OnAtmosphereChanged += HandleAtmosphereChanged;
            Supplies.OnWaterChanged += HandleWaterChanged;

            InitializeUI();
        }

        private void OnDisable()
        {
            if (!Application.isPlaying) return;
            Bus<UnitSelectedEvent>.OnEvent[displayedOwner] -= HandleUnitSelected;
            Bus<UnitDeselectedEvent>.OnEvent[displayedOwner] -= HandleUnitDeselected;
            Bus<UnitDeathEvent>.OnEvent[displayedOwner] -= HandleUnitDeath;
            Bus<SupplyEvent>.OnEvent[displayedOwner] -= HandleSupplyChange;
            Bus<UnitLoadEvent>.OnEvent[displayedOwner] -= HandleLoadUnit;
            Bus<UnitUnloadEvent>.OnEvent[displayedOwner] -= HandleUnloadUnit;
            Bus<BuildingSpawnEvent>.OnEvent[displayedOwner] -= HandleBuildingSpawn;
            Bus<UpgradeResearchedEvent>.OnEvent[displayedOwner] -= HandleUpgradeResearched;
            Bus<BuildingDeathEvent>.OnEvent[displayedOwner] -= HandleBuildingDeath;

            Supplies.OnOxygenChanged -= HandleOxygenChanged;
            Supplies.OnMaterialsChanged -= HandleMaterialsChanged;
            Supplies.OnIntegrityChanged -= HandleIntegrityChanged;
            Supplies.OnBiomassChanged -= HandleBiomassChanged;
            Supplies.OnFoodChanged -= HandleFoodChanged;
            Supplies.OnPowerChanged -= HandlePowerChanged;
            Supplies.OnPopulationChanged -= HandlePopulationChanged;
            Supplies.OnPopulationLimitChanged -= HandlePopulationLimitChanged;
            Supplies.OnTemperatureChanged -= HandleTemperatureChanged;
            Supplies.OnAtmosphereChanged -= HandleAtmosphereChanged;
            Supplies.OnWaterChanged -= HandleWaterChanged;

            // Reset UI values when the component is disabled (e.g., game end)
            ResetUI();
        }

        [SerializeField] private TextMeshProUGUI biomassLabelText;
        [SerializeField] private TextMeshProUGUI biomassValueText;
        [SerializeField] private TextMeshProUGUI powerLabelText;
        [SerializeField] private TextMeshProUGUI powerValueText;
        
        [SerializeField] private TextMeshProUGUI temperatureLabelText;
        [SerializeField] private TextMeshProUGUI temperatureValueText;
        [SerializeField] private TextMeshProUGUI atmosphereLabelText;
        [SerializeField] private TextMeshProUGUI atmosphereValueText;
        [SerializeField] private TextMeshProUGUI waterLabelText;
        [SerializeField] private TextMeshProUGUI waterValueText;
        
        [Header("Warning UI")]
        [SerializeField] private GameObject warningBanner;
        [SerializeField] private TextMeshProUGUI warningText;

        private Transform FindChildRecursive(Transform parent, string name)
        {
            if (parent == null) return null;
            if (parent.name == name) return parent;
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform found = FindChildRecursive(parent.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }

        private void ResolveTextsFromClone(GameObject clone, GameObject template, TextMeshProUGUI templateLabel, TextMeshProUGUI templateValue, out TextMeshProUGUI cloneLabel, out TextMeshProUGUI cloneValue, string fallbackLabelName, string fallbackValueName)
        {
            cloneLabel = null;
            cloneValue = null;

            if (clone == null || template == null) return;

            var templateTexts = template.GetComponentsInChildren<TextMeshProUGUI>(true);
            var cloneTexts = clone.GetComponentsInChildren<TextMeshProUGUI>(true);

            // 1. Try structural index match
            int labelIdx = System.Array.IndexOf(templateTexts, templateLabel);
            int valueIdx = System.Array.IndexOf(templateTexts, templateValue);

            if (labelIdx >= 0 && labelIdx < cloneTexts.Length)
            {
                cloneLabel = cloneTexts[labelIdx];
            }
            if (valueIdx >= 0 && valueIdx < cloneTexts.Length)
            {
                cloneValue = cloneTexts[valueIdx];
            }

            // 2. Fallback to name search using template names
            if (cloneLabel == null && templateLabel != null)
            {
                cloneLabel = cloneTexts.FirstOrDefault(t => t.gameObject.name == templateLabel.name);
            }
            if (cloneValue == null && templateValue != null)
            {
                cloneValue = cloneTexts.FirstOrDefault(t => t.gameObject.name == templateValue.name);
            }

            // 3. Fallback by hardcoded template names (e.g. Integrity Header, Resource Label)
            if (cloneLabel == null)
            {
                cloneLabel = cloneTexts.FirstOrDefault(t => t.gameObject.name == "Integrity Header" || t.gameObject.name == "Header" || t.gameObject.name.Contains("Label"));
            }
            if (cloneValue == null)
            {
                cloneValue = cloneTexts.FirstOrDefault(t => t.gameObject.name == "Resource Label" || t.gameObject.name == "Value" || t.gameObject.name.Contains("Value"));
            }

            // 4. Fallback by position/order (usually index 0 is label, index 1 is value)
            if (cloneLabel == null && cloneTexts.Length > 0)
            {
                cloneLabel = cloneTexts[0];
            }
            if (cloneValue == null && cloneTexts.Length > 1)
            {
                cloneValue = cloneTexts[1];
            }
            else if (cloneValue == null && cloneTexts.Length == 1)
            {
                cloneValue = cloneTexts[0];
            }
        }

        /// <summary>
        /// Remove the classic opaque bottom HUD plate. Keep card buttons and selection
        /// action buttons; hide Border / panel Background images only.
        /// </summary>
        private void StripLegacyBottomBarChrome()
        {
            Transform bottomBar = FindChildRecursive(transform, "Bottom Bar");
            if (bottomBar == null)
            {
                var found = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);
                foreach (var t in found)
                {
                    if (t != null && t.name == "Bottom Bar")
                    {
                        bottomBar = t;
                        break;
                    }
                }
            }

            if (bottomBar != null)
            {
                Transform border = bottomBar.Find("Border");
                if (border != null) border.gameObject.SetActive(false);

                ClearUiImage(bottomBar.Find("Actions Container/Background"));
                ClearUiImage(bottomBar.Find("Minimap Container/Background"));
                ClearUiImage(bottomBar.Find("Menu Container"));
            }

            Transform cardContainer = FindChildRecursive(transform, "Bottom Action Bar Container");
            if (cardContainer != null)
            {
                ClearUiImage(cardContainer);
            }
        }

        private static void ClearUiImage(Transform target)
        {
            if (target == null) return;
            var image = target.GetComponent<Image>();
            if (image == null) return;
            Color c = image.color;
            c.a = 0f;
            image.color = c;
            image.raycastTarget = false;
            image.enabled = false;
        }

        private void CopyRectTransform(RectTransform source, RectTransform target)
        {
            if (source == null || target == null) return;
            target.anchorMin = source.anchorMin;
            target.anchorMax = source.anchorMax;
            target.pivot = source.pivot;
            target.sizeDelta = source.sizeDelta;
            target.localScale = source.localScale;
            target.localRotation = source.localRotation;
            target.anchoredPosition = source.anchoredPosition;
        }

        private void Awake()
        {
            Transform objectivesPanelT = FindChildRecursive(transform, "Active Objectives Panel");
            if (objectivesPanelT == null)
            {
                GameObject objectivesPanel = new GameObject("Active Objectives Panel");
                objectivesPanel.transform.SetParent(transform, false);
                objectivesPanel.AddComponent<GameDevTV.RTS.UI.Containers.ActiveObjectivesUI>();
#if UNITY_EDITOR
                if (!Application.isPlaying) UnityEditor.Undo.RegisterCreatedObjectUndo(objectivesPanel, "Create Objectives Panel");
#endif
            }

            // Wire the bottom bar action panel: use Inspector reference first,
            // then search children as fallback, then create as last resort
            StripLegacyBottomBarChrome();

            if (bottomBarActionsUI == null)
            {
                bottomBarActionsUI = GetComponentInChildren<BottomBarActionsUI>(true);
            }

            if (bottomBarActionsUI == null)
            {
                bottomBarActionsUI = UnityEngine.Object.FindAnyObjectByType<BottomBarActionsUI>();
            }

            if (bottomBarActionsUI == null)
            {
                // Create a proper RectTransform-based hierarchy for the bottom bar
                GameObject containerGo = new GameObject("Bottom Action Bar Container", typeof(RectTransform));
                containerGo.transform.SetParent(transform, false);
                RectTransform containerRt = containerGo.GetComponent<RectTransform>();
                containerRt.anchorMin = new Vector2(0f, 0f);
                containerRt.anchorMax = new Vector2(1f, 0f);
                containerRt.pivot = new Vector2(0.5f, 0f);
                containerRt.anchoredPosition = new Vector2(0f, 30f);
                containerRt.sizeDelta = new Vector2(0f, 60f);

                GameObject barGo = new GameObject("Bottom Action Bar", typeof(RectTransform));
                barGo.transform.SetParent(containerGo.transform, false);
                RectTransform barRt = barGo.GetComponent<RectTransform>();
                barRt.anchorMin = Vector2.zero;
                barRt.anchorMax = Vector2.one;
                barRt.sizeDelta = Vector2.zero;

                bottomBarActionsUI = barGo.AddComponent<BottomBarActionsUI>();
                Debug.LogWarning("[RuntimeUI] Created BottomBarActionsUI with RectTransform. Wire UIActionButton children in the Inspector.");
#if UNITY_EDITOR
                if (!Application.isPlaying) UnityEditor.Undo.RegisterCreatedObjectUndo(containerGo, "Create Bottom Action Bar");
#endif
            }

            FindAndLinkUI("Minerals Container", ref materialsLabelText, ref materialsValueText, "Materials Header", "Minerals Header", "Biomass Header");
            FindAndLinkUI("Oxygen Container", ref oxygenLabelText, ref oxygenValueText, "Oxygen Header");
            FindAndLinkUI("Integrity Container", ref integrityLabelText, ref integrityValueText, "Integrity Header");
            FindAndLinkUI("Biomass Container", ref biomassLabelText, ref biomassValueText, "Biomass Header");
            FindAndLinkUI("Gas Container", ref biomassLabelText, ref biomassValueText, "Biomass Header", "Gas Header");
            FindAndLinkUI("Sectors Container", ref sectorsLabelText, ref sectorsValueText, "Sectors Header");
            
            // Setup layouts and alignments dynamically (Power, Temp, Atmos)
            GameObject integrityContainerGo = GameObject.Find("Integrity Container");
            if (integrityContainerGo == null)
            {
                Transform t = FindChildRecursive(transform, "Integrity Container");
                if (t != null) integrityContainerGo = t.gameObject;
            }

            if (integrityContainerGo != null)
            {
                Transform containerParent = integrityContainerGo.transform.parent;
                GameObject template = integrityContainerGo;

                Transform powerT = FindChildRecursive(containerParent, "Power Container");
                Transform tempT = FindChildRecursive(containerParent, "Temperature Container");
                Transform atmosT = FindChildRecursive(containerParent, "Atmosphere Container");
                Transform waterT = FindChildRecursive(containerParent, "Water Container");

                // 1. Power Container
                if (powerT == null && powerValueText == null)
                {
                    GameObject powerClone = Instantiate(template, containerParent);
                    powerClone.name = "Power Container";
                    CopyRectTransform(template.GetComponent<RectTransform>(), powerClone.GetComponent<RectTransform>());
                    ResolveTextsFromClone(powerClone, template, integrityLabelText, integrityValueText, out powerLabelText, out powerValueText, "Power Header", "Resource Label");
                    if (powerLabelText != null) powerLabelText.gameObject.name = "Power Header";
                    powerClone.SetActive(true);
#if UNITY_EDITOR
                    if (!Application.isPlaying) UnityEditor.Undo.RegisterCreatedObjectUndo(powerClone, "Create Power Container");
#endif
                }
                else if (powerT != null && powerValueText == null)
                {
                    ResolveTextsFromClone(powerT.gameObject, template, integrityLabelText, integrityValueText, out powerLabelText, out powerValueText, "Power Header", "Resource Label");
                }

                // 2. Temperature Container
                if (tempT == null && temperatureValueText == null)
                {
                    GameObject temperatureClone = Instantiate(template, containerParent);
                    temperatureClone.name = "Temperature Container";
                    CopyRectTransform(template.GetComponent<RectTransform>(), temperatureClone.GetComponent<RectTransform>());
                    ResolveTextsFromClone(temperatureClone, template, integrityLabelText, integrityValueText, out temperatureLabelText, out temperatureValueText, "Temperature Header", "Resource Label");
                    if (temperatureLabelText != null) temperatureLabelText.gameObject.name = "Temperature Header";
                    temperatureClone.SetActive(true);
#if UNITY_EDITOR
                    if (!Application.isPlaying) UnityEditor.Undo.RegisterCreatedObjectUndo(temperatureClone, "Create Temperature Container");
#endif
                }
                else if (tempT != null && temperatureValueText == null)
                {
                    ResolveTextsFromClone(tempT.gameObject, template, integrityLabelText, integrityValueText, out temperatureLabelText, out temperatureValueText, "Temperature Header", "Resource Label");
                }

                // 3. Atmosphere Container
                if (atmosT == null && atmosphereValueText == null)
                {
                    GameObject atmosphereClone = Instantiate(template, containerParent);
                    atmosphereClone.name = "Atmosphere Container";
                    CopyRectTransform(template.GetComponent<RectTransform>(), atmosphereClone.GetComponent<RectTransform>());
                    ResolveTextsFromClone(atmosphereClone, template, integrityLabelText, integrityValueText, out atmosphereLabelText, out atmosphereValueText, "Atmosphere Header", "Resource Label");
                    if (atmosphereLabelText != null) atmosphereLabelText.gameObject.name = "Atmosphere Header";
                    atmosphereClone.SetActive(true);
#if UNITY_EDITOR
                    if (!Application.isPlaying) UnityEditor.Undo.RegisterCreatedObjectUndo(atmosphereClone, "Create Atmosphere Container");
#endif
                }
                else if (atmosT != null && atmosphereValueText == null)
                {
                    ResolveTextsFromClone(atmosT.gameObject, template, integrityLabelText, integrityValueText, out atmosphereLabelText, out atmosphereValueText, "Atmosphere Header", "Resource Label");
                }

                // 4. Water Container
                if (waterT == null && waterValueText == null)
                {
                    GameObject waterClone = Instantiate(template, containerParent);
                    waterClone.name = "Water Container";
                    CopyRectTransform(template.GetComponent<RectTransform>(), waterClone.GetComponent<RectTransform>());
                    ResolveTextsFromClone(waterClone, template, integrityLabelText, integrityValueText, out waterLabelText, out waterValueText, "Water Header", "Resource Label");
                    if (waterLabelText != null) waterLabelText.gameObject.name = "Water Header";
                    waterClone.SetActive(true);
#if UNITY_EDITOR
                    if (!Application.isPlaying) UnityEditor.Undo.RegisterCreatedObjectUndo(waterClone, "Create Water Container");
#endif
                }
                else if (waterT != null && waterValueText == null)
                {
                    ResolveTextsFromClone(waterT.gameObject, template, integrityLabelText, integrityValueText, out waterLabelText, out waterValueText, "Water Header", "Resource Label");
                }

                // Set proper layout group sibling indices so they layout in order
                int integrityIndex = integrityContainerGo.transform.GetSiblingIndex();
                var pObj = powerT ?? (containerParent.Find("Power Container"));
                if (pObj != null) pObj.SetSiblingIndex(integrityIndex + 1);

                var tObj = tempT ?? (containerParent.Find("Temperature Container"));
                if (tObj != null) tObj.SetSiblingIndex(integrityIndex + 2);

                var aObj = atmosT ?? (containerParent.Find("Atmosphere Container"));
                if (aObj != null) aObj.SetSiblingIndex(integrityIndex + 3);

                var wObj = waterT ?? (containerParent.Find("Water Container"));
                if (wObj != null) wObj.SetSiblingIndex(integrityIndex + 4);

                // Auto Layout Group
                if (containerParent.GetComponent<Canvas>() == null && containerParent.GetComponent<HorizontalLayoutGroup>() == null)
                {
                    HorizontalLayoutGroup hlg = containerParent.gameObject.AddComponent<HorizontalLayoutGroup>();
                    hlg.childControlWidth = false;
                    hlg.childControlHeight = false;
                    hlg.childForceExpandWidth = false;
                    hlg.childForceExpandHeight = false;
                    hlg.spacing = 20f;
                    hlg.childAlignment = TextAnchor.UpperLeft;
                }
                else if (containerParent.GetComponent<HorizontalLayoutGroup>() == null)
                {
                    // Fallback to manual offset if parent is the Canvas
                    RectTransform rtInt = template.GetComponent<RectTransform>();
                    
                    GameObject materialsContainerGo = GameObject.Find("Materials Container");
                    if (materialsContainerGo == null)
                    {
                        Transform t = FindChildRecursive(transform, "Materials Container");
                        if (t != null) materialsContainerGo = t.gameObject;
                    }
                    RectTransform rtMat = materialsContainerGo != null ? materialsContainerGo.GetComponent<RectTransform>() : null;
                    
                    float stepX = 150f;
                    if (rtInt != null && rtMat != null)
                    {
                        stepX = (rtInt.anchoredPosition.x - rtMat.anchoredPosition.x) / 3f;
                        if (stepX == 0) stepX = 150f;
                    }

                    if (pObj != null)
                    {
                        var rt = pObj.GetComponent<RectTransform>();
                        if (rt != null) rt.anchoredPosition = rtInt.anchoredPosition + new Vector2(stepX, 0f);
                    }
                    if (tObj != null)
                    {
                        var rt = tObj.GetComponent<RectTransform>();
                        if (rt != null) rt.anchoredPosition = rtInt.anchoredPosition + new Vector2(stepX * 2f, 0f);
                    }
                    if (aObj != null)
                    {
                        var rt = aObj.GetComponent<RectTransform>();
                        if (rt != null) rt.anchoredPosition = rtInt.anchoredPosition + new Vector2(stepX * 3f, 0f);
                    }
                }

                // Force layout recalculation
                var parentRt = containerParent.GetComponent<RectTransform>();
                if (parentRt != null)
                {
                    LayoutRebuilder.ForceRebuildLayoutImmediate(parentRt);
                }
            }

            // Setup Warning Banner
            if (warningBanner == null)
            {
                Canvas canvas = FindAnyObjectByType<Canvas>();
                if (canvas != null)
                {
                    Transform existingBanner = FindChildRecursive(canvas.transform, "Warning Banner");
                    if (existingBanner != null)
                    {
                        warningBanner = existingBanner.gameObject;
                        warningText = warningBanner.GetComponentInChildren<TextMeshProUGUI>();
                    }
                    else
                    {
                        warningBanner = new GameObject("Warning Banner");
                        warningBanner.transform.SetParent(canvas.transform, false);
                        RectTransform rt = warningBanner.AddComponent<RectTransform>();
                        rt.anchorMin = new Vector2(0, 1);
                        rt.anchorMax = new Vector2(1, 1);
                        rt.pivot = new Vector2(0.5f, 1);
                        rt.offsetMin = new Vector2(0, -100);
                        rt.offsetMax = new Vector2(0, 0);

                        Image bg = warningBanner.AddComponent<Image>();
                        bg.color = new Color(1f, 0f, 0f, 0.8f);

                        GameObject textObj = new GameObject("Warning Text");
                        textObj.transform.SetParent(warningBanner.transform, false);
                        RectTransform trt = textObj.AddComponent<RectTransform>();
                        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
                        trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;

                        warningText = textObj.AddComponent<TextMeshProUGUI>();
                        warningText.alignment = TextAlignmentOptions.Center;
                        warningText.fontSize = 48;
                        warningText.color = Color.white;
                        warningText.fontStyle = FontStyles.Bold;

                        warningBanner.SetActive(false);
                    }
                }
            }
            
            // Special case for the duplicate population text if it exists
            if (populationText == null && oxygenValueText != null) populationText = oxygenValueText;

            GameObject probeContainer = GameObject.Find("Probe Progress Container");
            if (probeContainer == null)
            {
                Transform t = FindChildRecursive(transform, "Probe Progress Container");
                if (t != null) probeContainer = t.gameObject;
            }
            if (probeContainer != null)
            {
                if (Application.isPlaying)
                    Destroy(probeContainer);
                else
                    DestroyImmediate(probeContainer);
            }

            if (!Application.isPlaying)
            {
                InitializeUI();
                RebuildLayouts();
                ApplyHudReadability();
            }
        }

        public void ShowWarningBanner(string message)
        {
            if (warningBanner != null && warningText != null)
            {
                warningText.text = message;
                warningBanner.SetActive(true);
                StopCoroutine(nameof(FlashWarningRoutine));
                StartCoroutine(nameof(FlashWarningRoutine));
            }
        }

        public void HideWarningBanner()
        {
            if (warningBanner != null)
            {
                warningBanner.SetActive(false);
                StopCoroutine(nameof(FlashWarningRoutine));
            }
        }

        private System.Collections.IEnumerator FlashWarningRoutine()
        {
            Image bg = warningBanner.GetComponent<Image>();
            if (bg == null)
            {
                yield return new WaitForSeconds(3.5f);
                HideWarningBanner();
                yield break;
            }

            // Flash a few times, then clear — never loop forever.
            const int flashCount = 4;
            for (int i = 0; i < flashCount; i++)
            {
                if (warningBanner == null || !warningBanner.activeSelf) yield break;
                bg.color = new Color(1f, 0f, 0f, 0.85f);
                yield return new WaitForSeconds(0.4f);
                if (warningBanner == null || !warningBanner.activeSelf) yield break;
                bg.color = new Color(1f, 0f, 0f, 0.35f);
                yield return new WaitForSeconds(0.4f);
            }

            HideWarningBanner();
        }

        private void FindAndLinkUI(string containerName, ref TextMeshProUGUI labelField, ref TextMeshProUGUI valueField, params string[] headerNames)
        {
            Transform containerT = FindChildRecursive(transform, containerName);
            if (containerT == null)
            {
                GameObject containerGo = GameObject.Find(containerName);
                if (containerGo != null) containerT = containerGo.transform;
            }
            if (containerT == null) return;

            GameObject container = containerT.gameObject;

            if (valueField == null)
            {
                // Look for Resource Label anywhere in children
                valueField = container.GetComponentsInChildren<TextMeshProUGUI>(true)
                    .FirstOrDefault(t => t.gameObject.name == "Resource Label" || t.gameObject.name.Contains("Value") || t.gameObject.name.Contains("Label"));
            }

            if (labelField == null)
            {
                // Look for header anywhere in children
                labelField = container.GetComponentsInChildren<TextMeshProUGUI>(true)
                    .FirstOrDefault(t => headerNames.Contains(t.gameObject.name) || t.gameObject.name.Contains("Header") || t.gameObject.name.Contains("Title"));
            }
        }

        private bool layoutRebuilt = false;

        private void Start()
        {
            if (!Application.isPlaying) return;
            InitializeUI();
            RebuildLayouts();
            ApplyHudReadability();
            RefreshUI();
        }

        private void Update()
        {
            if (!Application.isPlaying) return;
            UpdateSectorsUI();
            if (!layoutRebuilt)
            {
                layoutRebuilt = true;
                RebuildLayouts();
                ApplyHudReadability();
            }
        }

        private void RebuildLayouts()
        {
            GameObject integrityContainerGo = GameObject.Find("Integrity Container");
            if (integrityContainerGo == null)
            {
                Transform t = FindChildRecursive(transform, "Integrity Container");
                if (t != null) integrityContainerGo = t.gameObject;
            }
            if (integrityContainerGo != null)
            {
                Transform containerParent = integrityContainerGo.transform.parent;
                var parentRt = containerParent.GetComponent<RectTransform>();
                if (parentRt != null)
                {
                    LayoutRebuilder.ForceRebuildLayoutImmediate(parentRt);
                }
            }
        }

        /// <summary>
        /// Darken the top resource strip and tint climate/milestone headers so
        /// values stay readable over terrain and match Active Objectives colors.
        /// </summary>
        private void ApplyHudReadability()
        {
            EnsureTopResourceBar();

            StyleMetric(materialsLabelText, materialsValueText, null, "Materials");
            StyleMetric(biomassLabelText, biomassValueText, null, "Biomass");
            StyleMetric(oxygenLabelText, oxygenValueText, "OXYGEN", "Oxygen");
            StyleMetric(powerLabelText, powerValueText, "POWER", "Power");
            StyleMetric(integrityLabelText, integrityValueText, null, "Integrity");
            StyleMetric(sectorsLabelText, sectorsValueText, null, "Sectors");
            StyleMetric(temperatureLabelText, temperatureValueText, "TEMPERATURE", "Temp");
            StyleMetric(atmosphereLabelText, atmosphereValueText, "ATMOSPHERE", "Atmos");
            StyleMetric(waterLabelText, waterValueText, "WATER", "Water");

            if (populationText != null)
            {
                StyleValueText(populationText);
                populationText.color = TerraformingGoalColors.Population;
            }
        }

        /// <summary>
        /// Full-width opaque top HUD band so resource metrics stay readable over terrain.
        /// Parent is always the RuntimeUI / Canvas root — not the metric strip alone.
        /// </summary>
        private void EnsureTopResourceBar()
        {
            const string barName = "Top Resource Bar";
            const float barHeight = 128f;

            Transform root = transform;
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas != null) root = canvas.transform;

            Transform existing = root.Find(barName);
            if (existing == null)
            {
                // Also search descendants in case it was parented elsewhere previously.
                existing = FindChildRecursive(root, barName);
            }

            Image barImage;
            RectTransform rt;
            if (existing == null)
            {
                var go = new GameObject(barName, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                go.transform.SetParent(root, false);
                go.transform.SetAsFirstSibling();
                existing = go.transform;
                barImage = go.GetComponent<Image>();
                rt = go.GetComponent<RectTransform>();
                go.GetComponent<LayoutElement>().ignoreLayout = true;
            }
            else
            {
                if (existing.parent != root)
                {
                    existing.SetParent(root, false);
                }
                existing.SetAsFirstSibling();
                barImage = existing.GetComponent<Image>();
                if (barImage == null) barImage = existing.gameObject.AddComponent<Image>();
                rt = existing as RectTransform;
                if (rt == null) rt = existing.gameObject.AddComponent<RectTransform>();
                var le = existing.GetComponent<LayoutElement>();
                if (le == null) le = existing.gameObject.AddComponent<LayoutElement>();
                le.ignoreLayout = true;
            }

            // Edge-to-edge top band.
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(0f, barHeight);
            rt.offsetMin = new Vector2(0f, -barHeight);
            rt.offsetMax = Vector2.zero;

            // Match Active Objectives opacity so the strip reads as a real HUD chrome.
            barImage.color = new Color(0.02f, 0.03f, 0.07f, 0.94f);
            barImage.raycastTarget = false;

            // Soft underline so the bar edge separates from the world.
            const string edgeName = "Top Resource Bar Edge";
            Transform edgeT = existing.Find(edgeName);
            Image edgeImage;
            RectTransform edgeRt;
            if (edgeT == null)
            {
                var edgeGo = new GameObject(edgeName, typeof(RectTransform), typeof(Image));
                edgeGo.transform.SetParent(existing, false);
                edgeT = edgeGo.transform;
                edgeImage = edgeGo.GetComponent<Image>();
                edgeRt = edgeGo.GetComponent<RectTransform>();
            }
            else
            {
                edgeImage = edgeT.GetComponent<Image>();
                if (edgeImage == null) edgeImage = edgeT.gameObject.AddComponent<Image>();
                edgeRt = edgeT as RectTransform;
                if (edgeRt == null) edgeRt = edgeT.gameObject.AddComponent<RectTransform>();
            }

            edgeRt.anchorMin = new Vector2(0f, 0f);
            edgeRt.anchorMax = new Vector2(1f, 0f);
            edgeRt.pivot = new Vector2(0.5f, 0f);
            edgeRt.anchoredPosition = Vector2.zero;
            edgeRt.sizeDelta = new Vector2(0f, 3f);
            edgeImage.color = new Color(0.35f, 0.75f, 0.95f, 0.55f);
            edgeImage.raycastTarget = false;

            // Remove any old strip-local backdrop that only covered a small box.
            Transform legacy = FindChildRecursive(transform, "Resource Bar Backdrop");
            if (legacy != null && legacy != existing)
            {
                if (Application.isPlaying) Destroy(legacy.gameObject);
                else DestroyImmediate(legacy.gameObject);
            }
        }

        private static void StyleMetric(TextMeshProUGUI label, TextMeshProUGUI value, string goalKey, string fallbackLabel)
        {
            if (label != null)
            {
                if (!string.IsNullOrEmpty(fallbackLabel) && string.IsNullOrEmpty(label.text))
                {
                    label.SetText(fallbackLabel);
                }

                label.enableAutoSizing = false;
                label.fontSize = Mathf.Max(label.fontSize, 17f);
                label.fontStyle = FontStyles.Bold;
                label.color = string.IsNullOrEmpty(goalKey)
                    ? TerraformingGoalColors.Neutral
                    : TerraformingGoalColors.ForGoal(goalKey);
                EnsureTextOutline(label, new Color(0f, 0f, 0f, 0.95f), new Vector2(1.4f, -1.4f));
                label.raycastTarget = false;
            }

            StyleValueText(value);
        }

        private static void StyleValueText(TextMeshProUGUI value)
        {
            if (value == null) return;
            value.enableAutoSizing = false;
            value.fontSize = Mathf.Max(value.fontSize, 26f);
            value.fontStyle = FontStyles.Bold;
            value.color = Color.white;
            EnsureTextOutline(value, new Color(0f, 0f, 0f, 0.95f), new Vector2(1.6f, -1.6f));
            value.raycastTarget = false;
        }

        private static void EnsureTextOutline(TextMeshProUGUI text, Color color, Vector2 distance)
        {
            if (text == null) return;
            var outline = text.GetComponent<Outline>();
            if (outline == null) outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = distance;
            outline.useGraphicAlpha = true;
        }

        private void UpdateSectorsUI()
        {
            if (SectorManager.Instance == null || sectorsValueText == null) return;

            int occupied = SectorManager.Instance.Sectors.Count(s => s.IsOccupied);
            int total = SectorManager.Instance.Sectors.Count;

            if (total > 0)
            {
                string text = $"{occupied}/{total}";
                
                // Integrated expansion progress (the original "Exp" display)
                var activePipelines = Object.FindObjectsByType<EnergyPipelineManager>(FindObjectsInactive.Exclude);
                if (activePipelines.Length > 0)
                {
                    var lead = activePipelines.OrderByDescending(e => e.GetProgress()).First();
                    float maxProgress = lead.GetProgress();
                    string pausedSuffix = lead.IsPaused ? " PAUSED" : "";
                    text += $" (Exp: {maxProgress * 100:F0}%{pausedSuffix})";
                }

                sectorsValueText.SetText(text);
            }
        }

        private void InitializeUI()
        {
            displayedOwner = GameOverManager.MonitoredOwner;

            if (materialsLabelText != null) materialsLabelText.SetText("Materials");
            if (materialsValueText != null && Supplies.Materials != null && Supplies.Materials.TryGetValue(displayedOwner, out int initial))
                materialsValueText.SetText(initial.ToString());

            if (biomassLabelText != null) biomassLabelText.SetText("Biomass");
            if (biomassValueText != null && Supplies.Biomass != null && Supplies.Biomass.TryGetValue(displayedOwner, out float bInitial))
                biomassValueText.SetText($"{bInitial:F1}%");

            if (powerLabelText != null) powerLabelText.SetText("Power");
            if (powerValueText != null && Supplies.Power != null && Supplies.Power.TryGetValue(displayedOwner, out float pInitial))
                powerValueText.SetText($"{pInitial:F0}");

            if (oxygenLabelText != null) oxygenLabelText.SetText("Oxygen");
            if (oxygenValueText != null && Supplies.Oxygen != null && Supplies.Oxygen.TryGetValue(displayedOwner, out float oxyInitial))
            {
                oxygenValueText.SetText($"{oxyInitial:F1}%");
            }

            if (sectorsLabelText != null) sectorsLabelText.SetText("Sectors");
            UpdateSectorsUI();

            if (integrityLabelText != null) integrityLabelText.SetText("Integrity");
            if (integrityValueText != null && Supplies.Integrity != null && Supplies.Integrity.TryGetValue(displayedOwner, out float integrityInitial))
            {
                integrityValueText.SetText(integrityInitial.ToString("F1"));
            }

            if (temperatureLabelText != null) temperatureLabelText.SetText("Temp");
            if (temperatureValueText != null)
            {
                float tempVal = -60f;
                if (Supplies.Temperature != null && Supplies.Temperature.TryGetValue(displayedOwner, out float tempInitial))
                {
                    tempVal = tempInitial;
                }
                temperatureValueText.SetText($"{tempVal:F1}°C");
            }

            if (atmosphereLabelText != null) atmosphereLabelText.SetText("Atmos");
            if (atmosphereValueText != null)
            {
                float atmosVal = 0.01f;
                if (Supplies.Atmosphere != null && Supplies.Atmosphere.TryGetValue(displayedOwner, out float atmosInitial))
                {
                    atmosVal = atmosInitial;
                }
                atmosphereValueText.SetText($"{atmosVal:F2} atm");
            }

            if (waterLabelText != null) waterLabelText.SetText("Water");
            if (waterValueText != null)
            {
                float waterVal = 0f;
                if (Supplies.Water != null && Supplies.Water.TryGetValue(displayedOwner, out float waterInitial))
                {
                    waterVal = waterInitial;
                }
                waterValueText.SetText($"{waterVal:F1}%");
            }

            UpdatePopulationText();
        }

        private void ResetUI()
        {
            // Materials
            if (materialsValueText != null) materialsValueText.SetText("0");
            // Oxygen
            if (oxygenValueText != null) oxygenValueText.SetText("0.0%");
            // Integrity
            if (integrityValueText != null) integrityValueText.SetText("0.0");
            // Power
            if (powerValueText != null) powerValueText.SetText("0");
            // Biomass
            if (biomassValueText != null) biomassValueText.SetText("0.0%");
            // Sectors
            if (sectorsValueText != null) sectorsValueText.SetText("0/0");
            // Population
            if (populationText != null) populationText.SetText("0 / 0");
            // Temperature
            if (temperatureValueText != null) temperatureValueText.SetText("-60.0°C");
            // Atmosphere
            if (atmosphereValueText != null) atmosphereValueText.SetText("0.01 atm");
            // Water
            if (waterValueText != null) waterValueText.SetText("0.0%");
        }

        private void OnDestroy()
        {
            Supplies.OnOxygenChanged -= HandleOxygenChanged;
            Supplies.OnMaterialsChanged -= HandleMaterialsChanged;
            Supplies.OnIntegrityChanged -= HandleIntegrityChanged;
            Supplies.OnBiomassChanged -= HandleBiomassChanged;
            Supplies.OnFoodChanged -= HandleFoodChanged;
            Supplies.OnPowerChanged -= HandlePowerChanged;
            Supplies.OnPopulationChanged -= HandlePopulationChanged;
            Supplies.OnPopulationLimitChanged -= HandlePopulationLimitChanged;
            Supplies.OnTemperatureChanged -= HandleTemperatureChanged;
            Supplies.OnAtmosphereChanged -= HandleAtmosphereChanged;
            Supplies.OnWaterChanged -= HandleWaterChanged;

            // Reset UI values when the component is destroyed (e.g., game end)
            ResetUI();
        }

        private void HandlePopulationChanged(Owner owner, int newValue)
        {
            if (owner != displayedOwner) return;
            UpdatePopulationText();
        }

        private void HandlePopulationLimitChanged(Owner owner, int newValue)
        {
            if (owner != displayedOwner) return;
            UpdatePopulationText();
        }

        private void UpdatePopulationText()
        {
            if (populationText != null && Supplies.Population != null && Supplies.PopulationLimit != null)
            {
                int pop = Supplies.Population.TryGetValue(displayedOwner, out int p) ? p : 0;
                int limit = Supplies.PopulationLimit.TryGetValue(displayedOwner, out int l) ? l : 0;
                populationText.SetText($"{pop} / {limit}");
            }
        }

        private void HandleOxygenChanged(Owner owner, float newValue)
        {
            if (owner != displayedOwner) return;
            if (oxygenValueText != null)
                oxygenValueText.SetText($"{newValue:F1}%");
        }

        private void HandleIntegrityChanged(Owner owner, float newValue)
        {
            if (owner != displayedOwner) return;
            if (integrityValueText != null)
                integrityValueText.SetText(newValue.ToString("F1"));
        }

        private void HandleMaterialsChanged(Owner owner, int newValue)
        {
            if (owner != displayedOwner) 
            {
                return;
            }

            if (materialsValueText == null) 
            {
                return;
            }

            materialsValueText.SetText(newValue.ToString());
        }

        private void HandleBiomassChanged(Owner owner, float newValue)
        {
            if (owner != displayedOwner) return;
            UpdateBiomassAndFoodUI();
        }

        private void HandleFoodChanged(Owner owner, float newValue)
        {
            if (owner != displayedOwner) return;
            UpdateBiomassAndFoodUI();
        }

        private void UpdateBiomassAndFoodUI()
        {
            if (biomassValueText != null)
            {
                float bio = Supplies.Biomass.TryGetValue(displayedOwner, out float b) ? b : 0f;
                float food = Supplies.Food.TryGetValue(displayedOwner, out float f) ? f : 0f;
                biomassValueText.SetText($"{bio:F1}% | Food: {food:F0}");
            }
        }

        private void HandlePowerChanged(Owner owner, float newValue)
        {
            if (owner != displayedOwner) return;
            Debug.Log($"[RuntimeUI] HandlePowerChanged: value={newValue}, powerValueText={(powerValueText == null ? "NULL" : powerValueText.name)}");
            if (powerValueText != null) powerValueText.SetText($"{newValue:F0}");
        }

        private void HandleUnitSelected(UnitSelectedEvent evt)
        {
            if (evt.Unit is AbstractCommandable commandable)
            {
                selectedUnits.Add(commandable);
                RefreshUI();
            }
        }

        private void HandleUnitDeath(UnitDeathEvent evt)
        {
            selectedUnits.Remove(evt.Unit);
            RefreshUI();
        }

        private void HandleBuildingDeath(BuildingDeathEvent evt)
        {
            selectedUnits.Remove(evt.Building);
            RefreshUI();
        }

        private void HandleUpgradeResearched(UpgradeResearchedEvent args)
        {
            RefreshUI();
        }

        private void HandleBuildingSpawn(BuildingSpawnEvent args)
        {
            if (selectedUnits.Count == 1 && selectedUnits.First() is Worker)
            {
                actionsUI.EnableFor(selectedUnits);
            }
        }

        private void HandleLoadUnit(UnitLoadEvent evt)
        {
            if (selectedUnits.Count == 1 && selectedUnits.First() is ITransporter)
            {
                RefreshUI();
            }
            else if (evt.Unit is AbstractCommandable commandable && selectedUnits.Contains(commandable))
            {
                commandable.Deselect(); // RefreshUI will be called because of the UnitDeselectedEvent raised from this.
            }
        }

        private void HandleUnloadUnit(UnitUnloadEvent evt)
        {
            if (selectedUnits.Count == 1 && selectedUnits.First() is ITransporter)
            {
                RefreshUI();
            }
        }

        private void HandleUnitDeselected(UnitDeselectedEvent evt)
        {
            if (evt.Unit is AbstractCommandable commandable)
            {
                selectedUnits.Remove(commandable);

                RefreshUI();
            }
        }

        private void RefreshUI()
        {
            if (selectedUnits.Count > 0)
            {
                TryDisable(globalCommanderUI);
                actionsUI.EnableFor(selectedUnits);
                actionsUI.gameObject.SetActive(true);

                if (selectedUnits.Count == 1)
                {
                    ResolveSingleUnitSelectedUI();
                }
                else
                {
                    unitIconUI.Disable();
                    singleUnitSelectedUI.Disable();
                    buildingSelectedUI.Disable();
                    unitTransportUI.Disable();
                }
            }
            else
            {
                DisableAllContainers();
                
                if (globalCommander != null)
                {
                    actionsUI.EnableFor(new HashSet<AbstractCommandable> { globalCommander });
                    actionsUI.gameObject.SetActive(true);
                    
                    if (globalCommanderUI != null)
                    {
                        globalCommanderUI.EnableFor(globalCommander);
                    }
                }
            }
        }

        private void DisableAllContainers()
        {
            TryDisable(actionsUI);
            TryDisable(buildingSelectedUI);
            TryDisable(unitIconUI);
            TryDisable(singleUnitSelectedUI);
            TryDisable(unitTransportUI);
            TryDisable(globalCommanderUI);
            // Note: abilityHandUI is NOT disabled here — it persists across selection changes
        }

        // Safely call Disable on UI container objects that may have partially-destroyed child components.
        private void TryDisable(object container)
        {
            if (container == null) return;
            try
            {
                switch (container)
                {
                    case GameDevTV.RTS.UI.Containers.ActionsUI a: a.Disable(); return;
                    case GameDevTV.RTS.UI.Containers.BuildingSelectedUI b: b.Disable(); return;
                    case GameDevTV.RTS.UI.Containers.UnitIconUI u: u.Disable(); return;
                    case GameDevTV.RTS.UI.Containers.SingleUnitSelectedUI s: s.Disable(); return;
                    case GameDevTV.RTS.UI.Containers.UnitTransportUI t: t.Disable(); return;
                    case GameDevTV.RTS.UI.Containers.GlobalCommanderUI g: g.Disable(); return;

                }

                // Fallback: try to invoke a Disable method via reflection (covers unexpected types)
                var mi = container.GetType().GetMethod("Disable", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                mi?.Invoke(container, null);
            }
            catch (UnityEngine.MissingReferenceException)
            {
                // ignore: UI child was destroyed during shutdown/unload
            }
            catch (System.Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        private void ResolveSingleUnitSelectedUI()
        {
            AbstractCommandable commandable = selectedUnits.First();
            if (commandable == null)
            {
                selectedUnits.Clear();
                DisableAllContainers();
                return;
            }
            unitIconUI.EnableFor(commandable);

            if (commandable is BaseBuilding building)
            {
                singleUnitSelectedUI.Disable();
                unitTransportUI.Disable();
                buildingSelectedUI.EnableFor(building);
            }
            else if (commandable is ITransporter transporter && transporter.UsedCapacity > 0)
            {
                unitTransportUI.EnableFor(transporter);
                buildingSelectedUI.Disable();
                singleUnitSelectedUI.Disable();
            }
            else
            {
                buildingSelectedUI.Disable();
                unitTransportUI.Disable();
                singleUnitSelectedUI.EnableFor(commandable);
            }
        }

        private void HandleSupplyChange(SupplyEvent evt)
        {
            actionsUI.EnableFor(selectedUnits);
        }

        private void SetIcon(Sprite icon)
        {
            // Defensive: Unity objects may be destroyed while UI is tearing down.
            if (iconImage == null) return;
            try
            {
                if (icon == null)
                {
                    iconImage.sprite = null;
                    iconImage.enabled = false;
                }
                else
                {
                    iconImage.sprite = icon;
                    iconImage.enabled = true;
                }
            }
            catch (UnityEngine.MissingReferenceException)
            {
                // Reference was destroyed; clear local reference so future calls skip it.
                iconImage = null;
            }
        }

        private void HandleTemperatureChanged(Owner owner, float newValue)
        {
            if (owner != displayedOwner) return;
            if (temperatureValueText != null)
                temperatureValueText.SetText($"{newValue:F1}°C");
        }

        private void HandleAtmosphereChanged(Owner owner, float newValue)
        {
            if (owner != displayedOwner) return;
            if (atmosphereValueText != null)
                atmosphereValueText.SetText($"{newValue:F2} atm");
        }

        private void HandleWaterChanged(Owner owner, float newValue)
        {
            if (owner != displayedOwner) return;
            if (waterValueText != null)
                waterValueText.SetText($"{newValue:F1}%");
        }
    }
}
