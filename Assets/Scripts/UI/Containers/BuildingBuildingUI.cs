using System.Collections;
using GameDevTV.RTS.TechTree;
using GameDevTV.RTS.UI.Components;
using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.UI.Containers
{
    public class BuildingBuildingUI : MonoBehaviour, IUIElement<BaseBuilding>
    {
        [SerializeField] private UIBuildQueueButton[] unitButtons;
        [SerializeField] private ProgressBar progressBar;

        private Coroutine buildCoroutine;
        private BaseBuilding building;

        public void EnableFor(BaseBuilding item)
        {
            if (building == item && gameObject.activeSelf)
            {
                SetupUnitButtons();
                return;
            }

            if (building != null && building != item)
            {
                building.OnQueueUpdated -= HandleQueueUpdated;
            }

            progressBar.SetProgress(0);
            gameObject.SetActive(true);
            building = item;
            building.OnQueueUpdated -= HandleQueueUpdated; // Safety unsubscribe
            building.OnQueueUpdated += HandleQueueUpdated;
            SetupUnitButtons();

            if (building.QueueSize > 0 && buildCoroutine == null)
            {
                buildCoroutine = StartCoroutine(UpdateUnitProgress());
            }
        }

        private void SetupUnitButtons()
        {
            int i = 0;
            for (; i < building.QueueSize; i++)
            {
                int index = i;
                unitButtons[i].EnableFor(building.Queue[i], () => building.CancelBuildingUnit(index));
            }
            for (; i < unitButtons.Length; i++)
            {
                unitButtons[i].Disable();
            }
        }

        public void Disable()
        {
            if (building != null)
            {
                building.OnQueueUpdated -= HandleQueueUpdated;
            }
            gameObject.SetActive(false);
            building = null;
            if (buildCoroutine != null)
            {
                StopCoroutine(buildCoroutine);
                buildCoroutine = null;
            }
        }

        private void HandleQueueUpdated(UnlockableSO[] unitsInQueue)
        {
            if (unitsInQueue.Length > 0 && buildCoroutine == null)
            {
                buildCoroutine = StartCoroutine(UpdateUnitProgress());
            }

            if (building != null)
            {
                SetupUnitButtons();
            }
        }

        private IEnumerator UpdateUnitProgress()
        {
            try
            {
                while (this != null && enabled && building != null && building.QueueSize > 0)
                {
                    if (building.SOBeingBuilt == null)
                    {
                        yield return null;
                        continue;
                    }

                    if (progressBar == null)
                    {
                        yield break;
                    }

                    float startTime = building.CurrentQueueStartTime;
                    float buildTime = building.SOBeingBuilt.BuildTime;
                    if (buildTime <= 0) buildTime = 1f;

                    float progress = Mathf.Clamp01((Time.time - startTime) / buildTime);
                    progressBar.SetProgress(progress);
                    
                    yield return null;
                }
            }
            finally
            {
                if (progressBar != null) progressBar.SetProgress(0);
                buildCoroutine = null;
            }
        }
    }
}
