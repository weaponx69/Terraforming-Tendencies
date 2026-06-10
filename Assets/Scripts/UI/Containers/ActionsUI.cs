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
        private HashSet<FoundryCrawler> selectedCrawlers = new();

        public void EnableFor(HashSet<AbstractCommandable> selectedUnits)
        {
            RefreshButtons(selectedUnits);

            foreach(BaseBuilding building in selectedBuildings)
            {
                building.OnQueueUpdated -= OnBuildingQueueUpdated;
            }
            
            foreach(FoundryCrawler crawler in selectedCrawlers)
            {
                crawler.OnStatusUpdated -= OnCrawlerStatusUpdated;
            }

            selectedBuildings = selectedUnits
                .Where(selectedUnit => selectedUnit is BaseBuilding)
                .Cast<BaseBuilding>()
                .ToHashSet();
            
            selectedCrawlers = selectedUnits
                .Where(selectedUnit => selectedUnit is FoundryCrawler)
                .Cast<FoundryCrawler>()
                .ToHashSet();
            
            foreach(BaseBuilding building in selectedBuildings)
            {
                building.OnQueueUpdated += OnBuildingQueueUpdated;
            }

            foreach(FoundryCrawler crawler in selectedCrawlers)
            {
                crawler.OnStatusUpdated += OnCrawlerStatusUpdated;
            }
        }

        public void Disable()
        {
            if (actionButtons != null)
            {
                foreach(UIActionButton button in actionButtons)
                {
                    if (button != null) button.Disable();
                }
            }

            foreach (BaseBuilding building in selectedBuildings)
            {
                if (building != null) building.OnQueueUpdated -= OnBuildingQueueUpdated;
            }

            foreach (FoundryCrawler crawler in selectedCrawlers)
            {
                if (crawler != null) crawler.OnStatusUpdated -= OnCrawlerStatusUpdated;
            }

            selectedBuildings.Clear();
            selectedCrawlers.Clear();
            gameObject.SetActive(false);
        }

        private void OnBuildingQueueUpdated(UnlockableSO[] unitsInQueue)
        {
            RefreshButtons(selectedBuildings.Cast<AbstractCommandable>().Union(selectedCrawlers.Cast<AbstractCommandable>()).ToHashSet());
        }

        private void OnCrawlerStatusUpdated()
        {
            RefreshButtons(selectedBuildings.Cast<AbstractCommandable>().Union(selectedCrawlers.Cast<AbstractCommandable>()).ToHashSet());
        }

        private void RefreshButtons(HashSet<AbstractCommandable> selectedUnits)
        {
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
