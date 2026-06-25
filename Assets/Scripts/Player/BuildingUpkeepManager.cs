using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.Environment;

namespace GameDevTV.RTS.Player
{
    /// <summary>
    /// Manages the Materials-per-second upkeep tax on all completed buildings.
    /// When the Materials pool hits 0, buildings enter a degraded state (50% output).
    /// A grace floor prevents total lockout when Materials are critically low.
    /// </summary>
    public class BuildingUpkeepManager : MonoBehaviour
    {
        public static BuildingUpkeepManager Instance { get; private set; }

        [Header("Upkeep Settings")]
        [Tooltip("How often (in seconds) upkeep is deducted.")]
        [SerializeField] private float tickRate = 1f;

        [Tooltip("Base Materials consumed per building per second.")]
        [SerializeField] private float baseUpkeepPerBuilding = 0.5f;

        [Tooltip("Materials threshold below which panic mode activates (if all buildings degraded).")]
        [SerializeField] private int panicThreshold = 50;

        [Header("Debug")]
        [SerializeField] private bool logUpkeepTicks = false;

        /// <summary>Set of buildings currently registered for upkeep.</summary>
        private HashSet<BaseBuilding> registeredBuildings = new();

        /// <summary>Set of buildings currently in degraded state.</summary>
        private HashSet<BaseBuilding> degradedBuildings = new();

        /// <summary>Whether panic mode is active (upkeep paused, all buildings degraded).</summary>
        public bool IsPanicMode { get; private set; }

        /// <summary>Fired when a building enters degraded state.</summary>
        public static event System.Action<BaseBuilding> OnBuildingDegraded;

        /// <summary>Fired when a building recovers from degraded state.</summary>
        public static event System.Action<BaseBuilding> OnBuildingRecovered;

        /// <summary>Fired when panic mode activates.</summary>
        public static event System.Action OnPanicModeActivated;

        /// <summary>Fired when panic mode deactivates.</summary>
        public static event System.Action OnPanicModeDeactivated;

        private Coroutine upkeepRoutine;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            upkeepRoutine = StartCoroutine(UpkeepLoop());
        }

        private void OnDestroy()
        {
            if (upkeepRoutine != null)
            {
                StopCoroutine(upkeepRoutine);
            }
        }

        /// <summary>Register a completed building for upkeep tracking.</summary>
        public void RegisterBuilding(BaseBuilding building)
        {
            if (building == null) return;
            registeredBuildings.Add(building);
        }

        /// <summary>Unregister a building (destroyed, deconstructed, etc.).</summary>
        public void UnregisterBuilding(BaseBuilding building)
        {
            if (building == null) return;
            registeredBuildings.Remove(building);
            degradedBuildings.Remove(building);
        }

        /// <summary>Check if a specific building is currently degraded.</summary>
        public bool IsDegraded(BaseBuilding building)
        {
            return building != null && degradedBuildings.Contains(building);
        }

        /// <summary>Get the current total upkeep cost per tick.</summary>
        public float GetTotalUpkeepPerTick()
        {
            int activeCount = 0;
            foreach (var b in registeredBuildings)
            {
                if (b != null && b.IsOperating && !degradedBuildings.Contains(b))
                {
                    activeCount++;
                }
            }
            return activeCount * baseUpkeepPerBuilding * tickRate;
        }

        private IEnumerator UpkeepLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(tickRate);
                ProcessUpkeepTick();
            }
        }

        private void ProcessUpkeepTick()
        {
            // Count active (non-degraded, operating) buildings
            int activeCount = 0;
            var toRemove = new List<BaseBuilding>();

            foreach (var b in registeredBuildings)
            {
                if (b == null)
                {
                    toRemove.Add(b);
                    continue;
                }
                if (b.IsOperating && !degradedBuildings.Contains(b))
                {
                    activeCount++;
                }
            }

            // Clean up null references
            foreach (var b in toRemove)
            {
                registeredBuildings.Remove(b);
                degradedBuildings.Remove(b);
            }

            if (activeCount == 0) return;

            float upkeepCost = activeCount * baseUpkeepPerBuilding * tickRate;
            int intCost = Mathf.Max(1, Mathf.RoundToInt(upkeepCost));

            // Deduct from Materials pool
            int currentMaterials = 0;
            if (Supplies.Materials != null && Supplies.Materials.TryGetValue(Owner.Player1, out int m))
            {
                currentMaterials = m;
            }

            int newMaterials = currentMaterials - intCost;

            if (logUpkeepTicks)
            {
                Debug.Log($"[BuildingUpkeepManager] Upkeep tick: {activeCount} buildings × {baseUpkeepPerBuilding}/s = {intCost} Materials. Pool: {currentMaterials} → {newMaterials}");
            }

            if (newMaterials <= 0)
            {
                newMaterials = 0;
                // Degrade all active buildings
                foreach (var b in registeredBuildings)
                {
                    if (b != null && b.IsOperating && !degradedBuildings.Contains(b))
                    {
                        DegradeBuilding(b);
                    }
                }

                // Check for panic mode
                if (currentMaterials < panicThreshold && degradedBuildings.Count >= registeredBuildings.Count)
                {
                    if (!IsPanicMode)
                    {
                        IsPanicMode = true;
                        OnPanicModeActivated?.Invoke();
                        Debug.LogWarning("[BuildingUpkeepManager] PANIC MODE: All buildings degraded, Materials critically low. Upkeep paused.");
                    }
                    // Don't deduct — grace floor
                    return;
                }
            }
            else
            {
                // If we have materials and were in panic mode, recover
                if (IsPanicMode && newMaterials >= panicThreshold)
                {
                    IsPanicMode = false;
                    OnPanicModeDeactivated?.Invoke();
                    RecoverAllBuildings();
                    Debug.Log("[BuildingUpkeepManager] Panic mode deactivated — buildings recovering.");
                }
            }

            // Apply the deduction
            if (Supplies.Materials != null)
            {
                Supplies.Materials[Owner.Player1] = newMaterials;
                Supplies.RaiseMaterialsChanged(Owner.Player1, newMaterials);
            }
        }

        private void DegradeBuilding(BaseBuilding building)
        {
            degradedBuildings.Add(building);
            building.SetDegraded(true);
            OnBuildingDegraded?.Invoke(building);
        }

        private void RecoverAllBuildings()
        {
            var recovered = new List<BaseBuilding>(degradedBuildings);
            foreach (var b in recovered)
            {
                if (b != null)
                {
                    degradedBuildings.Remove(b);
                    b.SetDegraded(false);
                    OnBuildingRecovered?.Invoke(b);
                }
            }
        }
    }
}