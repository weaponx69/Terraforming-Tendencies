using System.Collections.Generic;
using System.Linq;
using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Events;
using GameDevTV.RTS.UI.Containers;
using GameDevTV.RTS.Units;
using UnityEngine;
using TMPro;
using GameDevTV.RTS.Player;

namespace GameDevTV.RTS.UI
{
    public class RuntimeUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI biomassLabelText;
        [SerializeField] private TextMeshProUGUI biomassValueText;

        // Oxygen UI
        [SerializeField] private TextMeshProUGUI oxygenLabelText;
        [SerializeField] private TextMeshProUGUI oxygenValueText;

        [SerializeField] private ActionsUI actionsUI;
        [SerializeField] private BuildingSelectedUI buildingSelectedUI;
        [SerializeField] private UnitIconUI unitIconUI;
        [SerializeField] private SingleUnitSelectedUI singleUnitSelectedUI;
        [SerializeField] private UnitTransportUI unitTransportUI;

        private HashSet<AbstractCommandable> selectedUnits = new(12);

        private void Awake()
        {
            Bus<UnitSelectedEvent>.OnEvent[Owner.Player1] += HandleUnitSelected;
            Bus<UnitDeselectedEvent>.OnEvent[Owner.Player1] += HandleUnitDeselected;
            Bus<UnitDeathEvent>.OnEvent[Owner.Player1] += HandleUnitDeath;
            Bus<SupplyEvent>.OnEvent[Owner.Player1] += HandleSupplyChange;
            Bus<UnitLoadEvent>.OnEvent[Owner.Player1] += HandleLoadUnit;
            Bus<UnitUnloadEvent>.OnEvent[Owner.Player1] += HandleUnloadUnit;
            Bus<BuildingSpawnEvent>.OnEvent[Owner.Player1] += HandleBuildingSpawn;
            Bus<UpgradeResearchedEvent>.OnEvent[Owner.Player1] += HandleUpgradeResearched;
            Bus<BuildingDeathEvent>.OnEvent[Owner.Player1] += HandleBuildingDeath;
        }

        private void Start()
        {
            actionsUI.Disable();
            buildingSelectedUI.Disable();
            unitIconUI.Disable();
            singleUnitSelectedUI.Disable();
            unitTransportUI.Disable();

            if (biomassLabelText != null) biomassLabelText.SetText("Biomass");
            if (biomassValueText != null && Supplies.Biomass.TryGetValue(Owner.Player1, out int initial))
                biomassValueText.SetText(initial.ToString());

            if (oxygenLabelText != null) oxygenLabelText.SetText("Oxygen");
            if (oxygenValueText != null && Supplies.Oxygen.TryGetValue(Owner.Player1, out int oxyInitial))
                oxygenValueText.SetText(oxyInitial.ToString());

            Supplies.OnOxygenChanged += HandleOxygenChanged;

            Supplies.OnBiomassChanged += HandleBiomassChanged;
        }

        private void OnDestroy()
        {
            Supplies.OnOxygenChanged -= HandleOxygenChanged;
            Supplies.OnBiomassChanged -= HandleBiomassChanged;
        }

        private void HandleOxygenChanged(Owner owner, int newValue)
        {
            if (owner != Owner.Player1) return;
            if (oxygenValueText == null) return;
            oxygenValueText.SetText(newValue.ToString());
        }

        private void HandleBiomassChanged(Owner owner, int newValue)
        {
            if (owner != Owner.Player1) return;
            if (biomassValueText == null) return;
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
            }
        }

        private void DisableAllContainers()
        {
            actionsUI.Disable();
            buildingSelectedUI.Disable();
            unitIconUI.Disable();
            singleUnitSelectedUI.Disable();
            unitTransportUI.Disable();
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
            // Update biomass HUD for Player1
            if (Owner.Player1 == evt.Owner && biomassValueText != null)
            {
                biomassValueText.SetText(GameDevTV.RTS.Player.Supplies.Biomass[evt.Owner].ToString());
            }
        }
    }
}
