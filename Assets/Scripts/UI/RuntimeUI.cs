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

            if (heroDroneReference != null)
            {
                heroDroneReference.OnCargoChanged -= HandleHeroCargoChanged;
            }
        }

        [SerializeField] private TextMeshProUGUI biomassLabelText;
        [SerializeField] private TextMeshProUGUI biomassValueText;
        [SerializeField] private TextMeshProUGUI powerLabelText;
        [SerializeField] private TextMeshProUGUI powerValueText;
        
        [Header("Warning UI")]
        [SerializeField] private GameObject warningBanner;
        [SerializeField] private TextMeshProUGUI warningText;

        private void Awake()
        {
            // Auto-hookup missing UI references by searching the hierarchy
            FindAndLinkUI("Materials Container", ref materialsLabelText, ref materialsValueText, "Materials Header");
            FindAndLinkUI("Biomass Container", ref biomassLabelText, ref biomassValueText, "Biomass Header");
            FindAndLinkUI("Oxygen Container", ref oxygenLabelText, ref oxygenValueText, "Oxygen Header");
            FindAndLinkUI("Integrity Container", ref integrityLabelText, ref integrityValueText, "Integrity Header");
            FindAndLinkUI("Hero Cargo Container", ref heroCargoLabelText, ref heroCargoValueText, "Hero Cargo Header");
            FindAndLinkUI("Probe Progress Container", ref probeProgressLabelText, ref probeProgressValueText, "Probe Progress Header");
            
            // We only need the Power Container cloned now
            if (powerValueText == null && integrityLabelText != null)
            {
                Transform containerParent = integrityLabelText.transform.parent.parent;
                GameObject template = integrityLabelText.transform.parent.gameObject;

                GameObject clone = Instantiate(template, containerParent);
                clone.name = "Power Container";
                powerValueText = clone.GetComponentsInChildren<TextMeshProUGUI>(true)
                    .FirstOrDefault(t => t.gameObject.name == "Resource Label");
                powerLabelText = clone.GetComponentsInChildren<TextMeshProUGUI>(true)
                    .FirstOrDefault(t => t.gameObject.name == "Integrity Header" || t.gameObject.name == "Power Header");
                if (powerLabelText != null) powerLabelText.gameObject.name = "Power Header";
                clone.SetActive(true);

                // Attempt to add a HorizontalLayoutGroup for clean automatic layout
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
                    // Fallback to manual offset if parent is the Canvas (to prevent destroying overall UI layout)
                    RectTransform rtClone = clone.GetComponent<RectTransform>();
                    RectTransform rtMat = materialsLabelText.transform.parent.GetComponent<RectTransform>();
                    RectTransform rtInt = template.GetComponent<RectTransform>();
                    
                    if (rtClone != null && rtMat != null && rtInt != null)
                    {
                        float stepX = (rtInt.anchoredPosition.x - rtMat.anchoredPosition.x) / 3f; 
                        if (stepX == 0) stepX = 150f; 
                        rtClone.anchoredPosition = rtInt.anchoredPosition + new Vector2(stepX, 0f);
                    }
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
            GameObject container = GameObject.Find(containerName);
            if (container == null) return;

            if (valueField == null)
            {
                // Look for Resource Label anywhere in children
                valueField = container.GetComponentsInChildren<TextMeshProUGUI>(true)
                    .FirstOrDefault(t => t.gameObject.name == "Resource Label");
            }

            if (labelField == null)
            {
                // Look for header anywhere in children
                labelField = container.GetComponentsInChildren<TextMeshProUGUI>(true)
                    .FirstOrDefault(t => headerNames.Contains(t.gameObject.name));
            }
        }

        private void Start()
        {
            RefreshUI();
        }

        private void Update()
        {
            UpdateSectorsUI();
            UpdateProbeProgressUI();
            UpdateHeroDroneSubscription();
        }

        private void UpdateProbeProgressUI()
{
            if (probeProgressValueText == null) return;

            GameObject container = probeProgressValueText.transform.parent?.parent?.gameObject;
            if (container == null) return;

            var probes = Object.FindObjectsByType<ProbeLogic>(FindObjectsInactive.Exclude);
            float maxPrep = 0f;
            bool anyAnalyzing = false;
            foreach (var p in probes)
            {
                if (p.IsAnalyzing)
                {
                    anyAnalyzing = true;
                    if (p.AnalysisProgress > maxPrep) maxPrep = p.AnalysisProgress;
                }
            }

            if (anyAnalyzing)
            {
                probeProgressValueText.SetText($"{maxPrep * 100:F0}%");
                if (probeProgressLabelText != null) probeProgressLabelText.SetText("Probe Analysis");
                container.SetActive(true);
            }
            else
            {
                container.SetActive(false);
            }
        }

        private void UpdateHeroDroneSubscription()
        {
            if (heroDroneReference != null) return;

            heroDroneReference = Object.FindAnyObjectByType<HeroDrone>();
            if (heroDroneReference != null)
            {
                heroDroneReference.OnCargoChanged += HandleHeroCargoChanged;
                HandleHeroCargoChanged();
            }
        }

        private void HandleHeroCargoChanged()
        {
            if (heroDroneReference == null || heroCargoValueText == null) return;

            GameObject container = heroCargoValueText.transform.parent?.parent?.gameObject;
            if (container == null) return;

            if (heroDroneReference.CarriedAmount > 0)
            {
                string supplyName = heroDroneReference.CarriedSupply != null ? heroDroneReference.CarriedSupply.name : "Resources";
                heroCargoValueText.SetText($"{heroDroneReference.CarriedAmount}/{heroDroneReference.MaxCapacity} ({supplyName})");
                if (heroCargoLabelText != null) heroCargoLabelText.SetText("Hero Cargo");
                container.SetActive(true);
            }
            else
            {
                container.SetActive(false);
            }
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
                if (ColonyExpansionManager.Instance != null)
                {
                    var expansions = ColonyExpansionManager.Instance.ActiveExpansions.ToList();
                    if (expansions.Count > 0)
                    {
                        var lead = expansions.OrderByDescending(e => e.GetProgress()).First();
                        float maxProgress = lead.GetProgress();
                        string pausedSuffix = lead.IsPaused ? " PAUSED" : "";
                        text += $" (Exp: {maxProgress * 100:F0}%{pausedSuffix})";
                    }
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
            if (biomassValueText != null && Supplies.Biomass != null && Supplies.Biomass.TryGetValue(displayedOwner, out int bInitial))
                biomassValueText.SetText(bInitial.ToString());

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

            Supplies.OnPopulationChanged += HandlePopulationChanged;
            Supplies.OnPopulationLimitChanged += HandlePopulationLimitChanged;
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

        private void HandleBiomassChanged(Owner owner, int newValue)
        {
            if (owner != displayedOwner) return;
            if (biomassValueText != null) biomassValueText.SetText(newValue.ToString());
        }

        private void HandlePowerChanged(Owner owner, float newValue)
        {
            if (owner != displayedOwner) return;
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
    }
}
