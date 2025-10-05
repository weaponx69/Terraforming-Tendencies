using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Events;
using GameDevTV.RTS.TechTree;
using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.UI.Containers
{
    public class BuildingSelectedUI : MonoBehaviour, IUIElement<BaseBuilding>
    {
        [SerializeField] private SingleUnitSelectedUI singleUnitSelectedUI;
        [SerializeField] private BuildingBuildingUI buildingBuildingUI;
        [SerializeField] private BuildingUnderConstructionUI buildingUnderConstructionUI;

        private BaseBuilding selectedBuilding;

        public void EnableFor(BaseBuilding building)
        {
            selectedBuilding = building;
            selectedBuilding.OnQueueUpdated -= OnBuildingQueueUpdated;
            selectedBuilding.OnQueueUpdated += OnBuildingQueueUpdated;

            if (building.Progress.State == BuildingProgress.BuildingState.Completed)
            {
                buildingUnderConstructionUI.Disable();
                OnBuildingQueueUpdated();
            }
            else
            {
                buildingUnderConstructionUI.EnableFor(building);
                buildingBuildingUI.Disable();
                singleUnitSelectedUI.Disable();
                Bus<BuildingSpawnEvent>.OnEvent[Owner.Player1] += HandleBuildingSpawn;
            }
        }

        public void Disable()
        {
            buildingBuildingUI.Disable();
            singleUnitSelectedUI.Disable();
            buildingUnderConstructionUI.Disable();
            Bus<BuildingSpawnEvent>.OnEvent[Owner.Player1] -= HandleBuildingSpawn;
            if (selectedBuilding != null)
            {
                selectedBuilding.OnQueueUpdated -= OnBuildingQueueUpdated;
                selectedBuilding = null;
            }
        }

        private void OnBuildingQueueUpdated(UnlockableSO[] _ = null)
        {
            if (selectedBuilding.QueueSize == 0)
            {
                singleUnitSelectedUI.EnableFor(selectedBuilding);
                buildingBuildingUI.Disable();
            }
            else
            {
                buildingBuildingUI.EnableFor(selectedBuilding);
                singleUnitSelectedUI.Disable();
            }
        }

        private void HandleBuildingSpawn(BuildingSpawnEvent evt)
        {
            if (evt.Building == selectedBuilding)
            {
                Bus<BuildingSpawnEvent>.OnEvent[Owner.Player1] -= HandleBuildingSpawn;
                OnBuildingQueueUpdated();
                buildingUnderConstructionUI.Disable();
            }
        }
    }
}
