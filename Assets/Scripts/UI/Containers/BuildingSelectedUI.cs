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
            if (selectedBuilding != null)
            {
                selectedBuilding.OnQueueUpdated -= OnBuildingQueueUpdated;
                Bus<BuildingSpawnEvent>.OnEvent[selectedBuilding.Owner] -= HandleBuildingSpawn;
            }

            selectedBuilding = building;
            selectedBuilding.OnQueueUpdated += OnBuildingQueueUpdated;
            gameObject.SetActive(true);

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
                Bus<BuildingSpawnEvent>.OnEvent[selectedBuilding.Owner] += HandleBuildingSpawn;
            }
        }

        public void Disable()
        {
            if (buildingBuildingUI != null) buildingBuildingUI.Disable();
            if (singleUnitSelectedUI != null) singleUnitSelectedUI.Disable();
            if (buildingUnderConstructionUI != null) buildingUnderConstructionUI.Disable();
            
            if (selectedBuilding != null)
            {
                Bus<BuildingSpawnEvent>.OnEvent[selectedBuilding.Owner] -= HandleBuildingSpawn;
                selectedBuilding.OnQueueUpdated -= OnBuildingQueueUpdated;
                selectedBuilding = null;
            }
            gameObject.SetActive(false);
        }

        private void OnBuildingQueueUpdated(UnlockableSO[] _ = null)
        {
            if (selectedBuilding == null) return;

            int queueSize = selectedBuilding.QueueSize;
            // Debug.Log($"[BuildingSelectedUI] Refreshing. QueueSize: {queueSize}");

            if (queueSize == 0)
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
                Bus<BuildingSpawnEvent>.OnEvent[selectedBuilding.Owner] -= HandleBuildingSpawn;
                OnBuildingQueueUpdated();
                buildingUnderConstructionUI.Disable();
            }
        }
    }
}
