using System.Collections.Generic;
using System.Linq;
using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Events;
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
        [SerializeField] private TextMeshProUGUI biomassLabelText;
        [SerializeField] private TextMeshProUGUI biomassValueText;

        // Oxygen UI
        [SerializeField] private TextMeshProUGUI oxygenLabelText;
        [SerializeField] private TextMeshProUGUI oxygenValueText;
        [SerializeField] private TextMeshProUGUI populationText;

        [SerializeField] private ActionsUI actionsUI;
        [SerializeField] private BuildingSelectedUI buildingSelectedUI;
        [SerializeField] private UnitIconUI unitIconUI;
        [SerializeField] private SingleUnitSelectedUI singleUnitSelectedUI;
        [SerializeField] private UnitTransportUI unitTransportUI;
        [SerializeField] private Image iconImage;

        [SerializeField] private AbstractCommandable globalCommander;

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
            Supplies.OnBiomassChanged += HandleBiomassChanged;

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
            Supplies.OnBiomassChanged -= HandleBiomassChanged;
        }

        private void Awake()
        {
        }

        private void Start()
        {
            actionsUI.Disable();
            buildingSelectedUI.Disable();
            unitIconUI.Disable();
            singleUnitSelectedUI.Disable();
            unitTransportUI.Disable();
        }

        private void InitializeUI()
        {
            displayedOwner = GameOverManager.MonitoredOwner;

            if (biomassLabelText != null) biomassLabelText.SetText("Biomass");
            if (biomassValueText != null && Supplies.Biomass != null && Supplies.Biomass.TryGetValue(displayedOwner, out int initial))
                biomassValueText.SetText(initial.ToString());

            if (oxygenLabelText != null) oxygenLabelText.SetText("Oxygen");
            if (oxygenValueText != null && Supplies.Oxygen != null && Supplies.Oxygen.TryGetValue(displayedOwner, out float oxyInitial))
            {
                oxygenValueText.SetText(oxyInitial.ToString("F3"));
                if (populationText != null) populationText.SetText($"{oxyInitial:F3}%");
            }
        }

        private void OnDestroy()
        {
            Supplies.OnOxygenChanged -= HandleOxygenChanged;
            Supplies.OnBiomassChanged -= HandleBiomassChanged;
        }

        private void HandleOxygenChanged(Owner owner, float newValue)
        {
            if (owner != displayedOwner) return;
            if (oxygenValueText != null)
                oxygenValueText.SetText(newValue.ToString("F3"));
            if (populationText != null)
                populationText.SetText($"{newValue:F3}%");
        }

        private void HandleBiomassChanged(Owner owner, int newValue)
        {
            if (owner != displayedOwner) 
            {
                // // // Debug.Log($"[RuntimeUI] Biomass changed for {owner} to {newValue}, but HUD is showing {displayedOwner}. Ignoring.");
                return;
            }

            if (biomassValueText == null) 
            {
                // // Debug.LogWarning("[RuntimeUI] HandleBiomassChanged: biomassValueText is NULL!");
                return;
            }

            // // // Debug.Log($"[RuntimeUI] Updating HUD biomass text to {newValue}");
            biomassValueText.SetText(newValue.ToString());
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
