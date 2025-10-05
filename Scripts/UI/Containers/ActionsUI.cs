using System.Collections.Generic;
using System.Linq;
using GameDevTV.RTS.Commands;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.UI.Components;
using UnityEngine;
using UnityEngine.Events;
using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Events;
using System;
using GameDevTV.RTS.TechTree;

namespace GameDevTV.RTS.UI.Containers
{
    public class ActionsUI : MonoBehaviour, IUIElement<HashSet<AbstractCommandable>>
    {
        [SerializeField] private UIActionButton[] actionButtons;

        private HashSet<BaseBuilding> selectedBuildings = new();

        public void EnableFor(HashSet<AbstractCommandable> selectedUnits)
        {
            RefreshButtons(selectedUnits);

            foreach(BaseBuilding building in selectedBuildings)
            {
                building.OnQueueUpdated -= OnBuildingQueueUpdated;
            }

            selectedBuildings = selectedUnits
                .Where(selectedUnit => selectedUnit is BaseBuilding)
                .Cast<BaseBuilding>()
                .ToHashSet();
            
            foreach(BaseBuilding building in selectedBuildings)
            {
                building.OnQueueUpdated += OnBuildingQueueUpdated;
            }
        }

        public void Disable()
        {
            foreach(UIActionButton button in actionButtons)
            {
                button.Disable();
            }

            foreach (BaseBuilding building in selectedBuildings)
            {
                building.OnQueueUpdated -= OnBuildingQueueUpdated;
            }
            selectedBuildings.Clear();
        }

        private void OnBuildingQueueUpdated(UnlockableSO[] unitsInQueue)
        {
            RefreshButtons(selectedBuildings.Cast<AbstractCommandable>().ToHashSet());
        }

        private void RefreshButtons(HashSet<AbstractCommandable> selectedUnits)
        {
            IEnumerable<BaseCommand> availableCommands = selectedUnits.Count > 0 
                ? selectedUnits.ElementAt(0).AvailableCommands 
                : Array.Empty<BaseCommand>();

            availableCommands = availableCommands.Where(action => action.IsAvailable(
                new CommandContext(
                    Owner.Player1,
                    selectedUnits.FirstOrDefault(),
                    new RaycastHit()
                )
            ));

            for(int i = 1; i<selectedUnits.Count; i++)
            {
                AbstractCommandable commandable = selectedUnits.ElementAt(i);
                if (commandable.AvailableCommands != null)
                {
                    availableCommands = availableCommands.Intersect(commandable.AvailableCommands);
                }
            }

            for (int i = 0; i < actionButtons.Length; i++)
            {
                BaseCommand actionForSlot = availableCommands.Where(action => action.Slot == i).FirstOrDefault();

                if (actionForSlot != null)
                {
                    actionButtons[i].EnableFor(actionForSlot, selectedUnits, HandleClick(actionForSlot));
                }
                else
                {
                    actionButtons[i].Disable();
                }
            }
        }

        private UnityAction HandleClick(BaseCommand action)
        {
            return () => Bus<CommandSelectedEvent>.Raise(Owner.Player1, new CommandSelectedEvent(action));
        }
    }
}
