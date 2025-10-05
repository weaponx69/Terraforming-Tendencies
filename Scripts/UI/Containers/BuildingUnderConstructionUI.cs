using System.Collections;
using GameDevTV.RTS.UI.Components;
using GameDevTV.RTS.Units;
using TMPro;
using UnityEngine;

namespace GameDevTV.RTS.UI.Containers
{
    public class BuildingUnderConstructionUI : MonoBehaviour, IUIElement<BaseBuilding>
    {
        [SerializeField] private TextMeshProUGUI unitName;
        [SerializeField] private ProgressBar progressBar;

        public void EnableFor(BaseBuilding building)
        {
            gameObject.SetActive(true);
            unitName.SetText(building.UnitSO.Name);
            StartCoroutine(AnimateBuildingProgress(building));
        }

        public void Disable()
        {
            gameObject.SetActive(false);
        }

        private IEnumerator AnimateBuildingProgress(BaseBuilding building)
        {
            while(enabled && building.Progress.Progress < 1)
            {
                if (building.Progress.State != BuildingProgress.BuildingState.Building)
                {
                    yield return null;
                    continue;
                }

                float startTime = building.Progress.StartTime;
                float endTime = startTime + building.BuildingSO.BuildTime;

                progressBar.SetProgress(Mathf.Clamp01((Time.time - startTime) / (endTime - startTime)));
                yield return null;
            }
        }
    }
}