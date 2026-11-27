using System.Collections.Generic;
using System.Diagnostics;
using GameDevTV.RTS.Environment;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.MainGame;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace GameDevTV.RTS.Units
{
    /// <summary>
    /// Single tick loop for building simulation — avoids per-building <c>Update()</c> overhead.
    /// </summary>
    public class BuildingManager : MonoBehaviour
    {
        public static BuildingManager Instance { get; private set; }

        [Tooltip("Seconds between climate / oxygen simulation passes.")]
        [SerializeField] float simulationInterval = 0.5f;

        const float NavCarveDelaySec = 0.5f;

        float simulationAccumulator;

        readonly List<(BaseBuilding building, float enableAt)> navCarveQueue = new(32);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void EnsureExists()
        {
            if (Instance != null) return;
            var go = new GameObject(nameof(BuildingManager));
            Object.DontDestroyOnLoad(go);
            go.AddComponent<BuildingManager>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            ProcessNavCarveQueue();

            simulationAccumulator += Time.deltaTime;
            if (simulationAccumulator < simulationInterval) return;

            float dt = simulationAccumulator;
            simulationAccumulator = 0f;

            bool prologue = MainGameController.IsActive;
            bool colonyActs = ColonyActManager.Instance != null && ColonyActManager.Instance.IsRunActive;

            if (!prologue && !colonyActs)
            {
                TickClimate(dt);
                TickOxygen(dt);
            }
        }

        public void QueueNavMeshCarve(BaseBuilding building)
        {
            if (building == null) return;
            building.SuppressNavMeshCarving();
            navCarveQueue.Add((building, Time.time + NavCarveDelaySec));
        }

        void ProcessNavCarveQueue()
        {
            if (navCarveQueue.Count == 0) return;

            float now = Time.time;
            for (int i = navCarveQueue.Count - 1; i >= 0; i--)
            {
                var entry = navCarveQueue[i];
                if (entry.building == null)
                {
                    navCarveQueue.RemoveAt(i);
                    continue;
                }

                if (now < entry.enableAt) continue;

                entry.building.EnableLazyNavMeshCarve();
                navCarveQueue.RemoveAt(i);
            }
        }

        void TickClimate(float dt)
        {
            if (dt <= 0f) return;

            var buildings = BaseBuilding.ActiveBuildings;
            if (buildings == null) return;

            var sw = Stopwatch.StartNew();
            int count = 0;
            for (int i = 0; i < buildings.Count; i++)
            {
                BaseBuilding building = buildings[i];
                if (building == null) continue;
                building.TickClimateGeneration(dt);
                count++;
            }

            ClimateGenerationTicker.Instance?.OnBuildingSimulationTick(dt);
            sw.Stop();
            if (sw.Elapsed.TotalMilliseconds > 2.0)
            {
                Debug.LogError(
                    $"[BuildingManager] TickClimate exceeded 2ms: {sw.Elapsed.TotalMilliseconds:F2}ms " +
                    $"buildings={count}");
            }
        }

        void TickOxygen(float dt)
        {
            if (dt <= 0f) return;

            var buildings = BaseBuilding.ActiveBuildings;
            if (buildings == null) return;

            var sw = Stopwatch.StartNew();
            int count = 0;
            for (int i = 0; i < buildings.Count; i++)
            {
                BaseBuilding building = buildings[i];
                if (building == null) continue;
                if (building is not OxygenProcessor processor) continue;
                processor.SimulationTick(dt);
                count++;
            }

            sw.Stop();
            if (sw.Elapsed.TotalMilliseconds > 2.0)
            {
                Debug.LogError(
                    $"[BuildingManager] TickOxygen exceeded 2ms: {sw.Elapsed.TotalMilliseconds:F2}ms " +
                    $"processors={count}");
            }
        }
    }
}