using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.UI;
using GameDevTV.RTS.Environment;
using GameDevTV.RTS.EventBus;

namespace GameDevTV.RTS.Player
{
    public class ColonistManager : MonoBehaviour
    {
        public static ColonistManager Instance { get; private set; }

        [Header("Settings")]
        [SerializeField] private float initialDelay = 300f; // 5 minutes until first arrival warning
        [SerializeField] private float arrivalIntervalMin = 300f; // 5 mins
        [SerializeField] private float arrivalIntervalMax = 600f; // 10 mins
        [SerializeField] private float warningDuration = 300f; // 5 minute warning

        private float nextArrivalTime;
        private bool isWarningActive = false;
        private int currentWaveSize = 2; // Starts at 2, scales up

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(this);
            }
        }

        private void Start()
        {
            nextArrivalTime = Time.time + initialDelay + warningDuration;
            PlanetGenerator.OnPlanetGenerated += HandlePlanetGenerated;
        }

        private void OnDestroy()
        {
            PlanetGenerator.OnPlanetGenerated -= HandlePlanetGenerated;
        }

        private void HandlePlanetGenerated()
        {
            // Reset timers when planet is generated
            nextArrivalTime = Time.time + initialDelay + warningDuration;
            isWarningActive = false;
        }

        private bool HasUnlockedHousing()
        {
            foreach (string name in BlueprintDraftManager.GetUnlockedBuildingNames())
            {
                BuildingSO bld = BlueprintDraftManager.GetBuildingSOByName(name);
                if (bld != null && bld.BuildingConfig != null && bld.BuildingConfig.HousingCapacity > 0)
                {
                    return true;
                }
                if (name.Contains("Habitat") || name.Contains("Apartment") || name.Contains("Dome") || name.Contains("Commons"))
                {
                    return true;
                }
            }
            return false;
        }

        private bool HasBuiltSpaceport()
        {
            foreach (var building in BaseBuilding.ActiveBuildings)
            {
                if (building != null && building.BuildingSO != null &&
                    building.BuildingSO.Name.Contains("Spaceport", System.StringComparison.OrdinalIgnoreCase) &&
                    building.Progress.State == BuildingProgress.BuildingState.Completed &&
                    building.Owner == Owner.Player1)
                {
                    return true;
                }
            }
            return false;
        }

        private void Update()
        {
#if UNITY_EDITOR
            if (Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame)
            {
                Arrive();
            }
#endif

            if (GameOverManager.Instance != null && GameOverManager.Instance.gameObject.activeInHierarchy)
            {
                // Don't run logic if game over UI is showing
                return;
            }

            // Do not run colonist timers or warnings until the player has unlocked at least one housing building card AND built a Spaceport
            if (!HasUnlockedHousing() || !HasBuiltSpaceport())
            {
                nextArrivalTime = Time.time + initialDelay + warningDuration;
                return;
            }

            if (!isWarningActive && Time.time >= nextArrivalTime - warningDuration)
            {
                StartWarning();
            }

            if (isWarningActive && Time.time >= nextArrivalTime)
            {
                Arrive();
            }
        }

        [ContextMenu("Force Colonist Warning")]
        private void StartWarning()
        {
            isWarningActive = true;
            RuntimeUI ui = Object.FindAnyObjectByType<RuntimeUI>();
            if (ui != null)
            {
                ui.ShowWarningBanner("WARNING: COLONISTS INCOMING");
            }
            Debug.Log($"[ColonistManager] Warning started. Colonists arriving in {warningDuration}s");
        }

        [ContextMenu("Force Colonist Arrival")]
        public void Arrive()
        {
            isWarningActive = false;
            RuntimeUI ui = Object.FindAnyObjectByType<RuntimeUI>();
            if (ui != null)
            {
                ui.HideWarningBanner();
            }

            // Grant colonists
            int currentPop = Supplies.Population != null && Supplies.Population.TryGetValue(Owner.Player1, out int p) ? p : 0;
            Supplies.UpdatePopulation(Owner.Player1, currentPop + currentWaveSize);

            Debug.Log($"[ColonistManager] {currentWaveSize} Colonists arrived!");

            // Scale up next wave
            currentWaveSize += Random.Range(1, 4);

            // Schedule next
            nextArrivalTime = Time.time + Random.Range(arrivalIntervalMin, arrivalIntervalMax) + warningDuration;
        }

#if UNITY_EDITOR
        private void OnGUI()
        {
            if (GUI.Button(new Rect(10, 80, 220, 40), "Debug: Force Colonist Arrival"))
            {
                Arrive();
            }
        }
#endif
    }
}
