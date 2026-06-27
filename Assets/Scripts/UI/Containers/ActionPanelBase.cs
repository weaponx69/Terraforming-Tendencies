using System;
using System.Collections.Generic;
using System.Linq;
using GameDevTV.RTS.Commands;
using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Events;
using GameDevTV.RTS.TechTree;
using GameDevTV.RTS.UI.Components;
using GameDevTV.RTS.Units;
using UnityEngine;
using UnityEngine.Events;

namespace GameDevTV.RTS.UI.Containers
{
    /// <summary>
    /// Base class for action panel UIs. Contains the shared logic for collecting
    /// available commands from selected units and rendering them into UIActionButton slots.
    /// Both the original ActionsUI and the persistent BottomBarActionsUI inherit from this.
    /// </summary>
    public abstract class ActionPanelBase : MonoBehaviour
    {
        [SerializeField] protected UIActionButton[] actionButtons;

        protected HashSet<BaseBuilding> selectedBuildings = new();

        /// <summary>
        /// Refresh the button display based on the currently selected units.
        /// Called by both panels whenever selection changes.
        /// </summary>
        public virtual void EnableFor(HashSet<AbstractCommandable> selectedUnits)
        {
            RefreshButtons(selectedUnits);

            foreach (BaseBuilding building in selectedBuildings)
            {
                building.OnQueueUpdated -= OnBuildingQueueUpdated;
            }

            selectedBuildings = selectedUnits
                .Where(selectedUnit => selectedUnit is BaseBuilding)
                .Cast<BaseBuilding>()
                .ToHashSet();

            foreach (BaseBuilding building in selectedBuildings)
            {
                building.OnQueueUpdated += OnBuildingQueueUpdated;
            }
        }

        /// <summary>
        /// Disable all buttons and unsubscribe from events.
        /// </summary>
        public virtual void Disable()
        {
            if (actionButtons != null)
            {
                foreach (UIActionButton button in actionButtons)
                {
                    if (button != null) button.Disable();
                }
            }

            foreach (BaseBuilding building in selectedBuildings)
            {
                if (building != null) building.OnQueueUpdated -= OnBuildingQueueUpdated;
            }

            selectedBuildings.Clear();
        }

        private void OnBuildingQueueUpdated(UnlockableSO[] unitsInQueue)
        {
            RefreshButtons(selectedBuildings.Cast<AbstractCommandable>().ToHashSet());
        }

        protected void RefreshButtons(HashSet<AbstractCommandable> selectedUnits)
        {
            if (actionButtons == null || actionButtons.Length == 0) return;

            IEnumerable<BaseCommand> availableCommands = selectedUnits.Count > 0
                ? selectedUnits.ElementAt(0).AvailableCommands
                : Array.Empty<BaseCommand>();

            availableCommands = availableCommands?.Where(action => action != null && action.IsAvailable(
                new CommandContext(
                    Owner.Player1,
                    selectedUnits.FirstOrDefault(),
                    new RaycastHit()
                )
            )) ?? Enumerable.Empty<BaseCommand>();

            for (int i = 1; i < selectedUnits.Count; i++)
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
