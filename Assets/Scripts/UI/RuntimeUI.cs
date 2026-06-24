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

        [SerializeField] private Image iconImage;

        [SerializeField] private AbstractCommandable globalCommander;

        [Header("Hero & Probe HUD")]
        [SerializeField] private TextMeshProUGUI heroCargoLabelText;
        [SerializeField] private TextMeshProUGUI heroCargoValueText;
        [SerializeField] private TextMeshProUGUI probeProgressLabelText;
        [SerializeField] private TextMeshProUGUI probeProgressValueText;

        private HeroDrone heroDroneReference;
private HashSet<AbstractCommandable> selectedUnits = new(12);
        private Owner displayedOwner = Owner.Player1;

        private void OnEnable()
        {
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
            Supplies.OnPowerChanged += HandlePowerChanged;
            Supplies.OnPopulationChanged += HandlePopulationChanged;
            Supplies.OnPopulationLimitChanged += HandlePopulationLimitChanged;
            Supplies.OnTemperatureChanged += HandleTemperatureChanged;
            Supplies.OnAtmosphereChanged += HandleAtmosphereChanged;

            InitializeUI();
        }

        private void OnDisable()
        {
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
            Supplies.OnPowerChanged -= HandlePowerChanged;
            Supplies.OnPopulationChanged -= HandlePopulationChanged;
            Supplies.OnPopulationLimitChanged -= HandlePopulationLimitChanged;
            Supplies.OnTemperatureChanged -= HandleTemperatureChanged;
            Supplies.OnAtmosphereChanged -= HandleAtmosphereChanged;
        }

        [SerializeField] private TextMeshProUGUI biomassLabelText;
        [SerializeField] private TextMeshProUGUI biomassValueText;
        [SerializeField] private TextMeshProUGUI powerLabelText;
        [SerializeField] private TextMeshProUGUI powerValueText;
        
        private TextMeshProUGUI temperatureLabelText;
        private TextMeshProUGUI temperatureValueText;
        private TextMeshProUGUI atmosphereLabelText;
        private TextMeshProUGUI atmosphereValueText;
        
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
            GameObject objectivesPanel = new GameObject("Active Objectives Panel");
            objectivesPanel.transform.SetParent(transform, false);
            objectivesPanel.AddComponent<GameDevTV.RTS.UI.Containers.ActiveObjectivesUI>();

            FindAndLinkUI("Minerals Container", ref materialsLabelText, ref materialsValueText, "Materials Header", "Minerals Header", "Biomass Header");
            FindAndLinkUI("Oxygen Container", ref oxygenLabelText, ref oxygenValueText, "Oxygen Header");
            FindAndLinkUI("Integrity Container", ref integrityLabelText, ref integrityValueText, "Integrity Header");
            FindAndLinkUI("Hero Cargo Container", ref heroCargoLabelText, ref heroCargoValueText, "Hero Cargo Header");
            FindAndLinkUI("Probe Progress Container", ref probeProgressLabelText, ref probeProgressValueText, "Probe Progress Header");
            
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

                GameObject powerClone = null;
                GameObject temperatureClone = null;
                GameObject atmosphereClone = null;

                // 1. Power Container
                if (powerValueText == null)
                {
                    powerClone = Instantiate(template, containerParent);
                    powerClone.name = "Power Container";
                    CopyRectTransform(template.GetComponent<RectTransform>(), powerClone.GetComponent<RectTransform>());
                    ResolveTextsFromClone(powerClone, template, integrityLabelText, integrityValueText, out powerLabelText, out powerValueText, "Power Header", "Resource Label");
                    if (powerLabelText != null) powerLabelText.gameObject.name = "Power Header";
                    powerClone.SetActive(true);
                }

                // 2. Temperature Container
                if (temperatureValueText == null)
                {
                    temperatureClone = Instantiate(template, containerParent);
                    temperatureClone.name = "Temperature Container";
                    CopyRectTransform(template.GetComponent<RectTransform>(), temperatureClone.GetComponent<RectTransform>());
                    ResolveTextsFromClone(temperatureClone, template, integrityLabelText, integrityValueText, out temperatureLabelText, out temperatureValueText, "Temperature Header", "Resource Label");
                    if (temperatureLabelText != null) temperatureLabelText.gameObject.name = "Temperature Header";
                    temperatureClone.SetActive(true);
                }

                // 3. Atmosphere Container
                if (atmosphereValueText == null)
                {
                    atmosphereClone = Instantiate(template, containerParent);
                    atmosphereClone.name = "Atmosphere Container";
                    CopyRectTransform(template.GetComponent<RectTransform>(), atmosphereClone.GetComponent<RectTransform>());
                    ResolveTextsFromClone(atmosphereClone, template, integrityLabelText, integrityValueText, out atmosphereLabelText, out atmosphereValueText, "Atmosphere Header", "Resource Label");
                    if (atmosphereLabelText != null) atmosphereLabelText.gameObject.name = "Atmosphere Header";
                    atmosphereClone.SetActive(true);
                }

                // Set proper layout group sibling indices so they layout in order
                int integrityIndex = integrityContainerGo.transform.GetSiblingIndex();
                if (powerClone != null) powerClone.transform.SetSiblingIndex(integrityIndex + 1);
                if (temperatureClone != null) temperatureClone.transform.SetSiblingIndex(integrityIndex + 2);
                if (atmosphereClone != null) atmosphereClone.transform.SetSiblingIndex(integrityIndex + 3);

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

                    if (powerClone != null)
                    {
                        var rt = powerClone.GetComponent<RectTransform>();
                        if (rt != null) rt.anchoredPosition = rtInt.anchoredPosition + new Vector2(stepX, 0f);
                    }
                    if (temperatureClone != null)
                    {
                        var rt = temperatureClone.GetComponent<RectTransform>();
                        if (rt != null) rt.anchoredPosition = rtInt.anchoredPosition + new Vector2(stepX * 2f, 0f);
                    }
                    if (atmosphereClone != null)
                    {
                        var rt = atmosphereClone.GetComponent<RectTransform>();
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
                // Create a huge red flashing warning banner dynamically
                Canvas canvas = FindAnyObjectByType<Canvas>();
                if (canvas != null)
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
            
            // Special case for the duplicate population text if it exists
            if (populationText == null && oxygenValueText != null) populationText = oxygenValueText;

            // Permanently deactivate Hero Cargo and Probe Progress containers as they are not needed
            GameObject cargoContainer = GameObject.Find("Hero Cargo Container");
            if (cargoContainer == null)
            {
                Transform t = FindChildRecursive(transform, "Hero Cargo Container");
                if (t != null) cargoContainer = t.gameObject;
            }
            if (cargoContainer != null) cargoContainer.SetActive(false);

            GameObject probeContainer = GameObject.Find("Probe Progress Container");
            if (probeContainer == null)
            {
                Transform t = FindChildRecursive(transform, "Probe Progress Container");
                if (t != null) probeContainer = t.gameObject;
            }
            if (probeContainer != null) probeContainer.SetActive(false);
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
            if (bg == null) yield break;

            while (true)
            {
                bg.color = new Color(1f, 0f, 0f, 0.8f);
                yield return new WaitForSeconds(0.5f);
                bg.color = new Color(1f, 0f, 0f, 0.3f);
                yield return new WaitForSeconds(0.5f);
            }
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

        private void Start()
        {
            RefreshUI();
        }

        private void Update()
        {
            UpdateSectorsUI();
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
            if (temperatureValueText != null && Supplies.Temperature != null && Supplies.Temperature.TryGetValue(displayedOwner, out float tempInitial))
            {
                temperatureValueText.SetText($"{tempInitial:F1}°C");
            }

            if (atmosphereLabelText != null) atmosphereLabelText.SetText("Atmos");
            if (atmosphereValueText != null && Supplies.Atmosphere != null && Supplies.Atmosphere.TryGetValue(displayedOwner, out float atmosInitial))
            {
                atmosphereValueText.SetText($"{atmosInitial:F2} atm");
            }

            UpdatePopulationText();
        }

        private void OnDestroy()
        {
            Supplies.OnOxygenChanged -= HandleOxygenChanged;
            Supplies.OnMaterialsChanged -= HandleMaterialsChanged;
            Supplies.OnIntegrityChanged -= HandleIntegrityChanged;
            Supplies.OnBiomassChanged -= HandleBiomassChanged;
            Supplies.OnPowerChanged -= HandlePowerChanged;
            Supplies.OnPopulationChanged -= HandlePopulationChanged;
            Supplies.OnPopulationLimitChanged -= HandlePopulationLimitChanged;
            Supplies.OnTemperatureChanged -= HandleTemperatureChanged;
            Supplies.OnAtmosphereChanged -= HandleAtmosphereChanged;
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
            if (biomassValueText != null) biomassValueText.SetText($"{newValue:F1}%");
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
    }
}
